namespace HardwareIds.NET.Structures.Components
{
    using Newtonsoft.Json;

    public class HwVolume
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
        /// Gets or sets the path.
        /// </summary>
        [JsonProperty("path")]
        public string Path
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the letter.
        /// </summary>
        [JsonProperty("letter")]
        public string Letter
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the serial number.
        /// </summary>
        [JsonProperty("serial_number")]
        public uint SerialNumber
        {
            get;
            set;
        }
    }
}
