#pragma once

//
// Internal declarations shared by the collectors and the tests. Nothing here is part of the public API.
//

#include <HardwareIds/HardwareIds.hpp>

#include <windows.h>
#include <cfgmgr32.h>

#include <array>
#include <cstdint>
#include <optional>
#include <span>
#include <string>
#include <string_view>
#include <vector>

namespace HardwareIds::Detail
{
    using Bytes = std::vector<std::uint8_t>;
    using ByteSpan = std::span<const std::uint8_t>;

    //
    // Text.
    //

    /// <summary>
    /// Formats bytes as colon-separated upper-case hexadecimal ("00:1A:2B"), like the .NET FormatMacAddress.
    /// </summary>
    std::wstring FormatMacAddress(ByteSpan InBytes);

    /// <summary>
    /// Formats bytes as hexadecimal without separators.
    /// </summary>
    std::wstring FormatHex(ByteSpan InBytes, bool InUpperCase = true);

    /// <summary>
    /// Computes the SHA-256 of bytes, as lower-case hexadecimal.
    /// </summary>
    std::wstring Sha256Hex(ByteSpan InBytes);

    /// <summary>
    /// Decodes bytes like .NET's Encoding.ASCII (bytes above 0x7F become '?').
    /// </summary>
    std::wstring DecodeAscii(ByteSpan InBytes);

    /// <summary>
    /// Decodes bytes as Latin-1 (each byte is one character).
    /// </summary>
    std::wstring DecodeLatin1(ByteSpan InBytes);

    /// <summary>
    /// Decodes bytes as UTF-8, replacing invalid sequences with U+FFFD.
    /// </summary>
    std::wstring DecodeUtf8(ByteSpan InBytes);

    /// <summary>
    /// Encodes a UTF-16 string as UTF-8.
    /// </summary>
    std::string EncodeUtf8(std::wstring_view InText);

    /// <summary>
    /// Tells whether a character is white space, like .NET's char.IsWhiteSpace.
    /// </summary>
    bool IsWhiteSpace(wchar_t InCharacter);

    /// <summary>
    /// Removes leading and trailing white space, like .NET's string.Trim().
    /// </summary>
    std::wstring Trim(std::wstring_view InText);

    /// <summary>
    /// Removes the given leading and trailing characters, like .NET's string.Trim(params char[]).
    /// </summary>
    std::wstring TrimCharacters(std::wstring_view InText, std::wstring_view InCharacters);

    /// <summary>
    /// Like .NET's string.IsNullOrWhiteSpace.
    /// </summary>
    bool IsNullOrWhiteSpace(const NullableString& InText);

    /// <summary>
    /// Upper-cases ASCII letters, like .NET's ToUpperInvariant for the identifiers it is used on.
    /// </summary>
    std::wstring ToUpperInvariant(std::wstring_view InText);

    /// <summary>
    /// Compares two strings ignoring the case, like StringComparison.OrdinalIgnoreCase.
    /// </summary>
    bool EqualsIgnoreCase(std::wstring_view InLeft, std::wstring_view InRight);

    /// <summary>
    /// Formats a GUID like .NET's Guid.ToString() ("00112233-4455-6677-8899-aabbccddeeff").
    /// </summary>
    std::wstring FormatGuid(const GUID& InGuid);

    /// <summary>
    /// Formats a GUID like .NET's Guid.ToString("B").ToUpperInvariant() ("{00112233-...}").
    /// </summary>
    std::wstring FormatGuidBraces(const GUID& InGuid);

    /// <summary>
    /// Parses a GUID written with or without braces, like .NET's Guid.TryParse for the "D" and "B" formats.
    /// </summary>
    std::optional<GUID> ParseGuid(std::wstring_view InText);

    /// <summary>
    /// Reads a GUID from 16 bytes in the .NET Guid(byte[]) layout (the first three fields little-endian).
    /// </summary>
    GUID GuidFromBytes(ByteSpan InBytes);

