namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using WindowsMonitor.Hardware;

    public static partial class HardwareIds
    {
        private static void RetrieveMotherBoards(Hwid InHwid)
        {
            try
            {
                foreach (var Motherboard in Win32ComputerSystemProduct.Retrieve())
                {
                    InHwid.Motherboards.Add(new HwMotherboard
                    {
                        Id = (int) InHwid.Motherboards.Count,
                        Name = Motherboard.Name,
                        Vendor = Motherboard.Vendor,
                        Version = Motherboard.Version,
                        UUID = Guid.Parse(Motherboard.Uuid),
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
