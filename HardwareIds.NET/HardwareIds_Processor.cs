namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using WindowsMonitor.Hardware.Processors;

    public static partial class HardwareIds
    {
        private static void RetrieveProcessors(Hwid InHwid)
        {
            try
            {
                foreach (var Processor in Win32Processor.Retrieve())
                {
                    InHwid.Processors.Add(new HwProcessor
                    {
                        Id = (int) InHwid.Processors.Count,
                        Manufacturer = Processor.Manufacturer,
                        Model = Processor.Name,
                        ModelNumber = Processor.ProcessorId,
                        Socket = Processor.SocketDesignation,
                        SerialNumber = Processor.SerialNumber,
                        PartNumber = Processor.PartNumber,
                        ClockSpeed = $"{Processor.CurrentClockSpeed} MHz",
                        Voltage = $"{Processor.CurrentVoltage} V",
                        Channel = Processor.DeviceId,
                        NumberOfCores = Processor.NumberOfCores,
                        NumberOfLogicalProcessors = Processor.NumberOfLogicalProcessors,
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
