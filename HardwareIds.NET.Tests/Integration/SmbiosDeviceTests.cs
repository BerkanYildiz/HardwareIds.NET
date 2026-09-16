namespace HardwareIds.NET.Tests.Integration
{
    using System;
    using System.Linq;
    using System.Security.Cryptography;

    using Xunit;

    public class SmbiosDeviceTests
    {
        [Fact]
        public void SmbiosTable_IsReadAndHashed()
        {
            var Table = Assert.Single(HwidFixture.Hwid.SmbiosTables);

            Assert.Equal(0, Table.Id);
            Assert.Matches(@"^\d+\.\d+\.\d+$", Table.Version);
            Assert.Matches("^[0-9a-f]{64}$", Table.Hash);
            Assert.True(Table.Length > 0);
        }

        [Fact]
        public void SmbiosTable_MatchesWmi()
        {
            var Rows = Wmi.Query("MSSmBios_RawSMBiosTables", @"root\wmi");
            Assert.SkipWhen(Rows is null || Rows.Count == 0, "The WMI SMBIOS provider is not available on this machine.");

            var Row = Rows[0];
            var Table = Assert.Single(HwidFixture.Hwid.SmbiosTables);
            var Data = Assert.IsType<byte[]>(Row["SMBiosData"]);

            using var Hasher = SHA256.Create();
            var ExpectedHash = string.Concat(Hasher.ComputeHash(Data).Select(T => T.ToString("x2")));

            Assert.Equal(ExpectedHash, Table.Hash);
            Assert.Equal(Row.GetNumber("Size"), Table.Length);
            Assert.Equal($"{Row.GetNumber("SmbiosMajorVersion")}.{Row.GetNumber("SmbiosMinorVersion")}.{Row.GetNumber("DmiRevision")}", Table.Version);
        }

        [Fact]
        public void Motherboards_AreEnumeratedWithValidFields()
        {
            var Motherboard = Assert.Single(HwidFixture.Hwid.Motherboards);

            Assert.Equal(0, Motherboard.Id);
            Assert.False(string.IsNullOrWhiteSpace(Motherboard.Vendor));
            Assert.False(string.IsNullOrWhiteSpace(Motherboard.Name));
        }

        [Fact]
        public void Motherboards_MatchWmi()
        {
            var Rows = Wmi.Query("Win32_ComputerSystemProduct");
            Assert.SkipWhen(Rows is null, "WMI is not available on this machine.");

            var Motherboards = HwidFixture.Hwid.Motherboards;
            Assert.Equal(Rows.Count, Motherboards.Count);

            foreach (var (Row, Motherboard) in Rows.Zip(Motherboards, (InRow, InBoard) => (InRow, InBoard)))
            {
                Assert.Equal(Row.GetString("Name"), Motherboard.Name);
                Assert.Equal(Row.GetString("Vendor"), Motherboard.Vendor);
                Assert.Equal(Wmi.Normalize(Row.GetString("Version")), Wmi.Normalize(Motherboard.Version));
                Assert.Equal(Guid.Parse(Row.GetString("UUID")!), Motherboard.UUID);
            }
        }

        [Fact]
        public void Baseboards_MatchWmi()
        {
            var Rows = Wmi.Query("Win32_BaseBoard");
            Assert.SkipWhen(Rows is null, "WMI is not available on this machine.");

            var Baseboards = HwidFixture.Hwid.Baseboards;
            Assert.Equal(Rows.Count, Baseboards.Count);
            Assert.Equal(Enumerable.Range(0, Baseboards.Count), Baseboards.Select(T => T.Id));

            foreach (var (Row, Baseboard) in Rows.Zip(Baseboards, (InRow, InBoard) => (InRow, InBoard)))
            {
                Assert.Equal(Wmi.Normalize(Row.GetString("Manufacturer")), Wmi.Normalize(Baseboard.Manufacturer));
                Assert.Equal(Wmi.Normalize(Row.GetString("Product")), Wmi.Normalize(Baseboard.Model));
                Assert.Equal(Wmi.Normalize(Row.GetString("Version")), Wmi.Normalize(Baseboard.Version));
                Assert.Equal(Wmi.Normalize(Row.GetString("SerialNumber")), Wmi.Normalize(Baseboard.SerialNumber));
            }
        }

        [Fact]
        public void BiosFirmwares_AreEnumeratedWithValidFields()
        {
            var Bios = Assert.Single(HwidFixture.Hwid.BiosFirmwares);

            Assert.Equal(0, Bios.Id);
            Assert.False(string.IsNullOrWhiteSpace(Bios.Manufacturer));
            Assert.False(string.IsNullOrWhiteSpace(Bios.Version));
        }

        [Fact]
        public void BiosFirmwares_MatchWmi()
        {
            var Rows = Wmi.Query("Win32_BIOS");
            Assert.SkipWhen(Rows is null, "WMI is not available on this machine.");

            var Row = Assert.Single(Rows);
            var Bios = Assert.Single(HwidFixture.Hwid.BiosFirmwares);

            Assert.Equal(Row.GetString("Manufacturer"), Bios.Manufacturer);
            Assert.Equal(Row.GetString("Version"), Bios.Version);
            Assert.Equal(Wmi.Normalize(Row.GetString("SerialNumber")), Wmi.Normalize(Bios.SerialNumber));
        }

        [Fact]
        public void Processors_AreEnumeratedWithValidFields()
        {
            var Processors = HwidFixture.Hwid.Processors;

            Assert.NotEmpty(Processors);
            Assert.Equal(Enumerable.Range(0, Processors.Count), Processors.Select(T => T.Id));

            Assert.All(Processors, Processor =>
            {
                Assert.False(string.IsNullOrWhiteSpace(Processor.Manufacturer));
                Assert.False(string.IsNullOrWhiteSpace(Processor.Model));
                Assert.Matches("^[0-9A-F]{16}$", Processor.ModelNumber);
                Assert.Matches("^[0-9]+ MHz$", Processor.ClockSpeed);
                Assert.Matches(@"^[0-9]+\.[0-9] V$", Processor.Voltage);
                Assert.Equal($"CPU{Processor.Id}", Processor.Channel);
                Assert.True(Processor.NumberOfCores >= 1);
                Assert.True(Processor.NumberOfLogicalProcessors >= Processor.NumberOfCores);
            });

            Assert.Equal(Environment.ProcessorCount, (int) Processors.Sum(T => T.NumberOfLogicalProcessors));
        }

        [Fact]
        public void Processors_MatchWmi()
        {
            var Rows = Wmi.Query("Win32_Processor");
            Assert.SkipWhen(Rows is null, "WMI is not available on this machine.");

            var Processors = HwidFixture.Hwid.Processors;
            Assert.Equal(Rows.Count, Processors.Count);

            foreach (var Row in Rows)
            {
                var Processor = Assert.Single(Processors, T => T.Channel == Row.GetString("DeviceID"));

                Assert.Equal(Row.GetString("Name"), Processor.Model);
                Assert.Equal(Row.GetString("Manufacturer"), Processor.Manufacturer);
                Assert.Equal(Row.GetString("ProcessorId"), Processor.ModelNumber);
                Assert.Equal(Wmi.Normalize(Row.GetString("SocketDesignation")), Wmi.Normalize(Processor.Socket));
                Assert.Equal(Row.GetNumber("NumberOfCores"), Processor.NumberOfCores);
                Assert.Equal(Row.GetNumber("NumberOfLogicalProcessors"), Processor.NumberOfLogicalProcessors);
                Assert.Equal(Row.GetNumber("CurrentClockSpeed") + " MHz", Processor.ClockSpeed);
            }
        }

        [Fact]
        public void MemorySticks_AreEnumeratedWithValidFields()
        {
            var MemorySticks = HwidFixture.Hwid.MemorySticks;
            Assert.SkipWhen(MemorySticks.Count == 0, "The firmware of this machine does not describe its memory devices.");

            Assert.Equal(Enumerable.Range(0, MemorySticks.Count), MemorySticks.Select(T => T.Id));

            Assert.All(MemorySticks, MemoryStick =>
            {
                Assert.Matches("^[1-9][0-9]* GB$", MemoryStick.Capacity);
                Assert.Matches("^[0-9]+ MHz$", MemoryStick.ClockSpeed);
                Assert.Matches(@"^[0-9]+\.[0-9]{2} V$", MemoryStick.Voltage);
                Assert.False(string.IsNullOrWhiteSpace(MemoryStick.Channel));
            });
        }

        [Fact]
        public void MemorySticks_MatchWmi()
        {
            var Rows = Wmi.Query("Win32_PhysicalMemory");
            Assert.SkipWhen(Rows is null, "WMI is not available on this machine.");

            var MemorySticks = HwidFixture.Hwid.MemorySticks;
            Assert.Equal(Rows.Count, MemorySticks.Count);

            foreach (var (Row, MemoryStick) in Rows.Zip(MemorySticks, (InRow, InStick) => (InRow, InStick)))
            {
                Assert.Equal(Wmi.Normalize(Row.GetString("DeviceLocator")), Wmi.Normalize(MemoryStick.Channel));
                Assert.Equal(Wmi.Normalize(Row.GetString("Manufacturer")), Wmi.Normalize(MemoryStick.Manufacturer));
                Assert.Equal(Wmi.Normalize(Row.GetString("SerialNumber")), Wmi.Normalize(MemoryStick.SerialNumber));
                Assert.Equal(Wmi.Normalize(Row.GetString("PartNumber")), Wmi.Normalize(MemoryStick.PartNumber));
                Assert.Equal((Row.GetNumber("Capacity") / 1024 / 1024 / 1024) + " GB", MemoryStick.Capacity);
                Assert.Equal(Row.GetNumber("ConfiguredClockSpeed") + " MHz", MemoryStick.ClockSpeed);
            }
        }
    }
}
