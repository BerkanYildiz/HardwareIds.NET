namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using Microsoft.Win32;

    public static partial class HardwareIds
    {
        internal static void RetrieveMonitors(Hwid InHwid)
        {
            try
            {
                foreach (var InterfacePath in CfgMgr32.GetDeviceInterfaces(CfgMgr32.GUID_DEVINTERFACE_MONITOR))
                {
                    var InstanceId = CfgMgr32.GetInterfaceProperty(InterfacePath, CfgMgr32.DEVPKEY_Device_InstanceId);

                    if (InstanceId is null)
                        continue;

                    // 
                    // The EDID block Windows read from the monitor is cached under the device's "Device Parameters" key.
                    // 

                    using var ParametersKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\" + InstanceId + @"\Device Parameters");
                    var Info = Edid.Parse(ParametersKey?.GetValue("EDID") as byte[]);

                    if (Info is null)
                        continue;

                    InHwid.Monitors.Add(new HwMonitor
                    {
                        Id = InHwid.Monitors.Count,
                        Manufacturer = Info.Manufacturer,
                        Name = Info.Name,
                        Product = Info.ProductCode,
                        SerialNumber = Info.SerialNumber,
                    });
                }
            }
            catch (Exception)
            {
                // ...
            }
        }
    }
}
