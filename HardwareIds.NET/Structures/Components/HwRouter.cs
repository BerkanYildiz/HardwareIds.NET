namespace HardwareIds.NET.Structures.Components
{
    using System.Collections.Generic;
    using System.Net;

    using Newtonsoft.Json;

    public class HwRouter
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonProperty("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the gateways.
        /// </summary>
        [JsonProperty("gateways")]
        public List<HwNetworkDevice> Gateways { get; set; } = new List<HwNetworkDevice>();

        /// <summary>
        /// Gets or sets the DNS servers.
        /// </summary>
        [JsonProperty("dns_servers")]
        public List<string> DnsServers { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the DHCP servers.
        /// </summary>
        [JsonProperty("dhcp_servers")]
        public List<string> DhcpServers { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the network devices.
        /// </summary>
        [JsonProperty("network_devices")]
        public List<HwNetworkDevice> NetworkDevices { get; set; } = new List<HwNetworkDevice>();
    }

    public class HwNetworkDevice
    {
        [JsonIgnore]
        public IPAddress Address { get; set; }

        [JsonProperty("mac_address")]
        public string MacAddress { get; set; }

        [JsonProperty("ip_address")]
        public string Ip
        {
            get => Address?.ToString();
            set => Address = IPAddress.Parse(value);
        }
    }
}
