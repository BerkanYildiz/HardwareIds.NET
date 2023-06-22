namespace HardwareIds.NET
{
    using System;
    using System.Linq;
    using System.Security.Cryptography;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using WindowsMonitor.Hardware.Bios;

    public static partial class HardwareIds
    {
        private static void RetrieveSmbiosTables(Hwid InHwid)
        {
            try
            {
                foreach (var SmbiosTable in SmBiosRawSmBiosTables.Retrieve())
                {
                    using (var Hasher = SHA256.Create())
                    {
                        InHwid.SmbiosTables.Add(new HwSmbios
                        {
                            Id = (int) InHwid.SmbiosTables.Count,
                            Version = $"{SmbiosTable.SmbiosMajorVersion}.{SmbiosTable.SmbiosMinorVersion}.{SmbiosTable.DmiRevision}",
                            Hash = string.Join(string.Empty, Hasher.ComputeHash(SmbiosTable.SmBiosData).Select(T => T.ToString("x2"))),
                            Length = SmbiosTable.Size,
                        });
                    }
                }
            }
            catch (Exception)
            {
                // ...
            }
        }
    }
}
