namespace HardwareIds.NET.Structures.Components
{
    using Newtonsoft.Json;

    public class HwNetworkAdapter
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
        /// Gets or sets the network interface identifier.
        /// </summary>
        [JsonProperty("interface_id")]
        public int InterfaceId
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
        /// Gets or sets the interface guid.
        /// </summary>
        [JsonProperty("interface_guid")]
        public string InterfaceGuid
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the service name.
        /// </summary>
        [JsonProperty("service_name")]
        public string ServiceName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the MAC addresses.
        /// </summary>
        [JsonProperty("address")]
        public HwNetworkAddress Address
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this network adapter is physical or not.
        /// </summary>
        [JsonProperty("is_physical")]
        public bool IsPhysical
        {
            get;
            set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HwNetworkAdapter"/> class.
        /// </summary>
        public HwNetworkAdapter()
        {
            this.Address = new HwNetworkAddress();
        }
    }

    public class HwNetworkAddress
    {
        /// <summary>
        /// Gets or sets the current MAC address.
        /// </summary>
        [JsonProperty("current")]
        public string Current
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the permanent MAC address.
        /// </summary>
        [JsonProperty("permanent")]
        public string Permanent
        {
            get;
            set;
        }
    }
}
