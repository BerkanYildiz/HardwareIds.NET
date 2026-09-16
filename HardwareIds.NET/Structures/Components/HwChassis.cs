namespace HardwareIds.NET.Structures.Components
{
    using System.Text.Json.Serialization;

    public class HwChassis
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the manufacturer.
        /// </summary>
        [JsonPropertyName("manufacturer")]
        public string? Manufacturer { get; set; }

        /// <summary>
        /// Gets or sets the SMBIOS chassis type (3 = Desktop, 9 = Laptop, 10 = Notebook, ...).
        /// </summary>
        [JsonPropertyName("type")]
        public int Type { get; set; }

        /// <summary>
        /// Gets or sets the name of the chassis type.
        /// </summary>
        [JsonPropertyName("type_name")]
        public string? TypeName { get; set; }

        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets the serial number.
        /// </summary>
        [JsonPropertyName("serial_number")]
        public string? SerialNumber { get; set; }

        /// <summary>
        /// Gets or sets the asset tag.
        /// </summary>
        [JsonPropertyName("asset_tag")]
        public string? AssetTag { get; set; }
    }
}
