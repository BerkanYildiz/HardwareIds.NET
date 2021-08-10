namespace HardwareIds.NET.Structures.Components
{
    using Newtonsoft.Json;

    public class HwProcessor
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
        /// Gets or sets the model.
        /// </summary>
        [JsonProperty("model")]
        public string Model
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the model number.
        /// </summary>
        [JsonProperty("model_number")]
        public string ModelNumber
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the socket.
        /// </summary>
        [JsonProperty("socket")]
        public string Socket
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the part number.
        /// </summary>
        [JsonProperty("part_number")]
        public string PartNumber
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

        /// <summary>
        /// Gets or sets the configured clock speed.
        /// </summary>
        [JsonProperty("clock_speed")]
        public string ClockSpeed
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the configured voltage.
        /// </summary>
        [JsonProperty("voltage")]
        public string Voltage
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the channel.
        /// </summary>
        [JsonProperty("channel")]
        public string Channel
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the number of cores.
        /// </summary>
        [JsonProperty("number_of_cores")]
        public uint NumberOfCores
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the number of logical processors.
        /// </summary>
        [JsonProperty("number_of_logical_processors")]
        public uint NumberOfLogicalProcessors
        {
            get;
            set;
        }
    }
}
