namespace HardwareIds.NET.Structures.Components
{
    using System.Text.Json.Serialization;

    public class HwNetworkSignature
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the profile GUID.
        /// </summary>
        [JsonPropertyName("profile_guid")]
        public string ProfileGuid { get; set; }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the default gateway mac address.
        /// </summary>
        [JsonPropertyName("gateway_address")]
        public string DefaultGatewayMac { get; set; }
    }
}
