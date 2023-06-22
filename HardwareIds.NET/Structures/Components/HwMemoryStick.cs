namespace HardwareIds.NET.Structures.Components
{
    using Newtonsoft.Json;

    public class HwMemoryStick
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonProperty("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the manufacturer.
        /// </summary>
        [JsonProperty("manufacturer")]
        public string Manufacturer { get; set; }

        /// <summary>
        /// Gets or sets the part number.
        /// </summary>
        [JsonProperty("part_number")]
        public string PartNumber { get; set; }

        /// <summary>
        /// Gets or sets the serial number.
        /// </summary>
        [JsonProperty("serial_number")]
        public string SerialNumber { get; set; }

        /// <summary>
        /// Gets or sets the capacity.
        /// </summary>
        [JsonProperty("capacity_in_gb")]
        public string Capacity { get; set; }

        /// <summary>
        /// Gets or sets the configured clock speed.
        /// </summary>
        [JsonProperty("clock_speed")]
        public string ClockSpeed { get; set; }

        /// <summary>
        /// Gets or sets the configured voltage.
        /// </summary>
        [JsonProperty("voltage")]
        public string Voltage { get; set; }

        /// <summary>
        /// Gets or sets the channel.
        /// </summary>
        [JsonProperty("channel")]
        public string Channel { get; set; }
    }
}
