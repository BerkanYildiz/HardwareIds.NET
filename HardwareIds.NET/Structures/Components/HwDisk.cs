namespace HardwareIds.NET.Structures.Components
{
    using System.Text.Json.Serialization;

    public class HwDisk
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the interface.
        /// </summary>
        [JsonPropertyName("interface")]
        public string? Interface { get; set; }

        /// <summary>
        /// Gets or sets the model.
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        /// <summary>
        /// Gets or sets the serial number, as reported by the storage device descriptor (the same value WMI reports).
        /// </summary>
        [JsonPropertyName("serial_number")]
        public string? SerialNumber { get; set; }

        /// <summary>
        /// Gets or sets the capacity
        /// </summary>
        [JsonPropertyName("capacity")]
        public string? Capacity { get; set; }

        /// <summary>
        /// Gets or sets the number of partitions.
        /// </summary>
        [JsonPropertyName("partitions")]
        public int Partitions { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this disk device is removable or not.
        /// </summary>
        [JsonPropertyName("is_removable")]
        public bool IsRemovable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this disk device supports SMART requests or not.
        /// </summary>
        [JsonPropertyName("is_smart")]
        public bool IsSMART { get; set; }

        /// <summary>
        /// Gets or sets the firmware revision.
        /// </summary>
        [JsonPropertyName("firmware")]
        public string? Firmware { get; set; }

        /// <summary>
        /// Gets or sets the World Wide Name (NAA or EUI-64) of the disk, when it has one.
        /// </summary>
        [JsonPropertyName("world_wide_name")]
        public string? WorldWideName { get; set; }

        /// <summary>
        /// Gets or sets the GPT disk GUID, or the MBR disk signature, of the partition table.
        /// </summary>
        [JsonPropertyName("disk_guid")]
        public string? DiskGuid { get; set; }

        /// <summary>
        /// Gets or sets the PnP device instance identifier.
        /// </summary>
        [JsonPropertyName("instance_id")]
        public string? InstanceId { get; set; }

        /// <summary>
        /// Gets or sets the serial number reported by NVMe Identify Controller.
        /// </summary>
        [JsonPropertyName("nvme_serial")]
        public string? NvmeSerial { get; set; }

        /// <summary>
        /// Gets or sets the IEEE EUI-64 of the first NVMe namespace.
        /// </summary>
        [JsonPropertyName("nvme_eui64")]
        public string? NvmeEui64 { get; set; }

        /// <summary>
        /// Gets or sets the NGUID of the first NVMe namespace.
        /// </summary>
        [JsonPropertyName("nvme_nguid")]
        public string? NvmeNguid { get; set; }

        /// <summary>
        /// Gets or sets the FRU globally unique identifier of the NVMe controller.
        /// </summary>
        [JsonPropertyName("nvme_fguid")]
        public string? NvmeFguid { get; set; }

        /// <summary>
        /// Gets or sets the serial number reported by ATA IDENTIFY DEVICE.
        /// </summary>
        [JsonPropertyName("ata_serial")]
        public string? AtaSerial { get; set; }

        /// <summary>
        /// Gets or sets the World Wide Name reported by ATA IDENTIFY DEVICE.
        /// </summary>
        [JsonPropertyName("ata_wwn")]
        public string? AtaWwn { get; set; }

        /// <summary>
        /// Gets or sets the T10 vendor identifier of the SCSI device identification page.
        /// </summary>
        [JsonPropertyName("vpd_t10")]
        public string? VpdT10 { get; set; }

        /// <summary>
        /// Gets or sets the EUI-64 identifier of the SCSI device identification page.
        /// </summary>
        [JsonPropertyName("vpd_eui64")]
        public string? VpdEui64 { get; set; }

        /// <summary>
        /// Gets or sets the 16-byte EUI (NGUID) identifier of the SCSI device identification page.
        /// </summary>
        [JsonPropertyName("vpd_nguid")]
        public string? VpdNguid { get; set; }

        /// <summary>
        /// Gets or sets the NAA identifier of the SCSI device identification page (Windows' translation of the World Wide Name).
        /// </summary>
        [JsonPropertyName("vpd_naa")]
        public string? VpdNaa { get; set; }

        /// <summary>
        /// Gets or sets the SCSI name string of the SCSI device identification page.
        /// </summary>
        [JsonPropertyName("vpd_scsi_name")]
        public string? VpdScsiName { get; set; }

        /// <summary>
        /// Gets or sets the vendor-specific identifier of the SCSI device identification page.
        /// </summary>
        [JsonPropertyName("vpd_vendor")]
        public string? VpdVendor { get; set; }

        /// <summary>
        /// Gets or sets the SHA-256 hash of the unique identifier (DUID) Windows computes for the disk.
        /// </summary>
        [JsonPropertyName("duid")]
        public string? Duid { get; set; }
    }
}
