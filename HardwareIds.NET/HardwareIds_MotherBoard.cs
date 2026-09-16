namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    public static partial class HardwareIds
    {
        internal static void RetrieveMotherBoards(Hwid InHwid, SmbiosTable? InSmbios)
        {
            try
            {
                if (InSmbios is null)
                    return;

                // 
                // SMBIOS type 1: System Information (what WMI exposes as Win32_ComputerSystemProduct).
                // 

                foreach (var SystemInfo in InSmbios.OfType(1))
                {
                    var Uuid = SystemInfo.GetBytes(0x08, 16);

                    InHwid.Motherboards.Add(new HwMotherboard
                    {
                        Id = InHwid.Motherboards.Count,
                        Name = SystemInfo.GetString(0x05),
                        Vendor = SystemInfo.GetString(0x04),
                        Version = SystemInfo.GetString(0x06),
                        UUID = Uuid != null ? new Guid(Uuid) : Guid.Empty,
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
