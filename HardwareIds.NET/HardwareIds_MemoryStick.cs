namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using WindowsMonitor.Hardware.Memories;

    public static partial class HardwareIds
    {
        private static void RetrieveMemorySticks(Hwid InHwid)
        {
            try
            {
                foreach (var MemoryStick in PhysicalMemory.Retrieve())
                {
                    InHwid.MemorySticks.Add(new HwMemoryStick
                    {
                        Id = (int) InHwid.MemorySticks.Count,
                        Manufacturer = MemoryStick.Manufacturer,
                        Capacity = (MemoryStick.Capacity / 1024 / 1024 / 1024) + " GB",
                        ClockSpeed = $"{MemoryStick.ConfiguredClockSpeed} MHz",
                        Voltage = $"{MemoryStick.ConfiguredVoltage / 1000:0.00} V",
                        SerialNumber = MemoryStick.SerialNumber,
                        PartNumber = MemoryStick.PartNumber,
                        Channel = MemoryStick.DeviceLocator,
                    });
                }
            }
            catch (Exception)
            {
                // ..
            }
        }
    }
}
