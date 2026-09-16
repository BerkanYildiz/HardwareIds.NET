namespace HardwareIds.NET.Structures.Components
{
    using System.Text.Json.Serialization;

    public class HwBluetoothRadio
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the Bluetooth address of the radio.
        /// </summary>
        [JsonPropertyName("address")]
        public string? Address { get; set; }

        /// <summary>
        /// Gets or sets the name of the radio.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the Bluetooth SIG company identifier of the manufacturer.
        /// </summary>
        [JsonPropertyName("manufacturer")]
        public int Manufacturer { get; set; }

        /// <summary>
        /// Gets or sets the class of device.
        /// </summary>
        [JsonPropertyName("class_of_device")]
        public uint ClassOfDevice { get; set; }

        /// <summary>
        /// Gets or sets the LMP subversion of the radio firmware.
        /// </summary>
        [JsonPropertyName("lmp_subversion")]
        public int LmpSubversion { get; set; }
    }
}
