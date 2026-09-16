namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    public static partial class HardwareIds
    {
        internal static void RetrieveBaseBoards(Hwid InHwid, SmbiosTable? InSmbios)
        {
            try
            {
                if (InSmbios is null)
                    return;

                // 
                // SMBIOS type 2: Baseboard (or Module) Information.
                // 

                foreach (var Baseboard in InSmbios.OfType(2))
                {
                    InHwid.Baseboards.Add(new HwBaseboard
                    {
                        Id = InHwid.Baseboards.Count,
                        Manufacturer = Baseboard.GetString(0x04),
                        Model = Baseboard.GetString(0x05),
                        Version = Baseboard.GetString(0x06),
                        SerialNumber = Baseboard.GetString(0x07),
                        PartNumber = null,
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
