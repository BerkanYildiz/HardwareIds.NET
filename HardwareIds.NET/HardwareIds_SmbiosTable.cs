namespace HardwareIds.NET
{
    using System;
    using System.Linq;
    using System.Security.Cryptography;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    public static partial class HardwareIds
    {
        internal static void RetrieveSmbiosTables(Hwid InHwid, SmbiosTable? InSmbios)
        {
            try
            {
                if (InSmbios is null)
                    return;

                using var Hasher = SHA256.Create();

                InHwid.SmbiosTables.Add(new HwSmbios
                {
                    Id = InHwid.SmbiosTables.Count,
                    Version = $"{InSmbios.MajorVersion}.{InSmbios.MinorVersion}.{InSmbios.DmiRevision}",
                    Hash = string.Concat(Hasher.ComputeHash(InSmbios.Data).Select(T => T.ToString("x2"))),
                    Length = (uint) InSmbios.Data.Length,
                });
            }
            catch (Exception)
            {
                // ...
            }
        }
    }
}
