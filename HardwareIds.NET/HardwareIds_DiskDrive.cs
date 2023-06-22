namespace HardwareIds.NET
{
    using System;
    using System.Linq;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using WindowsMonitor.Hardware.Drives.Win32DiskDrives;

    public static partial class HardwareIds
    {
        private static void RetrieveDiskDrives(Hwid InHwid)
        {
            try
            {
                foreach (var Disk in Win32DiskDrive.Retrieve().OrderBy(T => T.Index))
                {
                    InHwid.Disks.Add(new HwDisk
                    {
                        Id = (int) Disk.Index,
                        Interface = Disk.InterfaceType,
                        Model = Disk.Model,
                        SerialNumber = Disk.SerialNumber,
                        Capacity = (Disk.Size / 1024 / 1024 / 1024) + " GB",
                        Partitions = (int) Disk.Partitions,
                        IsRemovable = Disk.CapabilityDescriptions.Any(T => T.Contains("Removable")),
                        IsSMART = Disk.CapabilityDescriptions.Any(T => T.Contains("SMART") || T.Contains("S.M.A.R.T")),
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
