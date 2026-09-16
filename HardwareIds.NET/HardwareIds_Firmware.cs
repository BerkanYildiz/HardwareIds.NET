namespace HardwareIds.NET
{
    using System;
    using System.Linq;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using Microsoft.Win32;

    public static partial class HardwareIds
    {
        internal static void RetrieveFirmwares(Hwid InHwid, SmbiosTable? InSmbios)
        {
            try
            {
                if (InSmbios is null)
                    return;

                // 
                // WMI reports the first entry of the SystemBiosVersion registry value as the BIOS version,
                // and the system serial number (SMBIOS type 1) as the BIOS serial number.
                // 

                string? RegistryVersion;

                using (var SystemKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System"))
                    RegistryVersion = (SystemKey?.GetValue("SystemBiosVersion") as string[])?.FirstOrDefault();

                var SystemSerialNumber = InSmbios.OfType(1).Select(T => T.GetString(0x07)).FirstOrDefault();

                // 
                // SMBIOS type 0: BIOS Information.
                // 

                foreach (var Bios in InSmbios.OfType(0))
                {
                    InHwid.BiosFirmwares.Add(new HwBios
                    {
                        Id = InHwid.BiosFirmwares.Count,
                        Manufacturer = Bios.GetString(0x04),
                        Version = RegistryVersion ?? Bios.GetString(0x05),
                        SerialNumber = SystemSerialNumber,
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
