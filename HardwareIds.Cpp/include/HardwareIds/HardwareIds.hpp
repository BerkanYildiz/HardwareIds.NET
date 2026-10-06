#pragma once

//
// HardwareIds: hardware identifiers of the local Windows computer, read straight from the native Windows APIs.
//
// This is the C++ port of HardwareIds.NET. Every field holds the same value the .NET library reports, and
// ToJson() writes the same JSON as System.Text.Json does for the .NET Hwid object, byte for byte, so snapshots
// taken by either library can be compared directly.
//

#include <chrono>
#include <cstdint>
#include <optional>
#include <stop_token>
#include <string>
#include <vector>

namespace HardwareIds
{
    /// <summary>
    /// A string that may be missing (serialized as JSON null), like a nullable .NET string.
    /// </summary>
    using NullableString = std::optional<std::wstring>;

    /// <summary>
    /// A point in time, as a .NET DateTime: wall-clock ticks, plus whether it is local time.
    /// Local times are serialized with their UTC offset ("2025-02-17T00:05:46+01:00"), unspecified ones without ("0001-01-01T00:00:00").
    /// </summary>
    struct DateTime
    {
        std::int64_t Ticks = 0;                 // 100-nanosecond intervals since 0001-01-01T00:00:00, in the time's own clock.
        bool IsLocal = false;                   // True for a local time, false for an unspecified one.
        std::int32_t UtcOffsetMinutes = 0;      // For a local time: its offset from UTC, in minutes.
    };

    struct HwDisk
    {
        int Id = 0;
        NullableString Interface;
        NullableString Model;
        NullableString SerialNumber;            // The storage device descriptor serial (what WMI reports).
        NullableString Capacity;
        int Partitions = 0;
        bool IsRemovable = false;
        bool IsSMART = false;
        NullableString Firmware;
        NullableString WorldWideName;
        NullableString DiskGuid;                // GPT disk GUID, or "0x" + MBR signature.
        NullableString InstanceId;
        NullableString NvmeSerial;
        NullableString NvmeEui64;
        NullableString NvmeNguid;
        NullableString NvmeFguid;
        NullableString AtaSerial;
        NullableString AtaWwn;
        NullableString VpdT10;
        NullableString VpdEui64;
        NullableString VpdNguid;
        NullableString VpdNaa;
        NullableString VpdScsiName;
        NullableString VpdVendor;
        NullableString Duid;                    // SHA-256 of the unique identifier Windows computes for the disk.
    };

    struct HwVolume
    {
        int Id = 0;
        NullableString Path;
        NullableString Letter;
        std::uint32_t SerialNumber = 0;
    };

    struct HwNetworkAddress
    {
        NullableString Current;
        NullableString Permanent;
    };

    struct HwNetworkAdapter
    {
        int Id = 0;
        int InterfaceId = 0;
        NullableString Name;
        NullableString InterfaceGuid;
        NullableString ServiceName;
        HwNetworkAddress Address;
        bool IsPhysical = false;
        bool IsEnabled = false;
        std::optional<DateTime> InstallDate;
        NullableString InstanceId;
    };

    struct HwBluetoothRadio
    {
        int Id = 0;
        NullableString Address;
        NullableString Name;
        int Manufacturer = 0;
        std::uint32_t ClassOfDevice = 0;
        int LmpSubversion = 0;
    };

    struct HwBaseboard
    {
        int Id = 0;
        NullableString Manufacturer;
        NullableString Model;
        NullableString Version;
        NullableString SerialNumber;
        NullableString PartNumber;
    };

    struct HwMotherboard
    {
        int Id = 0;
        NullableString Name;
        NullableString Vendor;
        NullableString Version;
        std::wstring UUID = L"00000000-0000-0000-0000-000000000000";
    };

    struct HwChassis
    {
        int Id = 0;
        NullableString Manufacturer;
        int Type = 0;
        NullableString TypeName;
        NullableString Version;
        NullableString SerialNumber;
        NullableString AssetTag;
    };

    struct HwBios
    {
        int Id = 0;
        NullableString Manufacturer;
        NullableString Version;
        NullableString SerialNumber;
    };

    struct HwSmbios
    {
        int Id = 0;
        NullableString Version;
        NullableString Hash;
        std::uint32_t Length = 0;
    };

    struct HwProcessor
    {
        int Id = 0;
        NullableString Manufacturer;
        NullableString Model;
        NullableString ModelNumber;
        NullableString Socket;
        NullableString PartNumber;
        NullableString SerialNumber;
        NullableString ClockSpeed;
        NullableString Voltage;
        NullableString Channel;
        std::uint32_t NumberOfCores = 0;
        std::uint32_t NumberOfLogicalProcessors = 0;
    };

    struct HwMemoryStick
    {
        int Id = 0;
        NullableString Manufacturer;
        NullableString PartNumber;
        NullableString SerialNumber;
        NullableString Capacity;
        NullableString ClockSpeed;
        NullableString Voltage;
        NullableString Channel;
    };

    struct HwBattery
    {
        int Id = 0;
        NullableString DeviceName;
        NullableString Manufacturer;
        NullableString SerialNumber;
        NullableString UniqueId;
        NullableString Chemistry;
        std::uint32_t DesignedCapacity = 0;
        std::uint32_t FullChargedCapacity = 0;
        std::optional<DateTime> ManufactureDate;
    };

