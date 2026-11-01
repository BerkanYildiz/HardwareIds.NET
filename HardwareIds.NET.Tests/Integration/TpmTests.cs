namespace HardwareIds.NET.Tests.Integration
{
    using System;
    using System.Linq;

    using global::HardwareIds.NET.Native;

    using Xunit;

    public class TpmTests
    {
        [Fact]
        public void Tpm_IdentityMatchesWmiWhenAvailable()
        {
            var Rows = Wmi.Query("Win32_Tpm", @"root\CIMV2\Security\MicrosoftTpm");
            Assert.SkipWhen(Rows == null || Rows.Count == 0, "No TPM WMI instance is accessible.");
            var Device = Assert.Single(HwidFixture.Hwid.Tpms);
            var Row = Assert.Single(Rows);
            Assert.Equal((uint) Row.GetNumber("ManufacturerId"), Device.ManufacturerId);
            Assert.StartsWith(Device.Version!, Row.GetString("SpecVersion")!);
        }

        [Fact]
        public void Tpm_AvailablePublicMaterialIsSelfConsistent()
        {
            var Devices = HwidFixture.Hwid.Tpms;
            Assert.SkipWhen(Devices.Count == 0, "No TPM is available.");
            var Device = Assert.Single(Devices);
            Assert.All(Device.Keys.Where(T => T.PublicKeyBlob != null), Key => Assert.Equal(Tpm.Sha256(Convert.FromBase64String(Key.PublicKeyBlob!)), Key.PublicKeySha256));

            foreach (var Certificate in Device.Certificates.Concat(Device.Keys.SelectMany(T => T.Certificates)))
            {
                var Parsed = Tpm.ParseCertificate(Convert.FromBase64String(Certificate.Der!), Certificate.Source!);
                Assert.NotNull(Parsed);
                Assert.Equal(Parsed.Sha256, Certificate.Sha256);
                Assert.Equal(Parsed.Subject, Certificate.Subject);
            }

            Assert.Equal(Device.Properties.Count, Device.Properties.Select(T => T.Property).Distinct().Count());
            Assert.All(Device.Errors, Error => Assert.NotEqual(0u, Error.Code));
        }
    }
}
