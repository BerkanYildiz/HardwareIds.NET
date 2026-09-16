namespace HardwareIds.NET.Native
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    using Microsoft.Win32.SafeHandles;

    internal sealed class BatteryInfo
    {
        public string? DeviceName { get; set; }
        public string? Manufacturer { get; set; }
        public string? SerialNumber { get; set; }
        public string? UniqueId { get; set; }
        public string? Chemistry { get; set; }
        public uint DesignedCapacity { get; set; }
        public uint FullChargedCapacity { get; set; }
        public DateTime? ManufactureDate { get; set; }
    }

    internal static unsafe class Battery
    {
        public static readonly Guid GUID_DEVICE_BATTERY = new("72631E54-78A4-11D0-BCF7-00AA00B7B32A");
        public const uint IOCTL_BATTERY_QUERY_TAG = 0x00294040;
        public const uint IOCTL_BATTERY_QUERY_INFORMATION = 0x00294044;
        public const uint BATTERY_TAG_INVALID = 0xFFFFFFFF;
        public const uint GENERIC_READ = 0x80000000;
        public const uint BatteryInformation = 0;
        public const uint BatteryDeviceName = 4;
        public const uint BatteryManufactureDate = 5;
        public const uint BatteryManufactureName = 6;
        public const uint BatteryUniqueID = 7;
        public const uint BatterySerialNumber = 8;

        /// <summary>
        /// Gets the batteries currently present in this computer.
        /// </summary>
        public static List<BatteryInfo> GetBatteries()
        {
            var Result = new List<BatteryInfo>();

            foreach (var InterfacePath in CfgMgr32.GetDeviceInterfaces(GUID_DEVICE_BATTERY))
            {
                try
                {
                    using var Handle = Kernel32.CreateFileW(InterfacePath, GENERIC_READ, Kernel32.FILE_SHARE_READ | Kernel32.FILE_SHARE_WRITE, IntPtr.Zero, Kernel32.OPEN_EXISTING, 0, IntPtr.Zero);

                    if (Handle.IsInvalid)
                        continue;

                    var Tag = QueryTag(Handle);

                    if (Tag == null || Tag == BATTERY_TAG_INVALID)
                        continue;

                    var Info = new BatteryInfo
                    {
                        DeviceName = QueryString(Handle, Tag.Value, BatteryDeviceName),
                        Manufacturer = QueryString(Handle, Tag.Value, BatteryManufactureName),
                        SerialNumber = QueryString(Handle, Tag.Value, BatterySerialNumber),
                        UniqueId = QueryString(Handle, Tag.Value, BatteryUniqueID),
                    };

                    var Information = QueryInformation(Handle, Tag.Value, BatteryInformation, 36);

                    if (Information != null && Information.Length >= 20)
                    {
                        Info.Chemistry = Encoding.ASCII.GetString(Information, 8, 4).Trim('\0', ' ');
                        Info.DesignedCapacity = BitConverter.ToUInt32(Information, 12);
                        Info.FullChargedCapacity = BitConverter.ToUInt32(Information, 16);
                    }

                    var Date = QueryInformation(Handle, Tag.Value, BatteryManufactureDate, 4);

                    if (Date != null && Date.Length >= 4)
                        Info.ManufactureDate = ParseManufactureDate(Date[0], Date[1], BitConverter.ToUInt16(Date, 2));

                    Result.Add(Info);
                }
                catch (Exception)
                {
                    // ...
                }
            }

            return Result;
        }

        /// <summary>
        /// Converts a BATTERY_MANUFACTURE_DATE into a date, or null when the battery does not report one.
        /// </summary>
        public static DateTime? ParseManufactureDate(byte InDay, byte InMonth, ushort InYear)
        {
            if (InYear < 1980 || InYear > 9999 || InMonth < 1 || InMonth > 12 || InDay < 1 || InDay > DateTime.DaysInMonth(InYear, InMonth))
                return null;

            return new DateTime(InYear, InMonth, InDay);
        }

        private static uint? QueryTag(SafeFileHandle InHandle)
        {
            var Wait = new byte[4];
            var Tag = new byte[4];

            fixed (byte* WaitPtr = Wait)
            fixed (byte* TagPtr = Tag)
            {
                if (!Kernel32.DeviceIoControl(InHandle, IOCTL_BATTERY_QUERY_TAG, WaitPtr, 4, TagPtr, 4, out _, IntPtr.Zero))
                    return null;
            }

            return BitConverter.ToUInt32(Tag, 0);
        }

        private static byte[]? QueryInformation(SafeFileHandle InHandle, uint InTag, uint InLevel, int InOutputSize)
        {
            var Query = new byte[12];
            var Output = new byte[InOutputSize];
            uint Returned;

            Array.Copy(BitConverter.GetBytes(InTag), 0, Query, 0, 4);
            Array.Copy(BitConverter.GetBytes(InLevel), 0, Query, 4, 4);

            fixed (byte* QueryPtr = Query)
            fixed (byte* OutputPtr = Output)
            {
                if (!Kernel32.DeviceIoControl(InHandle, IOCTL_BATTERY_QUERY_INFORMATION, QueryPtr, (uint) Query.Length, OutputPtr, (uint) Output.Length, out Returned, IntPtr.Zero))
                    return null;
            }

            if (Returned < Output.Length)
                Array.Resize(ref Output, (int) Returned);

            return Output;
        }

        private static string? QueryString(SafeFileHandle InHandle, uint InTag, uint InLevel)
        {
            var Output = QueryInformation(InHandle, InTag, InLevel, 1024);

            if (Output == null)
                return null;

            var Value = Encoding.Unicode.GetString(Output).Trim('\0', ' ');
            return Value.Length > 0 ? Value : null;
        }
    }
}
