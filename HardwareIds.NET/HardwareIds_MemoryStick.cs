namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    public static partial class HardwareIds
    {
        internal static void RetrieveMemorySticks(Hwid InHwid, SmbiosTable? InSmbios)
        {
            try
            {
                if (InSmbios is null)
                    return;

                // 
                // SMBIOS type 17: Memory Device, skipping empty slots like WMI does.
                // 

                foreach (var MemoryStick in InSmbios.OfType(17))
                {
                    var Capacity = GetMemoryDeviceCapacity(MemoryStick);

                    if (Capacity == 0)
                        continue;

                    var ClockSpeed = MemoryStick.Length > 0x21 ? MemoryStick.GetWord(0x20) : MemoryStick.GetWord(0x15);

                    InHwid.MemorySticks.Add(new HwMemoryStick
                    {
                        Id = InHwid.MemorySticks.Count,
                        Manufacturer = MemoryStick.GetString(0x17),
                        Capacity = (Capacity / 1024 / 1024 / 1024) + " GB",
                        ClockSpeed = $"{ClockSpeed} MHz",
                        Voltage = $"{MemoryStick.GetWord(0x26) / 1000.0:0.00} V",
                        SerialNumber = MemoryStick.GetString(0x18),
                        PartNumber = MemoryStick.GetString(0x1A),
                        Channel = MemoryStick.GetString(0x10),
                    });
                }
            }
            catch (Exception)
            {
                // ..
            }
        }

        /// <summary>
        /// Gets the capacity in bytes of a SMBIOS memory device, honouring the extended size field.
        /// </summary>
        internal static ulong GetMemoryDeviceCapacity(SmbiosStructure InMemoryDevice)
        {
            var Size = InMemoryDevice.GetWord(0x0C);

            switch (Size)
            {
                case 0x0000:
                case 0xFFFF:
                    return 0;

                case 0x7FFF:
                    return (ulong) InMemoryDevice.GetDword(0x1C) * 1024 * 1024;

                default:
                    return (Size & 0x8000) != 0 ? (ulong) (Size & 0x7FFF) * 1024 : (ulong) Size * 1024 * 1024;
            }
        }
    }
}
