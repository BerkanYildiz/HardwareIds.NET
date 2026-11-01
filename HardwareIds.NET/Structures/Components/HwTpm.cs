namespace HardwareIds.NET.Structures.Components
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class HwTpm
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the TPM generation (1.2 or 2.0).
        /// </summary>
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets the TPM_IFTYPE value reported by TBS.
        /// </summary>
        [JsonPropertyName("interface_type")]
        public uint? InterfaceType { get; set; }

        /// <summary>
        /// Gets or sets the TBS implementation revision.
        /// </summary>
        [JsonPropertyName("implementation_revision")]
        public uint? ImplementationRevision { get; set; }

        /// <summary>
        /// Gets or sets the numeric TCG manufacturer identifier.
        /// </summary>
        [JsonPropertyName("manufacturer_id")]
        public uint? ManufacturerId { get; set; }

        /// <summary>
        /// Gets or sets the four-character TCG manufacturer identifier.
        /// </summary>
        [JsonPropertyName("manufacturer")]
        public string? Manufacturer { get; set; }

        /// <summary>
        /// Gets or sets the TPM vendor string.
        /// </summary>
        [JsonPropertyName("vendor_string")]
        public string? VendorString { get; set; }

        /// <summary>
        /// Gets or sets the specification family.
        /// </summary>
        [JsonPropertyName("family")]
        public string? Family { get; set; }

        /// <summary>
        /// Gets or sets the specification level.
        /// </summary>
        [JsonPropertyName("specification_level")]
        public uint? SpecificationLevel { get; set; }

        /// <summary>
        /// Gets or sets the specification revision in hundredths.
        /// </summary>
        [JsonPropertyName("specification_revision")]
        public uint? SpecificationRevision { get; set; }

        /// <summary>
        /// Gets or sets the specification year.
        /// </summary>
        [JsonPropertyName("specification_year")]
        public uint? SpecificationYear { get; set; }

        /// <summary>
        /// Gets or sets the specification day of year.
        /// </summary>
        [JsonPropertyName("specification_day_of_year")]
        public uint? SpecificationDayOfYear { get; set; }

        /// <summary>
        /// Gets or sets the first vendor-defined firmware version word.
        /// </summary>
        [JsonPropertyName("firmware_version1")]
        public uint? FirmwareVersion1 { get; set; }

        /// <summary>
        /// Gets or sets the second vendor-defined firmware version word.
        /// </summary>
        [JsonPropertyName("firmware_version2")]
        public uint? FirmwareVersion2 { get; set; }

        /// <summary>
        /// Gets or sets the TPMA_PERMANENT flags.
        /// </summary>
        [JsonPropertyName("permanent_flags")]
        public uint? PermanentFlags { get; set; }

        /// <summary>
        /// Gets or sets the TPMA_STARTUP_CLEAR flags.
        /// </summary>
        [JsonPropertyName("startup_clear_flags")]
        public uint? StartupClearFlags { get; set; }

        /// <summary>
        /// Gets or sets whether owner authorization is set.
        /// </summary>
        [JsonPropertyName("is_owner_auth_set")]
        public bool? IsOwnerAuthSet { get; set; }

        /// <summary>
        /// Gets or sets whether endorsement authorization is set.
        /// </summary>
        [JsonPropertyName("is_endorsement_auth_set")]
        public bool? IsEndorsementAuthSet { get; set; }

        /// <summary>
        /// Gets or sets whether lockout authorization is set.
        /// </summary>
        [JsonPropertyName("is_lockout_auth_set")]
        public bool? IsLockoutAuthSet { get; set; }

        /// <summary>
        /// Gets or sets whether dictionary-attack lockout is active.
        /// </summary>
        [JsonPropertyName("is_in_lockout")]
        public bool? IsInLockout { get; set; }

        /// <summary>
        /// Gets or sets the dictionary-attack failure counter.
        /// </summary>
        [JsonPropertyName("lockout_counter")]
        public uint? LockoutCounter { get; set; }

        /// <summary>
        /// Gets or sets the dictionary-attack failure limit.
        /// </summary>
        [JsonPropertyName("max_auth_fail")]
        public uint? MaxAuthFail { get; set; }

        /// <summary>
        /// Gets or sets the counter recovery interval, in seconds.
        /// </summary>
        [JsonPropertyName("lockout_interval")]
        public uint? LockoutInterval { get; set; }

        /// <summary>
        /// Gets or sets the lockout authorization recovery interval, in seconds.
        /// </summary>
        [JsonPropertyName("lockout_recovery")]
        public uint? LockoutRecovery { get; set; }

        /// <summary>
        /// Gets or sets the raw fixed and variable TPM 2.0 properties.
        /// </summary>
        [JsonPropertyName("properties")]
        public List<HwTpmProperty> Properties { get; set; } = [];

        /// <summary>
        /// Gets or sets the supported TPM 2.0 algorithms.
        /// </summary>
        [JsonPropertyName("algorithms")]
        public List<HwTpmAlgorithm> Algorithms { get; set; } = [];

        /// <summary>
        /// Gets or sets the allocated TPM 2.0 PCR banks.
        /// </summary>
        [JsonPropertyName("pcr_banks")]
        public List<HwTpmPcrBank> PcrBanks { get; set; } = [];

        /// <summary>
        /// Gets or sets the accessible public TPM keys.
        /// </summary>
        [JsonPropertyName("keys")]
        public List<HwTpmKey> Keys { get; set; } = [];

        /// <summary>
        /// Gets or sets the available endorsement certificates, with their sources.
        /// </summary>
        [JsonPropertyName("certificates")]
        public List<HwTpmCertificate> Certificates { get; set; } = [];

        /// <summary>
        /// Gets or sets the failed queries; unsupported or restricted data stays unset.
        /// </summary>
        [JsonPropertyName("errors")]
        public List<HwTpmError> Errors { get; set; } = [];
    }
}
