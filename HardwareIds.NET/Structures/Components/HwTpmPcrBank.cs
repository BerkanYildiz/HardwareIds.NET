namespace HardwareIds.NET.Structures.Components
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class HwTpmPcrBank
    {
        /// <summary>
        /// Gets or sets the bank hash TPM_ALG_ID.
        /// </summary>
        [JsonPropertyName("algorithm")]
        public uint Algorithm { get; set; }

        /// <summary>
        /// Gets or sets the selected PCR bitmap, in upper-case hexadecimal.
        /// </summary>
        [JsonPropertyName("selection")]
        public string? Selection { get; set; }
    }
}
