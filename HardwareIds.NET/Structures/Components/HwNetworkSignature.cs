namespace HardwareIds.NET.Structures.Components
{
    using Newtonsoft.Json;

    public class HwNetworkSignature
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonProperty("id")]
        public int Id
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the profile GUID.
        /// </summary>
        [JsonProperty("profile_guid")]
        public string ProfileGuid
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [JsonProperty("name")]
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the default gateway mac address.
        /// </summary>
        [JsonProperty("gateway_address")]
        public string DefaultGatewayMac
        {
            get;
            set;
        }
    }
}
