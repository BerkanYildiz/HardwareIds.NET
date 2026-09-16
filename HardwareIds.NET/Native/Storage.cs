namespace HardwareIds.NET.Native
{
    using System;
    using System.Text;

    using Microsoft.Win32.SafeHandles;

    internal sealed class StorageDeviceDescriptor
    {
        public bool RemovableMedia { get; set; }
        public uint BusType { get; set; }
        public string? VendorId { get; set; }
        public string? ProductId { get; set; }
        public string? ProductRevision { get; set; }
        public string? SerialNumber { get; set; }
    }

    internal static unsafe class Storage
    {
        public const uint IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x002D1080;
        public const uint IOCTL_STORAGE_PREDICT_FAILURE = 0x002D1100;
        public const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;
        public const uint IOCTL_DISK_GET_DRIVE_LAYOUT_EX = 0x00070050;
        public const uint IOCTL_DISK_GET_DRIVE_GEOMETRY_EX = 0x000700A0;
        private const int DRIVE_LAYOUT_HEADER_SIZE = 48;
        private const int PARTITION_INFORMATION_EX_SIZE = 144;
        private const int MAX_PARTITIONS = 256;
        private static readonly Guid PARTITION_MSFT_RESERVED_GUID = new("E3C9E316-0B5C-4DB8-817D-F92DF00215AE");

        /// <summary>
        /// Opens a storage device for querying, without requiring read access to its contents.
        /// </summary>
        /// <param name="InPath">The device path.</param>
        public static SafeFileHandle? Open(string InPath)
        {
            var Handle = Kernel32.CreateFileW(InPath, 0, Kernel32.FILE_SHARE_READ | Kernel32.FILE_SHARE_WRITE, IntPtr.Zero, Kernel32.OPEN_EXISTING, 0, IntPtr.Zero);

            if (!Handle.IsInvalid)
                return Handle;

            Handle.Dispose();
            return null;
        }

        /// <summary>
        /// Gets the physical drive number of the device.
        /// </summary>
        /// <param name="InHandle">The device handle.</param>
        public static int? GetDeviceNumber(SafeFileHandle InHandle)
        {
            var Buffer = new byte[12];

            fixed (byte* BufferPtr = Buffer)
            {
                if (!Kernel32.DeviceIoControl(InHandle, IOCTL_STORAGE_GET_DEVICE_NUMBER, null, 0, BufferPtr, (uint) Buffer.Length, out _, IntPtr.Zero))
                    return null;
            }

            return BitConverter.ToInt32(Buffer, 4);
        }

        /// <summary>
        /// Gets the device descriptor (vendor, product, serial number, bus type) of the device.
        /// </summary>
        /// <param name="InHandle">The device handle.</param>
        public static StorageDeviceDescriptor? GetDeviceDescriptor(SafeFileHandle InHandle)
        {
            var Query = new byte[12];
            var Header = new byte[8];

            fixed (byte* QueryPtr = Query)
            fixed (byte* HeaderPtr = Header)
            {
                if (!Kernel32.DeviceIoControl(InHandle, IOCTL_STORAGE_QUERY_PROPERTY, QueryPtr, (uint) Query.Length, HeaderPtr, (uint) Header.Length, out _, IntPtr.Zero))
                    return null;
            }

            var Size = Math.Max(BitConverter.ToUInt32(Header, 4), 36u);
            var Buffer = new byte[Size];

            fixed (byte* QueryPtr = Query)
            fixed (byte* BufferPtr = Buffer)
            {
                if (!Kernel32.DeviceIoControl(InHandle, IOCTL_STORAGE_QUERY_PROPERTY, QueryPtr, (uint) Query.Length, BufferPtr, Size, out _, IntPtr.Zero))
                    return null;
            }

            return new StorageDeviceDescriptor
            {
                RemovableMedia = Buffer[10] != 0,
                BusType = BitConverter.ToUInt32(Buffer, 28),
                VendorId = ReadOffsetString(Buffer, 12),
                ProductId = ReadOffsetString(Buffer, 16),
                ProductRevision = ReadOffsetString(Buffer, 20),
                SerialNumber = ReadOffsetString(Buffer, 24),
            };
        }

        /// <summary>
        /// Gets the size of the disk in bytes, computed from its geometry the same way WMI does.
        /// </summary>
        /// <param name="InHandle">The device handle.</param>
        public static ulong? GetSize(SafeFileHandle InHandle)
        {
            var Buffer = new byte[1024];

            fixed (byte* BufferPtr = Buffer)
            {
                if (!Kernel32.DeviceIoControl(InHandle, IOCTL_DISK_GET_DRIVE_GEOMETRY_EX, null, 0, BufferPtr, (uint) Buffer.Length, out _, IntPtr.Zero))
                    return null;
            }

            var Cylinders = (ulong) BitConverter.ToInt64(Buffer, 0);
            var TracksPerCylinder = BitConverter.ToUInt32(Buffer, 12);
            var SectorsPerTrack = BitConverter.ToUInt32(Buffer, 16);
            var BytesPerSector = BitConverter.ToUInt32(Buffer, 20);
            var Size = Cylinders * TracksPerCylinder * SectorsPerTrack * BytesPerSector;

            return Size != 0 ? Size : BitConverter.ToUInt64(Buffer, 24);
        }

        /// <summary>
        /// Gets the number of partitions in use on the disk.
        /// </summary>
        /// <param name="InHandle">The device handle.</param>
        public static int? GetPartitionCount(SafeFileHandle InHandle)
        {
            var Buffer = new byte[DRIVE_LAYOUT_HEADER_SIZE + MAX_PARTITIONS * PARTITION_INFORMATION_EX_SIZE];

            fixed (byte* BufferPtr = Buffer)
            {
                if (!Kernel32.DeviceIoControl(InHandle, IOCTL_DISK_GET_DRIVE_LAYOUT_EX, null, 0, BufferPtr, (uint) Buffer.Length, out _, IntPtr.Zero))
                    return null;
            }

            var Style = BitConverter.ToUInt32(Buffer, 0);
            var Count = Math.Min(BitConverter.ToInt32(Buffer, 4), MAX_PARTITIONS);
            var Result = 0;

            for (var I = 0; I < Count; I++)
            {
                var Entry = DRIVE_LAYOUT_HEADER_SIZE + I * PARTITION_INFORMATION_EX_SIZE;

                switch (Style)
                {
                    case 0: // PARTITION_STYLE_MBR
                        var PartitionType = Buffer[Entry + 32];

                        if (PartitionType != 0x00 && PartitionType != 0x05 && PartitionType != 0x0F && PartitionType != 0x85)
                            Result++;

                        break;

                    case 1: // PARTITION_STYLE_GPT
                        var TypeGuid = new byte[16];
                        Array.Copy(Buffer, Entry + 32, TypeGuid, 0, 16);
                        var PartitionTypeGuid = new Guid(TypeGuid);

                        //
                        // WMI does not report empty entries nor the Microsoft Reserved partition.
                        //

                        if (PartitionTypeGuid != Guid.Empty && PartitionTypeGuid != PARTITION_MSFT_RESERVED_GUID)
                            Result++;

                        break;
                }
            }

            return Result;
        }

        /// <summary>
        /// Gets a value indicating whether the device supports failure prediction (S.M.A.R.T.).
        /// </summary>
        /// <param name="InHandle">The device handle.</param>
        public static bool SupportsFailurePrediction(SafeFileHandle InHandle)
        {
            var Buffer = new byte[516];

            fixed (byte* BufferPtr = Buffer)
                return Kernel32.DeviceIoControl(InHandle, IOCTL_STORAGE_PREDICT_FAILURE, null, 0, BufferPtr, (uint) Buffer.Length, out _, IntPtr.Zero);
        }

        private static string? ReadOffsetString(byte[] InBuffer, int InOffsetField)
        {
            var Offset = BitConverter.ToUInt32(InBuffer, InOffsetField);

            if (Offset == 0 || Offset == 0xFFFFFFFF || Offset >= InBuffer.Length)
                return null;

            var End = Array.IndexOf(InBuffer, (byte) 0, (int) Offset);

            if (End < 0)
                End = InBuffer.Length;

            return Encoding.ASCII.GetString(InBuffer, (int) Offset, End - (int) Offset);
        }
    }
}
