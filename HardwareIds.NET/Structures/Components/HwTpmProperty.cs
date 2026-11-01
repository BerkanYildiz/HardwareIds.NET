namespace HardwareIds.NET.Structures.Components
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class HwTpmProperty
    {
        /// <summary>
        /// Gets or sets the TPM_PT identifier.
        /// </summary>
        [JsonPropertyName("property")]
        public uint Property { get; set; }

        /// <summary>
        /// Gets or sets the raw TPM property value.
        /// </summary>
        [JsonPropertyName("value")]
        public uint Value { get; set; }
    }
}
