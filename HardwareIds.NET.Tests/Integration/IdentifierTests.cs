namespace HardwareIds.NET.Tests.Integration
{
    using System;
    using System.Linq;

    using Microsoft.Win32;

    using Xunit;

    /// <summary>
    /// Covers the identifiers added on top of the WMI-compatible fields: disk identifier fields, PnP instance paths,
    /// installation identifiers, chassis, Bluetooth radios and batteries.
    /// </summary>
    public class IdentifierTests
    {
        private const string GuidPattern = "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$";
        private const string BracedGuidPattern = "^\\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\\}$";

        [Fact]
        public void Disks_ExposeWellFormedIdentifierFields()
        {
            Assert.All(HwidFixture.Hwid.Disks, Disk =>
            {
                Assert.Matches("^[0-9a-f]{64}$", Disk.Duid);

                foreach (var Hex in new[] { Disk.NvmeEui64, Disk.NvmeNguid, Disk.NvmeFguid, Disk.AtaWwn, Disk.VpdEui64, Disk.VpdNguid, Disk.VpdNaa, Disk.WorldWideName })
                {
                    if (Hex != null)
                        Assert.Matches("^[0-9A-F]{16,32}$", Hex);
                }

                foreach (var Text in new[] { Disk.NvmeSerial, Disk.AtaSerial, Disk.VpdT10, Disk.VpdScsiName, Disk.VpdVendor })
                {
                    if (Text != null)
                        Assert.False(string.IsNullOrWhiteSpace(Text));
                }

                //
                // NVMe and ATA fields are exclusive: a disk answers one identify protocol, never both.
                //

                Assert.False(Disk.NvmeSerial != null && Disk.AtaSerial != null);

                //
                // The World Wide Name is one of the identifiers, in order of preference: ATA, NVMe EUI-64/NGUID, then the SCSI page.
                //

                if (Disk.WorldWideName != null)
                    Assert.Contains(Disk.WorldWideName, new[] { Disk.AtaWwn, Disk.NvmeEui64, Disk.NvmeNguid, Disk.VpdNaa, Disk.VpdEui64 });
            });
        }

        [Fact]
        public void Disks_ExposeThePartitionTableIdentifier()
        {
            Assert.All(HwidFixture.Hwid.Disks.Where(T => T.Partitions > 0), Disk =>
            {
                Assert.NotNull(Disk.DiskGuid);
                Assert.True(System.Text.RegularExpressions.Regex.IsMatch(Disk.DiskGuid, GuidPattern) || System.Text.RegularExpressions.Regex.IsMatch(Disk.DiskGuid, "^0x[0-9A-F]{8}$"), Disk.DiskGuid);
            });
        }

        [Fact]
        public void Disks_ExposeTheSameInstancePathAsWmi()
        {
            var Rows = Wmi.Query("Win32_DiskDrive");
            Assert.SkipWhen(Rows is null, "WMI is not available on this machine.");

            foreach (var Row in Rows)
            {
                var Disk = Assert.Single(HwidFixture.Hwid.Disks, T => T.Id == (int) Row.GetNumber("Index"));
                Assert.Equal(Row.GetString("PNPDeviceID"), Disk.InstanceId, StringComparer.OrdinalIgnoreCase);
                Assert.Equal(Wmi.Normalize(Row.GetString("FirmwareRevision")).Trim(), Wmi.Normalize(Disk.Firmware));   // WMI keeps the descriptor padding; the firmware field is trimmed
            }
        }

        [Fact]
        public void Disks_ProtocolIdentitiesAreWellFormed()
        {
            var NvmeOrAta = HwidFixture.Hwid.Disks.Where(T => T.NvmeSerial != null || T.AtaSerial != null).ToList();
            Assert.SkipWhen(NvmeOrAta.Count == 0, "No NVMe or ATA disk answered an identify request on this machine.");

            Assert.All(NvmeOrAta, Disk =>
            {
                //
                // Serials are free-form printable ASCII: most are alphanumeric, but virtual disks report values like "SN: 00000".
                //

                Assert.Matches(@"^[\x21-\x7E]([\x20-\x7E]*[\x21-\x7E])?$", Disk.NvmeSerial ?? Disk.AtaSerial);
                Assert.False(string.IsNullOrEmpty(Disk.Firmware));
                Assert.NotNull(Disk.WorldWideName);
            });
        }

        [Fact]
        public void NetworkAdapters_ExposeTheSameInstancePathAsWmi()
        {
            var Rows = Wmi.Query("Win32_NetworkAdapter", InCondition: "PhysicalAdapter = TRUE");
            Assert.SkipWhen(Rows is null || Rows.Count == 0, "No physical network adapter on this machine.");

            foreach (var Row in Rows)
            {
                var Adapter = Assert.Single(HwidFixture.Hwid.NetworkAdapters, T => T.Id == (int) Row.GetNumber("Index"));
                Assert.Equal(Row.GetString("PNPDeviceID"), Adapter.InstanceId, StringComparer.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void VideoControllers_ExposeTheSameInstancePathAsWmi()
        {
            var Rows = Wmi.Query("Win32_VideoController");
            Assert.SkipWhen(Rows is null, "WMI is not available on this machine.");

            Assert.Equal(Rows.Select(T => T.GetString("PNPDeviceID")!.ToUpperInvariant()).OrderBy(T => T), HwidFixture.Hwid.VideoControllers.Select(T => T.InstanceId!.ToUpperInvariant()).OrderBy(T => T));
        }

        [Fact]
        public void Monitors_ExposeInstancePathAndEdidHash()
        {
            var Monitors = HwidFixture.Hwid.Monitors;
            Assert.SkipWhen(Monitors.Count == 0, "No monitor with an EDID block is attached to this machine.");

            Assert.All(Monitors, Monitor =>
            {
                Assert.StartsWith("DISPLAY\\", Monitor.InstanceId, StringComparison.OrdinalIgnoreCase);
                Assert.Matches("^[0-9a-f]{64}$", Monitor.EdidHash);
                Assert.InRange(Monitor.ManufactureWeek, 0, 54);
                Assert.True(Monitor.ManufactureYear == 0 || Monitor.ManufactureYear >= 1990, Monitor.ManufactureYear.ToString());
            });

            Assert.Equal(Monitors.Count, Monitors.Select(T => T.InstanceId).Distinct().Count());
        }

        [Fact]
        public void OperatingSystem_ExposesTheInstallationIdentifiers()
        {
            var OperatingSystem = Assert.Single(HwidFixture.Hwid.OperatingSystems);

            Assert.Matches(GuidPattern, OperatingSystem.MachineGuid);
            Assert.Matches(BracedGuidPattern, OperatingSystem.HardwareProfileGuid);
            Assert.Matches(@"^S-1-5-21(-\d+){3}$", OperatingSystem.MachineSid);
            Assert.NotNull(OperatingSystem.InstallTime);
            Assert.InRange(OperatingSystem.InstallTime.Value, new DateTime(2000, 1, 1), DateTime.Now);
            Assert.Equal(OperatingSystem.InstallDate, OperatingSystem.InstallTime.Value, TimeSpan.FromSeconds(1));

            if (OperatingSystem.SqmMachineId != null)
                Assert.Matches(BracedGuidPattern, OperatingSystem.SqmMachineId);
        }

        [Fact]
        public void OperatingSystem_IdentifiersMatchTheRegistry()
        {
            var OperatingSystem = Assert.Single(HwidFixture.Hwid.OperatingSystems);

            using var Cryptography = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            using var Sqm = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\SQMClient");
            using var Profile = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\IDConfigDB\Hardware Profiles\0001");
            using var Version = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");

            Assert.Equal(Cryptography?.GetValue("MachineGuid") as string, OperatingSystem.MachineGuid);
            Assert.Equal(Sqm?.GetValue("MachineId") as string, OperatingSystem.SqmMachineId);
            Assert.Equal(Profile?.GetValue("HwProfileGuid") as string, OperatingSystem.HardwareProfileGuid);
            Assert.Equal(Version?.GetValue("InstallTime") is long InstallTime ? DateTime.FromFileTime(InstallTime) : null, OperatingSystem.InstallTime);
        }

        [Fact]
        public void OperatingSystem_MachineSidIsTheDomainOfTheLocalUsers()
        {
            var OperatingSystem = Assert.Single(HwidFixture.Hwid.OperatingSystems);
            var Users = HwidFixture.Hwid.Users;

            Assert.SkipWhen(Users.Count == 0, "No local user on this machine.");
            Assert.All(Users, User => Assert.StartsWith(OperatingSystem.MachineSid + "-", User.SID));
        }

        [Fact]
        public void Chassis_AreEnumeratedWithValidFields()
        {
            var Chassis = HwidFixture.Hwid.Chassis;

            Assert.NotEmpty(Chassis);
            Assert.Equal(Enumerable.Range(0, Chassis.Count), Chassis.Select(T => T.Id));
            Assert.All(Chassis, Entry =>
            {
                Assert.InRange(Entry.Type, 1, 127);
                Assert.Equal(HardwareIds.GetChassisTypeName(Entry.Type), Entry.TypeName);
            });
        }

        [Fact]
        public void Chassis_MatchWmi()
        {
            var Rows = Wmi.Query("Win32_SystemEnclosure");
            Assert.SkipWhen(Rows is null, "WMI is not available on this machine.");

            var Chassis = HwidFixture.Hwid.Chassis;
            Assert.Equal(Rows.Count, Chassis.Count);

            foreach (var (Row, Entry) in Rows.Zip(Chassis, (InRow, InEntry) => (InRow, InEntry)))
            {
                Assert.Equal(Wmi.Normalize(Row.GetString("Manufacturer")), Wmi.Normalize(Entry.Manufacturer));
                Assert.Equal(Wmi.Normalize(Row.GetString("Version")), Wmi.Normalize(Entry.Version));
                Assert.Equal(Wmi.Normalize(Row.GetString("SerialNumber")), Wmi.Normalize(Entry.SerialNumber));
                Assert.Equal(Wmi.Normalize(Row.GetString("SMBIOSAssetTag")), Wmi.Normalize(Entry.AssetTag));

                if (Row["ChassisTypes"] is ushort[] Types && Types.Length > 0)
                    Assert.Equal(Types[0], Entry.Type);
            }
        }

        [Fact]
        public void BluetoothRadios_HaveValidFields()
        {
            var Radios = HwidFixture.Hwid.BluetoothRadios;
            Assert.SkipWhen(Radios.Count == 0, "No Bluetooth radio on this machine.");

            Assert.Equal(Enumerable.Range(0, Radios.Count), Radios.Select(T => T.Id));
            Assert.All(Radios, Radio =>
            {
                Assert.Matches("^([0-9A-F]{2}:){5}[0-9A-F]{2}$", Radio.Address);
                Assert.NotEqual("00:00:00:00:00:00", Radio.Address);
                Assert.False(string.IsNullOrWhiteSpace(Radio.Name));
            });
        }

        [Fact]
        public void Batteries_HaveValidFields()
        {
            var Batteries = HwidFixture.Hwid.Batteries;
            Assert.SkipWhen(Batteries.Count == 0, "No battery in this machine.");

            Assert.Equal(Enumerable.Range(0, Batteries.Count), Batteries.Select(T => T.Id));
            Assert.All(Batteries, Battery =>
            {
                Assert.True(Battery.DesignedCapacity > 0);
                Assert.True(Battery.FullChargedCapacity > 0);
                Assert.False(string.IsNullOrWhiteSpace(Battery.UniqueId) && string.IsNullOrWhiteSpace(Battery.SerialNumber));
            });
        }

        [Fact]
        public void Batteries_MatchWmi()
        {
            var Rows = Wmi.Query("Win32_Battery");
            Assert.SkipWhen(Rows is null || Rows.Count == 0, "No battery in this machine.");

            Assert.Equal(Rows.Count, HwidFixture.Hwid.Batteries.Count);
        }
    }
}
