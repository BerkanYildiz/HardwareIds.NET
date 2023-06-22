namespace HardwareIds.NET.Structures.Components
{
    using Newtonsoft.Json;

    public class HwDisk
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonProperty("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the interface.
        /// </summary>
        [JsonProperty("interface")]
        public string Interface { get; set; }

        /// <summary>
        /// Gets or sets the model.
        /// </summary>
        [JsonProperty("model")]
        public string Model { get; set; }

        /// <summary>
        /// Gets or sets the serial number.
        /// </summary>
        [JsonProperty("serial_number")]
        public string SerialNumber { get; set; }

        /// <summary>
        /// Gets or sets the capacity
        /// </summary>
        [JsonProperty("capacity")]
        public string Capacity { get; set; }

        /// <summary>
        /// Gets or sets the number of partitions.
        /// </summary>
        [JsonProperty("partitions")]
        public int Partitions { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this disk device is removable or not.
        /// </summary>
        [JsonProperty("is_removable")]
        public bool IsRemovable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this disk device supports SMART requests or not.
        /// </summary>
        [JsonProperty("is_smart")]
        public bool IsSMART { get; set; }
    }
}
