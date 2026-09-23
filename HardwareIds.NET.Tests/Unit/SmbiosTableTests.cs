namespace HardwareIds.NET.Tests.Unit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;

    using Xunit;

    /// <summary>
    /// Builds synthetic SMBIOS tables so the parser and the collectors can be tested without firmware.
    /// </summary>
    internal sealed class SmbiosBuilder
    {
        private readonly List<byte> Bytes = [];
        private ushort NextHandle;

        /// <summary>
        /// Appends a structure. The formatted bytes start at offset 0x04 (right after the 4-byte header).
        /// </summary>
        public SmbiosBuilder Add(byte InType, byte[] InFormatted, params string[] InStrings)
        {
            this.Bytes.Add(InType);
            this.Bytes.Add((byte) (4 + InFormatted.Length));
            this.Bytes.AddRange(BitConverter.GetBytes(this.NextHandle++));
            this.Bytes.AddRange(InFormatted);

            foreach (var Text in InStrings)
            {
                this.Bytes.AddRange(Encoding.ASCII.GetBytes(Text));
                this.Bytes.Add(0);
            }

            if (InStrings.Length == 0)
                this.Bytes.Add(0);

            this.Bytes.Add(0);
            return this;
        }

        public SmbiosBuilder End()
        {
            return this.Add(127, []);
        }

        public byte[] Build()
        {
            return this.Bytes.ToArray();
        }

        public SmbiosTable BuildTable(byte InMajor = 3, byte InMinor = 4, byte InRevision = 0)
        {
            return SmbiosTable.FromData(InMajor, InMinor, InRevision, this.Build());
        }

        public static byte[] Formatted(int InLength, params (int Offset, byte[] Value)[] InFields)
        {
            var Result = new byte[InLength];

            foreach (var (Offset, Value) in InFields)
                Array.Copy(Value, 0, Result, Offset - 4, Value.Length);

            return Result;
        }

        public static byte[] Bios(byte InVendor = 1, byte InVersion = 2, byte InReleaseDate = 3)
        {
            return Formatted(0x18 - 4, (0x04, [InVendor]), (0x05, [InVersion]), (0x08, [InReleaseDate]));
        }

        public static byte[] System(byte[] InUuid, byte InManufacturer = 1, byte InProduct = 2, byte InVersion = 3, byte InSerial = 4)
        {
            return Formatted(0x1B - 4, (0x04, [InManufacturer]), (0x05, [InProduct]), (0x06, [InVersion]), (0x07, [InSerial]), (0x08, InUuid));
        }

        public static byte[] Baseboard(byte InManufacturer = 1, byte InProduct = 2, byte InVersion = 3, byte InSerial = 4)
        {
            return Formatted(0x0F - 4, (0x04, [InManufacturer]), (0x05, [InProduct]), (0x06, [InVersion]), (0x07, [InSerial]));
        }

        public static byte[] Chassis(byte InType, byte InManufacturer = 1, byte InVersion = 2, byte InSerial = 3, byte InAssetTag = 4)
        {
            return Formatted(0x0D - 4, (0x04, [InManufacturer]), (0x05, [InType]), (0x06, [InVersion]), (0x07, [InSerial]), (0x08, [InAssetTag]));
        }

        public static byte[] Processor(byte[] InProcessorId, byte InVoltage, ushort InCurrentSpeed, byte InStatus, byte InCoreCount, byte InThreadCount, byte InSocket = 1, byte InManufacturer = 2, byte InVersion = 3, byte InSerial = 4, byte InPartNumber = 5)
        {
            return Formatted(0x30 - 4,
                (0x04, [InSocket]), (0x07, [InManufacturer]), (0x08, InProcessorId), (0x10, [InVersion]), (0x11, [InVoltage]),
                (0x16, BitConverter.GetBytes(InCurrentSpeed)), (0x18, [InStatus]), (0x20, [InSerial]), (0x22, [InPartNumber]),
                (0x23, [InCoreCount]), (0x25, [InThreadCount]));
        }

        public static byte[] MemoryDevice(ushort InSize, uint InExtendedSize = 0, ushort InConfiguredSpeed = 0, ushort InConfiguredVoltage = 0, byte InLocator = 1, byte InManufacturer = 2, byte InSerial = 3, byte InPartNumber = 4)
        {
            return Formatted(0x28 - 4,
                (0x0C, BitConverter.GetBytes(InSize)), (0x10, [InLocator]), (0x17, [InManufacturer]), (0x18, [InSerial]), (0x1A, [InPartNumber]),
                (0x1C, BitConverter.GetBytes(InExtendedSize)), (0x20, BitConverter.GetBytes(InConfiguredSpeed)), (0x26, BitConverter.GetBytes(InConfiguredVoltage)));
        }
    }

    public class SmbiosTableTests
    {
        private static readonly byte[] SampleUuid = [0x33, 0x22, 0x11, 0x00, 0x55, 0x44, 0x77, 0x66, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF];

        [Fact]
        public void Parser_ReadsStructuresHandlesAndStrings()
        {
            var Table = new SmbiosBuilder()
                .Add(0, SmbiosBuilder.Bios(), "American Megatrends", "1.23", "01/02/2026")
                .Add(1, SmbiosBuilder.System(SampleUuid), "Vendor", "Product", "Version", "Serial")
                .End()
                .BuildTable();

            Assert.Equal(3, Table.Structures.Count);
            Assert.Equal([0, 1, 127], Table.Structures.Select(T => (int) T.Type));
            Assert.Equal([0, 1, 2], Table.Structures.Select(T => (int) T.Handle));

            var Bios = Table.OfType(0).Single();
            Assert.Equal(0x18, Bios.Length);
            Assert.Equal("American Megatrends", Bios.GetString(0x04));
            Assert.Equal("1.23", Bios.GetString(0x05));
            Assert.Equal("01/02/2026", Bios.GetString(0x08));
        }

        [Fact]
        public void Parser_HandlesStructuresWithoutStrings()
        {
            var Table = new SmbiosBuilder()
                .Add(3, new byte[10])
                .Add(1, SmbiosBuilder.System(SampleUuid), "Vendor", "Product", "Version", "Serial")
                .End()
                .BuildTable();

            Assert.Equal([3, 1, 127], Table.Structures.Select(T => (int) T.Type));
            Assert.Empty(Table.OfType(3).Single().Strings);
            Assert.Equal("Serial", Table.OfType(1).Single().GetString(0x07));
        }

        [Fact]
        public void Parser_StopsAtEndOfTableAndSurvivesTruncation()
        {
            var Bytes = new SmbiosBuilder().Add(0, SmbiosBuilder.Bios(), "V", "1", "D").End().Build().Concat(new byte[] { 4, 0x30, 0, 0 }).ToArray();
            var Table = SmbiosTable.FromData(2, 8, 0, Bytes);
            Assert.Equal([0, 127], Table.Structures.Select(T => (int) T.Type));

            var Truncated = new SmbiosBuilder().Add(0, SmbiosBuilder.Bios(), "Vendor").Build().Take(12).ToArray();
            var Exception = Record.Exception(() => SmbiosTable.FromData(2, 8, 0, Truncated));
            Assert.Null(Exception);
        }

        [Fact]
        public void Structure_FieldAccessorsAreBoundsChecked()
        {
            var Structure = new SmbiosBuilder().Add(1, SmbiosBuilder.System(SampleUuid), "Vendor").BuildTable().OfType(1).Single();

            Assert.Equal("Vendor", Structure.GetString(0x04));
            Assert.Null(Structure.GetString(0x05));
            Assert.Null(Structure.GetString(0x7F));
            Assert.Equal(0, Structure.GetByte(0x7F));
            Assert.Equal(0, Structure.GetWord(0x1A));
            Assert.Equal(0u, Structure.GetDword(0x19));
            Assert.Null(Structure.GetBytes(0x10, 16));
            Assert.Equal(SampleUuid, Structure.GetBytes(0x08, 16));
        }

        [Fact]
        public void Structure_DecodesNonAsciiStringsAsLatin1()
        {
            var Bytes = new List<byte>();
            Bytes.AddRange([0, 0x08, 0, 0, 1, 0, 0, 0]);
            Bytes.AddRange([0x41, 0xE9, 0x00, 0x00]);
            var Structure = SmbiosTable.FromData(2, 8, 0, Bytes.ToArray()).OfType(0).Single();
            Assert.Equal("Aé", Structure.GetString(0x04));
        }

        [Fact]
        public void Collectors_MapSystemBaseboardAndBiosFields()
        {
            var Table = new SmbiosBuilder()
                .Add(0, SmbiosBuilder.Bios(), "Firmware Vendor", "F.12", "01/02/2026")
                .Add(1, SmbiosBuilder.System(SampleUuid), "System Vendor", "System Product", "System Version", "SYS-SERIAL")
                .Add(2, SmbiosBuilder.Baseboard(), "Board Vendor", "Board Product", "Board Version", "BOARD-SERIAL")
                .End()
                .BuildTable(3, 2, 1);

            var Hwid = new Hwid();
            HardwareIds.RetrieveMotherBoards(Hwid, Table);
            HardwareIds.RetrieveBaseBoards(Hwid, Table);
            HardwareIds.RetrieveFirmwares(Hwid, Table);
            HardwareIds.RetrieveSmbiosTables(Hwid, Table);

            var Motherboard = Assert.Single(Hwid.Motherboards);
            Assert.Equal("System Product", Motherboard.Name);
            Assert.Equal("System Vendor", Motherboard.Vendor);
            Assert.Equal("System Version", Motherboard.Version);
            Assert.Equal(new Guid("00112233-4455-6677-8899-AABBCCDDEEFF"), Motherboard.UUID);

            var Baseboard = Assert.Single(Hwid.Baseboards);
            Assert.Equal("Board Vendor", Baseboard.Manufacturer);
            Assert.Equal("Board Product", Baseboard.Model);
            Assert.Equal("Board Version", Baseboard.Version);
            Assert.Equal("BOARD-SERIAL", Baseboard.SerialNumber);

            var Bios = Assert.Single(Hwid.BiosFirmwares);
            Assert.Equal("Firmware Vendor", Bios.Manufacturer);
            Assert.Equal("SYS-SERIAL", Bios.SerialNumber);
            Assert.False(string.IsNullOrEmpty(Bios.Version));

            var Smbios = Assert.Single(Hwid.SmbiosTables);
            Assert.Equal("3.2.1", Smbios.Version);
            Assert.Equal((uint) Table.Data.Length, Smbios.Length);
            Assert.Matches("^[0-9a-f]{64}$", Smbios.Hash);
        }

        [Fact]
        public void Collectors_MapChassisFieldsAndStripTheLockBit()
        {
            var Table = new SmbiosBuilder()
                .Add(3, SmbiosBuilder.Chassis(0x8A), "Dell Inc.", "A00", "CHASSIS-SERIAL", "ASSET-42")
                .Add(3, SmbiosBuilder.Chassis(0x03, InAssetTag: 0), "Micro-Star", "1.0", "MS-SERIAL")
                .End()
                .BuildTable();

            var Hwid = new Hwid();
            HardwareIds.RetrieveChassis(Hwid, Table);

            Assert.Equal(2, Hwid.Chassis.Count);
            Assert.Equal([0, 1], Hwid.Chassis.Select(T => T.Id));

            var Laptop = Hwid.Chassis[0];
            Assert.Equal("Dell Inc.", Laptop.Manufacturer);
            Assert.Equal(10, Laptop.Type);
            Assert.Equal("Notebook", Laptop.TypeName);
            Assert.Equal("A00", Laptop.Version);
            Assert.Equal("CHASSIS-SERIAL", Laptop.SerialNumber);
            Assert.Equal("ASSET-42", Laptop.AssetTag);

            var Desktop = Hwid.Chassis[1];
            Assert.Equal(3, Desktop.Type);
            Assert.Equal("Desktop", Desktop.TypeName);
            Assert.Null(Desktop.AssetTag);
            Assert.Same(Laptop, Hwid.MainChassis);
        }

        [Theory]
        [InlineData(1, "Other")]
        [InlineData(3, "Desktop")]
        [InlineData(9, "Laptop")]
        [InlineData(10, "Notebook")]
        [InlineData(23, "Rack Mount Chassis")]
        [InlineData(31, "Convertible")]
        [InlineData(36, "Stick PC")]
        [InlineData(0, "Unknown")]
        [InlineData(2, "Unknown")]
        [InlineData(99, "Unknown")]
        public void GetChassisTypeName_FollowsTheSmbiosSpecification(int InType, string InExpected)
        {
            Assert.Equal(InExpected, HardwareIds.GetChassisTypeName(InType));
        }

        [Fact]
        public void Collectors_MapProcessorFieldsAndSkipEmptySockets()
        {
            var ProcessorId = new byte[] { 0xEA, 0x06, 0x09, 0x00, 0xFF, 0xFB, 0xEB, 0xBF };
            var Table = new SmbiosBuilder()
                .Add(4, SmbiosBuilder.Processor(ProcessorId, 0x8B, 3600, 0x41, 8, 16), "CPU 0", "Intel(R) Corporation", "Intel(R) Core(TM) i7", "CPU-SERIAL", "CPU-PART")
                .Add(4, SmbiosBuilder.Processor(ProcessorId, 0x00, 0, 0x00, 0, 0), "CPU 1", "Intel(R) Corporation", "Not Specified", "", "")
                .End()
                .BuildTable();

            var Hwid = new Hwid();
            HardwareIds.RetrieveProcessors(Hwid, Table);

            var Processor = Assert.Single(Hwid.Processors);
            Assert.Equal("CPU 0", Processor.Socket);
            Assert.Equal("CPU-SERIAL", Processor.SerialNumber);
            Assert.Equal("CPU-PART", Processor.PartNumber);
            Assert.Equal("CPU0", Processor.Channel);
            Assert.Equal("1.1 V", Processor.Voltage);
            Assert.Equal("BFEBFBFF000906EA", Processor.ModelNumber);
            Assert.Matches("^[1-9][0-9]* MHz$", Processor.ClockSpeed);
            Assert.False(string.IsNullOrEmpty(Processor.Manufacturer));
            Assert.False(string.IsNullOrEmpty(Processor.Model));
            Assert.True(Processor.NumberOfCores >= 1);
            Assert.True(Processor.NumberOfLogicalProcessors >= Processor.NumberOfCores);
        }

        [Fact]
        public void Collectors_ReportTheSmbiosProcessorIdEvenWhenZeroed()
        {
            //
            // Hyper-V leaves the SMBIOS processor ID zeroed; WMI reports it as is, so the collector does too instead of reading CPUID.
            //

            var Table = new SmbiosBuilder()
                .Add(4, SmbiosBuilder.Processor(new byte[8], 0x00, 2400, 0x41, 4, 8), "CPU 0", "GenuineIntel", "Intel(R) Xeon(R)", "", "")
                .End()
                .BuildTable();

            var Hwid = new Hwid();
            HardwareIds.RetrieveProcessors(Hwid, Table);

            Assert.Equal("0000000000000000", Assert.Single(Hwid.Processors).ModelNumber);
        }

        [Fact]
        public void Collectors_FallBackToCpuIdWhenTheProcessorRecordHasNoId()
        {
            //
            // SMBIOS 2.0 records shorter than 0x10 bytes carry no processor ID.
            //

            var Table = new SmbiosBuilder()
                .Add(4, SmbiosBuilder.Formatted(0x0C - 4, (0x04, [1]), (0x07, [2])), "CPU 0", "GenuineIntel")
                .End()
                .BuildTable();

            var Hwid = new Hwid();
            HardwareIds.RetrieveProcessors(Hwid, Table);

            Assert.Equal(HardwareIds.GetProcessorIdFromCpuId(), Assert.Single(Hwid.Processors).ModelNumber);
        }

        [Fact]
        public void Collectors_MapMemoryDevicesAndSkipEmptySlots()
        {
            var Table = new SmbiosBuilder()
                .Add(17, SmbiosBuilder.MemoryDevice(0x4000, 0, 3200, 1200), "DIMM_A1", "Kingston", "MEM-SERIAL-1", "KF3200C16D4/16GX")
                .Add(17, SmbiosBuilder.MemoryDevice(0x0000, InManufacturer: 0, InSerial: 0, InPartNumber: 0), "DIMM_A2")
                .Add(17, SmbiosBuilder.MemoryDevice(0x7FFF, 65536, 2133, 1350), "DIMM_B1", "Samsung", "MEM-SERIAL-2", "M393A8G40MB2")
                .Add(17, SmbiosBuilder.MemoryDevice(0x8200), "DIMM_B2", "Legacy", "MEM-SERIAL-3", "PART")
                .End()
                .BuildTable();

            var Hwid = new Hwid();
            HardwareIds.RetrieveMemorySticks(Hwid, Table);

            Assert.Equal(3, Hwid.MemorySticks.Count);
            Assert.Equal([0, 1, 2], Hwid.MemorySticks.Select(T => T.Id));

            var First = Hwid.MemorySticks[0];
            Assert.Equal("DIMM_A1", First.Channel);
            Assert.Equal("Kingston", First.Manufacturer);
            Assert.Equal("MEM-SERIAL-1", First.SerialNumber);
            Assert.Equal("KF3200C16D4/16GX", First.PartNumber);
            Assert.Equal("16 GB", First.Capacity);
            Assert.Equal("3200 MHz", First.ClockSpeed);
            Assert.Equal("1.20 V", First.Voltage);

            Assert.Equal("64 GB", Hwid.MemorySticks[1].Capacity);
            Assert.Equal("1.35 V", Hwid.MemorySticks[1].Voltage);
            Assert.Equal("0 GB", Hwid.MemorySticks[2].Capacity);
        }

        [Fact]
        public void Collectors_ToleratesMissingTable()
        {
            var Hwid = new Hwid();
            HardwareIds.RetrieveMotherBoards(Hwid, null);
            HardwareIds.RetrieveBaseBoards(Hwid, null);
            HardwareIds.RetrieveFirmwares(Hwid, null);
            HardwareIds.RetrieveSmbiosTables(Hwid, null);
            HardwareIds.RetrieveProcessors(Hwid, null);
            HardwareIds.RetrieveMemorySticks(Hwid, null);

            Assert.Empty(Hwid.Motherboards);
            Assert.Empty(Hwid.Baseboards);
            Assert.Empty(Hwid.BiosFirmwares);
            Assert.Empty(Hwid.SmbiosTables);
            Assert.Empty(Hwid.Processors);
            Assert.Empty(Hwid.MemorySticks);
        }

        [Fact]
        public void Read_ReturnsTheFirmwareTable()
        {
            var Table = SmbiosTable.Read();

            Assert.NotNull(Table);
            Assert.True(Table.MajorVersion >= 2);
            Assert.NotEmpty(Table.Data);
            Assert.Contains(Table.Structures, T => T.Type == 0);
            Assert.Contains(Table.Structures, T => T.Type == 1);
            Assert.Contains(Table.Structures, T => T.Type == 4);
        }
    }
}
