namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using WindowsMonitor.Hardware.Storage.Volumes;

    public static partial class HardwareIds
    {
        private static void RetrieveDiskVolumes(Hwid InHwid)
        {
            try
            {
                foreach (var Volume in Volume.Retrieve())
                {
                    InHwid.Volumes.Add(new HwVolume
                    {
                        Id = (int) InHwid.Volumes.Count,
                        Path = Volume.DeviceId,
                        Letter = Volume.DriveLetter,
                        SerialNumber = Volume.SerialNumber,
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
