namespace HardwareIds.NET.Structures.Components
{
    using System;
    using System.Text.Json.Serialization;

    public class HwVideo
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
        /// Gets or sets the resolution width.
        /// </summary>
        [JsonPropertyName("width")]
        public uint Width { get; set; }

        /// <summary>
        /// Gets or sets the resolution height.
        /// </summary>
        [JsonPropertyName("height")]
        public uint Height { get; set; }

        /// <summary>
        /// Gets or sets the refresh rate.
        /// </summary>
        [JsonPropertyName("refresh_rate")]
        public uint RefreshRate { get; set; }

        /// <summary>
        /// Gets or sets the driver date.
        /// </summary>
        [JsonPropertyName("driver_date")]
        public DateTime DriverDate { get; set; }

        /// <summary>
        /// Gets or sets the driver version.
        /// </summary>
        [JsonPropertyName("driver_version")]
        public string DriverVersion { get; set; }
    }
}
