namespace HardwareIds.NET
{
    using System;
    using System.Linq;
    using System.Text;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    public static partial class HardwareIds
    {
        internal static void RetrieveDiskVolumes(Hwid InHwid)
        {
            try
            {
                // 
                // Prevent Windows from showing an "insert a disk" dialog for removable drives without media.
                // 

                Kernel32.SetThreadErrorMode(Kernel32.SEM_FAILCRITICALERRORS, out var PreviousErrorMode);

                try
                {
                    var VolumeName = new StringBuilder(260);
                    var Find = Kernel32.FindFirstVolumeW(VolumeName, (uint) VolumeName.Capacity);

                    if (Find == Kernel32.INVALID_HANDLE_VALUE)
                        return;

                    try
                    {
                        do
                        {
                            var Path = VolumeName.ToString();

                            if (!Kernel32.GetVolumeInformationW(Path, null, 0, out var SerialNumber, out _, out _, null, 0))
                                SerialNumber = 0;

                            InHwid.Volumes.Add(new HwVolume
                            {
                                Id = InHwid.Volumes.Count,
                                Path = Path,
                                Letter = GetVolumeDriveLetter(Path),
                                SerialNumber = SerialNumber,
                            });
                        }
                        while (Kernel32.FindNextVolumeW(Find, VolumeName, (uint) VolumeName.Capacity));
                    }
                    finally
                    {
                        Kernel32.FindVolumeClose(Find);
                    }
                }
                finally
                {
                    Kernel32.SetThreadErrorMode(PreviousErrorMode, out _);
                }
            }
            catch (Exception)
            {
                // ...
            }
        }

        /// <summary>
        /// Gets the drive letter ("C:") a volume is mounted on, if any.
        /// </summary>
        /// <param name="InVolumePath">The volume GUID path.</param>
        internal static unsafe string? GetVolumeDriveLetter(string InVolumePath)
        {
            var Buffer = new char[1024];
            uint Length;

            fixed (char* BufferPtr = Buffer)
            {
                if (!Kernel32.GetVolumePathNamesForVolumeNameW(InVolumePath, BufferPtr, (uint) Buffer.Length, out Length))
                    return null;
            }

            return Kernel32.SplitMultiString(Buffer, (int) Math.Min(Length, (uint) Buffer.Length))
                           .FirstOrDefault(T => T.Length == 3 && T[1] == ':' && T[2] == '\\')?
                           .Substring(0, 2);
        }
    }
}
