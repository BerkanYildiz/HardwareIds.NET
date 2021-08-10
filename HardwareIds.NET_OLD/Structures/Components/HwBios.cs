namespace HardwareIds.NET.Structures.Components
{
    using Newtonsoft.Json;

    public class HwBios
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
        /// Gets or sets the manufacturer.
        /// </summary>
        [JsonProperty("manufacturer")]
        public string Manufacturer
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        [JsonProperty("version")]
        public string Version
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the serial number.
        /// </summary>
        [JsonProperty("serial_number")]
        public string SerialNumber
        {
            get;
            set;
        }
    }
}
