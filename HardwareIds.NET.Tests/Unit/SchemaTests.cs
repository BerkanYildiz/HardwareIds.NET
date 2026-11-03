namespace HardwareIds.NET.Tests.Unit
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using Xunit;

    /// <summary>
    /// Keeps hwid.schema.json in sync with the models: the schema forbids unknown properties and requires every known one,
    /// so adding, renaming or removing a serialized property fails here until the schema follows.
    /// </summary>
    public class SchemaTests
    {
        private static Hwid BuildFullyPopulatedHwid()
        {
            var Hwid = SerializationTests.BuildPopulatedHwid();
            var Blob = Encoding.ASCII.GetBytes("RSA1 synthetic public key blob");
            var Der = Encoding.ASCII.GetBytes("synthetic DER certificate");

            var Certificate = new HwTpmCertificate
            {
                Source = "PCP_EKCERT",
                Subject = "",
                Issuer = "CN=Example EK CA",
                SerialNumber = "4F1C2A",
                NotBefore = "2024-03-07T00:00:00Z",
                NotAfter = "2044-03-07T00:00:00Z",
                SignatureAlgorithm = "1.2.840.113549.1.1.11",
                PublicKeyAlgorithm = "1.2.840.113549.1.1.1",
                Sha256 = Tpm.Sha256(Der),
                Der = Convert.ToBase64String(Der),
            };

            Hwid.Tpms[0] = new HwTpm
            {
                Id = 0,
                Version = "2.0",
                InterfaceType = 1,
                ImplementationRevision = 1,
                ManufacturerId = 0x58595A00,
                Manufacturer = "XYZ",
                VendorString = "Example TPM",
                Family = "2.0",
                SpecificationLevel = 0,
                SpecificationRevision = 159,
                SpecificationYear = 2023,
                SpecificationDayOfYear = 100,
                FirmwareVersion1 = 0x00070002,
                FirmwareVersion2 = 0x00040000,
                PermanentFlags = 0x7,
                StartupClearFlags = 0x8000000F,
                IsOwnerAuthSet = true,
                IsEndorsementAuthSet = false,
                IsLockoutAuthSet = true,
                IsInLockout = false,
                LockoutCounter = 0,
                MaxAuthFail = 32,
                LockoutInterval = 7200,
                LockoutRecovery = 86400,
                Properties = [new HwTpmProperty { Property = 0x100, Value = 0x322E3000 }, new HwTpmProperty { Property = 0x200, Value = 0x7 }],
                Algorithms = [new HwTpmAlgorithm { Algorithm = 0x0001, Attributes = 0x9 }],
                PcrBanks = [new HwTpmPcrBank { Algorithm = 0x000B, Selection = "FFFFFF" }],
                Keys = [new HwTpmKey { Kind = "Endorsement", Name = "PCP_EKPUB", Scope = "Platform", Algorithm = "RSA", Length = 2048, UniqueName = null, Usage = 0x2, TpmUsage = 0x8, PublicKeyBlob = Convert.ToBase64String(Blob), PublicKeySha256 = Tpm.Sha256(Blob), Certificates = [Certificate] }],
                Certificates = [Certificate],
                Errors = [new HwTpmError { Operation = "TPM2_GetCapability/6/512", Source = "TBS", Code = 0x80284001 }],
            };

            return Hwid;
        }

        [Fact]
        public void Schema_AcceptsAFullyPopulatedScan()
        {
            Assert.Empty(HwidSchema.Validate(BuildFullyPopulatedHwid()));
        }

        [Fact]
        public void Schema_AcceptsAnEmptyScan()
        {
            Assert.Empty(HwidSchema.Validate(new Hwid()));
        }

        [Fact]
        public void Schema_AcceptsDefaultValues()
        {
            //
            // Unset strings, nullable numbers and dates serialize as null, and unset dates and GUIDs as their default values.
            //

            var Hwid = new Hwid
            {
                Disks = [new()],
                Volumes = [new()],
                NetworkAdapters = [new()],
                BluetoothRadios = [new()],
                Baseboards = [new()],
                Motherboards = [new()],
                Chassis = [new()],
                BiosFirmwares = [new()],
                SmbiosTables = [new()],
                Processors = [new()],
                MemorySticks = [new()],
                Batteries = [new()],
                Monitors = [new()],
                VideoControllers = [new()],
                Printers = [new()],
                Users = [new()],
                OperatingSystems = [new()],
                Wifis = [new()],
                Routers = [new() { Gateways = [new()], NetworkDevices = [new()] }],
                NetworkSignatures = [new()],
                Tpms = [new() { Properties = [new()], Algorithms = [new()], PcrBanks = [new()], Keys = [new() { Certificates = [new()] }], Certificates = [new()], Errors = [new()] }],
            };

            Assert.Empty(HwidSchema.Validate(Hwid));
        }

        [Fact]
        public void Schema_AcceptsLocalDates()
        {
            var Hwid = SerializationTests.BuildPopulatedHwid();
            Hwid.OperatingSystems[0].LastBootUpTime = new DateTime(2026, 9, 15, 8, 30, 12, DateTimeKind.Local).AddTicks(1234567);
            Hwid.OperatingSystems[0].InstallTime = new DateTime(2024, 3, 7, 9, 41, 27, DateTimeKind.Utc);

            Assert.Empty(HwidSchema.Validate(Hwid));
        }

        [Theory]
        [InlineData("an unknown root property")]
        [InlineData("a missing list")]
        [InlineData("an unknown component property")]
        [InlineData("a missing component property")]
        [InlineData("a mistyped number")]
        [InlineData("a negative unsigned number")]
        [InlineData("a null list")]
        [InlineData("a lower-case MAC address")]
        [InlineData("a malformed date")]
        [InlineData("an upper-case hash")]
        [InlineData("an unknown TPM key kind")]
        public void Schema_Rejects(string InCase)
        {
            var Root = JsonNode.Parse(JsonSerializer.Serialize(BuildFullyPopulatedHwid()))!.AsObject();
            var Disk = Root["disks"]![0]!.AsObject();

            switch (InCase)
            {
                case "an unknown root property":
                    Root["extra"] = 1;
                    break;
                case "a missing list":
                    Root.Remove("volumes");
                    break;
                case "an unknown component property":
                    Disk["extra"] = "value";
                    break;
                case "a missing component property":
                    Disk.Remove("serial_number");
                    break;
                case "a mistyped number":
                    Disk["partitions"] = "1";
                    break;
                case "a negative unsigned number":
                    Root["volumes"]![0]!["serial_number"] = -1;
                    break;
                case "a null list":
                    Root["printers"] = null;
                    break;
                case "a lower-case MAC address":
                    Root["network_adapters"]![0]!["address"]!["current"] = "00:aa:22:33:44:55";
                    break;
                case "a malformed date":
                    Root["operating_systems"]![0]!["install_date"] = "07/03/2024";
                    break;
                case "an upper-case hash":
                    Root["smbios_tables"]![0]!["hash"] = new string('A', 64);
                    break;
                case "an unknown TPM key kind":
                    Root["tpms"]![0]!["keys"]![0]!["kind"] = "Attestation";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(InCase));
            }

            Assert.NotEmpty(HwidSchema.Validate(Root.ToJsonString()));
        }

        [Fact]
        public void Schema_DescribesEverySerializedComponent()
        {
            var Json = JsonSerializer.Serialize(BuildFullyPopulatedHwid());
            using var Document = JsonDocument.Parse(Json);

            //
            // The fixture must fill every list, otherwise a component could drift from the schema without failing the tests above.
            //

            Assert.All(Document.RootElement.EnumerateObject().Where(T => T.Value.ValueKind == JsonValueKind.Array), T => Assert.NotEqual(0, T.Value.GetArrayLength()));
            Assert.Equal(Document.RootElement.EnumerateObject().Select(T => T.Name), HwidSchema.Schema.RequiredProperties);
        }

        [Fact]
        public void Schema_RequiresTheLibraryVersion()
        {
            //
            // The version tells a consumer which schema describes the snapshot, so it is never optional nor free-form.
            //

            var Root = JsonNode.Parse(JsonSerializer.Serialize(new Hwid()))!.AsObject();
            Assert.Equal(HardwareIds.Version, Root["library_version"]!.GetValue<string>());

            foreach (var Version in new[] { "2.4.0", "2.4.0-beta.1", "10.0.12-rc.1.2" })
            {
                Root["library_version"] = Version;
                Assert.Empty(HwidSchema.Validate(Root.ToJsonString()));
            }

            foreach (var Version in new[] { "", "2.4", "v2.4.0", "2.4.0+1a2b3c", "2.4.0 " })
            {
                Root["library_version"] = Version;
                Assert.NotEmpty(HwidSchema.Validate(Root.ToJsonString()));
            }

            Root.Remove("library_version");
            Assert.NotEmpty(HwidSchema.Validate(Root.ToJsonString()));
        }
    }
}