    /// <summary>
    /// Formats a number of tenths ("1.3") or hundredths ("1.20") with the decimal separator of the user's culture,
    /// like .NET's ToString("0.0") and ToString("0.00") on the current culture.
    /// </summary>
    std::wstring FormatTenths(std::uint32_t InTenths);
    std::wstring FormatHundredths(std::uint32_t InHundredths);

    /// <summary>
    /// Splits a REG_MULTI_SZ style buffer (strings separated by a null character) into its non-empty strings.
    /// </summary>
    std::vector<std::wstring> SplitMultiString(const wchar_t* InBuffer, std::size_t InLength);

    //
    // Dates.
    //

    inline constexpr std::int64_t TicksPerSecond = 10'000'000;
    inline constexpr std::int64_t TicksPerMinute = 60 * TicksPerSecond;
    inline constexpr std::int64_t TicksPerDay = 24 * 60 * TicksPerMinute;
    inline constexpr std::int64_t FileTimeEpochTicks = 504'911'232'000'000'000;   // 1601-01-01 in .NET ticks.
    inline constexpr std::int64_t MaxDateTimeTicks = 3'155'378'975'999'999'999;   // 9999-12-31T23:59:59.9999999.

    /// <summary>
    /// An unspecified date at midnight, like .NET's new DateTime(year, month, day).
    /// </summary>
    DateTime MakeDate(int InYear, int InMonth, int InDay);

    /// <summary>
    /// Converts a UTC FILETIME to a local time, like .NET's DateTime.FromFileTime; empty when it is out of range.
    /// </summary>
    std::optional<DateTime> FromFileTime(std::int64_t InFileTime);

    /// <summary>
    /// Converts Unix seconds to a local time, like .NET's DateTimeOffset.FromUnixTimeSeconds(..).LocalDateTime.
    /// </summary>
    DateTime FromUnixTimeSeconds(std::int64_t InSeconds);

    /// <summary>
    /// The local time a number of milliseconds ago, like .NET's DateTime.Now - TimeSpan.FromMilliseconds(..).
    /// </summary>
    DateTime LocalNowMinus(std::uint64_t InMilliseconds);

    /// <summary>
    /// Formats a date like System.Text.Json ("2025-02-17T00:05:46.869274+01:00").
    /// </summary>
    std::string FormatDateTime(const DateTime& InDateTime);

    //
    // Registry (64-bit view, like an AnyCPU .NET process on a 64-bit Windows).
    //

    class RegistryKey
    {
    public:
        RegistryKey() = default;
        RegistryKey(const RegistryKey&) = delete;
        RegistryKey& operator=(const RegistryKey&) = delete;
        RegistryKey(RegistryKey&& InOther) noexcept;
        RegistryKey& operator=(RegistryKey&& InOther) noexcept;
        ~RegistryKey();

        static RegistryKey OpenLocalMachine(std::wstring_view InPath);
        RegistryKey OpenSubKey(std::wstring_view InName) const;
        explicit operator bool() const { return this->Handle != nullptr; }

        NullableString GetString(const wchar_t* InName) const;                              // REG_SZ, REG_EXPAND_SZ (expanded).
        std::optional<std::int32_t> GetDword(const wchar_t* InName) const;                  // REG_DWORD.
        std::optional<std::int64_t> GetQword(const wchar_t* InName) const;                  // REG_QWORD.
        std::optional<Bytes> GetBinary(const wchar_t* InName) const;                        // REG_BINARY, REG_NONE.
        std::optional<std::vector<std::wstring>> GetMultiString(const wchar_t* InName) const; // REG_MULTI_SZ.
        std::vector<std::wstring> GetSubKeyNames() const;

    private:
        explicit RegistryKey(HKEY InHandle) : Handle(InHandle) {}
        bool Query(const wchar_t* InName, DWORD& OutType, Bytes& OutData) const;

        HKEY Handle = nullptr;
    };

    //
    // Kernel handles.
    //

    class UniqueHandle
    {
    public:
        UniqueHandle() = default;
        explicit UniqueHandle(HANDLE InHandle) : Handle(InHandle == INVALID_HANDLE_VALUE ? nullptr : InHandle) {}
        UniqueHandle(const UniqueHandle&) = delete;
        UniqueHandle& operator=(const UniqueHandle&) = delete;
        UniqueHandle(UniqueHandle&& InOther) noexcept : Handle(InOther.Handle) { InOther.Handle = nullptr; }
        ~UniqueHandle() { if (this->Handle != nullptr) CloseHandle(this->Handle); }

