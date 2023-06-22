namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using WindowsMonitor.Windows;

    public static partial class HardwareIds
    {
        private static void RetrieveOperatingSystems(Hwid InHwid)
        {
            try
            {
                foreach (var OperatingSystem in Win32OperatingSystem.Retrieve())
                {
                    InHwid.OperatingSystems.Add(new HwOperatingSystem
                    {
                        Id = (int) InHwid.OperatingSystems.Count,
                        Name = OperatingSystem.Caption,
                        Version = OperatingSystem.Version,
                        Architecture = OperatingSystem.OsArchitecture,
                        RegisteredUser = OperatingSystem.RegisteredUser,
                        SerialNumber = OperatingSystem.SerialNumber,
                        InstallDate = OperatingSystem.InstallDate,
                        LastBootUpTime = OperatingSystem.LastBootUpTime,
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
