namespace HardwareIds.NET.Structures.Components
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class HwTpmKey
    {
        /// <summary>
        /// Gets or sets the key kind (Endorsement, StorageRoot or Persisted).
        /// </summary>
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        /// <summary>
        /// Gets or sets the provider property or persisted key name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the key scope (Platform, CurrentUser or LocalMachine).
        /// </summary>
        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        /// <summary>
        /// Gets or sets the CNG algorithm name.
        /// </summary>
        [JsonPropertyName("algorithm")]
        public string? Algorithm { get; set; }

        /// <summary>
        /// Gets or sets the key length, in bits.
        /// </summary>
        [JsonPropertyName("length")]
        public uint? Length { get; set; }

        /// <summary>
        /// Gets or sets the unique persisted key name.
        /// </summary>
        [JsonPropertyName("unique_name")]
        public string? UniqueName { get; set; }

        /// <summary>
        /// Gets or sets the NCRYPT_KEY_USAGE_PROPERTY flags.
        /// </summary>
        [JsonPropertyName("usage")]
        public uint? Usage { get; set; }

        /// <summary>
        /// Gets or sets the PCP_KEY_USAGE_POLICY flags (8 denotes an identity key).
        /// </summary>
        [JsonPropertyName("tpm_usage")]
        public uint? TpmUsage { get; set; }

        /// <summary>
        /// Gets or sets the BCRYPT public key blob, encoded as Base64.
        /// </summary>
        [JsonPropertyName("public_key_blob")]
        public string? PublicKeyBlob { get; set; }

        /// <summary>
        /// Gets or sets the SHA-256 of the public key blob, in lower-case hexadecimal.
        /// </summary>
        [JsonPropertyName("public_key_sha256")]
        public string? PublicKeySha256 { get; set; }

        /// <summary>
        /// Gets or sets the certificates associated with this key.
        /// </summary>
        [JsonPropertyName("certificates")]
        public List<HwTpmCertificate> Certificates { get; set; } = [];
    }
}
