namespace HardwareIds.NET.Structures.Components
{
    using System.Text.Json.Serialization;

    public class HwMemoryStick
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
        public string Manufacturer { get; set; }

        /// <summary>
        /// Gets or sets the part number.
        /// </summary>
        [JsonPropertyName("part_number")]
        public string PartNumber { get; set; }

        /// <summary>
        /// Gets or sets the serial number.
        /// </summary>
        [JsonPropertyName("serial_number")]
        public string SerialNumber { get; set; }

        /// <summary>
        /// Gets or sets the capacity.
        /// </summary>
        [JsonPropertyName("capacity_in_gb")]
        public string Capacity { get; set; }

        /// <summary>
        /// Gets or sets the configured clock speed.
        /// </summary>
        [JsonPropertyName("clock_speed")]
        public string ClockSpeed { get; set; }

        /// <summary>
        /// Gets or sets the configured voltage.
        /// </summary>
        [JsonPropertyName("voltage")]
        public string Voltage { get; set; }

        /// <summary>
        /// Gets or sets the channel.
        /// </summary>
        [JsonPropertyName("channel")]
        public string Channel { get; set; }
    }
}
