namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using WindowsMonitor.Hardware.Video.WmiMonitor;

    public static partial class HardwareIds
    {
        private static void RetrieveMonitors(Hwid InHwid)
        {
            try
            {
                foreach (var Monitor in WmiMonitorId.Retrieve())
                {
                    InHwid.Monitors.Add(new HwMonitor
                    {
                        Id = (int) InHwid.Monitors.Count,
                        Manufacturer = HwMonitor.ArrayToString(Monitor.ManufacturerName),
                        Name = HwMonitor.ArrayToString(Monitor.UserFriendlyName),
                        Product = HwMonitor.ArrayToString(Monitor.ProductCodeId),
                        SerialNumber = HwMonitor.ArrayToString(Monitor.SerialNumberId),
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