        HANDLE Get() const { return this->Handle; }
        explicit operator bool() const { return this->Handle != nullptr; }

    private:
        HANDLE Handle = nullptr;
    };

    //
    // Plug and Play configuration manager.
    //

    inline constexpr GUID GUID_DEVINTERFACE_DISK_ = { 0x53F56307, 0xB6BF, 0x11D0, { 0x94, 0xF2, 0x00, 0xA0, 0xC9, 0x1E, 0xFB, 0x8B } };
    inline constexpr GUID GUID_DEVINTERFACE_MONITOR_ = { 0xE6F07B5F, 0xEE97, 0x4A90, { 0xB0, 0x76, 0x33, 0xF5, 0x7B, 0xF4, 0xEA, 0xA7 } };
    inline constexpr GUID GUID_DEVICE_BATTERY_ = { 0x72631E54, 0x78A4, 0x11D0, { 0xBC, 0xF7, 0x00, 0xAA, 0x00, 0xB7, 0xB3, 0x2A } };
    inline constexpr GUID GUID_DEVCLASS_DISPLAY_ = { 0x4D36E968, 0xE325, 0x11CE, { 0xBF, 0xC1, 0x08, 0x00, 0x2B, 0xE1, 0x03, 0x18 } };
    inline constexpr GUID GUID_DEVCLASS_NET_ = { 0x4D36E972, 0xE325, 0x11CE, { 0xBF, 0xC1, 0x08, 0x00, 0x2B, 0xE1, 0x03, 0x18 } };

    inline constexpr DEVPROPKEY DEVPKEY_Device_DeviceDesc_ = { { 0xA45C254E, 0xDF1C, 0x4EFD, { 0x80, 0x20, 0x67, 0xD1, 0x46, 0xA8, 0x50, 0xE0 } }, 2 };
    inline constexpr DEVPROPKEY DEVPKEY_Device_Service_ = { { 0xA45C254E, 0xDF1C, 0x4EFD, { 0x80, 0x20, 0x67, 0xD1, 0x46, 0xA8, 0x50, 0xE0 } }, 6 };
    inline constexpr DEVPROPKEY DEVPKEY_Device_FriendlyName_ = { { 0xA45C254E, 0xDF1C, 0x4EFD, { 0x80, 0x20, 0x67, 0xD1, 0x46, 0xA8, 0x50, 0xE0 } }, 14 };
    inline constexpr DEVPROPKEY DEVPKEY_Device_InstanceId_ = { { 0x78C34FC8, 0x104A, 0x4ACA, { 0x9E, 0xA4, 0x52, 0x4D, 0x52, 0x99, 0x6E, 0x57 } }, 256 };
    inline constexpr DEVPROPKEY DEVPKEY_Device_DriverDate_ = { { 0xA8B865DD, 0x2E3D, 0x4094, { 0xAD, 0x97, 0xE5, 0x93, 0xA7, 0x0C, 0x75, 0xD6 } }, 2 };
    inline constexpr DEVPROPKEY DEVPKEY_Device_DriverVersion_ = { { 0xA8B865DD, 0x2E3D, 0x4094, { 0xAD, 0x97, 0xE5, 0x93, 0xA7, 0x0C, 0x75, 0xD6 } }, 3 };
    inline constexpr DEVPROPKEY DEVPKEY_Device_InstallDate_ = { { 0x83DA6326, 0x97A6, 0x4088, { 0x94, 0x53, 0xA1, 0x92, 0x3F, 0x57, 0x3B, 0x29 } }, 100 };

    std::vector<std::wstring> GetDeviceInterfaces(const GUID& InInterfaceClass);
    std::vector<std::wstring> GetDeviceIds(const GUID& InSetupClass);
    std::optional<DEVINST> LocateDevNode(const std::wstring& InInstanceId);
    NullableString GetInterfaceProperty(const std::wstring& InInterfacePath, const DEVPROPKEY& InKey);
    NullableString GetDevNodeProperty(DEVINST InDevInst, const DEVPROPKEY& InKey);
    std::optional<DateTime> GetDevNodeDateProperty(DEVINST InDevInst, const DEVPROPKEY& InKey);

