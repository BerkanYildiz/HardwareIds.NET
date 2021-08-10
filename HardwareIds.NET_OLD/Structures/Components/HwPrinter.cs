namespace HardwareIds.NET.Structures.Components
{
    using Newtonsoft.Json;

    public class HwPrinter
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
        /// Gets or sets the name.
        /// </summary>
        [JsonProperty("name")]
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the port name.
        /// </summary>
        [JsonProperty("port_name")]
        public string PortName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the location.
        /// </summary>
        [JsonProperty("location")]
        public string Location
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the resolution width.
        /// </summary>
        [JsonProperty("width")]
        public uint Width
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the resolution height.
        /// </summary>
        [JsonProperty("height")]
        public uint Height
        {
            get;
            set;
        }
    }
}
