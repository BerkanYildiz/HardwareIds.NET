namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using WindowsMonitor.Hardware.Printers;

    public static partial class HardwareIds
    {
        private static void RetrievePrinters(Hwid InHwid)
        {
            try
            {
                foreach (var Printer in Win32Printer.Retrieve())
                {
                    InHwid.Printers.Add(new HwPrinter
                    {
                        Id = (int) InHwid.Printers.Count,
                        Name = Printer.Name,
                        PortName = Printer.PortName,
                        Location = Printer.Location,
                        Width = Printer.HorizontalResolution,
                        Height = Printer.VerticalResolution,
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
