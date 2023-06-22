namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using WindowsMonitor.Hardware.Video;

    public static partial class HardwareIds
    {
        private static void RetrieveVideoControllers(Hwid InHwid)
        {
            try
            {
                foreach (var VideoController in Win32VideoController.Retrieve())
                {
                    InHwid.VideoControllers.Add(new HwVideo
                    {
                        Id = (int) InHwid.VideoControllers.Count,
                        Name = VideoController.Name,
                        Width = VideoController.CurrentHorizontalResolution,
                        Height = VideoController.CurrentVerticalResolution,
                        RefreshRate = VideoController.CurrentRefreshRate,
                        DriverDate = VideoController.DriverDate,
                        DriverVersion = VideoController.DriverVersion,
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
