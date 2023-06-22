namespace HardwareIds.NET.Structures.Components
{
    using System;

    using Newtonsoft.Json;

    public class HwOperatingSystem
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
        /// Gets or sets the version.
        /// </summary>
        [JsonProperty("version")]
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the architecture.
        /// </summary>
        [JsonProperty("architecture")]
        public string Architecture { get; set; }

        /// <summary>
        /// Gets or sets the registered user.
        /// </summary>
        [JsonProperty("registered_user")]
        public string RegisteredUser { get; set; }

        /// <summary>
        /// Gets or sets the serial number.
        /// </summary>
        [JsonProperty("serial_number")]
        public string SerialNumber { get; set; }

        /// <summary>
        /// Gets or sets the install date.
        /// </summary>
        [JsonProperty("install_date")]
        public DateTime InstallDate { get; set; }

        /// <summary>
        /// Gets or sets the last boot up time.
        /// </summary>
        [JsonProperty("last_boot_up_time")]
        public DateTime LastBootUpTime { get; set; }
    }
}
