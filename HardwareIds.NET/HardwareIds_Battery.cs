namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    public static partial class HardwareIds
    {
        internal static void RetrieveBatteries(Hwid InHwid)
        {
            try
            {
                foreach (var Battery in Native.Battery.GetBatteries())
                {
                    InHwid.Batteries.Add(new HwBattery
                    {
                        Id = InHwid.Batteries.Count,
                        DeviceName = Battery.DeviceName,
                        Manufacturer = Battery.Manufacturer,
                        SerialNumber = Battery.SerialNumber,
                        UniqueId = Battery.UniqueId,
                        Chemistry = Battery.Chemistry,
                        DesignedCapacity = Battery.DesignedCapacity,
                        FullChargedCapacity = Battery.FullChargedCapacity,
                        ManufactureDate = Battery.ManufactureDate,
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
