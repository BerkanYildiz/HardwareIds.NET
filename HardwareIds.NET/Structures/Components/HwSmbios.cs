namespace HardwareIds.NET.Structures.Components
{
    using Newtonsoft.Json;

    public class HwSmbios
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonProperty("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        [JsonProperty("version")]
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the raw table's hash.
        /// </summary>
        [JsonProperty("hash")]
        public string Hash { get; set; }

        /// <summary>
        /// Gets or sets the size (in bytes) of the table.
        /// </summary>
        [JsonProperty("length")]
        public uint Length { get; set; }
    }
}
