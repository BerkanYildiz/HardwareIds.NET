﻿namespace HardwareIds.NET.Structures
{
    using System.Collections.Generic;
    using System.Linq;

    using global::HardwareIds.NET.Structures.Components;

    using Newtonsoft.Json;

    public class Hwid
    {
        /// <summary>
        /// The JSON serialization (and deserialization) settings.
        /// </summary>
        public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            DateFormatHandling          = DateFormatHandling.IsoDateFormat,
            DateTimeZoneHandling        = DateTimeZoneHandling.Utc,
            NullValueHandling           = NullValueHandling.Include,
            DefaultValueHandling        = DefaultValueHandling.Include,
            TypeNameHandling            = TypeNameHandling.Auto,
            PreserveReferencesHandling  = PreserveReferencesHandling.None,
            ReferenceLoopHandling       = ReferenceLoopHandling.Ignore,
        };

        /// <summary>
        /// Gets or sets the disks.
        /// </summary>
        [JsonProperty("disks")]
        public List<HwDisk> Disks
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the volumes.
        /// </summary>
        [JsonProperty("volumes")]
        public List<HwVolume> Volumes
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the network adapters.
        /// </summary>
        [JsonProperty("network_adapters")]
        public List<HwNetworkAdapter> NetworkAdapters
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the baseboards.
        /// </summary>
        [JsonProperty("baseboards")]
        public List<HwBaseboard> Baseboards
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the main baseboard.
        /// </summary>
        [JsonIgnore]
        public HwBaseboard Baseboard
        {
            get
            {
                return this.Baseboards?.FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets or sets the motherboards.
        /// </summary>
        [JsonProperty("motherboards")]
        public List<HwMotherboard> Motherboards
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the main motherboard.
        /// </summary>
        [JsonIgnore]
        public HwMotherboard Motherboard
        {
            get
            {
                return this.Motherboards?.FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets or sets the BIOS firmwares.
        /// </summary>
        [JsonProperty("bios_firmwares")]
        public List<HwBios> BiosFirmwares
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the main BIOS firmware.
        /// </summary>
        [JsonIgnore]
        public HwBios BiosFirmware
        {
            get
            {
                return this.BiosFirmwares?.FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets or sets the SMBIOS Table(s).
        /// </summary>
        [JsonProperty("smbios_tables")]
        public List<HwSmbios> SmbiosTables
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the main SMBIOS Table.
        /// </summary>
        [JsonIgnore]
        public HwSmbios SmbiosTable
        {
            get
            {
                return this.SmbiosTables?.FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets or sets the processors.
        /// </summary>
        [JsonProperty("processors")]
        public List<HwProcessor> Processors
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the main processor.
        /// </summary>
        [JsonIgnore]
        public HwProcessor Processor
        {
            get
            {
                return this.Processors?.FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets or sets the memory sticks.
        /// </summary>
        [JsonProperty("memory_sticks")]
        public List<HwMemoryStick> MemorySticks
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the monitors.
        /// </summary>
        [JsonProperty("monitors")]
        public List<HwMonitor> Monitors
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the main monitor.
        /// </summary>
        [JsonIgnore]
        public HwMonitor Monitor
        {
            get
            {
                return this.Monitors?.FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets or sets the video controllers.
        /// </summary>
        [JsonProperty("video_controllers")]
        public List<HwVideo> VideoControllers
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the main video controller.
        /// </summary>
        [JsonIgnore]
        public HwVideo VideoController
        {
            get
            {
                return this.VideoControllers?.FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets or sets the printers.
        /// </summary>
        [JsonProperty("printers")]
        public List<HwPrinter> Printers
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the main printers.
        /// </summary>
        [JsonIgnore]
        public HwPrinter Printer
        {
            get
            {
                return this.Printers?.FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets or sets the users.
        /// </summary>
        [JsonProperty("users")]
        public List<HwUser> Users
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the currently logged-in user.
        /// </summary>
        [JsonIgnore]
        public HwUser User
        {
            get
            {
                return this.Users?.FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets or sets the operating systems.
        /// </summary>
        [JsonProperty("operating_systems")]
        public List<HwOperatingSystem> OperatingSystems
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the main operating system.
        /// </summary>
        [JsonIgnore]
        public HwOperatingSystem OperatingSystem
        {
            get
            {
                return this.OperatingSystems?.FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets or sets the wifis.
        /// </summary>
        [JsonProperty("wifis")]
        public List<HwWifi> Wifis
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the main wifi.
        /// </summary>
        [JsonIgnore]
        public HwWifi Wifi
        {
            get
            {
                return this.Wifis?.FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets or sets the routers.
        /// </summary>
        [JsonProperty("routers")]
        public List<HwRouter> Routers
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the main router.
        /// </summary>
        [JsonIgnore]
        public HwRouter Router
        {
            get
            {
                return this.Routers?.FirstOrDefault();
            }
        }

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
        }
    }
}
