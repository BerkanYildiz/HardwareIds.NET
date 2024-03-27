namespace HardwareIds.NET.Structures
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Serialization;

    using global::HardwareIds.NET.Structures.Components;

    public class Hwid
    {
        /// <summary>
        /// Gets or sets the disks.
        /// </summary>
        [JsonPropertyName("disks")]
        public List<HwDisk> Disks { get; set; }

        /// <summary>
        /// Gets or sets the volumes.
        /// </summary>
        [JsonPropertyName("volumes")]
        public List<HwVolume> Volumes { get; set; }

        /// <summary>
        /// Gets or sets the network adapters.
        /// </summary>
        [JsonPropertyName("network_adapters")]
        public List<HwNetworkAdapter> NetworkAdapters { get; set; }

        /// <summary>
        /// Gets or sets the baseboards.
        /// </summary>
        [JsonPropertyName("baseboards")]
        public List<HwBaseboard> Baseboards { get; set; }

        /// <summary>
        /// Gets the main baseboard.
        /// </summary>
        [JsonIgnore]
        public HwBaseboard Baseboard => this.Baseboards?.FirstOrDefault();

        /// <summary>
        /// Gets or sets the motherboards.
        /// </summary>
        [JsonPropertyName("motherboards")]
        public List<HwMotherboard> Motherboards { get; set; }

        /// <summary>
        /// Gets the main motherboard.
        /// </summary>
        [JsonIgnore]
        public HwMotherboard Motherboard => this.Motherboards?.FirstOrDefault();

        /// <summary>
        /// Gets or sets the BIOS firmwares.
        /// </summary>
        [JsonPropertyName("bios_firmwares")]
        public List<HwBios> BiosFirmwares { get; set; }

        /// <summary>
        /// Gets the main BIOS firmware.
        /// </summary>
        [JsonIgnore]
        public HwBios BiosFirmware => this.BiosFirmwares?.FirstOrDefault();

        /// <summary>
        /// Gets or sets the SMBIOS Table(s).
        /// </summary>
        [JsonPropertyName("smbios_tables")]
        public List<HwSmbios> SmbiosTables { get; set; }

        /// <summary>
        /// Gets the main SMBIOS Table.
        /// </summary>
        [JsonIgnore]
        public HwSmbios SmbiosTable => this.SmbiosTables?.FirstOrDefault();

        /// <summary>
        /// Gets or sets the processors.
        /// </summary>
        [JsonPropertyName("processors")]
        public List<HwProcessor> Processors { get; set; }

        /// <summary>
        /// Gets the main processor.
        /// </summary>
        [JsonIgnore]
        public HwProcessor Processor => this.Processors?.FirstOrDefault();

        /// <summary>
        /// Gets or sets the memory sticks.
        /// </summary>
        [JsonPropertyName("memory_sticks")]
        public List<HwMemoryStick> MemorySticks { get; set; }

        /// <summary>
        /// Gets or sets the monitors.
        /// </summary>
        [JsonPropertyName("monitors")]
        public List<HwMonitor> Monitors { get; set; }

        /// <summary>
        /// Gets the main monitor.
        /// </summary>
        [JsonIgnore]
        public HwMonitor Monitor => this.Monitors?.FirstOrDefault();

        /// <summary>
        /// Gets or sets the video controllers.
        /// </summary>
        [JsonPropertyName("video_controllers")]
        public List<HwVideo> VideoControllers { get; set; }

        /// <summary>
        /// Gets the main video controller.
        /// </summary>
        [JsonIgnore]
        public HwVideo VideoController => this.VideoControllers?.FirstOrDefault();

        /// <summary>
        /// Gets or sets the printers.
        /// </summary>
        [JsonPropertyName("printers")]
        public List<HwPrinter> Printers { get; set; }

        /// <summary>
        /// Gets the main printers.
        /// </summary>
        [JsonIgnore]
        public HwPrinter Printer => this.Printers?.FirstOrDefault();

        /// <summary>
        /// Gets or sets the users.
        /// </summary>
        [JsonPropertyName("users")]
        public List<HwUser> Users { get; set; }

        /// <summary>
        /// Gets the currently logged-in user.
        /// </summary>
        [JsonIgnore]
        public HwUser User => this.Users?.FirstOrDefault();

        /// <summary>
        /// Gets or sets the operating systems.
        /// </summary>
        [JsonPropertyName("operating_systems")]
        public List<HwOperatingSystem> OperatingSystems { get; set; }

        /// <summary>
        /// Gets the main operating system.
        /// </summary>
        [JsonIgnore]
        public HwOperatingSystem OperatingSystem => this.OperatingSystems?.FirstOrDefault();

        /// <summary>
        /// Gets or sets the wifis.
        /// </summary>
        [JsonPropertyName("wifis")]
        public List<HwWifi> Wifis { get; set; }

        /// <summary>
        /// Gets the main wifi.
        /// </summary>
        [JsonIgnore]
        public HwWifi Wifi => this.Wifis?.FirstOrDefault();

        /// <summary>
        /// Gets or sets the routers.
        /// </summary>
        [JsonPropertyName("routers")]
        public List<HwRouter> Routers { get; set; }

        /// <summary>
        /// Gets the main router.
        /// </summary>
        [JsonIgnore]
        public HwRouter Router => this.Routers?.FirstOrDefault();

        /// <summary>
        /// Gets or sets the network signatures.
        /// </summary>
        [JsonPropertyName("network_signatures")]
        public List<HwNetworkSignature> NetworkSignatures { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Hwid"/> class.
        /// </summary>
        public Hwid()
        {
            this.Disks = new List<HwDisk>();
            this.Volumes = new List<HwVolume>();
            this.NetworkAdapters = new List<HwNetworkAdapter>();
            this.Baseboards = new List<HwBaseboard>();
            this.Motherboards = new List<HwMotherboard>();
            this.BiosFirmwares = new List<HwBios>();
            this.SmbiosTables = new List<HwSmbios>();
            this.Processors = new List<HwProcessor>();
            this.MemorySticks = new List<HwMemoryStick>();
            this.Monitors = new List<HwMonitor>();
            this.VideoControllers = new List<HwVideo>();
            this.Printers = new List<HwPrinter>();
            this.Users = new List<HwUser>();
            this.OperatingSystems = new List<HwOperatingSystem>();
            this.Wifis = new List<HwWifi>();
            this.Routers = new List<HwRouter>();
            this.NetworkSignatures = new List<HwNetworkSignature>();
        }
    }
}