    /// <summary>
    /// Converts a device interface name into the device instance identifier it was derived from.
    /// </summary>
    NullableString InterfaceNameToInstanceId(const NullableString& InInterfaceName);

    //
    // SMBIOS.
    //

    class SmbiosStructure
    {
    public:
        SmbiosStructure(std::uint8_t InType, std::uint8_t InLength, std::uint16_t InHandle, Bytes InFormatted, std::vector<std::wstring> InStrings);

        std::uint8_t Type = 0;
        std::uint8_t Length = 0;
        std::uint16_t Handle = 0;
        std::vector<std::wstring> Strings;

        std::uint8_t GetByte(int InOffset) const;
        std::uint16_t GetWord(int InOffset) const;
        std::uint32_t GetDword(int InOffset) const;
        std::optional<Bytes> GetBytes(int InOffset, int InCount) const;
        NullableString GetString(int InOffset) const;   // The string referenced by the 1-based index stored at the offset.

    private:
        Bytes Formatted;                                // The whole formatted area, header included.
    };

    class SmbiosTable
    {
    public:
        std::uint8_t MajorVersion = 0;
        std::uint8_t MinorVersion = 0;
        std::uint8_t DmiRevision = 0;
        Bytes Data;
        std::vector<SmbiosStructure> Structures;

        static std::optional<SmbiosTable> Read();
        static SmbiosTable FromData(std::uint8_t InMajorVersion, std::uint8_t InMinorVersion, std::uint8_t InDmiRevision, Bytes InData);
        std::vector<const SmbiosStructure*> OfType(std::uint8_t InType) const;

    private:
        void Parse();
    };

    std::wstring GetChassisTypeName(int InType);
    NullableString FormatProcessorId(const std::optional<Bytes>& InProcessorId);
    NullableString GetProcessorIdFromCpuId();
    std::wstring FormatProcessorVoltage(std::uint8_t InVoltage);
    std::uint64_t GetMemoryDeviceCapacity(const SmbiosStructure& InMemoryDevice);

    struct ProcessorTopology { int Cores = 0; int LogicalProcessors = 0; int Packages = 0; };
    ProcessorTopology GetProcessorTopology();

    //
    // Storage.
    //

    inline constexpr int ScsiCodeSetBinary = 1;
    inline constexpr int ScsiCodeSetAscii = 2;
    inline constexpr int ScsiCodeSetUtf8 = 3;
    inline constexpr int ScsiTypeVendorSpecific = 0;
    inline constexpr int ScsiTypeT10VendorId = 1;
    inline constexpr int ScsiTypeEui64 = 2;
    inline constexpr int ScsiTypeNaa = 3;
    inline constexpr int ScsiTypeScsiNameString = 8;
    inline constexpr int ScsiAssociationLogicalUnit = 0;

    struct ScsiDeviceIdentifier
    {
        int CodeSet = 0;
        int Type = 0;
        int Association = 0;
        Bytes Value;

        std::wstring Text() const;   // Hexadecimal for binary identifiers, the trimmed string otherwise.
    };

    struct NvmeControllerIdentity { NullableString SerialNumber, ModelNumber, FirmwareRevision, FruGuid; };
    struct NvmeNamespaceIdentity { NullableString Nguid, Eui64; };
    struct AtaIdentity { NullableString SerialNumber, ModelNumber, FirmwareRevision, WorldWideName; };

    std::vector<ScsiDeviceIdentifier> ParseDeviceIdentifiers(ByteSpan InDescriptor);
    std::optional<NvmeControllerIdentity> ParseNvmeControllerIdentity(ByteSpan InData);
    std::optional<NvmeNamespaceIdentity> ParseNvmeNamespaceIdentity(ByteSpan InData);
    std::optional<AtaIdentity> ParseAtaIdentity(ByteSpan InData);
    NullableString ParseDiskIdentifier(ByteSpan InLayout);
    std::wstring GetDiskInterfaceType(const NullableString& InInstanceId, std::optional<std::uint32_t> InBusType);

