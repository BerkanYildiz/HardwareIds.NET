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

                    var InstanceId = AdapterKey.GetValue("DeviceInstanceID") as string;
                    var DevNode = InstanceId != null ? CfgMgr32.LocateDevNode(InstanceId) : null;
                    var Interface = Interfaces.FirstOrDefault(T => T.InterfaceGuid == InterfaceGuid);

                    if (!IsPhysicalAdapter(AdapterKey.GetValue("Characteristics") as int?, DevNode != null, Interface))
                        continue;

                    Entries.Add(new HwNetworkAdapter
                    {
                        Id = Index,
                        InterfaceId = (int) Interface!.InterfaceIndex,
                        Name = AdapterKey.GetValue("DriverDesc") as string ?? Interface.Description,
                        InterfaceGuid = InterfaceGuidText,
                        ServiceName = CfgMgr32.GetDevNodeProperty(DevNode!.Value, CfgMgr32.DEVPKEY_Device_Service),
                        IsPhysical = true,
                        IsEnabled = Interface.IsAdminUp,
                        InstallDate = GetNetworkAdapterInstallDate(AdapterKey, DevNode),
                        InstanceId = InstanceId,
                        Address =
                        {
                            Current = Interface.PhysicalAddress != null ? FormatMacAddress(Interface.PhysicalAddress) : null,
                            Permanent = Interface.PermanentPhysicalAddress != null ? FormatMacAddress(Interface.PermanentPhysicalAddress) : null,
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
        /// Tells whether an adapter is one WMI reports as physical ("PhysicalAdapter"). The rule was derived from every case met so far:
        /// - the driver declares the adapter physical (NCF_PHYSICAL); kernel debugger, Hyper-V switch, WAN miniport and VPN adapters declare NCF_VIRTUAL;
        /// - the device is present; adapters of removed hardware, or of the machine a VM image was captured on, keep their class key and even their interface;
        /// - its network interface exists and has a connector (IF_FLAG_CONNECTOR_PRESENT); a present adapter whose driver did not start has no interface,
        ///   and ghost interfaces lose their connector flag.
        /// </summary>
        /// <param name="InCharacteristics">The "Characteristics" value of the adapter's class key.</param>
        /// <param name="InIsPresent">Whether the adapter's device node is present.</param>
        /// <param name="InInterface">The adapter's network interface, if it has one.</param>
        internal static bool IsPhysicalAdapter(int? InCharacteristics, bool InIsPresent, NetworkInterfaceInfo? InInterface)
        {
            const int NCF_PHYSICAL = 0x4;
            const byte IF_FLAG_CONNECTOR_PRESENT = 0x4;

            return InCharacteristics != null
                && (InCharacteristics.Value & NCF_PHYSICAL) != 0
                && InIsPresent
                && InInterface != null
                && (InInterface.Flags & IF_FLAG_CONNECTOR_PRESENT) != 0;
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
