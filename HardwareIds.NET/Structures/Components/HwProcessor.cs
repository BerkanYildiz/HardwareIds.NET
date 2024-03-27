namespace HardwareIds.NET.Structures.Components
{
    using System.Text.Json.Serialization;

    public class HwProcessor
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
        /// Gets or sets the model.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; }

        /// <summary>
        /// Gets or sets the model number.
        /// </summary>
        [JsonPropertyName("model_number")]
        public string ModelNumber { get; set; }

        /// <summary>
        /// Gets or sets the socket.
        /// </summary>
        [JsonPropertyName("socket")]
        public string Socket { get; set; }

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

        /// <summary>
        /// Gets or sets the number of cores.
        /// </summary>
        [JsonPropertyName("number_of_cores")]
        public uint NumberOfCores { get; set; }

        /// <summary>
        /// Gets or sets the number of logical processors.
        /// </summary>
        [JsonPropertyName("number_of_logical_processors")]
        public uint NumberOfLogicalProcessors { get; set; }
    }
}
