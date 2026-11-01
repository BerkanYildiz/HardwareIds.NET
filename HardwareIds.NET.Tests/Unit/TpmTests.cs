namespace HardwareIds.NET.Tests.Unit
{
    using System;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Text.Json;
    using System.Threading;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using Xunit;

    public class TpmTests
    {
        /// <summary>
        /// Builds a synthetic capability reply with explicit big-endian fields.
        /// </summary>
        private static byte[] Response(uint InCapability, uint InCount, byte[] InEntries, bool InMore = false)
        {
            var Bytes = new byte[19 + InEntries.Length];
            Bytes[0] = 0x80;
            Bytes[1] = 0x01;
            Write(Bytes, 2, (uint) Bytes.Length);
            Bytes[10] = InMore ? (byte) 1 : (byte) 0;
            Write(Bytes, 11, InCapability);
            Write(Bytes, 15, InCount);
            InEntries.CopyTo(Bytes, 19);
            return Bytes;
        }

        /// <summary>
        /// Writes a synthetic 32-bit TPM wire value.
        /// </summary>
        private static void Write(byte[] InBytes, int InOffset, uint InValue)
        {
            InBytes[InOffset] = (byte) (InValue >> 24);
            InBytes[InOffset + 1] = (byte) (InValue >> 16);
            InBytes[InOffset + 2] = (byte) (InValue >> 8);
            InBytes[InOffset + 3] = (byte) InValue;
        }

        [Fact]
        public void NativeStructures_MatchWindowsLayouts()
        {
            Assert.Equal(16, Marshal.SizeOf<TPM_DEVICE_INFO>());
            Assert.Equal(8, Marshal.SizeOf<TBS_CONTEXT_PARAMS2>());
            Assert.Equal(IntPtr.Size * 2 + 8, Marshal.SizeOf<NCryptKeyName>());
            Assert.Equal(IntPtr.Size == 8 ? 40 : 20, Marshal.SizeOf<CERT_CONTEXT>());
            Assert.Equal(IntPtr.Size == 8 ? 48 : 28, Marshal.SizeOf<CRYPT_KEY_PROV_INFO>());
            Assert.Equal(IntPtr.Size == 8 ? 64 : 32, Marshal.OffsetOf<CERT_INFO_PREFIX>(nameof(CERT_INFO_PREFIX.NotBefore)).ToInt32());
        }

        [Fact]
        public void CapabilityCommand_UsesTheTpmWireFormat()
        {
            Assert.Equal(new byte[] { 0x80, 1, 0, 0, 0, 22, 0, 0, 1, 0x7A, 0, 0, 0, 6, 0, 0, 1, 0, 0, 0, 0, 64 }, Tpm.BuildCapabilityCommand(6, 0x100, 64));
        }

        [Fact]
        public void Capability_PropertiesRetainUnsignedValuesAndAdvancePages()
        {
            var Device = new HwTpm();
            var Page = Response(6, 2, [0, 0, 1, 5, 0x49, 0x4E, 0x54, 0x43, 0, 0, 1, 6, 0xFF, 0xFF, 0xFF, 0xFF], true);
            Assert.True(Tpm.ParseCapability(Page, 6, 0x100, Device, out var More, out var Next));
            Assert.True(More);
            Assert.Equal(0x107u, Next);
            Assert.Equal(0x494E5443u, Device.Properties[0].Value);
            Assert.Equal(uint.MaxValue, Device.Properties[1].Value);
            Assert.True(Tpm.ParseCapability(Response(6, 1, [0, 0, 1, 7, 0, 0, 0, 1]), 6, Next, Device, out More, out Next));
            Assert.False(More);
            Assert.Equal(3, Device.Properties.Count);
        }

        [Fact]
        public void Capability_AlgorithmsAndPcrSelectionsUseDifferentEntryLayouts()
        {
            var Device = new HwTpm();
            Assert.True(Tpm.ParseCapability(Response(0, 2, [0, 1, 0, 0, 0, 9, 0, 0xB, 0, 0, 0, 4]), 0, 0, Device, out _, out _));
            Assert.Equal(1u, Device.Algorithms[0].Algorithm);
            Assert.Equal(9u, Device.Algorithms[0].Attributes);
            Assert.True(Tpm.ParseCapability(Response(5, 2, [0, 4, 3, 0, 0, 0, 0, 0xB, 3, 0xFF, 0xFF, 0xFF]), 5, 0, Device, out _, out _));
            Assert.Equal("000000", Device.PcrBanks[0].Selection);
            Assert.Equal("FFFFFF", Device.PcrBanks[1].Selection);
        }

        [Fact]
        public void Capability_RejectsEveryTruncationWithoutPublishingPartialResults()
        {
            var Complete = Response(6, 2, [0, 0, 1, 5, 0x49, 0x4E, 0x54, 0x43, 0, 0, 1, 6, 0, 0, 0, 0]);

            for (var Length = 0; Length < Complete.Length; Length++)
            {
                var Bytes = Complete.Take(Length).ToArray();
                if (Length >= 6) Write(Bytes, 2, (uint) Length);
                var Device = new HwTpm { Properties = [new HwTpmProperty { Property = 1, Value = 2 }] };
                Assert.False(Tpm.ParseCapability(Bytes, 6, 0x100, Device, out var More, out var Next));
                Assert.False(More);
                Assert.Equal(0x100u, Next);
                Assert.Single(Device.Properties);
            }
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 2)]
        [InlineData(5, 1)]
        [InlineData(9, 1)]
        [InlineData(10, 2)]
        [InlineData(14, 5)]
        [InlineData(15, 255)]
        [InlineData(18, 2)]
        public void Capability_RejectsMalformedHeadersAndCounts(int InOffset, byte InValue)
        {
            var Page = Response(6, 1, [0, 0, 1, 5, 0x49, 0x4E, 0x54, 0x43]);
            Page[InOffset] = InValue;
            Assert.False(Tpm.ParseCapability(Page, 6, 0x100, new HwTpm(), out _, out _));
        }

        [Fact]
        public void Capability_RejectsNonprogressingDuplicateAndOverflowPages()
        {
            Assert.False(Tpm.ParseCapability(Response(6, 0, [], true), 6, 0x100, new HwTpm(), out _, out _));
            Assert.False(Tpm.ParseCapability(Response(6, 1, [0, 0, 1, 0, 0, 0, 0, 0]), 6, 0x101, new HwTpm(), out _, out _));
            Assert.False(Tpm.ParseCapability(Response(6, 2, [0, 0, 1, 5, 0, 0, 0, 0, 0, 0, 1, 5, 0, 0, 0, 0]), 6, 0x100, new HwTpm(), out _, out _));
            Assert.False(Tpm.ParseCapability(Response(6, 1, [255, 255, 255, 255, 0, 0, 0, 0]), 6, 0, new HwTpm(), out _, out _));
            Assert.False(Tpm.ParseCapability(Response(7, 0, []), 7, 0, new HwTpm(), out _, out _));
        }

        [Fact]
        public void Capability_RejectsInvalidPcrLengthsAndTrailingData()
        {
            Assert.False(Tpm.ParseCapability(Response(5, 1, [0, 0xB, 4, 255, 255, 255]), 5, 0, new HwTpm(), out _, out _));
            Assert.False(Tpm.ParseCapability(Response(5, 1, [0, 0xB, 0]), 5, 0, new HwTpm(), out _, out _));
            Assert.False(Tpm.ParseCapability(Response(6, 0, [1]), 6, 0, new HwTpm(), out _, out _));
            Assert.True(Tpm.ParseCapability(Response(6, 0, []), 6, 0, new HwTpm(), out var More, out var Next));
            Assert.False(More);
            Assert.Equal(0u, Next);
        }

        [Fact]
        public void Properties_DistinguishUnknownFlagsFromFalseAndDecodeBigEndianText()
        {
            var Device = new HwTpm();
            Tpm.ApplyProperties(Device);
            Assert.Null(Device.IsOwnerAuthSet);
            Assert.Null(Device.IsInLockout);
            Assert.Null(Device.LockoutCounter);
            Assert.Null(Device.Manufacturer);
            Device.Properties = [new HwTpmProperty { Property = 0x105, Value = 0x49465800 }, new HwTpmProperty { Property = 0x200, Value = 0x205 }, new HwTpmProperty { Property = 0x20E, Value = 7 }];
            Tpm.ApplyProperties(Device);
            Assert.Equal("IFX", Device.Manufacturer);
            Assert.True(Device.IsOwnerAuthSet);
            Assert.False(Device.IsEndorsementAuthSet);
            Assert.True(Device.IsLockoutAuthSet);
            Assert.True(Device.IsInLockout);
            Assert.Equal(7u, Device.LockoutCounter);
            Assert.Equal("INTC", Tpm.DecodeManufacturer(0x494E5443));
        }

        [Fact]
        public void PublicBlob_RetainsOriginalBytesAndHashesTheDocumentedEncoding()
        {
            var Key = new HwTpmKey();
            var Blob = new byte[24 + 3 + 256];
            new byte[] { 0x52, 0x53, 0x41, 0x31, 0, 8, 0, 0 }.CopyTo(Blob, 0);
            BitConverter.GetBytes(3u).CopyTo(Blob, 8);
            BitConverter.GetBytes(256u).CopyTo(Blob, 12);
            Assert.True(Tpm.SetPublicBlob(Key, Blob));
            Assert.Equal("RSA", Key.Algorithm);
            Assert.Equal(2048u, Key.Length);
            Assert.Equal(Blob, Convert.FromBase64String(Key.PublicKeyBlob!));
            using var Hash = SHA256.Create();
            Assert.Equal(BitConverter.ToString(Hash.ComputeHash(Blob)).Replace("-", "").ToLowerInvariant(), Key.PublicKeySha256);
            Assert.False(Tpm.SetPublicBlob(new HwTpmKey(), [1]));
        }

        [Fact]
        public void PublicBlob_RecognizesP521WithoutOverwritingProviderMetadata()
        {
            var Key = new HwTpmKey();
            var Blob = new byte[8 + 2 * 66];
            new byte[] { 0x45, 0x43, 0x53, 0x35, 66, 0, 0, 0 }.CopyTo(Blob, 0);
            Assert.True(Tpm.SetPublicBlob(Key, Blob));
            Assert.Equal("ECDSA", Key.Algorithm);
            Assert.Equal(521u, Key.Length);
            Key.Algorithm = "ECDSA_P521";
            Assert.True(Tpm.SetPublicBlob(Key, Blob));
            Assert.Equal("ECDSA_P521", Key.Algorithm);
        }

        [Theory]
        [InlineData(2030)]
        [InlineData(9999)]
        public void Certificate_ExposesDerIdentityAndUtcValidityWithoutTrustValidation(int InYear)
        {
            using var Key = RSA.Create(2048);
            var Request = new CertificateRequest("CN=Synthetic TPM Certificate", Key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var Certificate = Request.CreateSelfSigned(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(InYear, 12, 31, 23, 59, 59, TimeSpan.Zero));
            var Der = Certificate.Export(X509ContentType.Cert);
            var Parsed = Tpm.ParseCertificate(Der, "Synthetic");
            Assert.NotNull(Parsed);
            Assert.Equal(Certificate.Subject, Parsed.Subject);
            Assert.Equal(Certificate.SerialNumber, Parsed.SerialNumber);
            Assert.Equal("2025-01-01T00:00:00Z", Parsed.NotBefore);
            Assert.Equal(InYear + "-12-31T23:59:59Z", Parsed.NotAfter);
            Assert.Equal("1.2.840.113549.1.1.1", Parsed.PublicKeyAlgorithm);
            Assert.Equal(Tpm.Sha256(Der), Parsed.Sha256);
            Assert.Equal(Der, Convert.FromBase64String(Parsed.Der!));
            Assert.Null(Tpm.ParseCertificate([1, 2, 3], "Invalid"));
        }

        [Fact]
        public void TpmJson_RoundTripsNestedPublicMaterialAndIgnoresConvenienceGetter()
        {
            var Hwid = new Hwid { Tpms = [new HwTpm { Version = "2.0", IsInLockout = false, Keys = [new HwTpmKey { Name = "Synthetic", PublicKeyBlob = "AQID", Certificates = [new HwTpmCertificate { Source = "KeyProperty", Der = "AQID" }] }], Errors = [new HwTpmError { Operation = "Denied", Source = "NCrypt", Code = 0x80090010 }] }] };
            var Json = JsonSerializer.Serialize(Hwid);
            Assert.DoesNotContain("\"Tpm\"", Json);
            Assert.DoesNotContain("\"tpm\"", Json);
            Assert.Equal(Json, JsonSerializer.Serialize(JsonSerializer.Deserialize<Hwid>(Json)));
            using var Document = JsonDocument.Parse(Json);
            var Device = Document.RootElement.GetProperty("tpms")[0];
            Assert.Equal(JsonValueKind.Null, Device.GetProperty("manufacturer_id").ValueKind);
            Assert.False(Device.GetProperty("is_in_lockout").GetBoolean());
            Assert.Equal("AQID", Device.GetProperty("keys")[0].GetProperty("certificates")[0].GetProperty("der").GetString());
        }

        [Fact]
        public void CancelledCollector_DoesNotQueryTheTpm()
        {
            var Hwid = new Hwid();
            HardwareIds.RetrieveTpms(Hwid, new CancellationToken(true));
            Assert.Empty(Hwid.Tpms);
        }

        [Fact]
        public void TransientReads_RetryWithABoundAndPreservePermanentFailures()
        {
            var Calls = 0;
            Assert.Equal(0u, Tpm.RetryRead(() => ++Calls < 2 ? 0x909u : 0u, TestContext.Current.CancellationToken));
            Assert.Equal(2, Calls);
            Calls = 0;
            Assert.Equal(0x922u, Tpm.RetryRead(() => { Calls++; return 0x922u; }, TestContext.Current.CancellationToken));
            Assert.Equal(4, Calls);
            Calls = 0;
            Assert.Equal(0x80090010u, Tpm.RetryRead(() => { Calls++; return 0x80090010u; }, TestContext.Current.CancellationToken));
            Assert.Equal(1, Calls);
            Calls = 0;
            Assert.Equal(1223u, Tpm.RetryRead(() => { Calls++; return 0x909u; }, new CancellationToken(true)));
            Assert.Equal(0, Calls);
        }

        [Theory]
        [InlineData("IBM\0\0", 0x49424D00u, "IBM")]
        [InlineData("INTC\0", 0x494E5443u, "INTC")]
        [InlineData("IFX \0", 0x49465820u, "IFX")]
        public void ProviderManufacturer_DecodesUtf16RatherThanADword(string InText, uint InId, string InName)
        {
            var Device = new HwTpm();
            Assert.True(Tpm.ApplyManufacturer(Device, Encoding.Unicode.GetBytes(InText)));
            Assert.Equal(InId, Device.ManufacturerId);
            Assert.Equal(InName, Device.Manufacturer);
        }

        [Fact]
        public void ProviderIdentity_RejectsMalformedDataAndKeepsKnownFirmware()
        {
            var Device = new HwTpm { Manufacturer = "Existing", FirmwareVersion1 = 1 };
            foreach (var Bytes in new[] { new byte[] { 1, 0, 0, 0 }, Encoding.Unicode.GetBytes("INTC"), Encoding.Unicode.GetBytes("I\0BM\0"), Encoding.Unicode.GetBytes("ABCDE\0"), new byte[] { 0, 0 } })
                Assert.False(Tpm.ApplyManufacturer(Device, Bytes));
            Assert.Equal("Existing", Device.Manufacturer);
            Assert.True(Tpm.ApplyFirmware(Device, [0x19, 0x20, 0x23, 0x10, 0x16, 0, 0x36, 0x36]));
            Assert.Equal(1u, Device.FirmwareVersion1);
            Assert.Equal(0x00163636u, Device.FirmwareVersion2);
            Device.FirmwareVersion1 = null;
            Assert.True(Tpm.ApplyFirmware(Device, [0x19, 0x20, 0x23, 0x10, 0x16, 0, 0x36, 0x36]));
            Assert.Equal(0x20191023u, Device.FirmwareVersion1);
            Assert.False(Tpm.ApplyFirmware(Device, new byte[7]));
        }

        [Fact]
        public void Capability_PcrBanksMayBeUnsortedButCannotBeDuplicatedOrPaged()
        {
            var Device = new HwTpm();
            Assert.True(Tpm.ParseCapability(Response(5, 2, [0, 0xB, 3, 255, 255, 255, 0, 4, 3, 0, 0, 0]), 5, 0, Device, out _, out _));
            Assert.Equal(new uint[] { 11, 4 }, Device.PcrBanks.Select(T => T.Algorithm));
            Assert.False(Tpm.ParseCapability(Response(5, 2, [0, 4, 3, 0, 0, 0, 0, 4, 3, 0, 0, 0]), 5, 0, new HwTpm(), out _, out _));
            Assert.False(Tpm.ParseCapability(Response(5, 1, [0, 4, 3, 0, 0, 0], true), 5, 0, new HwTpm(), out _, out _));
        }

        [Fact]
        public void Capability_RejectsCrossGroupPropertiesAndExcessiveCounts()
        {
            Assert.False(Tpm.ParseCapability(Response(6, 1, [0, 0, 2, 0, 0, 0, 0, 0]), 6, 0x100, new HwTpm(), out _, out _));
            Assert.False(Tpm.ParseCapability(Response(0, 65, new byte[65 * 6]), 0, 0, new HwTpm(), out _, out _));
        }

        [Fact]
        public void PublicBlob_RejectsTruncationOverflowAndPrivateKeyHeaders()
        {
            var Rsa = new byte[24 + 3 + 256];
            new byte[] { 0x52, 0x53, 0x41, 0x31, 0, 8, 0, 0, 3, 0, 0, 0, 0, 1, 0, 0 }.CopyTo(Rsa, 0);
            Assert.True(Tpm.SetPublicBlob(new HwTpmKey(), Rsa));

            for (var Length = 0; Length < Rsa.Length; Length++)
            {
                var Key = new HwTpmKey();
                Assert.False(Tpm.SetPublicBlob(Key, Rsa.Take(Length).ToArray()));
                Assert.Null(Key.PublicKeyBlob);
                Assert.Null(Key.Length);
            }

            Rsa[16] = 1;
            Assert.False(Tpm.SetPublicBlob(new HwTpmKey(), Rsa));
            Rsa[16] = 0;
            Rsa[3] = 0x32;
            Assert.False(Tpm.SetPublicBlob(new HwTpmKey(), Rsa));
            Assert.False(Tpm.SetPublicBlob(new HwTpmKey(), [0x45, 0x43, 0x53, 0x31, 255, 255, 255, 255]));
            Assert.False(Tpm.SetPublicBlob(new HwTpmKey(), [0x45, 0x43, 0x53, 0x35, 66, 0, 0, 0]));
        }

        [Fact]
        public void PublicBlob_GenericCurvesDoNotInventABitLength()
        {
            var Blob = new byte[8 + 2 * 66];
            new byte[] { 0x45, 0x43, 0x44, 0x50, 66, 0, 0, 0 }.CopyTo(Blob, 0);
            var Key = new HwTpmKey();
            Assert.True(Tpm.SetPublicBlob(Key, Blob));
            Assert.Equal("ECDSA", Key.Algorithm);
            Assert.Null(Key.Length);
        }

        [Fact]
        public void PublicBlob_FullEccIncludesCurveParametersAndExactFieldBits()
        {
            var Blob = new byte[32 + 7 * 66 + 66 + 1];
            new byte[] { 0x45, 0x43, 0x4B, 0x50, 1, 0, 0, 0, 1, 0, 0, 0 }.CopyTo(Blob, 0);
            BitConverter.GetBytes(66u).CopyTo(Blob, 16);
            BitConverter.GetBytes(66u).CopyTo(Blob, 20);
            BitConverter.GetBytes(1u).CopyTo(Blob, 24);
            Blob[32] = 1;
            var Key = new HwTpmKey();
            Assert.True(Tpm.SetPublicBlob(Key, Blob));
            Assert.Equal("ECDH", Key.Algorithm);
            Assert.Equal(521u, Key.Length);
            Assert.Equal(Blob, Convert.FromBase64String(Key.PublicKeyBlob!));

            for (var Length = 0; Length < Blob.Length; Length++)
                Assert.False(Tpm.SetPublicBlob(new HwTpmKey(), Blob.Take(Length).ToArray()));

            Assert.False(Tpm.SetPublicBlob(new HwTpmKey(), Blob.Concat(new byte[66]).ToArray()));
            Blob[3] = 0x56;
            Assert.False(Tpm.SetPublicBlob(new HwTpmKey(), Blob));
            Blob[3] = 0x50;
            BitConverter.GetBytes(uint.MaxValue).CopyTo(Blob, 16);
            Assert.False(Tpm.SetPublicBlob(new HwTpmKey(), Blob));
        }

        [Fact]
        public void Certificate_RejectsPfxAndTrailingData()
        {
            using var Key = RSA.Create(2048);
            var Request = new CertificateRequest("CN=Synthetic", Key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var Certificate = Request.CreateSelfSigned(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
            Assert.Null(Tpm.ParseCertificate(Certificate.Export(X509ContentType.Pfx), "Pfx"));
            var Der = Certificate.Export(X509ContentType.Cert);
            Assert.NotNull(Tpm.ParseCertificate(Der, "Der"));
            Assert.Null(Tpm.ParseCertificate(Der.Concat(new byte[] { 0 }).ToArray(), "Trailing"));
            Assert.False(Tpm.IsDerSequence([0x30, 0x80, 0, 0]));
            Assert.False(Tpm.IsDerSequence([0x30, 0x81, 1, 0]));
            Assert.False(Tpm.IsDerSequence([0x30, 0x84, 255, 255, 255, 255]));
        }

        [Fact]
        public void CancelledPropertyRead_DoesNotCallCngOrReportAnAccessError()
        {
            var Device = new HwTpm();
            var Method = typeof(Tpm).GetMethod("GetProperty", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            Assert.Null(Method.Invoke(null, [IntPtr.Zero, "Length", Device, "Cancelled", new CancellationToken(true)]));
            Assert.Empty(Device.Errors);
        }
    }
}
