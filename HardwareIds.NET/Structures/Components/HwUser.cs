namespace HardwareIds.NET.Structures.Components
{
    using System;

    using Newtonsoft.Json;

    public class HwUser
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonProperty("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        [JsonProperty("username")]
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the full name.
        /// </summary>
        [JsonProperty("full_name")]
        public string FullName { get; set; }

        /// <summary>
        /// Gets or sets the SID.
        /// </summary>
        [JsonProperty("sid")]
        public string SID { get; set; }

        /// <summary>
        /// Gets or sets the domain.
        /// </summary>
        [JsonProperty("domain")]
        public string Domain { get; set; }

        /// <summary>
        /// Gets or sets the install date.
        /// </summary>
        [JsonProperty("install_date")]
        public DateTime InstallDate { get; set; }
    }
}
