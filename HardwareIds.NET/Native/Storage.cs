namespace HardwareIds.NET.Native
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

    /// <summary>
    /// An identification descriptor of the SCSI VPD page 0x83 (Device Identification).
    /// </summary>
    internal sealed class ScsiDeviceIdentifier
    {
        public const int CodeSetBinary = 1;
        public const int CodeSetAscii = 2;
        public const int CodeSetUtf8 = 3;
        public const int TypeVendorSpecific = 0;
        public const int TypeT10VendorId = 1;
        public const int TypeEui64 = 2;
        public const int TypeNaa = 3;
        public const int TypeScsiNameString = 8;
        public const int AssociationLogicalUnit = 0;

        public int CodeSet { get; set; }
        public int Type { get; set; }
        public int Association { get; set; }
        public byte[] Value { get; set; } = [];

        /// <summary>
        /// Gets the identifier as text: hexadecimal for binary identifiers, the trimmed string otherwise.
        /// </summary>
        public string Text => this.CodeSet switch
        {
            CodeSetAscii => Encoding.ASCII.GetString(this.Value).Trim('\0', ' '),
            CodeSetUtf8 => Encoding.UTF8.GetString(this.Value).Trim('\0', ' '),
            _ => Storage.FormatHex(this.Value),
        };
    }

    internal sealed class NvmeControllerIdentity
    {
        public string? SerialNumber { get; set; }
        public string? ModelNumber { get; set; }
        public string? FirmwareRevision { get; set; }
        public string? FruGuid { get; set; }
    }

    internal sealed class NvmeNamespaceIdentity
    {
        public string? Nguid { get; set; }
        public string? Eui64 { get; set; }
    }

    internal sealed class AtaIdentity
    {
        public string? SerialNumber { get; set; }
        public string? ModelNumber { get; set; }
        public string? FirmwareRevision { get; set; }
        public string? WorldWideName { get; set; }
    }

    internal static unsafe class Storage
    {
        public const uint IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x002D1080;
        public const uint IOCTL_STORAGE_PREDICT_FAILURE = 0x002D1100;
        public const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;
        public const uint IOCTL_DISK_GET_DRIVE_LAYOUT_EX = 0x00070050;
        public const uint IOCTL_DISK_GET_DRIVE_GEOMETRY_EX = 0x000700A0;
        public const uint StorageDeviceProperty = 0;
        public const uint StorageDeviceIdProperty = 2;
        public const uint StorageDeviceUniqueIdProperty = 3;
        public const uint StorageDeviceProtocolSpecificProperty = 50;
        public const uint ProtocolTypeAta = 2;
        public const uint ProtocolTypeNvme = 3;
        public const uint AtaDataTypeIdentify = 1;
        public const uint NVMeDataTypeIdentify = 1;
        public const uint BusTypeAtapi = 2;
        public const uint BusTypeAta = 3;
        public const uint BusTypeSata = 11;
        public const uint BusTypeNvme = 17;
        private const int STORAGE_PROPERTY_QUERY_SIZE = 12;
        private const int STORAGE_PROTOCOL_SPECIFIC_DATA_SIZE = 40;
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
            var Header = QueryProperty(InHandle, StorageDeviceProperty, null, 8);

            if (Header == null || Header.Length < 8)
                return null;

            var Buffer = QueryProperty(InHandle, StorageDeviceProperty, null, (int) Math.Max(BitConverter.ToUInt32(Header, 4), 36u));

            if (Buffer == null || Buffer.Length < 36)
                return null;

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
        /// Gets the SCSI device identifiers (VPD page 0x83) of the device.
        /// </summary>
        /// <param name="InHandle">The device handle.</param>
        public static List<ScsiDeviceIdentifier> GetDeviceIdentifiers(SafeFileHandle InHandle)
        {
            var Buffer = QueryProperty(InHandle, StorageDeviceIdProperty, null, 4096);
            return Buffer != null ? ParseDeviceIdentifiers(Buffer) : [];
        }

        /// <summary>
        /// Gets the SHA-256 hash of the unique identifier (DUID) Windows computes for the device. The DUID itself is
        /// a few hundred bytes (it embeds the device descriptor and the SCSI identifiers), so only its hash is kept.
        /// </summary>
        /// <param name="InHandle">The device handle.</param>
        public static string? GetUniqueId(SafeFileHandle InHandle)
        {
            var Buffer = QueryProperty(InHandle, StorageDeviceUniqueIdProperty, null, 4096);

            if (Buffer == null || Buffer.Length < 20)
                return null;

            var Size = (int) Math.Min(BitConverter.ToUInt32(Buffer, 4), (uint) Buffer.Length);

            if (Size <= 20)
                return null;

            using var Hasher = System.Security.Cryptography.SHA256.Create();
            return string.Concat(Hasher.ComputeHash(Buffer, 0, Size).Select(T => T.ToString("x2")));
        }

        /// <summary>
        /// Gets the NVMe Identify Controller data of the device.
        /// </summary>
        /// <param name="InHandle">The device handle.</param>
        public static NvmeControllerIdentity? GetNvmeControllerIdentity(SafeFileHandle InHandle)
        {
            var Data = QueryProtocolData(InHandle, ProtocolTypeNvme, NVMeDataTypeIdentify, 1, 0, 4096);
            return Data != null ? ParseNvmeControllerIdentity(Data) : null;
        }

        /// <summary>
        /// Gets the NVMe Identify Namespace data of the first namespace of the device.
        /// </summary>
        /// <param name="InHandle">The device handle.</param>
        public static NvmeNamespaceIdentity? GetNvmeNamespaceIdentity(SafeFileHandle InHandle)
        {
            var Data = QueryProtocolData(InHandle, ProtocolTypeNvme, NVMeDataTypeIdentify, 0, 1, 4096);
            return Data != null ? ParseNvmeNamespaceIdentity(Data) : null;
        }

        /// <summary>
        /// Gets the ATA IDENTIFY DEVICE data of the device.
        /// </summary>
        /// <param name="InHandle">The device handle.</param>
        public static AtaIdentity? GetAtaIdentity(SafeFileHandle InHandle)
        {
            var Data = QueryProtocolData(InHandle, ProtocolTypeAta, AtaDataTypeIdentify, 0, 0, 512);
            return Data != null ? ParseAtaIdentity(Data) : null;
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
            var Buffer = GetDriveLayout(InHandle);

            if (Buffer == null)
                return null;

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
        /// Gets the identifier of the partition table: the GPT disk GUID, or the MBR disk signature.
        /// </summary>
        /// <param name="InHandle">The device handle.</param>
        public static string? GetDiskIdentifier(SafeFileHandle InHandle)
        {
            var Buffer = GetDriveLayout(InHandle);
            return Buffer != null ? ParseDiskIdentifier(Buffer) : null;
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

        /// <summary>
        /// Parses a STORAGE_DEVICE_ID_DESCRIPTOR: a header followed by STORAGE_IDENTIFIER structures, one per
        /// SCSI VPD page 0x83 identification descriptor (Windows unpacks the page; the raw bytes are not returned).
        /// </summary>
        /// <param name="InDescriptor">The descriptor bytes.</param>
        public static List<ScsiDeviceIdentifier> ParseDeviceIdentifiers(byte[] InDescriptor)
        {
            const int HeaderSize = 12;
            const int IdentifierHeaderSize = 16;
            var Result = new List<ScsiDeviceIdentifier>();

            if (InDescriptor.Length < HeaderSize)
                return Result;

            var Count = BitConverter.ToInt32(InDescriptor, 8);
            var Offset = HeaderSize;

            for (var I = 0; I < Count && Offset + IdentifierHeaderSize <= InDescriptor.Length; I++)
            {
                var Size = BitConverter.ToUInt16(InDescriptor, Offset + 8);
                var NextOffset = BitConverter.ToUInt16(InDescriptor, Offset + 10);
                var Value = new byte[Math.Min(Size, InDescriptor.Length - Offset - IdentifierHeaderSize)];
                Array.Copy(InDescriptor, Offset + IdentifierHeaderSize, Value, 0, Value.Length);

                Result.Add(new ScsiDeviceIdentifier
                {
                    CodeSet = BitConverter.ToInt32(InDescriptor, Offset),
                    Type = BitConverter.ToInt32(InDescriptor, Offset + 4),
                    Association = BitConverter.ToInt32(InDescriptor, Offset + 12),
                    Value = Value,
                });

                if (NextOffset == 0)
                    break;

                Offset += NextOffset;
            }

            return Result;
        }

        /// <summary>
        /// Parses the NVMe Identify Controller data structure.
        /// </summary>
        /// <param name="InData">The 4096-byte identify data.</param>
        public static NvmeControllerIdentity? ParseNvmeControllerIdentity(byte[] InData)
        {
            if (InData.Length < 128)
                return null;

            return new NvmeControllerIdentity
            {
                SerialNumber = ReadAsciiField(InData, 4, 20),
                ModelNumber = ReadAsciiField(InData, 24, 40),
                FirmwareRevision = ReadAsciiField(InData, 64, 8),
                FruGuid = ReadHexField(InData, 112, 16),
            };
        }

        /// <summary>
        /// Parses the NVMe Identify Namespace data structure.
        /// </summary>
        /// <param name="InData">The 4096-byte identify data.</param>
        public static NvmeNamespaceIdentity? ParseNvmeNamespaceIdentity(byte[] InData)
        {
            if (InData.Length < 128)
                return null;

            return new NvmeNamespaceIdentity
            {
                Nguid = ReadHexField(InData, 104, 16),
                Eui64 = ReadHexField(InData, 120, 8),
            };
        }

        /// <summary>
        /// Parses the ATA IDENTIFY DEVICE data structure.
        /// </summary>
        /// <param name="InData">The 512-byte identify data.</param>
        public static AtaIdentity? ParseAtaIdentity(byte[] InData)
        {
            if (InData.Length < 512)
                return null;

            var WwnSupported = (BitConverter.ToUInt16(InData, 84 * 2) & 0x0100) != 0 || (BitConverter.ToUInt16(InData, 87 * 2) & 0x0100) != 0;
            var WorldWideName = ((ulong) BitConverter.ToUInt16(InData, 108 * 2) << 48) | ((ulong) BitConverter.ToUInt16(InData, 109 * 2) << 32) | ((ulong) BitConverter.ToUInt16(InData, 110 * 2) << 16) | BitConverter.ToUInt16(InData, 111 * 2);

            return new AtaIdentity
            {
                SerialNumber = ReadAtaString(InData, 10, 10),
                FirmwareRevision = ReadAtaString(InData, 23, 4),
                ModelNumber = ReadAtaString(InData, 27, 20),
                WorldWideName = WwnSupported && WorldWideName != 0 ? WorldWideName.ToString("X16") : null,
            };
        }

        /// <summary>
        /// Parses the identifier of a DRIVE_LAYOUT_INFORMATION_EX header: the GPT disk GUID, or the MBR signature.
        /// </summary>
        /// <param name="InLayout">The layout bytes.</param>
        public static string? ParseDiskIdentifier(byte[] InLayout)
        {
            if (InLayout.Length < 24)
                return null;

            switch (BitConverter.ToUInt32(InLayout, 0))
            {
                case 0: // PARTITION_STYLE_MBR
                    var Signature = BitConverter.ToUInt32(InLayout, 8);
                    return Signature != 0 ? "0x" + Signature.ToString("X8") : null;

                case 1: // PARTITION_STYLE_GPT
                    var DiskId = new byte[16];
                    Array.Copy(InLayout, 8, DiskId, 0, 16);
                    var Guid = new Guid(DiskId);
                    return Guid != Guid.Empty ? Guid.ToString() : null;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Formats bytes as upper-case hexadecimal.
        /// </summary>
        /// <param name="InBytes">The bytes.</param>
        public static string FormatHex(byte[] InBytes)
        {
            return string.Concat(InBytes.Select(T => T.ToString("X2")));
        }

        private static byte[]? GetDriveLayout(SafeFileHandle InHandle)
        {
            var Buffer = new byte[DRIVE_LAYOUT_HEADER_SIZE + MAX_PARTITIONS * PARTITION_INFORMATION_EX_SIZE];

            fixed (byte* BufferPtr = Buffer)
            {
                if (!Kernel32.DeviceIoControl(InHandle, IOCTL_DISK_GET_DRIVE_LAYOUT_EX, null, 0, BufferPtr, (uint) Buffer.Length, out _, IntPtr.Zero))
                    return null;
            }

            return Buffer;
        }

        /// <summary>
        /// Issues an IOCTL_STORAGE_QUERY_PROPERTY standard query and returns the bytes it returned.
        /// </summary>
        private static byte[]? QueryProperty(SafeFileHandle InHandle, uint InPropertyId, byte[]? InAdditionalParameters, int InOutputSize)
        {
            var Query = new byte[Math.Max(STORAGE_PROPERTY_QUERY_SIZE, 8 + (InAdditionalParameters?.Length ?? 0))];
            var Output = new byte[InOutputSize];
            uint Returned;

            Array.Copy(BitConverter.GetBytes(InPropertyId), 0, Query, 0, 4);
            InAdditionalParameters?.CopyTo(Query, 8);

            fixed (byte* QueryPtr = Query)
            fixed (byte* OutputPtr = Output)
            {
                if (!Kernel32.DeviceIoControl(InHandle, IOCTL_STORAGE_QUERY_PROPERTY, QueryPtr, (uint) Query.Length, OutputPtr, (uint) Output.Length, out Returned, IntPtr.Zero))
                    return null;
            }

            if (Returned < Output.Length)
                Array.Resize(ref Output, (int) Returned);

            return Output;
        }

        /// <summary>
        /// Issues a protocol-specific query (NVMe or ATA identify) and returns the protocol data it returned.
        /// </summary>
        private static byte[]? QueryProtocolData(SafeFileHandle InHandle, uint InProtocolType, uint InDataType, uint InRequestValue, uint InRequestSubValue, int InDataLength)
        {
            var Parameters = new byte[STORAGE_PROTOCOL_SPECIFIC_DATA_SIZE];

            Array.Copy(BitConverter.GetBytes(InProtocolType), 0, Parameters, 0, 4);
            Array.Copy(BitConverter.GetBytes(InDataType), 0, Parameters, 4, 4);
            Array.Copy(BitConverter.GetBytes(InRequestValue), 0, Parameters, 8, 4);
            Array.Copy(BitConverter.GetBytes(InRequestSubValue), 0, Parameters, 12, 4);
            Array.Copy(BitConverter.GetBytes((uint) STORAGE_PROTOCOL_SPECIFIC_DATA_SIZE), 0, Parameters, 16, 4);
            Array.Copy(BitConverter.GetBytes((uint) InDataLength), 0, Parameters, 20, 4);

            var Output = QueryProperty(InHandle, StorageDeviceProtocolSpecificProperty, Parameters, 8 + STORAGE_PROTOCOL_SPECIFIC_DATA_SIZE + InDataLength);

            if (Output == null || Output.Length < 8 + STORAGE_PROTOCOL_SPECIFIC_DATA_SIZE)
                return null;

            var DataOffset = 8 + (int) BitConverter.ToUInt32(Output, 8 + 16);
            var DataLength = (int) BitConverter.ToUInt32(Output, 8 + 20);

            if (DataOffset < 8 || DataLength <= 0 || DataOffset + DataLength > Output.Length)
                return null;

            var Data = new byte[DataLength];
            Array.Copy(Output, DataOffset, Data, 0, DataLength);
            return Data;
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

        private static string? ReadAsciiField(byte[] InData, int InOffset, int InLength)
        {
            var Value = Encoding.ASCII.GetString(InData, InOffset, InLength).Trim('\0', ' ');
            return Value.Length > 0 ? Value : null;
        }

        private static string? ReadHexField(byte[] InData, int InOffset, int InLength)
        {
            var Value = new byte[InLength];
            Array.Copy(InData, InOffset, Value, 0, InLength);
            return Value.Any(T => T != 0) ? FormatHex(Value) : null;
        }

        /// <summary>
        /// Reads an ATA string field, whose characters are stored swapped within each 16-bit word.
        /// </summary>
        private static string? ReadAtaString(byte[] InData, int InWord, int InWordCount)
        {
            var Value = new byte[InWordCount * 2];

            for (var I = 0; I < InWordCount; I++)
            {
                Value[I * 2] = InData[(InWord + I) * 2 + 1];
                Value[I * 2 + 1] = InData[(InWord + I) * 2];
            }

            var Text = Encoding.ASCII.GetString(Value).Trim('\0', ' ');
            return Text.Length > 0 ? Text : null;
        }
    }
}
