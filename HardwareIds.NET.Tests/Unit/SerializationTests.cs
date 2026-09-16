namespace HardwareIds.NET.Tests.Unit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text.Json;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using Xunit;

    /// <summary>
    /// Locks the JSON wire format, since consumers persist and compare serialized scans.
    /// </summary>
    public class SerializationTests
    {
        private static readonly Dictionary<string, string[]> ExpectedProperties = new()
        {
            ["disks"] = ["id", "interface", "model", "serial_number", "capacity", "partitions", "is_removable", "is_smart", "firmware", "world_wide_name", "disk_guid", "instance_id", "nvme_serial", "nvme_eui64", "nvme_nguid", "nvme_fguid", "ata_serial", "ata_wwn", "vpd_t10", "vpd_eui64", "vpd_nguid", "vpd_naa", "vpd_scsi_name", "vpd_vendor", "duid"],
            ["volumes"] = ["id", "path", "letter", "serial_number"],
            ["network_adapters"] = ["id", "interface_id", "name", "interface_guid", "service_name", "address", "is_physical", "is_enabled", "install_date", "instance_id"],
            ["bluetooth_radios"] = ["id", "address", "name", "manufacturer", "class_of_device", "lmp_subversion"],
            ["baseboards"] = ["id", "manufacturer", "model", "version", "serial_number", "part_number"],
            ["motherboards"] = ["id", "name", "vendor", "version", "UUID"],
            ["chassis"] = ["id", "manufacturer", "type", "type_name", "version", "serial_number", "asset_tag"],
            ["bios_firmwares"] = ["id", "manufacturer", "version", "serial_number"],
            ["smbios_tables"] = ["id", "version", "hash", "length"],
            ["processors"] = ["id", "manufacturer", "model", "model_number", "socket", "part_number", "serial_number", "clock_speed", "voltage", "channel", "number_of_cores", "number_of_logical_processors"],
            ["memory_sticks"] = ["id", "manufacturer", "part_number", "serial_number", "capacity_in_gb", "clock_speed", "voltage", "channel"],
            ["batteries"] = ["id", "device_name", "manufacturer", "serial_number", "unique_id", "chemistry", "designed_capacity", "full_charged_capacity", "manufacture_date"],
            ["monitors"] = ["id", "manufacturer", "name", "product", "serial_number", "instance_id", "edid_hash", "manufacture_week", "manufacture_year"],
            ["video_controllers"] = ["id", "name", "width", "height", "refresh_rate", "driver_date", "driver_version", "instance_id"],
            ["printers"] = ["id", "name", "port_name", "location", "width", "height"],
            ["users"] = ["id", "username", "full_name", "sid", "domain", "install_date"],
            ["operating_systems"] = ["id", "name", "version", "architecture", "registered_user", "serial_number", "install_date", "last_boot_up_time", "machine_guid", "sqm_machine_id", "hardware_profile_guid", "install_time", "machine_sid"],
            ["wifis"] = ["id", "ssid", "bssid", "Strength", "Channel", "Frequency", "Band", "Quality"],
            ["routers"] = ["id", "gateways", "dns_servers", "dhcp_servers", "network_devices"],
            ["network_signatures"] = ["id", "profile_guid", "name", "gateway_address"],
        };

        private static Hwid BuildPopulatedHwid()
        {
            var Hwid = new Hwid();
            Hwid.Disks.Add(new HwDisk { Id = 0, Interface = "SCSI", Model = "Disk", SerialNumber = "SN", Capacity = "1 GB", Partitions = 1, IsRemovable = false, IsSMART = true, Firmware = "1.0", WorldWideName = "5002538E41234567", DiskGuid = "34367483-8f7f-4160-83a9-f0d033bab3b3", InstanceId = @"SCSI\DISK&VEN_X&PROD_Y\1&2&0&0", NvmeSerial = "S4EWNX0N123456K", NvmeEui64 = "002538B71C9B5B4E", NvmeNguid = null, NvmeFguid = null, AtaSerial = null, AtaWwn = null, VpdT10 = null, VpdEui64 = null, VpdNguid = null, VpdNaa = "5002538E41234567", VpdScsiName = "eui.002538B71C9B5B4E", VpdVendor = null, Duid = new string('c', 64) });
            Hwid.Volumes.Add(new HwVolume { Id = 0, Path = @"\\?\Volume{00000000-0000-0000-0000-000000000000}\", Letter = "C:", SerialNumber = 1 });
            Hwid.NetworkAdapters.Add(new HwNetworkAdapter { Id = 1, InterfaceId = 2, Name = "NIC", InterfaceGuid = "{00000000-0000-0000-0000-000000000000}", ServiceName = "svc", IsPhysical = true, IsEnabled = true, InstallDate = new DateTime(2026, 1, 1), InstanceId = @"PCI\VEN_1AF4&DEV_1000\1&2&0&0", Address = { Current = "00:11:22:33:44:55", Permanent = "00:11:22:33:44:55" } });
            Hwid.BluetoothRadios.Add(new HwBluetoothRadio { Id = 0, Address = "00:1A:7D:DA:71:13", Name = "DESKTOP", Manufacturer = 10, ClassOfDevice = 0x1F0000, LmpSubversion = 0x2100 });
            Hwid.Baseboards.Add(new HwBaseboard { Id = 0, Manufacturer = "M", Model = "P", Version = "V", SerialNumber = "S", PartNumber = null });
            Hwid.Motherboards.Add(new HwMotherboard { Id = 0, Name = "N", Vendor = "V", Version = "1", UUID = Guid.NewGuid() });
            Hwid.Chassis.Add(new HwChassis { Id = 0, Manufacturer = "M", Type = 3, TypeName = "Desktop", Version = "V", SerialNumber = "S", AssetTag = "A" });
            Hwid.BiosFirmwares.Add(new HwBios { Id = 0, Manufacturer = "M", Version = "V", SerialNumber = "S" });
            Hwid.SmbiosTables.Add(new HwSmbios { Id = 0, Version = "3.4.0", Hash = new string('a', 64), Length = 10 });
            Hwid.Processors.Add(new HwProcessor { Id = 0, Manufacturer = "M", Model = "CPU", ModelNumber = "BFEBFBFF000906EA", Socket = "S", PartNumber = "P", SerialNumber = "S", ClockSpeed = "1 MHz", Voltage = "1.1 V", Channel = "CPU0", NumberOfCores = 4, NumberOfLogicalProcessors = 8 });
            Hwid.MemorySticks.Add(new HwMemoryStick { Id = 0, Manufacturer = "M", PartNumber = "P", SerialNumber = "S", Capacity = "16 GB", ClockSpeed = "3200 MHz", Voltage = "1.20 V", Channel = "DIMM_A1" });
            Hwid.Batteries.Add(new HwBattery { Id = 0, DeviceName = "DELL 1234", Manufacturer = "SMP", SerialNumber = "1234", UniqueId = "1234SMPDELL 1234", Chemistry = "LION", DesignedCapacity = 56000, FullChargedCapacity = 50000, ManufactureDate = new DateTime(2023, 6, 15) });
            Hwid.Monitors.Add(new HwMonitor { Id = 0, Manufacturer = "DEL", Name = "N", Product = "4070", SerialNumber = "S", InstanceId = @"DISPLAY\DEL4070\5&1&0&UID0", EdidHash = new string('b', 64), ManufactureWeek = 12, ManufactureYear = 2020 });
            Hwid.VideoControllers.Add(new HwVideo { Id = 0, Name = "GPU", Width = 1920, Height = 1080, RefreshRate = 60, DriverDate = new DateTime(2026, 1, 1), DriverVersion = "1.0", InstanceId = @"PCI\VEN_10DE&DEV_2206\4&1&0&0008" });
            Hwid.Printers.Add(new HwPrinter { Id = 0, Name = "P", PortName = "PORT", Location = "", Width = 600, Height = 600 });
            Hwid.Users.Add(new HwUser { Id = 0, Username = "U", FullName = "F", SID = "S-1-5-21-1-2-3-1001", Domain = "D", InstallDate = default });
            Hwid.OperatingSystems.Add(new HwOperatingSystem { Id = 0, Name = "OS", Version = "10.0.26200", Architecture = "64-bit", RegisteredUser = "R", SerialNumber = "S", InstallDate = new DateTime(2024, 11, 11), LastBootUpTime = new DateTime(2026, 9, 15), MachineGuid = "78091cf1-42d6-49ff-94c1-9801ef1d86ab", SqmMachineId = "{BF3FCD95-BFFD-4541-863B-EED0F3F8A060}", HardwareProfileGuid = "{01835c06-61d4-4b4c-ae2e-94ad1b5f7e31}", InstallTime = new DateTime(2024, 11, 11, 12, 8, 59), MachineSid = "S-1-5-21-1-2-3" });
            Hwid.Wifis.Add(new HwWifi { Id = 0, Ssid = "S", Bssid = "00:11:22:33:44:55", Strength = -50, Channel = 6, Frequency = 2437000, Band = 2.4f, Quality = 90 });
            Hwid.Routers.Add(new HwRouter { Id = 0, Gateways = [new HwNetworkDevice { Address = IPAddress.Parse("192.168.1.1"), MacAddress = "00:11:22:33:44:55" }], DnsServers = ["1.1.1.1"], DhcpServers = ["192.168.1.1"], NetworkDevices = [new HwNetworkDevice { Address = IPAddress.Parse("192.168.1.2"), MacAddress = "00:11:22:33:44:66" }] });
            Hwid.NetworkSignatures.Add(new HwNetworkSignature { Id = 0, ProfileGuid = "{00000000-0000-0000-0000-000000000000}", Name = "Network", DefaultGatewayMac = "00:11:22:33:44:55" });
            return Hwid;
        }

        [Fact]
        public void Serialize_UsesTheDocumentedPropertyNames()
        {
            using var Document = JsonDocument.Parse(JsonSerializer.Serialize(BuildPopulatedHwid()));
            var Root = Document.RootElement;

            Assert.Equal(ExpectedProperties.Keys.OrderBy(T => T), Root.EnumerateObject().Select(T => T.Name).OrderBy(T => T));

            foreach (var Expected in ExpectedProperties)
            {
                var Element = Root.GetProperty(Expected.Key)[0];
                Assert.Equal(Expected.Value, Element.EnumerateObject().Select(T => T.Name));
            }

            var Address = Root.GetProperty("network_adapters")[0].GetProperty("address");
            Assert.Equal(["current", "permanent"], Address.EnumerateObject().Select(T => T.Name));

            var Disk = Root.GetProperty("disks")[0];
            Assert.Equal("S4EWNX0N123456K", Disk.GetProperty("nvme_serial").GetString());
            Assert.Equal(JsonValueKind.Null, Disk.GetProperty("ata_serial").ValueKind);

            var Device = Root.GetProperty("routers")[0].GetProperty("gateways")[0];
            Assert.Equal(["mac_address", "ip_address"], Device.EnumerateObject().Select(T => T.Name));
            Assert.Equal("192.168.1.1", Device.GetProperty("ip_address").GetString());
        }

        [Fact]
        public void Serialize_KeepsThePreviousPropertyNamesFirst()
        {
            //
            // Fields added in 2.0 come after the historical ones, so consumers reading positional output keep working.
            //

            using var Document = JsonDocument.Parse(JsonSerializer.Serialize(BuildPopulatedHwid()));
            var Disk = Document.RootElement.GetProperty("disks")[0].EnumerateObject().Select(T => T.Name).ToList();

            Assert.Equal(["id", "interface", "model", "serial_number", "capacity", "partitions", "is_removable", "is_smart"], Disk.Take(8));
        }

        [Fact]
        public void Serialize_IgnoresConvenienceGetters()
        {
            var Json = JsonSerializer.Serialize(BuildPopulatedHwid());

            foreach (var Name in new[] { "\"baseboard\"", "\"motherboard\"", "\"main_chassis\"", "\"MainChassis\"", "\"bios_firmware\"", "\"smbios_table\"", "\"processor\"", "\"battery\"", "\"Battery\"", "\"bluetooth_radio\"", "\"BluetoothRadio\"", "\"monitor\"", "\"video_controller\"", "\"printer\"", "\"user\"", "\"operating_system\"", "\"wifi\"", "\"router\"", "\"Baseboard\"", "\"Processor\"" })
                Assert.DoesNotContain(Name, Json);
        }

        [Fact]
        public void Serialize_RoundTripsThroughDeserialization()
        {
            var Json = JsonSerializer.Serialize(BuildPopulatedHwid());
            var Hwid = JsonSerializer.Deserialize<Hwid>(Json);

            Assert.NotNull(Hwid);
            Assert.Equal(Json, JsonSerializer.Serialize(Hwid));
            Assert.Equal("192.168.1.2", Hwid.Routers[0].NetworkDevices[0].Ip);
            Assert.Equal("S4EWNX0N123456K", Hwid.Disks[0].NvmeSerial);
        }

        [Fact]
        public void Deserialize_AcceptsScansFromVersion1()
        {
            //
            // A 1.x document lacks every field added since; it must still load, with the new fields left empty.
            //

            var Hwid = JsonSerializer.Deserialize<Hwid>("{\"disks\":[{\"id\":0,\"interface\":\"SCSI\",\"model\":\"Disk\",\"serial_number\":\"SN\",\"capacity\":\"1 GB\",\"partitions\":1,\"is_removable\":false,\"is_smart\":false}],\"operating_systems\":[{\"id\":0,\"name\":\"OS\"}]}");

            Assert.NotNull(Hwid);
            Assert.Equal("SN", Hwid.Disks[0].SerialNumber);
            Assert.Null(Hwid.Disks[0].NvmeSerial);
            Assert.Null(Hwid.Disks[0].Duid);
            Assert.Null(Hwid.Disks[0].DiskGuid);
            Assert.Null(Hwid.OperatingSystems[0].MachineGuid);
            Assert.Empty(Hwid.Chassis);
            Assert.Empty(Hwid.Batteries);
            Assert.Empty(Hwid.BluetoothRadios);
        }

        [Fact]
        public void Deserialize_ToleratesNullIpAddresses()
        {
            var Hwid = JsonSerializer.Deserialize<Hwid>("{\"routers\":[{\"id\":0,\"gateways\":[{\"mac_address\":null,\"ip_address\":null}]}]}");

            Assert.NotNull(Hwid);
            Assert.Null(Hwid.Routers[0].Gateways[0].Address);
        }
    }
}
