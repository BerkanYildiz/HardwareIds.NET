namespace HardwareIds.NET
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using Microsoft.Win32;

    public static partial class HardwareIds
    {
        internal static void RetrieveNetworkAdapters(Hwid InHwid)
        {
            try
            {
                var Interfaces = IpHlpApi.GetInterfaces();
                var Entries = new List<HwNetworkAdapter>();

                // 
                // The network class registry key holds one numbered subkey per adapter; that number is the index WMI reports.
                // 

                using var ClassKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\" + CfgMgr32.GUID_DEVCLASS_NET.ToString("B").ToUpperInvariant());

                if (ClassKey is null)
                    return;

                foreach (var SubkeyName in ClassKey.GetSubKeyNames())
                {
                    if (SubkeyName.Length != 4 || !int.TryParse(SubkeyName, out var Index))
                        continue;

                    using var AdapterKey = ClassKey.OpenSubKey(SubkeyName);

                    if (AdapterKey?.GetValue("NetCfgInstanceId") is not string InterfaceGuidText || !Guid.TryParse(InterfaceGuidText, out var InterfaceGuid))
                        continue;

                    //
                    // WMI's "PhysicalAdapter" is the NCF_PHYSICAL flag the driver declares; virtual adapters (kernel debugger,
                    // Hyper-V switches, WAN miniports, VPNs) declare NCF_VIRTUAL instead, even when NDIS flags them as hardware.
                    //

                    if (!IsPhysicalAdapter(AdapterKey.GetValue("Characteristics") as int?))
                        continue;

                    var InstanceId = AdapterKey.GetValue("DeviceInstanceID") as string;
                    var DevNode = InstanceId != null ? CfgMgr32.LocateDevNode(InstanceId) : null;
                    var Interface = Interfaces.FirstOrDefault(T => T.InterfaceGuid == InterfaceGuid);

                    //
                    // Skip adapters that are no longer present (removed hardware, or the NICs of the machine a VM image was captured on):
                    // their class key and even their network interface remain, but their device node is gone, and WMI does not count them.
                    // A disabled adapter keeps its device node and is listed, without addresses, like WMI does.
                    //

                    if (DevNode is null)
                        continue;

                    Entries.Add(new HwNetworkAdapter
                    {
                        Id = Index,
                        InterfaceId = (int) (Interface?.InterfaceIndex ?? 0),
                        Name = AdapterKey.GetValue("DriverDesc") as string ?? Interface?.Description,
                        InterfaceGuid = InterfaceGuidText,
                        ServiceName = DevNode != null ? CfgMgr32.GetDevNodeProperty(DevNode.Value, CfgMgr32.DEVPKEY_Device_Service) : null,
                        IsPhysical = true,
                        IsEnabled = Interface?.IsAdminUp ?? false,
                        InstallDate = GetNetworkAdapterInstallDate(AdapterKey, DevNode),
                        InstanceId = InstanceId,
                        Address =
                        {
                            Current = Interface?.PhysicalAddress != null ? FormatMacAddress(Interface.PhysicalAddress) : null,
                            Permanent = Interface?.PermanentPhysicalAddress != null ? FormatMacAddress(Interface.PermanentPhysicalAddress) : null,
                        },
                    });
                }

                InHwid.NetworkAdapters.AddRange(Entries.OrderBy(T => T.Id));
            }
            catch (Exception)
            {
                // ...
            }
        }

        /// <summary>
        /// Tells whether a network adapter driver declares the adapter as physical (NCF_PHYSICAL in its "Characteristics" value).
        /// </summary>
        /// <param name="InCharacteristics">The "Characteristics" value of the adapter's class key.</param>
        internal static bool IsPhysicalAdapter(int? InCharacteristics)
        {
            const int NCF_PHYSICAL = 0x4;
            return InCharacteristics != null && (InCharacteristics.Value & NCF_PHYSICAL) != 0;
        }

        private static DateTime? GetNetworkAdapterInstallDate(RegistryKey InAdapterKey, uint? InDevNode)
        {
            if (InAdapterKey.GetValue("NetworkInterfaceInstallTimestamp") is long Timestamp && Timestamp > 0)
            {
                try
                {
                    return DateTime.FromFileTime(Timestamp);
                }
                catch (ArgumentException)
                {
                    // ...
                }
            }

            return InDevNode != null ? CfgMgr32.GetDevNodeDateProperty(InDevNode.Value, CfgMgr32.DEVPKEY_Device_InstallDate) : null;
        }
    }
}
