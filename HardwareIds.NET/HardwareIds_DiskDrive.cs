namespace HardwareIds.NET
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using Microsoft.Win32.SafeHandles;

    public static partial class HardwareIds
    {
        internal static void RetrieveDiskDrives(Hwid InHwid)
        {
            try
            {
                var Entries = new List<HwDisk>();

                foreach (var InterfacePath in CfgMgr32.GetDeviceInterfaces(CfgMgr32.GUID_DEVINTERFACE_DISK))
                {
                    try
                    {
                        using var Handle = Storage.Open(InterfacePath);

                        if (Handle is null)
                            continue;

                        var Descriptor = Storage.GetDeviceDescriptor(Handle);
                        var InstanceId = CfgMgr32.GetInterfaceProperty(InterfacePath, CfgMgr32.DEVPKEY_Device_InstanceId);
                        var DevNode = InstanceId != null ? CfgMgr32.LocateDevNode(InstanceId) : null;
                        var FriendlyName = DevNode != null ? CfgMgr32.GetDevNodeProperty(DevNode.Value, CfgMgr32.DEVPKEY_Device_FriendlyName) : null;
                        var Size = Storage.GetSize(Handle).GetValueOrDefault();

                        var Entry = new HwDisk
                        {
                            Id = Storage.GetDeviceNumber(Handle) ?? Entries.Count,
                            Interface = GetDiskInterfaceType(InstanceId, Descriptor?.BusType),
                            Model = FriendlyName ?? string.Join(" ", new[] { Descriptor?.VendorId?.Trim(), Descriptor?.ProductId?.Trim() }.Where(T => !string.IsNullOrEmpty(T))),
                            SerialNumber = Descriptor?.SerialNumber,
                            Capacity = (Size / 1024 / 1024 / 1024) + " GB",
                            Partitions = Storage.GetPartitionCount(Handle).GetValueOrDefault(),
                            IsRemovable = Descriptor?.RemovableMedia ?? false,
                            IsSMART = Storage.SupportsFailurePrediction(Handle),
                            Firmware = Descriptor?.ProductRevision?.Trim(),
                            DiskGuid = Storage.GetDiskIdentifier(Handle),
                            InstanceId = InstanceId,
                        };

                        RetrieveDiskIdentifiers(Entry, Handle, Descriptor);
                        Entries.Add(Entry);
                    }
                    catch (Exception)
                    {
                        // ...
                    }
                }

                InHwid.Disks.AddRange(Entries.OrderBy(T => T.Id));
            }
            catch (Exception)
            {
                // ...
            }
        }

        /// <summary>
        /// Collects every unique identifier a disk exposes beyond the descriptor serial (what WMI reports): the NVMe or
        /// ATA identify data, the SCSI device identification page and the Windows DUID. A drive can legitimately report
        /// several different serial numbers depending on the layer asked, so each one is kept in its own field.
        /// </summary>
        internal static void RetrieveDiskIdentifiers(HwDisk InDisk, SafeFileHandle InHandle, StorageDeviceDescriptor? InDescriptor)
        {
            if (InDescriptor?.BusType == Storage.BusTypeNvme)
            {
                var Controller = Storage.GetNvmeControllerIdentity(InHandle);
                var Namespace = Storage.GetNvmeNamespaceIdentity(InHandle);

                InDisk.NvmeSerial = Clean(Controller?.SerialNumber);
                InDisk.NvmeFguid = Clean(Controller?.FruGuid);
                InDisk.NvmeNguid = Clean(Namespace?.Nguid);
                InDisk.NvmeEui64 = Clean(Namespace?.Eui64);

                InDisk.Firmware = Clean(Controller?.FirmwareRevision) ?? InDisk.Firmware;
                InDisk.WorldWideName ??= InDisk.NvmeEui64 ?? InDisk.NvmeNguid;
            }
            else if (InDescriptor?.BusType is Storage.BusTypeAta or Storage.BusTypeSata or Storage.BusTypeAtapi)
            {
                var Ata = Storage.GetAtaIdentity(InHandle);

                InDisk.AtaSerial = Clean(Ata?.SerialNumber);
                InDisk.AtaWwn = Clean(Ata?.WorldWideName);

                InDisk.Firmware = Clean(Ata?.FirmwareRevision) ?? InDisk.Firmware;
                InDisk.WorldWideName ??= InDisk.AtaWwn;
            }

            foreach (var Identifier in Storage.GetDeviceIdentifiers(InHandle).Where(T => T.Association == ScsiDeviceIdentifier.AssociationLogicalUnit))
            {
                var Text = Clean(Identifier.Text);

                if (Text is null)
                    continue;

                switch (Identifier.Type)
                {
                    case ScsiDeviceIdentifier.TypeVendorSpecific:
                        InDisk.VpdVendor ??= Text;
                        break;

                    case ScsiDeviceIdentifier.TypeT10VendorId:
                        InDisk.VpdT10 ??= Text;
                        break;

                    case ScsiDeviceIdentifier.TypeEui64 when Identifier.Value.Length == 16:
                        InDisk.VpdNguid ??= Text;
                        break;

                    case ScsiDeviceIdentifier.TypeEui64:
                        InDisk.VpdEui64 ??= Text;
                        break;

                    case ScsiDeviceIdentifier.TypeNaa:
                        InDisk.VpdNaa ??= Text;
                        break;

                    case ScsiDeviceIdentifier.TypeScsiNameString:
                        InDisk.VpdScsiName ??= Text;
                        break;
                }
            }

            InDisk.WorldWideName ??= InDisk.VpdNaa ?? InDisk.VpdEui64;
            InDisk.Duid = Storage.GetUniqueId(InHandle);
        }

        private static string? Clean(string? InValue)
        {
            return string.IsNullOrWhiteSpace(InValue) ? null : InValue;
        }

        /// <summary>
        /// Maps a disk to the interface type names WMI reports, based on its PnP enumerator and bus type.
        /// </summary>
        internal static string GetDiskInterfaceType(string? InInstanceId, uint? InBusType)
        {
            var Enumerator = InInstanceId?.Split('\\').FirstOrDefault()?.ToUpperInvariant();

            switch (Enumerator)
            {
                case "USBSTOR":
                    return "USB";

                case "SCSI":
                case "IDE":
                case "1394":
                    return Enumerator;
            }

            switch (InBusType)
            {
                case 1: case 6: case 8: case 9: case 10: case 11: case 14: case 15: case 16: case 17:
                    return "SCSI";

                case 2: case 3:
                    return "IDE";

                case 4:
                    return "1394";

                case 7:
                    return "USB";

                default:
                    return Enumerator ?? "Unknown";
            }
        }
    }
}
