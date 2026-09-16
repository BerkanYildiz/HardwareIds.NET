namespace HardwareIds.NET.Tests.Integration
{
    using System;
    using System.Linq;
    using System.Security;

    using Microsoft.Win32;

    using Xunit;

    public class OperatingSystemTests
    {
        [Fact]
        public void OperatingSystem_HasValidFields()
        {
            var OperatingSystem = Assert.Single(HwidFixture.Hwid.OperatingSystems);

            Assert.Equal(0, OperatingSystem.Id);
            Assert.StartsWith("Microsoft Windows", OperatingSystem.Name);
            Assert.Matches(@"^\d+\.\d+\.\d+$", OperatingSystem.Version);
            Assert.Contains(OperatingSystem.Architecture, new[] { "64-bit", "32-bit", "ARM 64-bit", "ARM 32-bit" });
            Assert.False(string.IsNullOrWhiteSpace(OperatingSystem.SerialNumber));
            Assert.InRange(OperatingSystem.InstallDate, new DateTime(2000, 1, 1), DateTime.Now);
            Assert.InRange(OperatingSystem.LastBootUpTime, DateTime.Now.AddYears(-5), DateTime.Now);
            Assert.True(OperatingSystem.LastBootUpTime >= OperatingSystem.InstallDate);
        }

        [Fact]
        public void OperatingSystem_MatchesWmi()
        {
            var Rows = Wmi.Query("Win32_OperatingSystem");
            Assert.SkipWhen(Rows is null, "WMI is not available on this machine.");

            var Row = Assert.Single(Rows);
            var OperatingSystem = Assert.Single(HwidFixture.Hwid.OperatingSystems);

            Assert.Equal(Row.GetString("Caption"), OperatingSystem.Name);
            Assert.Equal(Row.GetString("Version"), OperatingSystem.Version);
            Assert.Equal(Row.GetString("OSArchitecture"), OperatingSystem.Architecture);
            Assert.Equal(Row.GetString("SerialNumber"), OperatingSystem.SerialNumber);
            Assert.Equal(Wmi.Normalize(Row.GetString("RegisteredUser")), Wmi.Normalize(OperatingSystem.RegisteredUser));
            Assert.Equal(Row.GetDate("InstallDate")!.Value, OperatingSystem.InstallDate, TimeSpan.FromSeconds(1));
            Assert.Equal(Row.GetDate("LastBootUpTime")!.Value, OperatingSystem.LastBootUpTime, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void NetworkSignatures_MatchTheRegistry()
        {
            RegistryKey? SignaturesKey;

            try
            {
                SignaturesKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\NetworkList\Signatures\Unmanaged");
            }
            catch (SecurityException)
            {
                SignaturesKey = null;
            }

            using (SignaturesKey)
            {
                var Signatures = HwidFixture.Hwid.NetworkSignatures;

                //
                // The signature keys are only readable when elevated; the library returns nothing otherwise.
                //

                Assert.SkipWhen(SignaturesKey is null && Signatures.Count == 0, "The network signature registry keys require elevation.");
                Assert.NotNull(SignaturesKey);
                Assert.Equal(SignaturesKey.GetSubKeyNames().Length, Signatures.Count);
            }

            AssertSignaturesAreWellFormed();
        }

        private static void AssertSignaturesAreWellFormed()
        {
            var Signatures = HwidFixture.Hwid.NetworkSignatures;
            Assert.Equal(Enumerable.Range(0, Signatures.Count), Signatures.Select(T => T.Id));

            Assert.All(Signatures, Signature =>
            {
                if (Signature.ProfileGuid != null)
                    Assert.True(Guid.TryParse(Signature.ProfileGuid, out _));

                if (!string.IsNullOrEmpty(Signature.DefaultGatewayMac))
                    Assert.Matches("^([0-9A-F]{2}:){5}[0-9A-F]{2}$", Signature.DefaultGatewayMac);
            });
        }
    }
}
