namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using WindowsMonitor.Hardware.Bios;

    public static partial class HardwareIds
    {
        private static void RetrieveFirmwares(Hwid InHwid)
        {
            try
            {
                foreach (var BiosFirmware in Bios.Retrieve())
                {
                    InHwid.BiosFirmwares.Add(new HwBios
                    {
                        Id = (int) InHwid.BiosFirmwares.Count,
                        Manufacturer = BiosFirmware.Manufacturer,
                        Version = BiosFirmware.Version,
                        SerialNumber = BiosFirmware.SerialNumber,
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
