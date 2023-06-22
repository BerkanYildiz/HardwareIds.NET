namespace HardwareIds.NET.Structures.Components
{
    using System;

    using Newtonsoft.Json;

    public class HwVideo
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonProperty("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the resolution width.
        /// </summary>
        [JsonProperty("width")]
        public uint Width { get; set; }

        /// <summary>
        /// Gets or sets the resolution height.
        /// </summary>
        [JsonProperty("height")]
        public uint Height { get; set; }

        /// <summary>
        /// Gets or sets the refresh rate.
        /// </summary>
        [JsonProperty("refresh_rate")]
        public uint RefreshRate { get; set; }

        /// <summary>
        /// Gets or sets the driver date.
        /// </summary>
        [JsonProperty("driver_date")]
        public DateTime DriverDate { get; set; }

        /// <summary>
        /// Gets or sets the driver version.
        /// </summary>
        [JsonProperty("driver_version")]
        public string DriverVersion { get; set; }
    }
}
