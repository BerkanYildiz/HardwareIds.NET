namespace HardwareIds.NET.Structures.Components
{
    using System;
    using System.Text.Json.Serialization;

    public class HwOperatingSystem
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
        /// Gets or sets the version.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the architecture.
        /// </summary>
        [JsonPropertyName("architecture")]
        public string Architecture { get; set; }

        /// <summary>
        /// Gets or sets the registered user.
        /// </summary>
        [JsonPropertyName("registered_user")]
        public string RegisteredUser { get; set; }

        /// <summary>
        /// Gets or sets the serial number.
        /// </summary>
        [JsonPropertyName("serial_number")]
        public string SerialNumber { get; set; }

        /// <summary>
        /// Gets or sets the install date.
        /// </summary>
        [JsonPropertyName("install_date")]
        public DateTime InstallDate { get; set; }

        /// <summary>
        /// Gets or sets the last boot up time.
        /// </summary>
        [JsonPropertyName("last_boot_up_time")]
        public DateTime LastBootUpTime { get; set; }
    }
}
