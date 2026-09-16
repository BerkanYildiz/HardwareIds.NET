namespace HardwareIds.NET
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

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

                        Entries.Add(new HwDisk
                        {
                            Id = Storage.GetDeviceNumber(Handle) ?? Entries.Count,
                            Interface = GetDiskInterfaceType(InstanceId, Descriptor?.BusType),
                            Model = FriendlyName ?? string.Join(" ", new[] { Descriptor?.VendorId?.Trim(), Descriptor?.ProductId?.Trim() }.Where(T => !string.IsNullOrEmpty(T))),
                            SerialNumber = Descriptor?.SerialNumber,
                            Capacity = (Size / 1024 / 1024 / 1024) + " GB",
                            Partitions = Storage.GetPartitionCount(Handle).GetValueOrDefault(),
                            IsRemovable = Descriptor?.RemovableMedia ?? false,
                            IsSMART = Storage.SupportsFailurePrediction(Handle),
                        });
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
