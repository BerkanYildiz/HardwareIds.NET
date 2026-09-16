namespace HardwareIds.NET.Native
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// The raw SMBIOS table of the local computer, as returned by the firmware.
    /// </summary>
    internal sealed unsafe class SmbiosTable
    {
        private const uint RSMB = 0x52534D42;
        private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

        /// <summary>
        /// Gets the SMBIOS major version.
        /// </summary>
        public byte MajorVersion { get; private set; }

        /// <summary>
        /// Gets the SMBIOS minor version.
        /// </summary>
        public byte MinorVersion { get; private set; }

        /// <summary>
        /// Gets the DMI revision.
        /// </summary>
        public byte DmiRevision { get; private set; }

        /// <summary>
        /// Gets the raw table data, without the firmware table header.
        /// </summary>
        public byte[] Data { get; private set; } = [];

        /// <summary>
        /// Gets the structures contained in the table.
        /// </summary>
        public List<SmbiosStructure> Structures { get; } = [];

        /// <summary>
        /// Reads the SMBIOS table from the firmware.
        /// </summary>
        public static SmbiosTable? Read()
        {
            var Size = Kernel32.GetSystemFirmwareTable(RSMB, 0, null, 0);

            if (Size < 8)
                return null;

            var Buffer = new byte[Size];
            uint Read;

            fixed (byte* BufferPtr = Buffer)
                Read = Kernel32.GetSystemFirmwareTable(RSMB, 0, BufferPtr, Size);

            if (Read < 8)
                return null;

            var Length = (int) Math.Min(BitConverter.ToUInt32(Buffer, 4), Math.Min(Read, Size) - 8);
            var Table = new SmbiosTable { MajorVersion = Buffer[1], MinorVersion = Buffer[2], DmiRevision = Buffer[3], Data = new byte[Length] };

            Array.Copy(Buffer, 8, Table.Data, 0, Length);
            Table.Parse();

            return Table;
        }

        /// <summary>
        /// Builds a table from raw table data (without the firmware table header), mainly for testing the parser.
        /// </summary>
        /// <param name="InMajorVersion">The SMBIOS major version.</param>
        /// <param name="InMinorVersion">The SMBIOS minor version.</param>
        /// <param name="InDmiRevision">The DMI revision.</param>
        /// <param name="InData">The raw table data.</param>
        public static SmbiosTable FromData(byte InMajorVersion, byte InMinorVersion, byte InDmiRevision, byte[] InData)
        {
            var Table = new SmbiosTable { MajorVersion = InMajorVersion, MinorVersion = InMinorVersion, DmiRevision = InDmiRevision, Data = InData };
            Table.Parse();
            return Table;
        }

        /// <summary>
        /// Gets the structures of the given type.
        /// </summary>
        /// <param name="InType">The structure type.</param>
        public IEnumerable<SmbiosStructure> OfType(byte InType)
        {
            return this.Structures.Where(T => T.Type == InType);
        }

        private void Parse()
        {
            var Position = 0;

            while (Position + 4 <= this.Data.Length)
            {
                var Type = this.Data[Position];
                var Length = this.Data[Position + 1];

                if (Length < 4 || Position + Length > this.Data.Length)
                    break;

                var Handle = BitConverter.ToUInt16(this.Data, Position + 2);
                var Strings = new List<string>();
                var Cursor = Position + Length;

                while (Cursor < this.Data.Length)
                {
                    if (this.Data[Cursor] == 0)
                    {
                        Cursor++;
                        break;
                    }

                    var End = Array.IndexOf(this.Data, (byte) 0, Cursor);

                    if (End < 0)
                        End = this.Data.Length;

                    Strings.Add(Latin1.GetString(this.Data, Cursor, End - Cursor));
                    Cursor = End + 1;
                }

                if (Strings.Count == 0)
                    Cursor++;

                this.Structures.Add(new SmbiosStructure(this.Data, Position, Type, Length, Handle, Strings.ToArray()));

                if (Type == 127)
                    break;

                Position = Cursor;
            }
        }
    }

    /// <summary>
    /// A single structure of the SMBIOS table.
    /// </summary>
    internal sealed class SmbiosStructure
    {
        private readonly byte[] Data;
        private readonly int Offset;

        public byte Type { get; }
        public byte Length { get; }
        public ushort Handle { get; }
        public string[] Strings { get; }

        public SmbiosStructure(byte[] InData, int InOffset, byte InType, byte InLength, ushort InHandle, string[] InStrings)
        {
            this.Data = InData;
            this.Offset = InOffset;
            this.Type = InType;
            this.Length = InLength;
            this.Handle = InHandle;
            this.Strings = InStrings;
        }

        public byte GetByte(int InOffset)
        {
            return InOffset + 1 <= this.Length ? this.Data[this.Offset + InOffset] : (byte) 0;
        }

        public ushort GetWord(int InOffset)
        {
            return InOffset + 2 <= this.Length ? BitConverter.ToUInt16(this.Data, this.Offset + InOffset) : (ushort) 0;
        }

        public uint GetDword(int InOffset)
        {
            return InOffset + 4 <= this.Length ? BitConverter.ToUInt32(this.Data, this.Offset + InOffset) : 0u;
        }

        public byte[]? GetBytes(int InOffset, int InCount)
        {
            if (InOffset + InCount > this.Length)
                return null;

            var Result = new byte[InCount];
            Array.Copy(this.Data, this.Offset + InOffset, Result, 0, InCount);
            return Result;
        }

        /// <summary>
        /// Gets the string referenced by the 1-based string index stored at the given offset.
        /// </summary>
        /// <param name="InOffset">The offset of the string index field.</param>
        public string? GetString(int InOffset)
        {
            var Index = this.GetByte(InOffset);
            return Index == 0 || Index > this.Strings.Length ? null : this.Strings[Index - 1];
        }
    }
}
