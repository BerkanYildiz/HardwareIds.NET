namespace HardwareIds.NET.Structures.Components
{
    using System.Text.Json.Serialization;

    public class HwPrinter
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the port name.
        /// </summary>
        [JsonPropertyName("port_name")]
        public string PortName { get; set; }

        /// <summary>
        /// Gets or sets the location.
        /// </summary>
        [JsonPropertyName("location")]
        public string Location { get; set; }

        /// <summary>
        /// Gets or sets the resolution width.
        /// </summary>
        [JsonPropertyName("width")]
        public uint Width { get; set; }

        /// <summary>
        /// Gets or sets the resolution height.
        /// </summary>
        [JsonPropertyName("height")]
        public uint Height { get; set; }
    }
}
