namespace HardwareIds.NET.Structures.Components
{
    using System;
    using System.Text.Json.Serialization;

    public class HwBattery
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the device name.
        /// </summary>
        [JsonPropertyName("device_name")]
        public string? DeviceName { get; set; }

        /// <summary>
        /// Gets or sets the manufacturer.
        /// </summary>
        [JsonPropertyName("manufacturer")]
        public string? Manufacturer { get; set; }

        /// <summary>
        /// Gets or sets the serial number.
        /// </summary>
        [JsonPropertyName("serial_number")]
        public string? SerialNumber { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier reported by the battery.
        /// </summary>
        [JsonPropertyName("unique_id")]
        public string? UniqueId { get; set; }

        /// <summary>
        /// Gets or sets the chemistry (for example "LION").
        /// </summary>
        [JsonPropertyName("chemistry")]
        public string? Chemistry { get; set; }

        /// <summary>
        /// Gets or sets the designed capacity, in mWh.
        /// </summary>
        [JsonPropertyName("designed_capacity")]
        public uint DesignedCapacity { get; set; }

        /// <summary>
        /// Gets or sets the full charged capacity, in mWh.
        /// </summary>
        [JsonPropertyName("full_charged_capacity")]
        public uint FullChargedCapacity { get; set; }

        /// <summary>
        /// Gets or sets the manufacture date.
        /// </summary>
        [JsonPropertyName("manufacture_date")]
        public DateTime? ManufactureDate { get; set; }
    }
}
