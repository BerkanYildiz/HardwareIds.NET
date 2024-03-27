namespace HardwareIds.NET.Structures.Components
{
    using System.Text.Json.Serialization;

    public class HwVolume
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the path.
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the letter.
        /// </summary>
        [JsonPropertyName("letter")]
        public string Letter { get; set; }

        /// <summary>
        /// Gets or sets the serial number.
        /// </summary>
        [JsonPropertyName("serial_number")]
        public uint SerialNumber { get; set; }
    }
}
