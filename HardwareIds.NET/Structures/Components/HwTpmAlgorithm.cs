namespace HardwareIds.NET.Structures.Components
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class HwTpmAlgorithm
    {
        /// <summary>
        /// Gets or sets the TPM_ALG_ID identifier.
        /// </summary>
        [JsonPropertyName("algorithm")]
        public uint Algorithm { get; set; }

        /// <summary>
        /// Gets or sets the TPMA_ALGORITHM flags.
        /// </summary>
        [JsonPropertyName("attributes")]
        public uint Attributes { get; set; }
    }
}
