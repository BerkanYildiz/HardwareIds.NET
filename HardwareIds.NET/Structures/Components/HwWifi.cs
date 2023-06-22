namespace HardwareIds.NET.Structures.Components
{
    using Newtonsoft.Json;

    public class HwWifi
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonProperty("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [JsonProperty("ssid")]
        public string Ssid { get; set; }

        /// <summary>
        /// Gets or sets the MAC address.
        /// </summary>
        [JsonProperty("bssid")]
        public string Bssid { get; set; }

        /// <summary>
        /// Gets or sets the strength.
        /// </summary>
        public int Strength { get; set; }

        /// <summary>
        /// Gets or sets the channel.
        /// </summary>
        public int Channel { get; set; }

        /// <summary>
        /// Gets or sets the frequency.
        /// </summary>
        public int Frequency { get; set; }

        /// <summary>
        /// Gets or sets the band.
        /// </summary>
        public float Band { get; set; }

        /// <summary>
        /// Gets or sets the link quality.
        /// </summary>
        public int Quality { get; set; }
    }
}
