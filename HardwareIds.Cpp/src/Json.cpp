#include "Internal.hpp"

#include <charconv>

namespace HardwareIds
{
    using namespace Detail;

    namespace
    {
        //
        // Writes JSON the way System.Text.Json does with its default options: properties in declaration order, strings escaped
        // with the default JavaScriptEncoder (everything outside printable ASCII, plus the HTML-sensitive characters, as \uXXXX),
        // and, when indented, two spaces per level with CRLF line breaks.
        //

        class JsonWriter
        {
        public:
            explicit JsonWriter(bool InIndented) : Indented(InIndented) {}

            std::string Output;

            void BeginObject() { this->BeginValue(); this->Output += '{'; this->Containers.push_back(false); }
            void EndObject() { this->EndContainer('}'); }
            void BeginArray() { this->BeginValue(); this->Output += '['; this->Containers.push_back(false); }
            void EndArray() { this->EndContainer(']'); }

            void Name(const char* InName)
            {
                this->Separate();
                this->Output += '"';
                this->Output += InName;
                this->Output += this->Indented ? "\": " : "\":";
                this->AfterName = true;
            }

            void Null() { this->BeginValue(); this->Output += "null"; }
            void Bool(bool InValue) { this->BeginValue(); this->Output += InValue ? "true" : "false"; }
            void Int(std::int64_t InValue) { this->BeginValue(); this->Output += std::to_string(InValue); }
            void UInt(std::uint64_t InValue) { this->BeginValue(); this->Output += std::to_string(InValue); }

            void Float(float InValue)
            {
                this->BeginValue();

                // The shortest representation that round-trips, like .NET ("2.4", "5", "1E+20").
                char Buffer[32];
                auto Result = std::to_chars(Buffer, Buffer + sizeof(Buffer), InValue);

                for (auto Character = Buffer; Character != Result.ptr; Character++)
                    this->Output += *Character == 'e' ? 'E' : *Character;
            }

            void String(std::wstring_view InValue)
            {
                this->BeginValue();
                this->Output += '"';

                for (std::size_t I = 0; I < InValue.size(); I++)
                {
                    auto Character = static_cast<std::uint32_t>(InValue[I]);

                    switch (Character)
                    {
                        case '\b': this->Output += '\\'; this->Output += 'b'; continue;
                        case '\t': this->Output += '\\'; this->Output += 't'; continue;
                        case '\n': this->Output += '\\'; this->Output += 'n'; continue;
                        case '\f': this->Output += '\\'; this->Output += 'f'; continue;
                        case '\r': this->Output += '\\'; this->Output += 'r'; continue;
                        case '\\': this->Output += '\\'; this->Output += '\\'; continue;
                        case '"': case '&': case '\'': case '+': case '<': case '>': case '`':
                            this->Escape(Character);
                            continue;
                    }

                    if (Character >= 0x20 && Character < 0x7F)
                    {
                        this->Output += static_cast<char>(Character);
                    }
                    else if (Character >= 0xD800 && Character <= 0xDBFF && I + 1 < InValue.size() && InValue[I + 1] >= 0xDC00 && InValue[I + 1] <= 0xDFFF)
                    {
                        // A valid surrogate pair is written as two escapes.
                        this->Escape(Character);
                        this->Escape(InValue[++I]);
                    }
                    else if (Character >= 0xD800 && Character <= 0xDFFF)
                    {
                        // A lone surrogate is replaced, like .NET does.
                        this->Escape(0xFFFD);
                    }
                    else
                    {
                        this->Escape(Character);
                    }
                }

                this->Output += '"';
            }

            void String(const NullableString& InValue)
            {
                if (InValue)
                    this->String(std::wstring_view(*InValue));
                else
                    this->Null();
            }

            void Date(const DateTime& InValue)
            {
                this->BeginValue();
                this->Output += '"';
                this->Output += FormatDateTime(InValue);
                this->Output += '"';
            }

            void Date(const std::optional<DateTime>& InValue)
            {
                if (InValue)
                    this->Date(*InValue);
                else
                    this->Null();
            }

        private:
            bool Indented;
            bool AfterName = false;
            std::vector<bool> Containers;   // Whether each open container already holds a value.

            void Escape(std::uint32_t InCharacter)
            {
                static constexpr char Digits[] = "0123456789ABCDEF";
                this->Output += '\\';
                this->Output += 'u';
                this->Output += Digits[(InCharacter >> 12) & 0xF];
                this->Output += Digits[(InCharacter >> 8) & 0xF];
                this->Output += Digits[(InCharacter >> 4) & 0xF];
                this->Output += Digits[InCharacter & 0xF];
            }