    struct HwMonitor
    {
        int Id = 0;
        NullableString Manufacturer;
        NullableString Name;
        NullableString Product;
        NullableString SerialNumber;
        NullableString InstanceId;
        NullableString EdidHash;
        int ManufactureWeek = 0;
        int ManufactureYear = 0;
    };

    struct HwVideo
    {
        int Id = 0;
        NullableString Name;
        std::uint32_t Width = 0;
        std::uint32_t Height = 0;
        std::uint32_t RefreshRate = 0;
        DateTime DriverDate;
        NullableString DriverVersion;
        NullableString InstanceId;
    };

    struct HwPrinter
    {
        int Id = 0;
        NullableString Name;
        NullableString PortName;
        NullableString Location;
        std::uint32_t Width = 0;
        std::uint32_t Height = 0;
    };

    struct HwUser
    {
        int Id = 0;
        NullableString Username;
        NullableString FullName;
        NullableString SID;
        NullableString Domain;
        DateTime InstallDate;
    };

    struct HwOperatingSystem
    {
        int Id = 0;
        NullableString Name;
        NullableString Version;
        NullableString Architecture;
        NullableString RegisteredUser;
        NullableString SerialNumber;
        DateTime InstallDate;
        DateTime LastBootUpTime;
        NullableString MachineGuid;
        NullableString SqmMachineId;
        NullableString HardwareProfileGuid;
        std::optional<DateTime> InstallTime;
        NullableString MachineSid;
    };

    struct HwWifi
    {
        int Id = 0;
        NullableString Ssid;
        NullableString Bssid;
        int Strength = 0;
        int Channel = 0;
        int Frequency = 0;
        float Band = 0;
        int Quality = 0;
    };

    struct HwNetworkDevice
    {
        NullableString MacAddress;
        NullableString Ip;
    };

    struct HwRouter
    {
        int Id = 0;
        std::vector<HwNetworkDevice> Gateways;
        std::vector<std::wstring> DnsServers;
        std::vector<std::wstring> DhcpServers;
        std::vector<HwNetworkDevice> NetworkDevices;
    };

    struct HwNetworkSignature
    {
        int Id = 0;
        NullableString ProfileGuid;
        NullableString Name;
        NullableString DefaultGatewayMac;
    };

    /// <summary>
    /// Everything collected about the computer. Each list follows the order of the .NET library.
    /// </summary>
    struct Hwid
    {
        std::vector<HwDisk> Disks;
        std::vector<HwVolume> Volumes;
        std::vector<HwNetworkAdapter> NetworkAdapters;
        std::vector<HwBluetoothRadio> BluetoothRadios;
        std::vector<HwBaseboard> Baseboards;
        std::vector<HwMotherboard> Motherboards;
        std::vector<HwChassis> Chassis;
        std::vector<HwBios> BiosFirmwares;
        std::vector<HwSmbios> SmbiosTables;
        std::vector<HwProcessor> Processors;
        std::vector<HwMemoryStick> MemorySticks;
        std::vector<HwBattery> Batteries;
        std::vector<HwMonitor> Monitors;
        std::vector<HwVideo> VideoControllers;
        std::vector<HwPrinter> Printers;
        std::vector<HwUser> Users;
        std::vector<HwOperatingSystem> OperatingSystems;
        std::vector<HwWifi> Wifis;
        std::vector<HwRouter> Routers;
        std::vector<HwNetworkSignature> NetworkSignatures;
    };

    struct HardwareIdsConfig
    {
        /// <summary>
        /// How long the Wi-Fi scan may take (7 seconds when not set).
        /// </summary>
        std::optional<std::chrono::milliseconds> DurationOfNetworkScan;

        /// <summary>
        /// How long the local network scan waits for the devices of each subnet to answer (1 second when not set).
        /// </summary>
        std::optional<std::chrono::milliseconds> DurationOfLocalNetworkScan;

        /// <summary>
        /// Whether to scan for the Wi-Fi networks around the computer.
        /// </summary>
        bool ScanNeighborEndpoints = false;

        /// <summary>
        /// Whether to scan the local networks for devices (gateways, DNS and DHCP servers, neighbours).
        /// </summary>
        bool ScanLocalNetworkDevices = false;
    };

    enum class JsonFormat
    {
        Compact,        // Like JsonSerializer.Serialize(Hwid).
        Indented,       // Like JsonSerializer.Serialize(Hwid, new JsonSerializerOptions { WriteIndented = true }).
    };

    /// <summary>
    /// Collects the hardware identifiers of the local computer. Never throws for a missing or unreadable component:
    /// its list is simply left empty. Like the .NET cancellation token, the stop token ends the network scans that are
    /// still running and skips the collectors that have not run yet.
    /// </summary>
    Hwid GetHwid(const HardwareIdsConfig& InConfig = {}, std::stop_token InStopToken = {});

    /// <summary>
    /// Serializes a scan the way the .NET library does. The result only contains ASCII characters (everything else is escaped).
    /// </summary>
    std::string ToJson(const Hwid& InHwid, JsonFormat InFormat = JsonFormat::Compact);
}
