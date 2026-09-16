namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using Microsoft.Win32;

    public static partial class HardwareIds
    {
        internal static void RetrieveOperatingSystems(Hwid InHwid)
        {
            try
            {
                using var VersionKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");

                if (VersionKey is null)
                    return;

                // 
                // The registry still says "Windows 10" on Windows 11; the branding API returns the marketed name, like WMI does.
                // 

                var ProductName = WinBrand.FormatString("%WINDOWS_LONG%") ?? VersionKey.GetValue("ProductName") as string;

                if (ProductName != null && !ProductName.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase))
                    ProductName = "Microsoft " + ProductName;

                var MajorVersion = VersionKey.GetValue("CurrentMajorVersionNumber") as int?;
                var MinorVersion = VersionKey.GetValue("CurrentMinorVersionNumber") as int?;
                var BuildNumber = VersionKey.GetValue("CurrentBuildNumber") as string;
                var Version = MajorVersion != null && MinorVersion != null ? $"{MajorVersion}.{MinorVersion}.{BuildNumber}" : $"{VersionKey.GetValue("CurrentVersion")}.{BuildNumber}";
                var InstallDate = VersionKey.GetValue("InstallDate") is int InstallTimestamp ? DateTimeOffset.FromUnixTimeSeconds(unchecked((uint) InstallTimestamp)).LocalDateTime : default;

                InHwid.OperatingSystems.Add(new HwOperatingSystem
                {
                    Id = InHwid.OperatingSystems.Count,
                    Name = ProductName,
                    Version = Version,
                    Architecture = GetOperatingSystemArchitecture(),
                    RegisteredUser = VersionKey.GetValue("RegisteredOwner") as string,
                    SerialNumber = VersionKey.GetValue("ProductId") as string,
                    InstallDate = InstallDate,
                    LastBootUpTime = DateTime.Now - TimeSpan.FromMilliseconds(Kernel32.GetTickCount64()),
                });
            }
            catch (Exception)
            {
                // ...
            }
        }

        /// <summary>
        /// Gets the operating system architecture using the same wording as WMI.
        /// </summary>
        internal static string GetOperatingSystemArchitecture()
        {
            Kernel32.GetNativeSystemInfo(out var SystemInfo);

            switch (SystemInfo.wProcessorArchitecture)
            {
                case Kernel32.PROCESSOR_ARCHITECTURE_AMD64:
                    return "64-bit";

                case Kernel32.PROCESSOR_ARCHITECTURE_ARM64:
                    return "ARM 64-bit";

                case Kernel32.PROCESSOR_ARCHITECTURE_ARM:
                    return "ARM 32-bit";

                case Kernel32.PROCESSOR_ARCHITECTURE_INTEL:
                    return "32-bit";

                default:
                    return Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
            }
        }
    }
}