            void NewLine(std::size_t InDepth)
            {
                this->Output += "\r\n";
                this->Output.append(InDepth * 2, ' ');
            }

            void Separate()
            {
                if (this->Containers.empty())
                    return;

                if (this->Containers.back())
                    this->Output += ',';

                if (this->Indented)
                    this->NewLine(this->Containers.size());

                this->Containers.back() = true;
            }

            void BeginValue()
            {
                if (this->AfterName)
                    this->AfterName = false;
                else
                    this->Separate();
            }

            void EndContainer(char InClosing)
            {
                // A bool, not auto: the vector<bool> proxy would read a popped bit.
                bool HasValues = this->Containers.back();
                this->Containers.pop_back();

                if (this->Indented && HasValues)
                    this->NewLine(this->Containers.size());

                this->Output += InClosing;
            }
        };

        template <typename TItem, typename TWriter>
        void WriteList(JsonWriter& InWriter, const char* InName, const std::vector<TItem>& InItems, TWriter&& InWriteItem)
        {
            InWriter.Name(InName);
            InWriter.BeginArray();

            for (const auto& Item : InItems)
            {
                InWriter.BeginObject();
                InWriteItem(Item);
                InWriter.EndObject();
            }

            InWriter.EndArray();
        }

        void WriteDevice(JsonWriter& InWriter, const HwNetworkDevice& InDevice)
        {
            InWriter.BeginObject();
            InWriter.Name("mac_address"); InWriter.String(InDevice.MacAddress);
            InWriter.Name("ip_address"); InWriter.String(InDevice.Ip);
            InWriter.EndObject();
        }
    }

