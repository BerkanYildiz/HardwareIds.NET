namespace HardwareIds.NET.Structures.Components
{
    using System.Text.Json.Serialization;

    public class HwDisk
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the interface.
        /// </summary>
        [JsonPropertyName("interface")]
        public string Interface { get; set; }

        /// <summary>
        /// Gets or sets the model.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; }

        /// <summary>
        /// Gets or sets the serial number.
        /// </summary>
        [JsonPropertyName("serial_number")]
        public string SerialNumber { get; set; }

        /// <summary>
        /// Gets or sets the capacity
        /// </summary>
        [JsonPropertyName("capacity")]
        public string Capacity { get; set; }

        /// <summary>
        /// Gets or sets the number of partitions.
        /// </summary>
        [JsonPropertyName("partitions")]
        public int Partitions { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this disk device is removable or not.
        /// </summary>
        [JsonPropertyName("is_removable")]
        public bool IsRemovable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this disk device supports SMART requests or not.
        /// </summary>
        [JsonPropertyName("is_smart")]
        public bool IsSMART { get; set; }
    }
}
