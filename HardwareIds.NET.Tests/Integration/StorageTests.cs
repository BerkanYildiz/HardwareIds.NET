namespace HardwareIds.NET.Tests.Integration
{
    using System;
    using System.IO;
    using System.Linq;

    using Xunit;

    public class StorageTests
    {
        private static readonly string[] KnownInterfaces = ["SCSI", "IDE", "USB", "1394", "SD", "HDC", "Unknown"];

        [Fact]
        public void Disks_AreEnumeratedWithValidFields()
        {
            var Disks = HwidFixture.Hwid.Disks;

            Assert.NotEmpty(Disks);
            Assert.Equal(Disks.Select(T => T.Id).OrderBy(T => T), Disks.Select(T => T.Id));
            Assert.Equal(Disks.Count, Disks.Select(T => T.Id).Distinct().Count());

            Assert.All(Disks, Disk =>
            {
                Assert.True(Disk.Id >= 0);
                Assert.False(string.IsNullOrWhiteSpace(Disk.Model));
                Assert.Contains(Disk.Interface, KnownInterfaces);
                Assert.Matches("^[0-9]+ GB$", Disk.Capacity);
                Assert.True(Disk.Partitions >= 0);
            });
        }

        [Fact]
        public void Disks_MatchWmi()
        {
            var Rows = Wmi.Query("Win32_DiskDrive");
            Assert.SkipWhen(Rows is null, "WMI is not available on this machine.");

            var Disks = HwidFixture.Hwid.Disks;
            Assert.Equal(Rows.Select(T => (int) T.GetNumber("Index")).OrderBy(T => T), Disks.Select(T => T.Id));

            foreach (var Row in Rows)
            {
                var Disk = Assert.Single(Disks, T => T.Id == (int) Row.GetNumber("Index"));

                Assert.Equal(Row.GetString("Model"), Disk.Model);
                Assert.Equal(Wmi.Normalize(Row.GetString("SerialNumber")), Wmi.Normalize(Disk.SerialNumber));
                Assert.Equal(Row.GetString("InterfaceType"), Disk.Interface);
                Assert.Equal((int) Row.GetNumber("Partitions"), Disk.Partitions);
                Assert.Equal((Row.GetNumber("Size") / 1024 / 1024 / 1024) + " GB", Disk.Capacity);
                Assert.Equal(Row.GetStrings("CapabilityDescriptions").Any(T => T.Contains("Removable")), Disk.IsRemovable);
            }
        }

        [Fact]
        public void Volumes_AreEnumeratedWithValidFields()
        {
            var Volumes = HwidFixture.Hwid.Volumes;

            Assert.NotEmpty(Volumes);
            Assert.Equal(Enumerable.Range(0, Volumes.Count), Volumes.Select(T => T.Id));

            Assert.All(Volumes, Volume =>
            {
                Assert.Matches(@"^\\\\\?\\Volume\{[0-9a-f-]{36}\}\\$", Volume.Path);

                if (Volume.Letter != null)
                    Assert.Matches("^[A-Z]:$", Volume.Letter);
            });

            var Letters = Volumes.Where(T => T.Letter != null).Select(T => T.Letter).ToList();
            Assert.Equal(Letters.Count, Letters.Distinct().Count());
        }

        [Fact]
        public void Volumes_IncludeTheSystemVolume()
        {
            var SystemLetter = Path.GetPathRoot(Environment.SystemDirectory)!.Substring(0, 2).ToUpperInvariant();
            var SystemVolume = Assert.Single(HwidFixture.Hwid.Volumes, T => T.Letter == SystemLetter);

            Assert.NotEqual(0u, SystemVolume.SerialNumber);
        }

        [Fact]
        public void Volumes_MatchWmi()
        {
            var Rows = Wmi.Query("Win32_Volume");
            Assert.SkipWhen(Rows is null, "WMI is not available on this machine.");

            var Volumes = HwidFixture.Hwid.Volumes;
            Assert.Equal(Rows.Select(T => T.GetString("DeviceID")).OrderBy(T => T), Volumes.Select(T => T.Path).OrderBy(T => T));

            foreach (var Row in Rows)
            {
                var Volume = Assert.Single(Volumes, T => T.Path == Row.GetString("DeviceID"));

                Assert.Equal(Wmi.Normalize(Row.GetString("DriveLetter")), Wmi.Normalize(Volume.Letter));
                Assert.Equal((uint) Row.GetNumber("SerialNumber"), Volume.SerialNumber);
            }
        }
    }
}