    //
    // Displays and peripherals.
    //

    struct EdidInfo
    {
        NullableString Manufacturer, ProductCode, SerialNumber, Name, Hash;
        int ManufactureWeek = 0;
        int ManufactureYear = 0;
    };

    std::optional<EdidInfo> ParseEdid(const std::optional<Bytes>& InData);
    std::optional<DateTime> ParseBatteryManufactureDate(std::uint8_t InDay, std::uint8_t InMonth, std::uint16_t InYear);
    Bytes BluetoothAddressToBytes(std::uint64_t InAddress);

    //
    // Network.
    //

    struct IpAddress
    {
        bool IsV6 = false;
        std::array<std::uint8_t, 16> Bytes = {};   // IPv4: the first 4 bytes, in network order.
        std::uint32_t ScopeId = 0;

        static IpAddress FromV4(std::uint32_t InHostOrder);
        std::uint32_t ToV4() const;                // Host order.
        bool IsV6LinkLocal() const;
        std::wstring ToString() const;             // Like .NET's IPAddress.ToString().
        bool operator==(const IpAddress& InOther) const = default;
    };

    bool IsPhysicalAdapter(std::optional<std::int32_t> InCharacteristics, bool InIsPresent, std::optional<std::uint8_t> InInterfaceFlags);
    bool HasUsableUnicastAddress(const std::vector<IpAddress>& InAddresses);
    bool IsInSubnet(std::uint32_t InAddress, std::uint32_t InSubnetAddress, std::uint32_t InMask);
    std::vector<std::uint32_t> GetProbeAddresses(std::uint32_t InAddress, std::uint32_t InMask, int InCount);
    bool IsReportableNeighbor(ByteSpan InAddress, ByteSpan InPhysicalAddress, std::uint32_t InState);
    int GetWifiChannel(std::uint32_t InFrequencyKHz);
    float GetWifiBand(std::uint32_t InFrequencyKHz);

    inline constexpr int NetworkProbeCount = 254;
    inline constexpr std::chrono::milliseconds DefaultNetworkProbeWait{ 1000 };
    inline constexpr std::chrono::milliseconds DefaultWifiScanDuration{ 7000 };

    //
    // Collectors, in the order the .NET library runs them.
    //

    void RetrieveNetworkSignatures(Hwid& InHwid);
    void RetrieveDiskDrives(Hwid& InHwid);
    void RetrieveDiskVolumes(Hwid& InHwid);
    void RetrieveNetworkAdapters(Hwid& InHwid);
    void RetrieveBluetoothRadios(Hwid& InHwid);
    void RetrieveBaseBoards(Hwid& InHwid, const SmbiosTable* InSmbios);
    void RetrieveMotherBoards(Hwid& InHwid, const SmbiosTable* InSmbios);
    void RetrieveChassis(Hwid& InHwid, const SmbiosTable* InSmbios);
    void RetrieveFirmwares(Hwid& InHwid, const SmbiosTable* InSmbios);
    void RetrieveSmbiosTables(Hwid& InHwid, const SmbiosTable* InSmbios);
    void RetrieveProcessors(Hwid& InHwid, const SmbiosTable* InSmbios);
    void RetrieveMemorySticks(Hwid& InHwid, const SmbiosTable* InSmbios);
    void RetrieveBatteries(Hwid& InHwid);
    void RetrieveMonitors(Hwid& InHwid);
    void RetrieveVideoControllers(Hwid& InHwid);
    void RetrievePrinters(Hwid& InHwid);
    void RetrieveUserAccounts(Hwid& InHwid);
    void RetrieveOperatingSystems(Hwid& InHwid);
    void ScanNetworkEndpoints(Hwid& InHwid, std::chrono::milliseconds InTimeout, std::stop_token InStopToken);
    void ScanNetworkDevices(Hwid& InHwid, std::chrono::milliseconds InProbeWait, std::stop_token InStopToken);
}
