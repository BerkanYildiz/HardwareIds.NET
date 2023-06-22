namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using WindowsMonitor.Hardware.OnBoard;

    public static partial class HardwareIds
    {
        private static void RetrieveBaseBoards(Hwid InHwid)
        {
            try
            {
                foreach (var Baseboard in BaseBoard.Retrieve())
                {
                    InHwid.Baseboards.Add(new HwBaseboard
                    {
                        Id = (int) InHwid.Baseboards.Count,
                        Manufacturer = Baseboard.Manufacturer,
                        Model = Baseboard.Product,
                        Version = Baseboard.Version,
                        SerialNumber = Baseboard.SerialNumber,
                        PartNumber = Baseboard.PartNumber,
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
