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

                    var Interface = Interfaces.FirstOrDefault(T => T.InterfaceGuid == InterfaceGuid);

                    if (Interface is null || !Interface.IsHardware || Interface.Type == IpHlpApi.IF_TYPE_SOFTWARE_LOOPBACK)
                        continue;

                    var InstanceId = AdapterKey.GetValue("DeviceInstanceID") as string;
                    var DevNode = InstanceId != null ? CfgMgr32.LocateDevNode(InstanceId) : null;

                    Entries.Add(new HwNetworkAdapter
                    {
                        Id = Index,
                        InterfaceId = (int) Interface.InterfaceIndex,
                        Name = AdapterKey.GetValue("DriverDesc") as string ?? Interface.Description,
                        InterfaceGuid = InterfaceGuidText,
                        ServiceName = DevNode != null ? CfgMgr32.GetDevNodeProperty(DevNode.Value, CfgMgr32.DEVPKEY_Device_Service) : null,
                        IsPhysical = Interface.IsHardware,
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
