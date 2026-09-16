namespace HardwareIds.NET.Structures.Components
{
    using System.Collections.Generic;
    using System.Net;
    using System.Text.Json.Serialization;

    public class HwRouter
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the gateways.
        /// </summary>
        [JsonPropertyName("gateways")]
        public List<HwNetworkDevice> Gateways { get; set; } = [];

        /// <summary>
        /// Gets or sets the DNS servers.
        /// </summary>
        [JsonPropertyName("dns_servers")]
        public List<string> DnsServers { get; set; } = [];

        /// <summary>
        /// Gets or sets the DHCP servers.
        /// </summary>
        [JsonPropertyName("dhcp_servers")]
        public List<string> DhcpServers { get; set; } = [];

        /// <summary>
        /// Gets or sets the network devices.
        /// </summary>
        [JsonPropertyName("network_devices")]
        public List<HwNetworkDevice> NetworkDevices { get; set; } = [];
    }

    public class HwNetworkDevice
    {
        /// <summary>
        /// Gets or sets the IP address.
        /// </summary>
        [JsonIgnore]
        public IPAddress? Address { get; set; }

        /// <summary>
        /// Gets or sets the MAC address.
        /// </summary>
        [JsonPropertyName("mac_address")]
        public string? MacAddress { get; set; }

        /// <summary>
        /// Gets or sets the IP address, as a string.
        /// </summary>
        [JsonPropertyName("ip_address")]
        public string? Ip
        {
            get => this.Address?.ToString();
            set => this.Address = value is null ? null : IPAddress.Parse(value);
        }
    }
}
