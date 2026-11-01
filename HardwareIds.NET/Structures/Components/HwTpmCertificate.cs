namespace HardwareIds.NET.Structures.Components
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class HwTpmCertificate
    {
        /// <summary>
        /// Gets or sets the certificate source.
        /// </summary>
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        /// <summary>
        /// Gets or sets the certificate subject.
        /// </summary>
        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        /// <summary>
        /// Gets or sets the certificate issuer.
        /// </summary>
        [JsonPropertyName("issuer")]
        public string? Issuer { get; set; }

        /// <summary>
        /// Gets or sets the certificate serial number, in upper-case hexadecimal.
        /// </summary>
        [JsonPropertyName("serial_number")]
        public string? SerialNumber { get; set; }

        /// <summary>
        /// Gets or sets the validity start, in UTC ISO 8601 format.
        /// </summary>
        [JsonPropertyName("not_before")]
        public string? NotBefore { get; set; }

        /// <summary>
        /// Gets or sets the validity end, in UTC ISO 8601 format.
        /// </summary>
        [JsonPropertyName("not_after")]
        public string? NotAfter { get; set; }

        /// <summary>
        /// Gets or sets the signature algorithm OID.
        /// </summary>
        [JsonPropertyName("signature_algorithm")]
        public string? SignatureAlgorithm { get; set; }

        /// <summary>
        /// Gets or sets the public key algorithm OID.
        /// </summary>
        [JsonPropertyName("public_key_algorithm")]
        public string? PublicKeyAlgorithm { get; set; }

        /// <summary>
        /// Gets or sets the SHA-256 of the DER certificate, in lower-case hexadecimal.
        /// </summary>
        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        /// <summary>
        /// Gets or sets the complete DER certificate, encoded as Base64.
        /// </summary>
        [JsonPropertyName("der")]
        public string? Der { get; set; }
    }
}
