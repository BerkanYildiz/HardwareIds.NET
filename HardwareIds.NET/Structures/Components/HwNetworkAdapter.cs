namespace HardwareIds.NET.Structures.Components
{
    using System;
    using System.Text.Json.Serialization;

    public class HwNetworkAdapter
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the network interface identifier.
        /// </summary>
        [JsonPropertyName("interface_id")]
        public int InterfaceId { get; set; }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the interface guid.
        /// </summary>
        [JsonPropertyName("interface_guid")]
        public string InterfaceGuid { get; set; }

        /// <summary>
        /// Gets or sets the service name.
        /// </summary>
        [JsonPropertyName("service_name")]
        public string ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the MAC addresses.
        /// </summary>
        [JsonPropertyName("address")]
        public HwNetworkAddress Address { get; set; } = new HwNetworkAddress();

        /// <summary>
        /// Gets or sets a value indicating whether this network adapter is physical or not.
        /// </summary>
        [JsonPropertyName("is_physical")]
        public bool IsPhysical { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this network adapter is enabled or not.
        /// </summary>
        [JsonPropertyName("is_enabled")]
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the installation date of this network adapter.
        /// </summary>
        [JsonPropertyName("install_date")]
        public DateTime? InstallDate { get; set; }
    }

    public class HwNetworkAddress
    {
        /// <summary>
        /// Gets or sets the current MAC address.
        /// </summary>
        [JsonPropertyName("current")]
        public string Current { get; set; }

        /// <summary>
        /// Gets or sets the permanent MAC address.
        /// </summary>
        [JsonPropertyName("permanent")]
        public string Permanent { get; set; }
    }
}
