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
        public int Id
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the gateways.
        /// </summary>
        [JsonProperty("gateways")]
        public List<NetworkDevice> Gateways
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the DNS servers.
        /// </summary>
        [JsonProperty("dns_servers")]
        public List<string> DnsServers
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the DHCP servers.
        /// </summary>
        [JsonProperty("dhcp_servers")]
        public List<string> DhcpServers
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the network devices.
        /// </summary>
        [JsonProperty("network_devices")]
        public List<NetworkDevice> NetworkDevices
        {
            get;
            set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HwRouter"/> class.
        /// </summary>
        public HwRouter()
        {
            this.Gateways = new List<NetworkDevice>();
            this.DnsServers = new List<string>();
            this.DhcpServers = new List<string>();
            this.NetworkDevices = new List<NetworkDevice>();
        }

        public class NetworkDevice
        {
            [JsonIgnore]
            public IPAddress Address
            {
                get;
                set;
            }

            [JsonProperty("ip_address")]
            public string Ip
            {
                get => Address?.ToString();
                set => Address = IPAddress.Parse(value);
            }

            [JsonProperty("mac_address")]
            public string MacAddress
            {
                get;
                set;
            }
        }
    }
}
