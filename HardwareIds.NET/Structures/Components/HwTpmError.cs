namespace HardwareIds.NET.Structures.Components
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class HwTpmError
    {
        /// <summary>
        /// Gets or sets the failed query or property.
        /// </summary>
        [JsonPropertyName("operation")]
        public string? Operation { get; set; }

        /// <summary>
        /// Gets or sets the error domain (TBS, TPM, NCrypt or Win32).
        /// </summary>
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        /// <summary>
        /// Gets or sets the raw error code in its source domain.
        /// </summary>
        [JsonPropertyName("code")]
        public uint Code { get; set; }
    }
}
