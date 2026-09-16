namespace HardwareIds.NET.Native
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;

    internal sealed class DisplayModeInfo
    {
        public uint Width { get; set; }
        public uint Height { get; set; }
        public uint RefreshRate { get; set; }
    }

    internal static unsafe class User32
    {
        public const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
        public const uint DISPLAYCONFIG_PATH_ACTIVE = 0x00000001;
        public const uint DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE = 1;
        public const uint DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME = 4;
        public const uint DISPLAYCONFIG_PATH_MODE_IDX_INVALID = 0xFFFFFFFF;
        private const int DISPLAYCONFIG_PATH_INFO_SIZE = 72;
        private const int DISPLAYCONFIG_MODE_INFO_SIZE = 64;
        private const int DISPLAYCONFIG_ADAPTER_NAME_SIZE = 20 + 128 * 2;

        [DllImport("user32.dll")]
        public static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        public static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, byte* pathArray, ref uint numModeInfoArrayElements, byte* modeInfoArray, IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(byte* requestPacket);

        /// <summary>
        /// Gets the current display mode of every video adapter with an active display path, keyed by the adapter's device instance identifier.
        /// </summary>
        public static Dictionary<string, DisplayModeInfo> GetActiveDisplayModes()
        {
            var Result = new Dictionary<string, DisplayModeInfo>(StringComparer.OrdinalIgnoreCase);

            for (var Attempt = 0; Attempt < 3; Attempt++)
            {
                if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out var PathCount, out var ModeCount) != 0 || PathCount == 0)
                    return Result;

                var Paths = new byte[PathCount * DISPLAYCONFIG_PATH_INFO_SIZE];
                var Modes = new byte[Math.Max(ModeCount, 1) * DISPLAYCONFIG_MODE_INFO_SIZE];
                int Status;

                fixed (byte* PathsPtr = Paths)
                fixed (byte* ModesPtr = Modes)
                    Status = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref PathCount, PathsPtr, ref ModeCount, ModesPtr, IntPtr.Zero);

                if (Status == Kernel32.ERROR_INSUFFICIENT_BUFFER)
                    continue;

                if (Status != 0)
                    return Result;

                for (var I = 0; I < PathCount; I++)
                {
                    var Path = I * DISPLAYCONFIG_PATH_INFO_SIZE;

                    if ((BitConverter.ToUInt32(Paths, Path + 68) & DISPLAYCONFIG_PATH_ACTIVE) == 0)
                        continue;

                    var AdapterId = BitConverter.ToInt64(Paths, Path + 0);
                    var SourceModeIndex = BitConverter.ToUInt32(Paths, Path + 12);
                    var RefreshNumerator = BitConverter.ToUInt32(Paths, Path + 48);
                    var RefreshDenominator = BitConverter.ToUInt32(Paths, Path + 52);
                    var InstanceId = InterfaceNameToInstanceId(GetAdapterDevicePath(AdapterId));

                    if (InstanceId == null || Result.ContainsKey(InstanceId))
                        continue;

                    var Mode = new DisplayModeInfo { RefreshRate = RefreshDenominator != 0 ? (uint) Math.Round((double) RefreshNumerator / RefreshDenominator) : 0 };

                    if (SourceModeIndex != DISPLAYCONFIG_PATH_MODE_IDX_INVALID && SourceModeIndex < ModeCount)
                    {
                        var ModeOffset = (int) SourceModeIndex * DISPLAYCONFIG_MODE_INFO_SIZE;

                        if (BitConverter.ToUInt32(Modes, ModeOffset) == DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
                        {
                            Mode.Width = BitConverter.ToUInt32(Modes, ModeOffset + 16);
                            Mode.Height = BitConverter.ToUInt32(Modes, ModeOffset + 20);
                        }
                    }

                    Result[InstanceId] = Mode;
                }

                return Result;
            }

            return Result;
        }

        /// <summary>
        /// Gets the device interface path of a video adapter from its LUID.
        /// </summary>
        /// <param name="InAdapterId">The adapter LUID.</param>
        private static string? GetAdapterDevicePath(long InAdapterId)
        {
            var Packet = new byte[DISPLAYCONFIG_ADAPTER_NAME_SIZE];

            Array.Copy(BitConverter.GetBytes(DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME), 0, Packet, 0, 4);
            Array.Copy(BitConverter.GetBytes(Packet.Length), 0, Packet, 4, 4);
            Array.Copy(BitConverter.GetBytes(InAdapterId), 0, Packet, 8, 8);

            fixed (byte* PacketPtr = Packet)
            {
                if (DisplayConfigGetDeviceInfo(PacketPtr) != 0)
                    return null;
            }

            var DevicePath = Encoding.Unicode.GetString(Packet, 20, Packet.Length - 20);
            var End = DevicePath.IndexOf('\0');

            return End >= 0 ? DevicePath.Substring(0, End) : DevicePath;
        }

        /// <summary>
        /// Converts a device interface name into the device instance identifier it was derived from.
        /// </summary>
        /// <param name="InInterfaceName">The device interface name.</param>
        public static string? InterfaceNameToInstanceId(string? InInterfaceName)
        {
            if (string.IsNullOrEmpty(InInterfaceName))
                return null;

            var Value = InInterfaceName!;

            if (Value.StartsWith(@"\\?\", StringComparison.Ordinal))
                Value = Value.Substring(4);

            var End = Value.IndexOf("#{", StringComparison.Ordinal);

            if (End > 0)
                Value = Value.Substring(0, End);

            return Value.Replace('#', '\\');
        }
    }
}
