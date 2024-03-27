namespace HardwareIds.NET.Structures.Components
{
    using System.Text.Json.Serialization;

    public class HwSmbios
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the raw table's hash.
        /// </summary>
        [JsonPropertyName("hash")]
        public string Hash { get; set; }

        /// <summary>
        /// Gets or sets the size (in bytes) of the table.
        /// </summary>
        [JsonPropertyName("length")]
        public uint Length { get; set; }
    }
}
