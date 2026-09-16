namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    public static partial class HardwareIds
    {
        internal static void RetrievePrinters(Hwid InHwid)
        {
            try
            {
                foreach (var Printer in WinSpool.GetPrinters())
                {
                    InHwid.Printers.Add(new HwPrinter
                    {
                        Id = InHwid.Printers.Count,
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