    std::string ToJson(const Hwid& InHwid, JsonFormat InFormat)
    {
        JsonWriter W(InFormat == JsonFormat::Indented);
        W.BeginObject();

        WriteList(W, "disks", InHwid.Disks, [&](const HwDisk& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("interface"); W.String(T.Interface);
            W.Name("model"); W.String(T.Model);
            W.Name("serial_number"); W.String(T.SerialNumber);
            W.Name("capacity"); W.String(T.Capacity);
            W.Name("partitions"); W.Int(T.Partitions);
            W.Name("is_removable"); W.Bool(T.IsRemovable);
            W.Name("is_smart"); W.Bool(T.IsSMART);
            W.Name("firmware"); W.String(T.Firmware);
            W.Name("world_wide_name"); W.String(T.WorldWideName);
            W.Name("disk_guid"); W.String(T.DiskGuid);
            W.Name("instance_id"); W.String(T.InstanceId);
            W.Name("nvme_serial"); W.String(T.NvmeSerial);
            W.Name("nvme_eui64"); W.String(T.NvmeEui64);
            W.Name("nvme_nguid"); W.String(T.NvmeNguid);
            W.Name("nvme_fguid"); W.String(T.NvmeFguid);
            W.Name("ata_serial"); W.String(T.AtaSerial);
            W.Name("ata_wwn"); W.String(T.AtaWwn);
            W.Name("vpd_t10"); W.String(T.VpdT10);
            W.Name("vpd_eui64"); W.String(T.VpdEui64);
            W.Name("vpd_nguid"); W.String(T.VpdNguid);
            W.Name("vpd_naa"); W.String(T.VpdNaa);
            W.Name("vpd_scsi_name"); W.String(T.VpdScsiName);
            W.Name("vpd_vendor"); W.String(T.VpdVendor);
            W.Name("duid"); W.String(T.Duid);
        });

        WriteList(W, "volumes", InHwid.Volumes, [&](const HwVolume& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("path"); W.String(T.Path);
            W.Name("letter"); W.String(T.Letter);
            W.Name("serial_number"); W.UInt(T.SerialNumber);
        });

        WriteList(W, "network_adapters", InHwid.NetworkAdapters, [&](const HwNetworkAdapter& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("interface_id"); W.Int(T.InterfaceId);
            W.Name("name"); W.String(T.Name);
            W.Name("interface_guid"); W.String(T.InterfaceGuid);
            W.Name("service_name"); W.String(T.ServiceName);
            W.Name("address");
            W.BeginObject();
            W.Name("current"); W.String(T.Address.Current);
            W.Name("permanent"); W.String(T.Address.Permanent);
            W.EndObject();
            W.Name("is_physical"); W.Bool(T.IsPhysical);
            W.Name("is_enabled"); W.Bool(T.IsEnabled);
            W.Name("install_date"); W.Date(T.InstallDate);
            W.Name("instance_id"); W.String(T.InstanceId);
        });

        WriteList(W, "bluetooth_radios", InHwid.BluetoothRadios, [&](const HwBluetoothRadio& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("address"); W.String(T.Address);
            W.Name("name"); W.String(T.Name);
            W.Name("manufacturer"); W.Int(T.Manufacturer);
            W.Name("class_of_device"); W.UInt(T.ClassOfDevice);
            W.Name("lmp_subversion"); W.Int(T.LmpSubversion);
        });

        WriteList(W, "baseboards", InHwid.Baseboards, [&](const HwBaseboard& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("manufacturer"); W.String(T.Manufacturer);
            W.Name("model"); W.String(T.Model);
            W.Name("version"); W.String(T.Version);
            W.Name("serial_number"); W.String(T.SerialNumber);
            W.Name("part_number"); W.String(T.PartNumber);
        });

        WriteList(W, "motherboards", InHwid.Motherboards, [&](const HwMotherboard& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("name"); W.String(T.Name);
            W.Name("vendor"); W.String(T.Vendor);
            W.Name("version"); W.String(T.Version);
            W.Name("UUID"); W.String(std::wstring_view(T.UUID));
        });

        WriteList(W, "chassis", InHwid.Chassis, [&](const HwChassis& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("manufacturer"); W.String(T.Manufacturer);
            W.Name("type"); W.Int(T.Type);
            W.Name("type_name"); W.String(T.TypeName);
            W.Name("version"); W.String(T.Version);
            W.Name("serial_number"); W.String(T.SerialNumber);
            W.Name("asset_tag"); W.String(T.AssetTag);
        });

        WriteList(W, "bios_firmwares", InHwid.BiosFirmwares, [&](const HwBios& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("manufacturer"); W.String(T.Manufacturer);
            W.Name("version"); W.String(T.Version);
            W.Name("serial_number"); W.String(T.SerialNumber);
        });

        WriteList(W, "smbios_tables", InHwid.SmbiosTables, [&](const HwSmbios& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("version"); W.String(T.Version);
            W.Name("hash"); W.String(T.Hash);
            W.Name("length"); W.UInt(T.Length);
        });

        WriteList(W, "processors", InHwid.Processors, [&](const HwProcessor& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("manufacturer"); W.String(T.Manufacturer);
            W.Name("model"); W.String(T.Model);
            W.Name("model_number"); W.String(T.ModelNumber);
            W.Name("socket"); W.String(T.Socket);
            W.Name("part_number"); W.String(T.PartNumber);
            W.Name("serial_number"); W.String(T.SerialNumber);
            W.Name("clock_speed"); W.String(T.ClockSpeed);
            W.Name("voltage"); W.String(T.Voltage);
            W.Name("channel"); W.String(T.Channel);
            W.Name("number_of_cores"); W.UInt(T.NumberOfCores);
            W.Name("number_of_logical_processors"); W.UInt(T.NumberOfLogicalProcessors);
        });

        WriteList(W, "memory_sticks", InHwid.MemorySticks, [&](const HwMemoryStick& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("manufacturer"); W.String(T.Manufacturer);
            W.Name("part_number"); W.String(T.PartNumber);
            W.Name("serial_number"); W.String(T.SerialNumber);
            W.Name("capacity_in_gb"); W.String(T.Capacity);
            W.Name("clock_speed"); W.String(T.ClockSpeed);
            W.Name("voltage"); W.String(T.Voltage);
            W.Name("channel"); W.String(T.Channel);
        });

        WriteList(W, "batteries", InHwid.Batteries, [&](const HwBattery& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("device_name"); W.String(T.DeviceName);
            W.Name("manufacturer"); W.String(T.Manufacturer);
            W.Name("serial_number"); W.String(T.SerialNumber);
            W.Name("unique_id"); W.String(T.UniqueId);
            W.Name("chemistry"); W.String(T.Chemistry);
            W.Name("designed_capacity"); W.UInt(T.DesignedCapacity);
            W.Name("full_charged_capacity"); W.UInt(T.FullChargedCapacity);
            W.Name("manufacture_date"); W.Date(T.ManufactureDate);
        });

        WriteList(W, "monitors", InHwid.Monitors, [&](const HwMonitor& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("manufacturer"); W.String(T.Manufacturer);
            W.Name("name"); W.String(T.Name);
            W.Name("product"); W.String(T.Product);
            W.Name("serial_number"); W.String(T.SerialNumber);
            W.Name("instance_id"); W.String(T.InstanceId);
            W.Name("edid_hash"); W.String(T.EdidHash);
            W.Name("manufacture_week"); W.Int(T.ManufactureWeek);
            W.Name("manufacture_year"); W.Int(T.ManufactureYear);
        });

        WriteList(W, "video_controllers", InHwid.VideoControllers, [&](const HwVideo& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("name"); W.String(T.Name);
            W.Name("width"); W.UInt(T.Width);
            W.Name("height"); W.UInt(T.Height);
            W.Name("refresh_rate"); W.UInt(T.RefreshRate);
            W.Name("driver_date"); W.Date(T.DriverDate);
            W.Name("driver_version"); W.String(T.DriverVersion);
            W.Name("instance_id"); W.String(T.InstanceId);
        });

        WriteList(W, "printers", InHwid.Printers, [&](const HwPrinter& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("name"); W.String(T.Name);
            W.Name("port_name"); W.String(T.PortName);
            W.Name("location"); W.String(T.Location);
            W.Name("width"); W.UInt(T.Width);
            W.Name("height"); W.UInt(T.Height);
        });

        WriteList(W, "users", InHwid.Users, [&](const HwUser& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("username"); W.String(T.Username);
            W.Name("full_name"); W.String(T.FullName);
            W.Name("sid"); W.String(T.SID);
            W.Name("domain"); W.String(T.Domain);
            W.Name("install_date"); W.Date(T.InstallDate);
        });

        WriteList(W, "operating_systems", InHwid.OperatingSystems, [&](const HwOperatingSystem& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("name"); W.String(T.Name);
            W.Name("version"); W.String(T.Version);
            W.Name("architecture"); W.String(T.Architecture);
            W.Name("registered_user"); W.String(T.RegisteredUser);
            W.Name("serial_number"); W.String(T.SerialNumber);
            W.Name("install_date"); W.Date(T.InstallDate);
            W.Name("last_boot_up_time"); W.Date(T.LastBootUpTime);
            W.Name("machine_guid"); W.String(T.MachineGuid);
            W.Name("sqm_machine_id"); W.String(T.SqmMachineId);
            W.Name("hardware_profile_guid"); W.String(T.HardwareProfileGuid);
            W.Name("install_time"); W.Date(T.InstallTime);
            W.Name("machine_sid"); W.String(T.MachineSid);
        });

        WriteList(W, "wifis", InHwid.Wifis, [&](const HwWifi& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("ssid"); W.String(T.Ssid);
            W.Name("bssid"); W.String(T.Bssid);
            W.Name("Strength"); W.Int(T.Strength);
            W.Name("Channel"); W.Int(T.Channel);
            W.Name("Frequency"); W.Int(T.Frequency);
            W.Name("Band"); W.Float(T.Band);
            W.Name("Quality"); W.Int(T.Quality);
        });

        WriteList(W, "routers", InHwid.Routers, [&](const HwRouter& T)
        {
            W.Name("id"); W.Int(T.Id);

            W.Name("gateways");
            W.BeginArray();
            for (const auto& Gateway : T.Gateways) WriteDevice(W, Gateway);
            W.EndArray();

            W.Name("dns_servers");
            W.BeginArray();
            for (const auto& Server : T.DnsServers) W.String(std::wstring_view(Server));
            W.EndArray();

            W.Name("dhcp_servers");
            W.BeginArray();
            for (const auto& Server : T.DhcpServers) W.String(std::wstring_view(Server));
            W.EndArray();

            W.Name("network_devices");
            W.BeginArray();
            for (const auto& Device : T.NetworkDevices) WriteDevice(W, Device);
            W.EndArray();
        });

        WriteList(W, "network_signatures", InHwid.NetworkSignatures, [&](const HwNetworkSignature& T)
        {
            W.Name("id"); W.Int(T.Id);
            W.Name("profile_guid"); W.String(T.ProfileGuid);
            W.Name("name"); W.String(T.Name);
            W.Name("gateway_address"); W.String(T.DefaultGatewayMac);
        });

        W.EndObject();
        return std::move(W.Output);
    }
}
