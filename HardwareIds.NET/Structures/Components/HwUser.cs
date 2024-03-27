namespace HardwareIds.NET.Structures.Components
{
    using System;
    using System.Text.Json.Serialization;

    public class HwUser
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        [JsonPropertyName("username")]
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the full name.
        /// </summary>
        [JsonPropertyName("full_name")]
        public string FullName { get; set; }

        /// <summary>
        /// Gets or sets the SID.
        /// </summary>
        [JsonPropertyName("sid")]
        public string SID { get; set; }

        /// <summary>
        /// Gets or sets the domain.
        /// </summary>
        [JsonPropertyName("domain")]
        public string Domain { get; set; }

        /// <summary>
        /// Gets or sets the install date.
        /// </summary>
        [JsonPropertyName("install_date")]
        public DateTime InstallDate { get; set; }
    }
}
