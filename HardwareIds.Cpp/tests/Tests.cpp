//
// HardwareIdsTests: the unit tests of the parsers and collectors (on synthetic data), and smoke tests of a real scan.
//
//   HardwareIdsTests [filter]     Runs the tests whose name contains the filter.
//
// Every identifier below is synthetic.
//

#include <winsock2.h>
#include <ws2tcpip.h>

#include "Internal.hpp"

#include <HardwareIds/HardwareIds.h>

#include <algorithm>
#include <chrono>
#include <condition_variable>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <functional>
#include <mutex>
#include <sstream>
#include <string>
#include <thread>
#include <vector>

namespace
{
    using namespace HardwareIds;
    using namespace HardwareIds::Detail;

    //
    // A minimal test runner.
    //

    struct TestCase
    {
        const char* Name;
        void (*Function)();
    };

    std::vector<TestCase>& GetTests()
    {
        static std::vector<TestCase> Tests;
        return Tests;
    }

    struct TestRegistrar
    {
        TestRegistrar(const char* InName, void (*InFunction)()) { GetTests().push_back({ InName, InFunction }); }
    };

    struct TestFailure { std::string Message; };
    struct TestSkipped { std::string Reason; };

    std::string Describe(const std::wstring& InValue) { return "\"" + EncodeUtf8(InValue) + "\""; }
    std::string Describe(const std::string& InValue) { return "\"" + InValue + "\""; }
    std::string Describe(const wchar_t* InValue) { return Describe(std::wstring(InValue)); }
    std::string Describe(const char* InValue) { return Describe(std::string(InValue)); }
    std::string Describe(std::nullopt_t) { return "null"; }
    std::string Describe(bool InValue) { return InValue ? "true" : "false"; }

    template <typename T> std::string Describe(const std::optional<T>& InValue);
    template <typename T> std::string Describe(const std::vector<T>& InValues);

    template <typename T>
        requires std::is_arithmetic_v<T>
    std::string Describe(T InValue)
    {
        std::ostringstream Stream;
        Stream << +InValue;
        return Stream.str();
    }

    template <typename T>
    std::string Describe(const std::optional<T>& InValue)
    {
        return InValue ? Describe(*InValue) : "null";
    }

    template <typename T>
    std::string Describe(const std::vector<T>& InValues)
    {
        std::string Result = "[";

        for (std::size_t I = 0; I < InValues.size(); I++)
            Result += (I == 0 ? "" : ", ") + Describe(InValues[I]);

        return Result + "]";
    }

    [[noreturn]] void Fail(const std::string& InMessage, const char* InFile, int InLine)
    {
        auto Name = std::string(InFile);
        Name = Name.substr(Name.find_last_of("\\/") + 1);
        throw TestFailure{ Name + "(" + std::to_string(InLine) + "): " + InMessage };
    }

    void Check(bool InCondition, const char* InExpression, const char* InFile, int InLine)
    {
        if (!InCondition)
            Fail(std::string("CHECK(") + InExpression + ") failed", InFile, InLine);
    }

    template <typename TActual, typename TExpected>
    void CheckEqual(const TActual& InActual, const TExpected& InExpected, const char* InActualExpression, const char* InFile, int InLine)
    {
        if (!(InActual == InExpected))
            Fail(std::string(InActualExpression) + " is " + Describe(InActual) + ", expected " + Describe(InExpected), InFile, InLine);
    }

    #define TEST(Name) \
        void Name(); \
        const TestRegistrar Name##Registrar(#Name, Name); \
        void Name()

    #define CHECK(Condition) Check(static_cast<bool>(Condition), #Condition, __FILE__, __LINE__)
    #define CHECK_EQ(Actual, Expected) CheckEqual((Actual), (Expected), #Actual, __FILE__, __LINE__)
    #define SKIP(Reason) throw TestSkipped{ Reason }

    //
    // Helpers.
    //

    Bytes FromHex(std::string_view InHex)
    {
        Bytes Result;

        for (std::size_t I = 0; I + 1 < InHex.size(); I += 2)
            Result.push_back(static_cast<std::uint8_t>(std::stoul(std::string(InHex.substr(I, 2)), nullptr, 16)));

        return Result;
    }

    Bytes Ascii(std::string_view InText)
    {
        return Bytes(InText.begin(), InText.end());
    }

    template <typename T>
    void Append(Bytes& InBytes, T InValue)
    {
        auto Data = reinterpret_cast<const std::uint8_t*>(&InValue);
        InBytes.insert(InBytes.end(), Data, Data + sizeof(T));
    }

    template <typename T>
    void WriteAt(Bytes& InBytes, std::size_t InOffset, T InValue)
    {
        std::memcpy(InBytes.data() + InOffset, &InValue, sizeof(T));
    }

    void CopyAt(Bytes& InBytes, std::size_t InOffset, const Bytes& InValue)
    {
        std::copy(InValue.begin(), InValue.end(), InBytes.begin() + static_cast<std::ptrdiff_t>(InOffset));
    }

    bool IsHex(const NullableString& InText, std::size_t InLength, bool InLowerCase)
    {
        return InText && InText->size() == InLength && std::all_of(InText->begin(), InText->end(), [&](wchar_t InCharacter)
        {
            return (InCharacter >= L'0' && InCharacter <= L'9') || (InLowerCase ? InCharacter >= L'a' && InCharacter <= L'f' : InCharacter >= L'A' && InCharacter <= L'F');
        });
    }

    IpAddress ParseIp(const std::wstring& InText)
    {
        IpAddress Address;

        if (InetPtonW(AF_INET, InText.c_str(), Address.Bytes.data()) == 1)
            return Address;

        auto Percent = InText.find(L'%');
        Address.IsV6 = true;

        if (InetPtonW(AF_INET6, InText.substr(0, Percent).c_str(), Address.Bytes.data()) != 1)
            throw TestFailure{ "Not an IP address: " + EncodeUtf8(InText) };

        if (Percent != std::wstring::npos)
            Address.ScopeId = static_cast<std::uint32_t>(std::stoul(InText.substr(Percent + 1)));

        return Address;
    }

    std::uint32_t V4(const std::wstring& InText)
    {
        return ParseIp(InText).ToV4();
    }

    std::wstring DecimalSeparator()
    {
        // .NET formats with the current culture, which follows the user's locale.
        wchar_t Buffer[8] = {};
        GetLocaleInfoEx(LOCALE_NAME_USER_DEFAULT, LOCALE_SDECIMAL, Buffer, static_cast<int>(std::size(Buffer)));
        return Buffer;
    }

    std::int64_t Ticks(int InYear, int InMonth, int InDay, int InHour = 0, int InMinute = 0, int InSecond = 0, std::int64_t InExtraTicks = 0)
    {
        return MakeDate(InYear, InMonth, InDay).Ticks + ((InHour * 60LL + InMinute) * 60 + InSecond) * TicksPerSecond + InExtraTicks;
    }

    //
    // Synthetic SMBIOS tables.
    //

    class SmbiosBuilder
    {
    public:
        // Appends a structure. The formatted bytes start at offset 0x04 (right after the 4-byte header).
        SmbiosBuilder& Add(std::uint8_t InType, const Bytes& InFormatted, std::initializer_list<std::string_view> InStrings = {})
        {
            this->Data.push_back(InType);
            this->Data.push_back(static_cast<std::uint8_t>(4 + InFormatted.size()));
            Append(this->Data, this->NextHandle++);
            this->Data.insert(this->Data.end(), InFormatted.begin(), InFormatted.end());

            for (auto Text : InStrings)
            {
                this->Data.insert(this->Data.end(), Text.begin(), Text.end());
                this->Data.push_back(0);
            }

            if (InStrings.size() == 0)
                this->Data.push_back(0);

            this->Data.push_back(0);
            return *this;
        }

        SmbiosBuilder& End() { return this->Add(127, {}); }
        Bytes Build() const { return this->Data; }
        SmbiosTable BuildTable(std::uint8_t InMajor = 3, std::uint8_t InMinor = 4, std::uint8_t InRevision = 0) const { return SmbiosTable::FromData(InMajor, InMinor, InRevision, this->Data); }

        static Bytes Formatted(std::size_t InLength, std::initializer_list<std::pair<std::size_t, Bytes>> InFields)
        {
            Bytes Result(InLength);

            for (const auto& [Offset, Value] : InFields)
                std::copy(Value.begin(), Value.end(), Result.begin() + static_cast<std::ptrdiff_t>(Offset - 4));

            return Result;
        }

        template <typename T>
        static Bytes Le(T InValue)
        {
            Bytes Result;
            Append(Result, InValue);
            return Result;
        }

        static Bytes Bios() { return Formatted(0x18 - 4, { { 0x04, { 1 } }, { 0x05, { 2 } }, { 0x08, { 3 } } }); }
        static Bytes System(const Bytes& InUuid) { return Formatted(0x1B - 4, { { 0x04, { 1 } }, { 0x05, { 2 } }, { 0x06, { 3 } }, { 0x07, { 4 } }, { 0x08, InUuid } }); }
        static Bytes Baseboard() { return Formatted(0x0F - 4, { { 0x04, { 1 } }, { 0x05, { 2 } }, { 0x06, { 3 } }, { 0x07, { 4 } } }); }

        static Bytes Chassis(std::uint8_t InType, std::uint8_t InAssetTag = 4)
        {
            return Formatted(0x0D - 4, { { 0x04, { 1 } }, { 0x05, { InType } }, { 0x06, { 2 } }, { 0x07, { 3 } }, { 0x08, { InAssetTag } } });
        }

        static Bytes Processor(const Bytes& InProcessorId, std::uint8_t InVoltage, std::uint16_t InCurrentSpeed, std::uint8_t InStatus, std::uint8_t InCoreCount, std::uint8_t InThreadCount)
        {
            return Formatted(0x30 - 4,
            {
                { 0x04, { 1 } }, { 0x07, { 2 } }, { 0x08, InProcessorId }, { 0x10, { 3 } }, { 0x11, { InVoltage } },
                { 0x16, Le(InCurrentSpeed) }, { 0x18, { InStatus } }, { 0x20, { 4 } }, { 0x22, { 5 } },
                { 0x23, { InCoreCount } }, { 0x25, { InThreadCount } },
            });
        }

        static Bytes MemoryDevice(std::uint16_t InSize, std::uint32_t InExtendedSize = 0, std::uint16_t InConfiguredSpeed = 0, std::uint16_t InConfiguredVoltage = 0, std::uint8_t InStrings = 1)
        {
            auto Index = [&](std::uint8_t InIndex) { return InStrings >= InIndex ? InIndex : std::uint8_t(0); };

            return Formatted(0x28 - 4,
            {
                { 0x0C, Le(InSize) }, { 0x10, { Index(1) } }, { 0x17, { Index(2) } }, { 0x18, { Index(3) } }, { 0x1A, { Index(4) } },
                { 0x1C, Le(InExtendedSize) }, { 0x20, Le(InConfiguredSpeed) }, { 0x26, Le(InConfiguredVoltage) },
            });
        }

    private:
        Bytes Data;
        std::uint16_t NextHandle = 0;
    };

    const Bytes SampleUuid = { 0x33, 0x22, 0x11, 0x00, 0x55, 0x44, 0x77, 0x66, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };

    std::vector<int> TypesOf(const SmbiosTable& InTable)
    {
        std::vector<int> Types;

        for (const auto& Structure : InTable.Structures)
            Types.push_back(Structure.Type);

        return Types;
    }

    //
    // SMBIOS.
    //

    TEST(Smbios_ParserReadsStructuresHandlesAndStrings)
    {
        auto Table = SmbiosBuilder()
            .Add(0, SmbiosBuilder::Bios(), { "Firmware Vendor", "1.23", "01/02/2026" })
            .Add(1, SmbiosBuilder::System(SampleUuid), { "Vendor", "Product", "Version", "Serial" })
            .End()
            .BuildTable();

        CHECK_EQ(TypesOf(Table), (std::vector<int>{ 0, 1, 127 }));
        CHECK_EQ(Table.Structures[1].Handle, 1);
        CHECK_EQ(Table.Structures[2].Handle, 2);

        auto Bios = Table.OfType(0);
        CHECK_EQ(Bios.size(), 1u);
        CHECK_EQ(Bios[0]->Length, 0x18);
        CHECK_EQ(Bios[0]->GetString(0x04), L"Firmware Vendor");
        CHECK_EQ(Bios[0]->GetString(0x05), L"1.23");
        CHECK_EQ(Bios[0]->GetString(0x08), L"01/02/2026");
    }

    TEST(Smbios_ParserHandlesStructuresWithoutStrings)
    {
        auto Table = SmbiosBuilder()
            .Add(3, Bytes(10))
            .Add(1, SmbiosBuilder::System(SampleUuid), { "Vendor", "Product", "Version", "Serial" })
            .End()
            .BuildTable();

        CHECK_EQ(TypesOf(Table), (std::vector<int>{ 3, 1, 127 }));
        CHECK(Table.OfType(3)[0]->Strings.empty());
        CHECK_EQ(Table.OfType(1)[0]->GetString(0x07), L"Serial");
    }

    TEST(Smbios_ParserStopsAtEndOfTableAndSurvivesTruncation)
    {
        auto Data = SmbiosBuilder().Add(0, SmbiosBuilder::Bios(), { "V", "1", "D" }).End().Build();
        Data.insert(Data.end(), { 4, 0x30, 0, 0 });
        CHECK_EQ(TypesOf(SmbiosTable::FromData(2, 8, 0, Data)), (std::vector<int>{ 0, 127 }));

        auto Truncated = SmbiosBuilder().Add(0, SmbiosBuilder::Bios(), { "Vendor" }).Build();
        Truncated.resize(12);
        SmbiosTable::FromData(2, 8, 0, Truncated);
        SmbiosTable::FromData(2, 8, 0, {});
        SmbiosTable::FromData(2, 8, 0, { 1, 2, 3 });
    }

    TEST(Smbios_FieldAccessorsAreBoundsChecked)
    {
        auto Table = SmbiosBuilder().Add(1, SmbiosBuilder::System(SampleUuid), { "Vendor" }).BuildTable();
        auto Structure = Table.OfType(1)[0];

        CHECK_EQ(Structure->GetString(0x04), L"Vendor");
        CHECK_EQ(Structure->GetString(0x05), std::nullopt);
        CHECK_EQ(Structure->GetString(0x7F), std::nullopt);
        CHECK_EQ(Structure->GetByte(0x7F), 0);
        CHECK_EQ(Structure->GetWord(0x1A), 0);
        CHECK_EQ(Structure->GetDword(0x19), 0u);
        CHECK_EQ(Structure->GetBytes(0x10, 16), std::nullopt);
        CHECK(Structure->GetBytes(0x08, 16) == SampleUuid);
    }

    TEST(Smbios_DecodesNonAsciiStringsAsLatin1)
    {
        auto Table = SmbiosTable::FromData(2, 8, 0, { 0, 0x08, 0, 0, 1, 0, 0, 0, 0x41, 0xE9, 0x00, 0x00 });
        CHECK_EQ(Table.OfType(0)[0]->GetString(0x04), L"A\x00E9");
    }

    TEST(Smbios_CollectorsMapSystemBaseboardAndBiosFields)
    {
        auto Table = SmbiosBuilder()
            .Add(0, SmbiosBuilder::Bios(), { "Firmware Vendor", "F.12", "01/02/2026" })
            .Add(1, SmbiosBuilder::System(SampleUuid), { "System Vendor", "System Product", "System Version", "SYS-SERIAL" })
            .Add(2, SmbiosBuilder::Baseboard(), { "Board Vendor", "Board Product", "Board Version", "BOARD-SERIAL" })
            .End()
            .BuildTable(3, 2, 1);

        Hwid Hwid;
        RetrieveMotherBoards(Hwid, &Table);
        RetrieveBaseBoards(Hwid, &Table);
        RetrieveFirmwares(Hwid, &Table);
        RetrieveSmbiosTables(Hwid, &Table);

        CHECK_EQ(Hwid.Motherboards.size(), 1u);
        CHECK_EQ(Hwid.Motherboards[0].Name, L"System Product");
        CHECK_EQ(Hwid.Motherboards[0].Vendor, L"System Vendor");
        CHECK_EQ(Hwid.Motherboards[0].Version, L"System Version");
        CHECK_EQ(Hwid.Motherboards[0].UUID, L"00112233-4455-6677-8899-aabbccddeeff");

        CHECK_EQ(Hwid.Baseboards.size(), 1u);
        CHECK_EQ(Hwid.Baseboards[0].Manufacturer, L"Board Vendor");
        CHECK_EQ(Hwid.Baseboards[0].Model, L"Board Product");
        CHECK_EQ(Hwid.Baseboards[0].Version, L"Board Version");
        CHECK_EQ(Hwid.Baseboards[0].SerialNumber, L"BOARD-SERIAL");

        CHECK_EQ(Hwid.BiosFirmwares.size(), 1u);
        CHECK_EQ(Hwid.BiosFirmwares[0].Manufacturer, L"Firmware Vendor");
        CHECK_EQ(Hwid.BiosFirmwares[0].SerialNumber, L"SYS-SERIAL");
        CHECK(!IsNullOrWhiteSpace(Hwid.BiosFirmwares[0].Version));

        CHECK_EQ(Hwid.SmbiosTables.size(), 1u);
        CHECK_EQ(Hwid.SmbiosTables[0].Version, L"3.2.1");
        CHECK_EQ(Hwid.SmbiosTables[0].Length, static_cast<std::uint32_t>(Table.Data.size()));
        CHECK(IsHex(Hwid.SmbiosTables[0].Hash, 64, true));
        CHECK_EQ(Hwid.SmbiosTables[0].Hash, Sha256Hex(Table.Data));
    }

    TEST(Smbios_CollectorsMapChassisFieldsAndStripTheLockBit)
    {
        auto Table = SmbiosBuilder()
            .Add(3, SmbiosBuilder::Chassis(0x8A), { "Chassis Vendor", "A00", "CHASSIS-SERIAL", "ASSET-42" })
            .Add(3, SmbiosBuilder::Chassis(0x03, 0), { "Board Vendor", "1.0", "BOARD-SERIAL" })
            .End()
            .BuildTable();

        Hwid Hwid;
        RetrieveChassis(Hwid, &Table);

        CHECK_EQ(Hwid.Chassis.size(), 2u);
        CHECK_EQ(Hwid.Chassis[0].Id, 0);
        CHECK_EQ(Hwid.Chassis[0].Manufacturer, L"Chassis Vendor");
        CHECK_EQ(Hwid.Chassis[0].Type, 10);
        CHECK_EQ(Hwid.Chassis[0].TypeName, L"Notebook");
        CHECK_EQ(Hwid.Chassis[0].Version, L"A00");
        CHECK_EQ(Hwid.Chassis[0].SerialNumber, L"CHASSIS-SERIAL");
        CHECK_EQ(Hwid.Chassis[0].AssetTag, L"ASSET-42");
        CHECK_EQ(Hwid.Chassis[1].Id, 1);
        CHECK_EQ(Hwid.Chassis[1].Type, 3);
        CHECK_EQ(Hwid.Chassis[1].TypeName, L"Desktop");
        CHECK_EQ(Hwid.Chassis[1].AssetTag, std::nullopt);
    }

    TEST(Smbios_ChassisTypeNamesFollowTheSpecification)
    {
        CHECK_EQ(GetChassisTypeName(1), L"Other");
        CHECK_EQ(GetChassisTypeName(3), L"Desktop");
        CHECK_EQ(GetChassisTypeName(9), L"Laptop");
        CHECK_EQ(GetChassisTypeName(10), L"Notebook");
        CHECK_EQ(GetChassisTypeName(23), L"Rack Mount Chassis");
        CHECK_EQ(GetChassisTypeName(31), L"Convertible");
        CHECK_EQ(GetChassisTypeName(36), L"Stick PC");
        CHECK_EQ(GetChassisTypeName(0), L"Unknown");
        CHECK_EQ(GetChassisTypeName(2), L"Unknown");
        CHECK_EQ(GetChassisTypeName(99), L"Unknown");
    }

    TEST(Smbios_CollectorsMapProcessorFieldsAndSkipEmptySockets)
    {
        Bytes ProcessorId = { 0xEA, 0x06, 0x09, 0x00, 0xFF, 0xFB, 0xEB, 0xBF };
        auto Table = SmbiosBuilder()
            .Add(4, SmbiosBuilder::Processor(ProcessorId, 0x8B, 3600, 0x41, 8, 16), { "CPU 0", "Synthetic Corporation", "Synthetic CPU", "CPU-SERIAL", "CPU-PART" })
            .Add(4, SmbiosBuilder::Processor(ProcessorId, 0x00, 0, 0x00, 0, 0), { "CPU 1", "Synthetic Corporation", "Not Specified", "", "" })
            .End()
            .BuildTable();

        Hwid Hwid;
        RetrieveProcessors(Hwid, &Table);

        CHECK_EQ(Hwid.Processors.size(), 1u);
        const auto& Processor = Hwid.Processors[0];
        CHECK_EQ(Processor.Socket, L"CPU 0");
        CHECK_EQ(Processor.SerialNumber, L"CPU-SERIAL");
        CHECK_EQ(Processor.PartNumber, L"CPU-PART");
        CHECK_EQ(Processor.Channel, L"CPU0");
        CHECK_EQ(Processor.Voltage, L"1" + DecimalSeparator() + L"1 V");
        CHECK_EQ(Processor.ModelNumber, L"BFEBFBFF000906EA");
        CHECK(Processor.ClockSpeed && Processor.ClockSpeed->ends_with(L" MHz") && (*Processor.ClockSpeed)[0] >= L'1' && (*Processor.ClockSpeed)[0] <= L'9');
        CHECK(!IsNullOrWhiteSpace(Processor.Manufacturer));
        CHECK(!IsNullOrWhiteSpace(Processor.Model));
        CHECK(Processor.NumberOfCores >= 1);
        CHECK(Processor.NumberOfLogicalProcessors >= Processor.NumberOfCores);
    }

    TEST(Smbios_CollectorsReportTheProcessorIdEvenWhenZeroed)
    {
        // Hyper-V leaves the SMBIOS processor ID zeroed; WMI reports it as is, so the collector does too instead of reading CPUID.
        auto Table = SmbiosBuilder()
            .Add(4, SmbiosBuilder::Processor(Bytes(8), 0x00, 2400, 0x41, 4, 8), { "CPU 0", "GenuineIntel", "Synthetic CPU", "", "" })
            .End()
            .BuildTable();

        Hwid Hwid;
        RetrieveProcessors(Hwid, &Table);

        CHECK_EQ(Hwid.Processors.size(), 1u);
        CHECK_EQ(Hwid.Processors[0].ModelNumber, L"0000000000000000");
    }

    TEST(Smbios_CollectorsFallBackToCpuIdWhenTheProcessorRecordHasNoId)
    {
        // SMBIOS 2.0 records shorter than 0x10 bytes carry no processor ID.
        auto Table = SmbiosBuilder()
            .Add(4, SmbiosBuilder::Formatted(0x0C - 4, { { 0x04, { 1 } }, { 0x07, { 2 } } }), { "CPU 0", "GenuineIntel" })
            .End()
            .BuildTable();

        Hwid Hwid;
        RetrieveProcessors(Hwid, &Table);

        CHECK_EQ(Hwid.Processors.size(), 1u);
        CHECK_EQ(Hwid.Processors[0].ModelNumber, GetProcessorIdFromCpuId());
    }

    TEST(Smbios_CollectorsMapMemoryDevicesAndSkipEmptySlots)
    {
        auto Table = SmbiosBuilder()
            .Add(17, SmbiosBuilder::MemoryDevice(0x4000, 0, 3200, 1200, 4), { "DIMM_A1", "Kingston", "MEM-SERIAL-1", "KF3200C16D4/16GX" })
            .Add(17, SmbiosBuilder::MemoryDevice(0x0000, 0, 0, 0, 1), { "DIMM_A2" })
            .Add(17, SmbiosBuilder::MemoryDevice(0x7FFF, 65536, 2133, 1350, 4), { "DIMM_B1", "Samsung", "MEM-SERIAL-2", "M393A8G40MB2" })
            .Add(17, SmbiosBuilder::MemoryDevice(0x8200, 0, 0, 0, 4), { "DIMM_B2", "Legacy", "MEM-SERIAL-3", "PART" })
            .End()
            .BuildTable();

        Hwid Hwid;
        RetrieveMemorySticks(Hwid, &Table);

        CHECK_EQ(Hwid.MemorySticks.size(), 3u);
        CHECK_EQ(Hwid.MemorySticks[2].Id, 2);

        const auto& First = Hwid.MemorySticks[0];
        CHECK_EQ(First.Channel, L"DIMM_A1");
        CHECK_EQ(First.Manufacturer, L"Kingston");
        CHECK_EQ(First.SerialNumber, L"MEM-SERIAL-1");
        CHECK_EQ(First.PartNumber, L"KF3200C16D4/16GX");
        CHECK_EQ(First.Capacity, L"16 GB");
        CHECK_EQ(First.ClockSpeed, L"3200 MHz");
        CHECK_EQ(First.Voltage, L"1" + DecimalSeparator() + L"20 V");

        CHECK_EQ(Hwid.MemorySticks[1].Capacity, L"64 GB");
        CHECK_EQ(Hwid.MemorySticks[1].Voltage, L"1" + DecimalSeparator() + L"35 V");
        CHECK_EQ(Hwid.MemorySticks[2].Capacity, L"0 GB");
    }

    TEST(Smbios_CollectorsTolerateAMissingTable)
    {
        Hwid Hwid;
        RetrieveMotherBoards(Hwid, nullptr);
        RetrieveBaseBoards(Hwid, nullptr);
        RetrieveChassis(Hwid, nullptr);
        RetrieveFirmwares(Hwid, nullptr);
        RetrieveSmbiosTables(Hwid, nullptr);
        RetrieveProcessors(Hwid, nullptr);
        RetrieveMemorySticks(Hwid, nullptr);

        CHECK(Hwid.Motherboards.empty() && Hwid.Baseboards.empty() && Hwid.Chassis.empty() && Hwid.BiosFirmwares.empty());
        CHECK(Hwid.SmbiosTables.empty() && Hwid.Processors.empty() && Hwid.MemorySticks.empty());
    }

    TEST(Smbios_FormatProcessorIdPutsEdxBeforeEax)
    {
        CHECK_EQ(FormatProcessorId(Bytes{ 0xEA, 0x06, 0x09, 0x00, 0xFF, 0xFB, 0xEB, 0xBF }), L"BFEBFBFF000906EA");
        CHECK_EQ(FormatProcessorId(std::nullopt), std::nullopt);
        CHECK_EQ(FormatProcessorId(Bytes(4)), std::nullopt);
    }

    TEST(Smbios_ProcessorIdFromCpuIdIsHexadecimal)
    {
    #if defined(_M_X64) || defined(_M_IX86)
        CHECK(IsHex(GetProcessorIdFromCpuId(), 16, false));
    #else
        CHECK_EQ(GetProcessorIdFromCpuId(), std::nullopt);
    #endif
    }

    TEST(Smbios_ProcessorVoltageDecodesTheVoltageByte)
    {
        auto Separator = DecimalSeparator();
        CHECK_EQ(FormatProcessorVoltage(0x8B), L"1" + Separator + L"1 V");
        CHECK_EQ(FormatProcessorVoltage(0x92), L"1" + Separator + L"8 V");
        CHECK_EQ(FormatProcessorVoltage(0x01), L"5" + Separator + L"0 V");
        CHECK_EQ(FormatProcessorVoltage(0x02), L"3" + Separator + L"3 V");
        CHECK_EQ(FormatProcessorVoltage(0x04), L"2" + Separator + L"9 V");
        CHECK_EQ(FormatProcessorVoltage(0x00), L"0" + Separator + L"0 V");
    }

    TEST(Smbios_ProcessorTopologyAgreesWithWindows)
    {
        auto Topology = GetProcessorTopology();

        CHECK(Topology.Cores >= 1);
        CHECK(Topology.Packages >= 1);
        CHECK(Topology.LogicalProcessors >= Topology.Cores);
        CHECK_EQ(static_cast<DWORD>(Topology.LogicalProcessors), GetActiveProcessorCount(ALL_PROCESSOR_GROUPS));
    }

    TEST(Smbios_ReadReturnsTheFirmwareTable)
    {
        auto Table = SmbiosTable::Read();

        CHECK(Table.has_value());
        CHECK(Table->MajorVersion >= 2);
        CHECK(!Table->Data.empty());
        CHECK(!Table->OfType(0).empty());
        CHECK(!Table->OfType(1).empty());
        CHECK(!Table->OfType(4).empty());
    }

    //
    // Storage.
    //

    struct IdentifierSpec
    {
        int CodeSet;
        int Type;
        int Association;
        Bytes Value;
    };

    // A STORAGE_DEVICE_ID_DESCRIPTOR holding the given STORAGE_IDENTIFIER entries, laid out like Windows does (16-byte headers, 4-byte aligned).
    Bytes BuildDeviceIdDescriptor(const std::vector<IdentifierSpec>& InIdentifiers)
    {
        Bytes Result;
        Append(Result, 16u);
        Append(Result, 0u);
        Append(Result, static_cast<std::uint32_t>(InIdentifiers.size()));

        for (std::size_t I = 0; I < InIdentifiers.size(); I++)
        {
            const auto& Identifier = InIdentifiers[I];
            auto Padded = (16 + Identifier.Value.size() + 3) / 4 * 4;

            Append(Result, static_cast<std::uint32_t>(Identifier.CodeSet));
            Append(Result, static_cast<std::uint32_t>(Identifier.Type));
            Append(Result, static_cast<std::uint16_t>(Identifier.Value.size()));
            Append(Result, static_cast<std::uint16_t>(I == InIdentifiers.size() - 1 ? 0 : Padded));
            Append(Result, static_cast<std::uint32_t>(Identifier.Association));
            Result.insert(Result.end(), Identifier.Value.begin(), Identifier.Value.end());
            Result.insert(Result.end(), Padded - 16 - Identifier.Value.size(), 0);
        }

        WriteAt(Result, 4, static_cast<std::uint32_t>(Result.size()));
        return Result;
    }

    // An ATA IDENTIFY string: the characters swapped within each 16-bit word, padded with spaces.
    Bytes AtaString(std::string InText, std::size_t InWords)
    {
        InText.resize(InWords * 2, ' ');
        Bytes Result(InWords * 2);

        for (std::size_t I = 0; I < InWords; I++)
        {
            Result[I * 2] = static_cast<std::uint8_t>(InText[I * 2 + 1]);
            Result[I * 2 + 1] = static_cast<std::uint8_t>(InText[I * 2]);
        }

        return Result;
    }

    TEST(Storage_ParseDeviceIdentifiersDecodesEveryDescriptorType)
    {
        auto Name = Ascii("eui.0025385A1B2C3D4E");
        Name.push_back(0);

        auto Identifiers = ParseDeviceIdentifiers(BuildDeviceIdDescriptor(
        {
            { ScsiCodeSetBinary, ScsiTypeEui64, 0, { 0x00, 0x25, 0x38, 0x5A, 0x1B, 0x2C, 0x3D, 0x4E } },
            { ScsiCodeSetBinary, ScsiTypeNaa, 0, { 0x50, 0x02, 0x53, 0x8D, 0x11, 0x22, 0x33, 0x44 } },
            { ScsiCodeSetAscii, ScsiTypeT10VendorId, 0, Ascii("NVMe    Synthetic NVMe Disk     SYNTH0N9X8Y7W6V ") },
            { ScsiCodeSetUtf8, ScsiTypeScsiNameString, 0, Name },
            { ScsiCodeSetBinary, 4, 1, { 0x00, 0x01 } },
        }));

        CHECK_EQ(Identifiers.size(), 5u);
        CHECK_EQ(Identifiers[0].Text(), L"0025385A1B2C3D4E");
        CHECK_EQ(Identifiers[0].Type, ScsiTypeEui64);
        CHECK_EQ(Identifiers[1].Text(), L"5002538D11223344");
        CHECK_EQ(Identifiers[2].Text(), L"NVMe    Synthetic NVMe Disk     SYNTH0N9X8Y7W6V");
        CHECK_EQ(Identifiers[3].Text(), L"eui.0025385A1B2C3D4E");
        CHECK_EQ(Identifiers[4].Association, 1);

        for (std::size_t I = 0; I < 4; I++)
            CHECK_EQ(Identifiers[I].Association, ScsiAssociationLogicalUnit);
    }

    TEST(Storage_ParseDeviceIdentifiersDecodesASataDescriptor)
    {
        // A SATA SSD behind storahci reports one NAA identifier translated from the ATA World Wide Name.
        auto Identifiers = ParseDeviceIdentifiers(FromHex("100000002800000001000000" "01000000030000000800" "1C00" "00000000" "5002538D1122334400000000"));

        CHECK_EQ(Identifiers.size(), 1u);
        CHECK_EQ(Identifiers[0].CodeSet, ScsiCodeSetBinary);
        CHECK_EQ(Identifiers[0].Type, ScsiTypeNaa);
        CHECK_EQ(Identifiers[0].Association, ScsiAssociationLogicalUnit);
        CHECK_EQ(Identifiers[0].Text(), L"5002538D11223344");
    }

    TEST(Storage_ParseDeviceIdentifiersDecodesAnNvmeDescriptor)
    {
        // An NVMe SSD behind stornvme reports one SCSI name string built from the namespace EUI-64.
        auto Identifiers = ParseDeviceIdentifiers(FromHex("100000003400000001000000" "03000000080000001400" "2800" "00000000" "6575692E30303235333835413142324333443445" "00000000"));

        CHECK_EQ(Identifiers.size(), 1u);
        CHECK_EQ(Identifiers[0].CodeSet, ScsiCodeSetUtf8);
        CHECK_EQ(Identifiers[0].Type, ScsiTypeScsiNameString);
        CHECK_EQ(Identifiers[0].Text(), L"eui.0025385A1B2C3D4E");
    }

    TEST(Storage_ParseDeviceIdentifiersToleratesEmptyAndTruncatedDescriptors)
    {
        CHECK(ParseDeviceIdentifiers({}).empty());
        CHECK(ParseDeviceIdentifiers(Bytes(12)).empty());

        auto Empty = ParseDeviceIdentifiers(BuildDeviceIdDescriptor({ { ScsiCodeSetAscii, ScsiTypeVendorSpecific, 0, {} } }));
        CHECK_EQ(Empty.size(), 1u);
        CHECK_EQ(Empty[0].Text(), L"");

        auto Truncated = BuildDeviceIdDescriptor({ { ScsiCodeSetBinary, ScsiTypeNaa, 0, Bytes(8) } });
        Truncated.resize(32);
        auto Partial = ParseDeviceIdentifiers(Truncated);
        CHECK_EQ(Partial.size(), 1u);
        CHECK_EQ(Partial[0].Value.size(), 4u);

        auto Lying = BuildDeviceIdDescriptor({ { ScsiCodeSetBinary, ScsiTypeNaa, 0, Bytes(8) } });
        WriteAt(Lying, 8, 50u);
        CHECK_EQ(ParseDeviceIdentifiers(Lying).size(), 1u);
    }

    TEST(Storage_ParseNvmeControllerIdentityReadsSerialModelFirmwareAndFruGuid)
    {
        Bytes Data(4096);
        CopyAt(Data, 4, Ascii("SYNTH0N9X8Y7W6V     "));
        CopyAt(Data, 24, Ascii("Synthetic NVMe Disk 1TB                 "));
        CopyAt(Data, 64, Ascii("1B2QJXD7"));
        Data[112] = 0xAB;
        Data[127] = 0xCD;

        auto Identity = ParseNvmeControllerIdentity(Data);

        CHECK(Identity.has_value());
        CHECK_EQ(Identity->SerialNumber, L"SYNTH0N9X8Y7W6V");
        CHECK_EQ(Identity->ModelNumber, L"Synthetic NVMe Disk 1TB");
        CHECK_EQ(Identity->FirmwareRevision, L"1B2QJXD7");
        CHECK_EQ(Identity->FruGuid, L"AB0000000000000000000000000000CD");
    }

    TEST(Storage_ParseNvmeControllerIdentityReturnsNullFieldsWhenBlank)
    {
        auto Identity = ParseNvmeControllerIdentity(Bytes(4096));

        CHECK(Identity.has_value());
        CHECK_EQ(Identity->SerialNumber, std::nullopt);
        CHECK_EQ(Identity->FruGuid, std::nullopt);
        CHECK(!ParseNvmeControllerIdentity(Bytes(64)).has_value());
    }

    TEST(Storage_ParseNvmeNamespaceIdentityReadsNguidAndEui64)
    {
        Bytes Data(4096);

        for (std::size_t I = 0; I < 16; I++)
            Data[104 + I] = static_cast<std::uint8_t>(0x10 + I);

        CopyAt(Data, 120, { 0x00, 0x25, 0x38, 0x5A, 0x1B, 0x2C, 0x3D, 0x4E });

        auto Identity = ParseNvmeNamespaceIdentity(Data);

        CHECK(Identity.has_value());
        CHECK_EQ(Identity->Nguid, L"101112131415161718191A1B1C1D1E1F");
        CHECK_EQ(Identity->Eui64, L"0025385A1B2C3D4E");
        CHECK_EQ(ParseNvmeNamespaceIdentity(Bytes(4096))->Eui64, std::nullopt);
    }

    TEST(Storage_ParseAtaIdentitySwapsCharactersAndReadsTheWorldWideName)
    {
        Bytes Data(512);
        CopyAt(Data, 10 * 2, AtaString("SYN4ATA0000001X", 10));
        CopyAt(Data, 23 * 2, AtaString("SYN04B6Q", 4));
        CopyAt(Data, 27 * 2, AtaString("Synthetic SATA SSD 512GB", 20));
        WriteAt(Data, 84 * 2, std::uint16_t(0x0100));
        WriteAt(Data, 108 * 2, std::uint16_t(0x5002));
        WriteAt(Data, 109 * 2, std::uint16_t(0x538D));
        WriteAt(Data, 110 * 2, std::uint16_t(0xA1B2));
        WriteAt(Data, 111 * 2, std::uint16_t(0xC3D4));

        auto Identity = ParseAtaIdentity(Data);

        CHECK(Identity.has_value());
        CHECK_EQ(Identity->SerialNumber, L"SYN4ATA0000001X");
        CHECK_EQ(Identity->FirmwareRevision, L"SYN04B6Q");
        CHECK_EQ(Identity->ModelNumber, L"Synthetic SATA SSD 512GB");
        CHECK_EQ(Identity->WorldWideName, L"5002538DA1B2C3D4");
    }

    TEST(Storage_ParseAtaIdentityOmitsAnUnsupportedWorldWideName)
    {
        Bytes Data(512);
        CopyAt(Data, 10 * 2, AtaString("WD-SYNTH0000001", 10));
        WriteAt(Data, 108 * 2, std::uint16_t(0x5001));

        auto Identity = ParseAtaIdentity(Data);

        CHECK(Identity.has_value());
        CHECK_EQ(Identity->SerialNumber, L"WD-SYNTH0000001");
        CHECK_EQ(Identity->WorldWideName, std::nullopt);
        CHECK(!ParseAtaIdentity(Bytes(511)).has_value());
    }

    TEST(Storage_ParseDiskIdentifierReturnsTheGptGuidOrMbrSignature)
    {
        Bytes Gpt(48);
        WriteAt(Gpt, 0, 1u);
        CopyAt(Gpt, 8, { 0x2B, 0x1A, 0x7C, 0x5E, 0x4F, 0x3D, 0x6B, 0x4A, 0x8C, 0x9D, 0x0E, 0x1F, 0x2A, 0x3B, 0x4C, 0x5D });
        CHECK_EQ(ParseDiskIdentifier(Gpt), L"5e7c1a2b-3d4f-4a6b-8c9d-0e1f2a3b4c5d");

        Bytes Mbr(48);
        WriteAt(Mbr, 8, 0x1234ABCDu);
        CHECK_EQ(ParseDiskIdentifier(Mbr), L"0x1234ABCD");

        CHECK_EQ(ParseDiskIdentifier(Bytes(48)), std::nullopt);
        CHECK_EQ(ParseDiskIdentifier(Bytes(8)), std::nullopt);

        Bytes Raw(48);
        WriteAt(Raw, 0, 2u);
        CHECK_EQ(ParseDiskIdentifier(Raw), std::nullopt);
    }

    TEST(Storage_DiskInterfaceTypeMapsTheEnumeratorThenTheBusType)
    {
        CHECK_EQ(GetDiskInterfaceType(LR"(USBSTOR\DISK&VEN_SYNTH&PROD_FLASH\0000000000000001&0)", 7u), L"USB");
        CHECK_EQ(GetDiskInterfaceType(LR"(SCSI\DISK&VEN_NVME&PROD_SYNTH\5&1&0&0)", 17u), L"SCSI");
        CHECK_EQ(GetDiskInterfaceType(LR"(IDE\DISKSYNTH_DISK\5&1&0&0.0.0)", 3u), L"IDE");
        CHECK_EQ(GetDiskInterfaceType(LR"(SD\DISK&GENERIC&SD\1&2&3)", 12u), L"SD");
        CHECK_EQ(GetDiskInterfaceType(std::nullopt, 17u), L"SCSI");
        CHECK_EQ(GetDiskInterfaceType(std::nullopt, 11u), L"SCSI");
        CHECK_EQ(GetDiskInterfaceType(std::nullopt, 3u), L"IDE");
        CHECK_EQ(GetDiskInterfaceType(std::nullopt, 7u), L"USB");
        CHECK_EQ(GetDiskInterfaceType(std::nullopt, 4u), L"1394");
        CHECK_EQ(GetDiskInterfaceType(std::nullopt, std::nullopt), L"Unknown");
    }

    //
    // Displays and peripherals.
    //

    Bytes BuildEdid(std::uint16_t InManufacturerId, std::uint16_t InProductCode, std::uint32_t InSerial, const char* InName, const char* InSerialText)
    {
        Bytes Edid(128);
        CopyAt(Edid, 0, { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 });
        Edid[8] = static_cast<std::uint8_t>(InManufacturerId >> 8);
        Edid[9] = static_cast<std::uint8_t>(InManufacturerId & 0xFF);
        Edid[10] = static_cast<std::uint8_t>(InProductCode & 0xFF);
        Edid[11] = static_cast<std::uint8_t>(InProductCode >> 8);
        WriteAt(Edid, 12, InSerial);

        auto WriteDescriptor = [&](std::size_t InOffset, std::uint8_t InTag, std::string InText)
        {
            Edid[InOffset + 3] = InTag;
            InText += '\n';
            InText.resize(13, ' ');
            CopyAt(Edid, InOffset + 5, Ascii(InText));
        };

        if (InName != nullptr)
            WriteDescriptor(54, 0xFC, InName);

        if (InSerialText != nullptr)
            WriteDescriptor(InName != nullptr ? 72 : 54, 0xFF, InSerialText);

        return Edid;
    }

    TEST(Edid_DecodesTheManufacturerProductAndDescriptors)
    {
        auto Info = ParseEdid(BuildEdid(0x10AC, 0x4070, 0x12345678, "DELL U2415", "SYNTH123XYZ"));

        CHECK(Info.has_value());
        CHECK_EQ(Info->Manufacturer, L"DEL");
        CHECK_EQ(Info->ProductCode, L"4070");
        CHECK_EQ(Info->Name, L"DELL U2415");
        CHECK_EQ(Info->SerialNumber, L"SYNTH123XYZ");
    }

    TEST(Edid_FallsBackToTheNumericSerial)
    {
        auto Info = ParseEdid(BuildEdid(0x4C2D, 0x0BC3, 305419896, "S24E450", nullptr));

        CHECK(Info.has_value());
        CHECK_EQ(Info->Manufacturer, L"SAM");
        CHECK_EQ(Info->ProductCode, L"0BC3");
        CHECK_EQ(Info->SerialNumber, L"305419896");
    }

    TEST(Edid_ReportsTheNumericSerialEvenWhenZero)
    {
        auto Info = ParseEdid(BuildEdid(0x1E6D, 0x0001, 0, nullptr, nullptr));

        CHECK(Info.has_value());
        CHECK_EQ(Info->Manufacturer, L"GSM");
        CHECK_EQ(Info->ProductCode, L"0001");
        CHECK_EQ(Info->Name, L"");
        CHECK_EQ(Info->SerialNumber, L"0");
    }

    TEST(Edid_TrimsTheDescriptorPadding)
    {
        auto Info = ParseEdid(BuildEdid(0x10AC, 0x4070, 1, "X", "Y"));

        CHECK(Info.has_value());
        CHECK_EQ(Info->Name, L"X");
        CHECK_EQ(Info->SerialNumber, L"Y");
    }

    TEST(Edid_RejectsInvalidInput)
    {
        CHECK(!ParseEdid(std::nullopt).has_value());
        CHECK(!ParseEdid(Bytes(127)).has_value());
        CHECK(!ParseEdid(Bytes(128)).has_value());

        auto Corrupted = BuildEdid(0x10AC, 0x4070, 1, "X", "Y");
        Corrupted[0] = 0x01;
        CHECK(!ParseEdid(Corrupted).has_value());
    }

    TEST(Edid_HashesTheBaseBlockAndReadsTheManufactureDate)
    {
        auto Edid = BuildEdid(0x10AC, 0x4070, 1, "Name", "Serial");
        Edid[16] = 12;
        Edid[17] = 30;

        auto Info = ParseEdid(Edid);

        CHECK(Info.has_value());
        CHECK_EQ(Info->ManufactureWeek, 12);
        CHECK_EQ(Info->ManufactureYear, 2020);
        CHECK(IsHex(Info->Hash, 64, true));
        CHECK_EQ(Info->Hash, Sha256Hex(Edid));

        auto Extended = Edid;
        Extended.resize(256);
        Extended[200] = 0xAA;
        CHECK_EQ(ParseEdid(Extended)->Hash, Info->Hash);
        CHECK_EQ(ParseEdid(Extended)->Name, L"Name");
    }

    TEST(Edid_ReportsUnknownManufactureDates)
    {
        auto Edid = BuildEdid(0x10AC, 0x4070, 1, nullptr, nullptr);
        Edid[16] = 0xFF;
        Edid[17] = 0;

        auto Info = ParseEdid(Edid);

        CHECK(Info.has_value());
        CHECK_EQ(Info->ManufactureWeek, 0);
        CHECK_EQ(Info->ManufactureYear, 0);
    }

    TEST(Peripherals_BluetoothAddressIsMostSignificantByteFirst)
    {
        CHECK(BluetoothAddressToBytes(0x02A1B2C3D4E5) == (Bytes{ 0x02, 0xA1, 0xB2, 0xC3, 0xD4, 0xE5 }));
        CHECK_EQ(FormatMacAddress(BluetoothAddressToBytes(0x02A1B2C3D4E5)), L"02:A1:B2:C3:D4:E5");
    }

    TEST(Peripherals_BatteryManufactureDateIsValidated)
    {
        CHECK_EQ(ParseBatteryManufactureDate(15, 6, 2023)->Ticks, Ticks(2023, 6, 15));
        CHECK_EQ(ParseBatteryManufactureDate(1, 1, 1980)->Ticks, Ticks(1980, 1, 1));
        CHECK_EQ(ParseBatteryManufactureDate(29, 2, 2024)->Ticks, Ticks(2024, 2, 29));
        CHECK(!ParseBatteryManufactureDate(0, 0, 0).has_value());
        CHECK(!ParseBatteryManufactureDate(31, 2, 2023).has_value());
        CHECK(!ParseBatteryManufactureDate(29, 2, 2023).has_value());
        CHECK(!ParseBatteryManufactureDate(1, 13, 2023).has_value());
        CHECK(!ParseBatteryManufactureDate(1, 1, 1979).has_value());
        CHECK(!ParseBatteryManufactureDate(15, 6, 2023)->IsLocal);
    }

    TEST(Peripherals_InterfaceNameToInstanceIdStripsThePrefixAndClassGuid)
    {
        CHECK_EQ(InterfaceNameToInstanceId(LR"(\\?\PCI#VEN_1AF4&DEV_1050&SUBSYS_11001AF4&REV_01#3&2A4C6E81&0&10#{5b45201d-f2f2-4f3b-85bb-30ff1f953599})"), LR"(PCI\VEN_1AF4&DEV_1050&SUBSYS_11001AF4&REV_01\3&2A4C6E81&0&10)");
        CHECK_EQ(InterfaceNameToInstanceId(LR"(\\?\SWD#REMOTEDISPLAYENUM#RDPIDD_INDIRECTDISPLAY&SESSIONID_0002#{5b45201d-f2f2-4f3b-85bb-30ff1f953599})"), LR"(SWD\REMOTEDISPLAYENUM\RDPIDD_INDIRECTDISPLAY&SESSIONID_0002)");
        CHECK_EQ(InterfaceNameToInstanceId(LR"(PCI#VEN_10DE&DEV_2206#4&2B0E5B6F&0&0008)"), LR"(PCI\VEN_10DE&DEV_2206\4&2B0E5B6F&0&0008)");
        CHECK_EQ(InterfaceNameToInstanceId(std::nullopt), std::nullopt);
        CHECK_EQ(InterfaceNameToInstanceId(L""), std::nullopt);
    }

    //
    // Network.
    //

    TEST(Network_IpAddressesAreFormattedLikeDotNet)
    {
        // Each expected value is what .NET's IPAddress.ToString() prints.
        const std::pair<const wchar_t*, const wchar_t*> Cases[] =
        {
            { L"192.168.1.1", L"192.168.1.1" },
            { L"::", L"::" },
            { L"::1", L"::1" },
            { L"2001:db8::1", L"2001:db8::1" },
            { L"2001:0db8:0000:0000:0001:0000:0000:0001", L"2001:db8::1:0:0:1" },
            { L"2001:db8:0:1:1:1:1:1", L"2001:db8:0:1:1:1:1:1" },
            { L"fe80::1234:5678:9abc:def0", L"fe80::1234:5678:9abc:def0" },
            { L"::ffff:192.168.1.1", L"::ffff:192.168.1.1" },
            { L"::192.168.1.1", L"::192.168.1.1" },
            { L"::ffff:0:10.0.0.1", L"::ffff:0:10.0.0.1" },
            { L"fe80::5efe:10.1.2.3", L"fe80::5efe:10.1.2.3" },
            { L"::5efe:10.1.2.3", L"::5efe:10.1.2.3" },
            { L"1:0:0:2:0:0:0:3", L"1:0:0:2::3" },
            { L"1:0:0:2:0:0:3:4", L"1::2:0:0:3:4" },
            { L"::0.0.0.1", L"::1" },
            { L"::ffff:0.0.0.1", L"::ffff:0:1" },
            { L"ff02::1:ff00:1", L"ff02::1:ff00:1" },
            { L"fe80::1%12", L"fe80::1%12" },
        };

        for (const auto& [Input, Expected] : Cases)
            CHECK_EQ(ParseIp(Input).ToString(), Expected);
    }

    TEST(Network_PhysicalAdapterRuleMatchesWmiOnKnownAdapters)
    {
        // Each case is an adapter met on a real machine, with the answer WMI gave for it.
        CHECK(IsPhysicalAdapter(0x84, true, std::uint8_t(0x05)));        // VirtIO Ethernet (QEMU).
        CHECK(IsPhysicalAdapter(0x84, true, std::uint8_t(0x05)));        // Mellanox ConnectX-5 VF (Azure).
        CHECK(IsPhysicalAdapter(0x04, true, std::uint8_t(0x05)));        // Hyper-V synthetic adapter (Azure).
        CHECK(!IsPhysicalAdapter(0x04, false, std::uint8_t(0x01)));      // Ghost Hyper-V adapter left by the VM image (Azure).
        CHECK(!IsPhysicalAdapter(0x04, true, std::nullopt));             // Azure Network Adapter (MANA) whose driver did not start.
        CHECK(!IsPhysicalAdapter(0x09, true, std::uint8_t(0x00)));       // Kernel debugger adapter.
        CHECK(!IsPhysicalAdapter(0x29, true, std::uint8_t(0x00)));       // WAN miniport.
        CHECK(!IsPhysicalAdapter(0x01, true, std::uint8_t(0x01)));       // Hyper-V virtual switch (vEthernet).
        CHECK(!IsPhysicalAdapter(std::nullopt, true, std::uint8_t(0x05))); // No Characteristics value.
    }

    TEST(Network_ProbeAddressesAreTheFirstHostsOfTheSubnet)
    {
        struct Case { const wchar_t* Address; const wchar_t* Mask; int Count; const wchar_t* First; const wchar_t* Last; std::size_t Expected; };

        const Case Cases[] =
        {
            { L"192.168.1.37", L"255.255.255.0", 20, L"192.168.1.1", L"192.168.1.20", 20 },
            { L"192.168.1.254", L"255.255.255.0", 20, L"192.168.1.1", L"192.168.1.20", 20 },
            { L"172.16.5.9", L"255.255.0.0", 3, L"172.16.0.1", L"172.16.0.3", 3 },
            { L"10.0.0.5", L"255.255.255.252", 20, L"10.0.0.5", L"10.0.0.6", 2 },
            { L"10.0.0.1", L"255.255.255.248", 20, L"10.0.0.1", L"10.0.0.6", 6 },
        };

        for (const auto& Entry : Cases)
        {
            auto Addresses = GetProbeAddresses(V4(Entry.Address), V4(Entry.Mask), Entry.Count);

            CHECK_EQ(Addresses.size(), Entry.Expected);
            CHECK_EQ(Addresses.front(), V4(Entry.First));
            CHECK_EQ(Addresses.back(), V4(Entry.Last));
        }

        CHECK(GetProbeAddresses(V4(L"62.210.1.5"), V4(L"255.255.255.255"), 20).empty());
        CHECK(GetProbeAddresses(V4(L"10.0.0.5"), V4(L"255.255.255.254"), 20).empty());
        CHECK(GetProbeAddresses(V4(L"169.254.10.20"), V4(L"255.255.0.0"), 20).empty());
        CHECK_EQ(GetProbeAddresses(V4(L"10.1.2.3"), V4(L"255.0.0.0"), NetworkProbeCount).size(), static_cast<std::size_t>(NetworkProbeCount));
        CHECK(GetProbeAddresses(V4(L"10.1.2.3"), V4(L"255.0.0.0"), 0).empty());
    }

    TEST(Network_IsInSubnetComparesTheNetworkPart)
    {
        CHECK(IsInSubnet(V4(L"192.168.1.20"), V4(L"192.168.1.37"), V4(L"255.255.255.0")));
        CHECK(!IsInSubnet(V4(L"192.168.2.20"), V4(L"192.168.1.37"), V4(L"255.255.255.0")));
        CHECK(IsInSubnet(V4(L"10.200.3.4"), V4(L"10.1.2.3"), V4(L"255.0.0.0")));
        CHECK(!IsInSubnet(V4(L"62.210.1.1"), V4(L"212.83.1.1"), V4(L"255.255.255.255")));
        CHECK(IsInSubnet(V4(L"212.83.1.1"), V4(L"212.83.1.1"), V4(L"255.255.255.255")));
    }

    TEST(Network_UsableUnicastAddressIsIpv4OrGlobalIpv6)
    {
        auto Usable = [](std::initializer_list<const wchar_t*> InAddresses)
        {
            std::vector<IpAddress> Addresses;

            for (auto Address : InAddresses)
                Addresses.push_back(ParseIp(Address));

            return HasUsableUnicastAddress(Addresses);
        };

        CHECK(!Usable({}));
        CHECK(!Usable({ L"fe80::1" }));
        CHECK(!Usable({ L"fe80::1", L"fe80::2" }));
        CHECK(Usable({ L"169.254.10.20" }));
        CHECK(Usable({ L"10.0.0.1" }));
        CHECK(Usable({ L"fe80::1", L"2001:db8::1" }));
        CHECK(Usable({ L"fe80::1", L"192.168.0.2" }));
    }

    TEST(Network_ReportableNeighborsAreResolvedUnicastEntries)
    {
        constexpr std::uint32_t Unreachable = 0, Incomplete = 1, Stale = 4, Reachable = 5, Permanent = 6;
        const Bytes Host = { 10, 0, 0, 1 };
        const Bytes Mac = { 0x02, 0x11, 0x22, 0x33, 0x44, 0x55 };

        CHECK(IsReportableNeighbor(Host, Mac, Reachable));
        CHECK(IsReportableNeighbor(Host, Mac, Stale));
        CHECK(IsReportableNeighbor(Host, Mac, Permanent));
        CHECK(!IsReportableNeighbor(Host, Mac, Incomplete));
        CHECK(!IsReportableNeighbor(Host, Mac, Unreachable));
        CHECK(!IsReportableNeighbor(Bytes{ 224, 0, 0, 251 }, Bytes{ 0x01, 0x00, 0x5E, 0x00, 0x00, 0xFB }, Permanent));
        CHECK(!IsReportableNeighbor(Bytes{ 255, 255, 255, 255 }, Bytes(6, 0xFF), Permanent));
        CHECK(!IsReportableNeighbor(Host, Bytes(6, 0x00), Reachable));
        CHECK(!IsReportableNeighbor(Host, Bytes{ 0x02, 0x11 }, Reachable));
        CHECK(!IsReportableNeighbor(Bytes{ 0, 0, 0, 0 }, Mac, Reachable));
    }

    TEST(Network_WifiChannelAndBandFollowTheCenterFrequency)
    {
        const std::pair<std::uint32_t, int> Channels[] =
        {
            { 2412000, 1 }, { 2437000, 6 }, { 2472000, 13 }, { 2484000, 14 }, { 5180000, 36 }, { 5500000, 100 },
            { 5745000, 149 }, { 5955000, 1 }, { 6115000, 33 }, { 0, 0 }, { 900000, 0 },
        };

        for (const auto& [Frequency, Channel] : Channels)
            CHECK_EQ(GetWifiChannel(Frequency), Channel);

        const std::pair<std::uint32_t, float> Bands[] =
        {
            { 2412000, 2.4f }, { 2484000, 2.4f }, { 5180000, 5.0f }, { 5825000, 5.0f }, { 5955000, 6.0f }, { 7115000, 6.0f }, { 0, 0.0f },
        };

        for (const auto& [Frequency, Band] : Bands)
            CHECK_EQ(GetWifiBand(Frequency), Band);
    }

    //
    // Text, dates and registry helpers.
    //

    TEST(Text_FormatsMacAddressesHexAndHashes)
    {
        CHECK_EQ(FormatMacAddress(Bytes{ 0x02, 0x1A, 0x2B, 0x3C, 0x4D, 0x5E }), L"02:1A:2B:3C:4D:5E");
        CHECK_EQ(FormatMacAddress(Bytes{}), L"");
        CHECK_EQ(FormatHex(Bytes{ 0x00, 0xFF, 0x10, 0xAB }), L"00FF10AB");
        CHECK_EQ(FormatHex(Bytes{ 0x00, 0xFF, 0x10, 0xAB }, false), L"00ff10ab");
        CHECK_EQ(FormatHex(Bytes{}), L"");
        CHECK_EQ(Sha256Hex(Ascii("abc")), L"ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    TEST(Text_DecodesAndEncodesLikeDotNet)
    {
        CHECK_EQ(DecodeAscii(Bytes{ 0x41, 0xE9, 0x7F }), L"A?\x007F");
        CHECK_EQ(DecodeLatin1(Bytes{ 0x41, 0xE9 }), L"A\x00E9");
        CHECK_EQ(DecodeUtf8(Bytes{ 0x43, 0x61, 0x66, 0xC3, 0xA9, 0x20, 0x57, 0x69, 0x46, 0x69 }), L"Caf\x00E9 WiFi");
        CHECK_EQ(DecodeUtf8(Bytes{ 0x41, 0xFF, 0x42 }), L"A\xFFFD" L"B");
        CHECK_EQ(DecodeUtf8(Bytes{}), L"");
        CHECK_EQ(EncodeUtf8(L"A\x00E9\x20AC"), std::string("A\xC3\xA9\xE2\x82\xAC"));
    }

    TEST(Text_TrimsLikeDotNet)
    {
        CHECK_EQ(Trim(L"\x0085\t\r\n \x00A0\x2003synthetic value\x3000 "), L"synthetic value");
        CHECK_EQ(Trim(std::wstring(L"\0x\0", 3)), std::wstring(L"\0x\0", 3));
        CHECK_EQ(TrimCharacters(std::wstring(L"\0 x \0", 5), std::wstring_view(L"\0 ", 2)), L"x");
        CHECK(IsNullOrWhiteSpace(std::nullopt));
        CHECK(IsNullOrWhiteSpace(L" \t"));
        CHECK(!IsNullOrWhiteSpace(L" x"));
        CHECK_EQ(ToUpperInvariant(L"usbstor"), L"USBSTOR");
        CHECK(EqualsIgnoreCase(L"Scsi", L"SCSI"));
        CHECK(!EqualsIgnoreCase(L"SCSI", L"SCSI0"));
    }

    TEST(Text_FormatsAndParsesGuids)
    {
        auto Guid = ParseGuid(L"5e7c1a2b-3d4f-4a6b-8c9d-0e1f2a3b4c5d");

        CHECK(Guid.has_value());
        CHECK_EQ(FormatGuid(*Guid), L"5e7c1a2b-3d4f-4a6b-8c9d-0e1f2a3b4c5d");
        CHECK_EQ(FormatGuidBraces(*Guid), L"{5E7C1A2B-3D4F-4A6B-8C9D-0E1F2A3B4C5D}");
        CHECK_EQ(FormatGuid(*ParseGuid(L"{5E7C1A2B-3D4F-4A6B-8C9D-0E1F2A3B4C5D}")), L"5e7c1a2b-3d4f-4a6b-8c9d-0e1f2a3b4c5d");
        CHECK(!ParseGuid(L"5e7c1a2b-3d4f-4a6b-8c9d").has_value());
        CHECK(!ParseGuid(L"not a guid").has_value());
        CHECK_EQ(FormatGuid(GuidFromBytes(Bytes{ 0x2B, 0x1A, 0x7C, 0x5E, 0x4F, 0x3D, 0x6B, 0x4A, 0x8C, 0x9D, 0x0E, 0x1F, 0x2A, 0x3B, 0x4C, 0x5D })), L"5e7c1a2b-3d4f-4a6b-8c9d-0e1f2a3b4c5d");
    }

    TEST(Text_SplitsMultiStrings)
    {
        const wchar_t Buffer[] = L"first\0second\0";

        CHECK_EQ(SplitMultiString(Buffer, 15), (std::vector<std::wstring>{ L"first", L"second" }));
        CHECK_EQ(SplitMultiString(Buffer, 6), (std::vector<std::wstring>{ L"first" }));
        CHECK(SplitMultiString(L"\0\0", 2).empty());
        CHECK(SplitMultiString(nullptr, 0).empty());
    }

    TEST(Text_FormatsTenthsAndHundredthsWithTheUserDecimalSeparator)
    {
        auto Separator = DecimalSeparator();
        CHECK_EQ(FormatTenths(0), L"0" + Separator + L"0");
        CHECK_EQ(FormatTenths(13), L"1" + Separator + L"3");
        CHECK_EQ(FormatTenths(127), L"12" + Separator + L"7");
        CHECK_EQ(FormatHundredths(120), L"1" + Separator + L"20");
        CHECK_EQ(FormatHundredths(5), L"0" + Separator + L"05");
    }

    TEST(Dates_AreFormattedLikeSystemTextJson)
    {
        CHECK_EQ(FormatDateTime({}), "0001-01-01T00:00:00");
        CHECK_EQ(FormatDateTime(MakeDate(2023, 6, 15)), "2023-06-15T00:00:00");
        CHECK_EQ(FormatDateTime({ Ticks(2024, 3, 7, 9, 41, 27, 5000000) }), "2024-03-07T09:41:27.5");
        CHECK_EQ(FormatDateTime({ Ticks(2026, 9, 15, 7, 1, 2, 1) }), "2026-09-15T07:01:02.0000001");
        CHECK_EQ(FormatDateTime({ Ticks(2026, 1, 1, 8, 30, 15, 1234500) }), "2026-01-01T08:30:15.12345");
        CHECK_EQ(FormatDateTime({ MaxDateTimeTicks }), "9999-12-31T23:59:59.9999999");
        CHECK_EQ(FormatDateTime({ Ticks(2025, 2, 17, 0, 5, 46, 8692740), true, 60 }), "2025-02-17T00:05:46.869274+01:00");
        CHECK_EQ(FormatDateTime({ Ticks(2025, 2, 17), true, -330 }), "2025-02-17T00:00:00-05:30");
        CHECK_EQ(FormatDateTime({ Ticks(2025, 2, 17), true, 0 }), "2025-02-17T00:00:00+00:00");
    }

    TEST(Dates_ConvertUnixAndFileTimesToLocalTime)
    {
        constexpr std::int64_t UnixEpochTicks = 621'355'968'000'000'000;

        auto Unix = FromUnixTimeSeconds(1'700'000'000);
        CHECK(Unix.IsLocal);
        CHECK_EQ(Unix.Ticks - Unix.UtcOffsetMinutes * TicksPerMinute, UnixEpochTicks + 1'700'000'000 * TicksPerSecond);

        auto File = FromFileTime(133'000'000'000'000'000);
        CHECK(File.has_value() && File->IsLocal);
        CHECK_EQ(File->Ticks - File->UtcOffsetMinutes * TicksPerMinute, FileTimeEpochTicks + 133'000'000'000'000'000);

        auto Now = LocalNowMinus(0);
        CHECK(Now.IsLocal);
        CHECK(Now.Ticks > Ticks(2024, 1, 1));
    }

    TEST(Registry_ReadsTypedValues)
    {
        auto Key = RegistryKey::OpenLocalMachine(L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion");

        CHECK(static_cast<bool>(Key));
        CHECK(!IsNullOrWhiteSpace(Key.GetString(L"ProductName")));
        CHECK(Key.GetDword(L"CurrentMajorVersionNumber").value_or(0) >= 10);
        CHECK_EQ(Key.GetDword(L"ProductName"), std::nullopt);
        CHECK_EQ(Key.GetString(L"ThisValueDoesNotExist"), std::nullopt);
        CHECK(!RegistryKey::OpenLocalMachine(L"SOFTWARE\\ThisKeyDoesNotExist"));
        CHECK(!RegistryKey::OpenLocalMachine(L"SYSTEM\\CurrentControlSet\\Control").GetSubKeyNames().empty());
    }

    //
    // JSON.
    //

    Hwid BuildSyntheticHwid()
    {
        Hwid Hwid;
        Hwid.Disks.push_back({ .Id = 0, .Interface = L"SCSI", .Model = L"Synthetic NVMe Disk", .SerialNumber = L"0000_0000_0000_0001_A1B2_C3D4_E5F6_0718.", .Capacity = L"954 GB", .Partitions = 3, .IsRemovable = false, .IsSMART = true, .Firmware = L"1B2QJXD7", .WorldWideName = L"eui.0025385A1B2C3D4E", .DiskGuid = L"5e7c1a2b-3d4f-4a6b-8c9d-0e1f2a3b4c5d", .NvmeSerial = L"SYNTH0N9X8Y7W6V", .NvmeEui64 = L"0025385A1B2C3D4E", .VpdT10 = L"NVMe    Synthetic NVMe Disk", .VpdScsiName = L"eui.0025385A1B2C3D4E", .Duid = std::wstring(64, L'c') });
        Hwid.Disks.push_back({ .Id = 1, .Interface = L"USB", .Model = L"Removable", .Capacity = L"0 GB", .IsRemovable = true });
        Hwid.Volumes.push_back({ .Id = 0, .Path = L"Volume{6a1f0c2e-9b3d-4e57-a8c1-2d3e4f5a6b7c}", .Letter = L"C:", .SerialNumber = 4000000000u });
        Hwid.NetworkAdapters.push_back({ .Id = 7, .InterfaceId = 12, .Name = L"Synthetic Ethernet", .InterfaceGuid = L"{0A1B2C3D-4E5F-6071-8293-A4B5C6D7E8F9}", .ServiceName = L"netsyn", .Address = { L"02:11:22:33:44:55", L"02:11:22:33:44:55" }, .IsPhysical = true, .IsEnabled = true, .InstallDate = DateTime{ Ticks(2026, 1, 1, 8, 30, 15, 1234500) }, .InstanceId = L"PCI/VEN_1AF4" });
        Hwid.NetworkAdapters.push_back({ .Id = 9, .InterfaceId = -1, .Name = L"Disabled", .Address = { std::nullopt, L"02:AA:BB:CC:DD:EE" } });
        Hwid.BluetoothRadios.push_back({ .Id = 0, .Address = L"02:1A:7D:DA:71:13", .Name = L"DESKTOP-SYNTH", .Manufacturer = 10, .ClassOfDevice = 0x1F0000, .LmpSubversion = 0x2100 });
        Hwid.Baseboards.push_back({ .Id = 0, .Manufacturer = L"Board Vendor", .Model = L"Board Product", .Version = L"1.0", .SerialNumber = L"BSN-0000-SYNTH" });
        Hwid.Motherboards.push_back({ .Id = 0, .Name = L"System Product", .Vendor = L"System Vendor", .Version = L"Rev 1", .UUID = L"8f2e4c6a-1b3d-4f5e-9a7c-0d2e4f6a8b1c" });
        Hwid.Motherboards.push_back({ .Id = 1 });
        Hwid.Chassis.push_back({ .Id = 0, .Manufacturer = L"Chassis Vendor", .Type = 10, .TypeName = L"Notebook", .Version = L"A00", .SerialNumber = L"CSN-SYNTH" });
        Hwid.BiosFirmwares.push_back({ .Id = 0, .Manufacturer = L"Firmware Vendor", .Version = L"F.12", .SerialNumber = L"SSN-SYNTH" });
        Hwid.SmbiosTables.push_back({ .Id = 0, .Version = L"3.4.0", .Hash = std::wstring(64, L'a'), .Length = 3456 });
        Hwid.Processors.push_back({ .Id = 0, .Manufacturer = L"GenuineIntel", .Model = L"Synthetic CPU @ 3.60GHz  ", .ModelNumber = L"BFEBFBFF000906EA", .Socket = L"CPU 0", .PartNumber = L"To Be Filled By O.E.M.", .SerialNumber = L"", .ClockSpeed = L"3600 MHz", .Voltage = L"1.1 V", .Channel = L"CPU0", .NumberOfCores = 8, .NumberOfLogicalProcessors = 16 });
        Hwid.MemorySticks.push_back({ .Id = 0, .Manufacturer = L"Kingston", .PartNumber = L"KF3200C16D4/16GX", .SerialNumber = L"MSN00001", .Capacity = L"16 GB", .ClockSpeed = L"3200 MHz", .Voltage = L"1.20 V", .Channel = L"DIMM_A1" });
        Hwid.Batteries.push_back({ .Id = 0, .DeviceName = L"Battery 1", .Manufacturer = L"SMP", .SerialNumber = L"1234", .UniqueId = L"1234SMPBattery 1", .Chemistry = L"LION", .DesignedCapacity = 56000, .FullChargedCapacity = 50000, .ManufactureDate = MakeDate(2023, 6, 15) });
        Hwid.Batteries.push_back({ .Id = 1, .Chemistry = L"" });
        Hwid.Monitors.push_back({ .Id = 0, .Manufacturer = L"DEL", .Name = L"DELL U2415", .Product = L"4070", .SerialNumber = L"0", .InstanceId = L"DISPLAY/DEL4070", .EdidHash = std::wstring(64, L'b'), .ManufactureWeek = 12, .ManufactureYear = 2020 });
        Hwid.VideoControllers.push_back({ .Id = 0, .Name = L"Synthetic GPU", .Width = 2560, .Height = 1440, .RefreshRate = 144, .DriverDate = MakeDate(2025, 9, 30), .DriverVersion = L"32.0.15.6094", .InstanceId = L"PCI/VEN_10DE" });
        Hwid.Printers.push_back({ .Id = 0, .Name = L"Microsoft Print to PDF", .PortName = L"PORTPROMPT:", .Location = L"", .Width = 600, .Height = 600 });
        Hwid.Users.push_back({ .Id = 0, .Username = L"Synth", .FullName = L"", .SID = L"S-1-5-21-1000000001-2000000002-3000000003-1001", .Domain = L"DESKTOP-SYNTH" });
        Hwid.OperatingSystems.push_back({ .Id = 0, .Name = L"Windows 11 Pro", .Version = L"10.0.26200", .Architecture = L"64-bit", .RegisteredUser = L"Synth", .SerialNumber = L"00330-80000-00000-AA000", .InstallDate = MakeDate(2024, 3, 7), .LastBootUpTime = { Ticks(2026, 9, 15, 7, 1, 2, 1) }, .MachineGuid = L"3c9a7e15-6b2d-4f80-a1c4-5e7d9b0f2a63", .SqmMachineId = L"{D41E7A93-0B5C-4F28-9E61-7A3C2B8F4D05}", .HardwareProfileGuid = L"{a7f3c912-5e04-4b6d-8c21-f09e3d7b6a48}", .InstallTime = DateTime{ Ticks(2024, 3, 7, 9, 41, 27, 5000000) }, .MachineSid = L"S-1-5-21-1000000001-2000000002-3000000003" });
        Hwid.Wifis.push_back({ .Id = 0, .Ssid = L"Synthetic WiFi", .Bssid = L"02:11:22:33:44:55", .Strength = -50, .Channel = 6, .Frequency = 2437000, .Band = 2.4f, .Quality = 90 });
        Hwid.Wifis.push_back({ .Id = 1, .Ssid = L"", .Bssid = L"02:11:22:33:44:56", .Strength = -80, .Channel = 36, .Frequency = 5180000, .Band = 5.0f, .Quality = 40 });
        Hwid.Routers.push_back({ .Id = 0, .Gateways = { { L"02:11:22:33:44:01", L"192.168.1.1" } }, .DnsServers = { L"1.1.1.1", L"fe80::1%12" }, .DhcpServers = {}, .NetworkDevices = { {} } });
        Hwid.NetworkSignatures.push_back({ .Id = 0, .ProfileGuid = L"{6B1C0E2A-3D4F-4A5B-8C9D-0E1F2A3B4C5D}", .Name = L"Network 2", .DefaultGatewayMac = L"02:11:22:33:44:01" });
        return Hwid;
    }

    // What JsonSerializer.Serialize(Hwid) prints for the same scan in HardwareIds.NET.
    const char* const DotNetJson =
        R"({"disks":[{"id":0,"interface":"SCSI","model":"Synthetic NVMe Disk","serial_number":"0000_0000_0000_0001_A1B2_C3D4_E5F6_0718.","capacity":"954 GB","partitions":3,"is_removable":false,"is_smart":true,"firmware":"1B2QJXD7","world_wide_name":"eui.0025385A1B2C3D4E","disk_guid":"5e7c1a2b-3d4f-4a6b-8c9d-0e1f2a3b4c5d","instance_id":null,"nvme_serial":"SYNTH0N9X8Y7W6V","nvme_eui64":"0025385A1B2C3D4E","nvme_nguid":null,"nvme_fguid":null,"ata_serial":null,"ata_wwn":null,"vpd_t10":"NVMe    Synthetic NVMe Disk","vpd_eui64":null,"vpd_nguid":null,"vpd_naa":null,"vpd_scsi_name":"eui.0025385A1B2C3D4E","vpd_vendor":null,"duid":"cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"},)"
        R"({"id":1,"interface":"USB","model":"Removable","serial_number":null,"capacity":"0 GB","partitions":0,"is_removable":true,"is_smart":false,"firmware":null,"world_wide_name":null,"disk_guid":null,"instance_id":null,"nvme_serial":null,"nvme_eui64":null,"nvme_nguid":null,"nvme_fguid":null,"ata_serial":null,"ata_wwn":null,"vpd_t10":null,"vpd_eui64":null,"vpd_nguid":null,"vpd_naa":null,"vpd_scsi_name":null,"vpd_vendor":null,"duid":null}],)"
        R"("volumes":[{"id":0,"path":"Volume{6a1f0c2e-9b3d-4e57-a8c1-2d3e4f5a6b7c}","letter":"C:","serial_number":4000000000}],)"
        R"("network_adapters":[{"id":7,"interface_id":12,"name":"Synthetic Ethernet","interface_guid":"{0A1B2C3D-4E5F-6071-8293-A4B5C6D7E8F9}","service_name":"netsyn","address":{"current":"02:11:22:33:44:55","permanent":"02:11:22:33:44:55"},"is_physical":true,"is_enabled":true,"install_date":"2026-01-01T08:30:15.12345","instance_id":"PCI/VEN_1AF4"},)"
        R"({"id":9,"interface_id":-1,"name":"Disabled","interface_guid":null,"service_name":null,"address":{"current":null,"permanent":"02:AA:BB:CC:DD:EE"},"is_physical":false,"is_enabled":false,"install_date":null,"instance_id":null}],)"
        R"("bluetooth_radios":[{"id":0,"address":"02:1A:7D:DA:71:13","name":"DESKTOP-SYNTH","manufacturer":10,"class_of_device":2031616,"lmp_subversion":8448}],)"
        R"("baseboards":[{"id":0,"manufacturer":"Board Vendor","model":"Board Product","version":"1.0","serial_number":"BSN-0000-SYNTH","part_number":null}],)"
        R"("motherboards":[{"id":0,"name":"System Product","vendor":"System Vendor","version":"Rev 1","UUID":"8f2e4c6a-1b3d-4f5e-9a7c-0d2e4f6a8b1c"},{"id":1,"name":null,"vendor":null,"version":null,"UUID":"00000000-0000-0000-0000-000000000000"}],)"
        R"("chassis":[{"id":0,"manufacturer":"Chassis Vendor","type":10,"type_name":"Notebook","version":"A00","serial_number":"CSN-SYNTH","asset_tag":null}],)"
        R"("bios_firmwares":[{"id":0,"manufacturer":"Firmware Vendor","version":"F.12","serial_number":"SSN-SYNTH"}],)"
        R"("smbios_tables":[{"id":0,"version":"3.4.0","hash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","length":3456}],)"
        R"("processors":[{"id":0,"manufacturer":"GenuineIntel","model":"Synthetic CPU @ 3.60GHz  ","model_number":"BFEBFBFF000906EA","socket":"CPU 0","part_number":"To Be Filled By O.E.M.","serial_number":"","clock_speed":"3600 MHz","voltage":"1.1 V","channel":"CPU0","number_of_cores":8,"number_of_logical_processors":16}],)"
        R"("memory_sticks":[{"id":0,"manufacturer":"Kingston","part_number":"KF3200C16D4/16GX","serial_number":"MSN00001","capacity_in_gb":"16 GB","clock_speed":"3200 MHz","voltage":"1.20 V","channel":"DIMM_A1"}],)"
        R"("batteries":[{"id":0,"device_name":"Battery 1","manufacturer":"SMP","serial_number":"1234","unique_id":"1234SMPBattery 1","chemistry":"LION","designed_capacity":56000,"full_charged_capacity":50000,"manufacture_date":"2023-06-15T00:00:00"},)"
        R"({"id":1,"device_name":null,"manufacturer":null,"serial_number":null,"unique_id":null,"chemistry":"","designed_capacity":0,"full_charged_capacity":0,"manufacture_date":null}],)"
        R"("monitors":[{"id":0,"manufacturer":"DEL","name":"DELL U2415","product":"4070","serial_number":"0","instance_id":"DISPLAY/DEL4070","edid_hash":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","manufacture_week":12,"manufacture_year":2020}],)"
        R"("video_controllers":[{"id":0,"name":"Synthetic GPU","width":2560,"height":1440,"refresh_rate":144,"driver_date":"2025-09-30T00:00:00","driver_version":"32.0.15.6094","instance_id":"PCI/VEN_10DE"}],)"
        R"("printers":[{"id":0,"name":"Microsoft Print to PDF","port_name":"PORTPROMPT:","location":"","width":600,"height":600}],)"
        R"("users":[{"id":0,"username":"Synth","full_name":"","sid":"S-1-5-21-1000000001-2000000002-3000000003-1001","domain":"DESKTOP-SYNTH","install_date":"0001-01-01T00:00:00"}],)"
        R"("operating_systems":[{"id":0,"name":"Windows 11 Pro","version":"10.0.26200","architecture":"64-bit","registered_user":"Synth","serial_number":"00330-80000-00000-AA000","install_date":"2024-03-07T00:00:00","last_boot_up_time":"2026-09-15T07:01:02.0000001","machine_guid":"3c9a7e15-6b2d-4f80-a1c4-5e7d9b0f2a63","sqm_machine_id":"{D41E7A93-0B5C-4F28-9E61-7A3C2B8F4D05}","hardware_profile_guid":"{a7f3c912-5e04-4b6d-8c21-f09e3d7b6a48}","install_time":"2024-03-07T09:41:27.5","machine_sid":"S-1-5-21-1000000001-2000000002-3000000003"}],)"
        R"("wifis":[{"id":0,"ssid":"Synthetic WiFi","bssid":"02:11:22:33:44:55","Strength":-50,"Channel":6,"Frequency":2437000,"Band":2.4,"Quality":90},{"id":1,"ssid":"","bssid":"02:11:22:33:44:56","Strength":-80,"Channel":36,"Frequency":5180000,"Band":5,"Quality":40}],)"
        R"("routers":[{"id":0,"gateways":[{"mac_address":"02:11:22:33:44:01","ip_address":"192.168.1.1"}],"dns_servers":["1.1.1.1","fe80::1%12"],"dhcp_servers":[],"network_devices":[{"mac_address":null,"ip_address":null}]}],)"
        R"("network_signatures":[{"id":0,"profile_guid":"{6B1C0E2A-3D4F-4A5B-8C9D-0E1F2A3B4C5D}","name":"Network 2","gateway_address":"02:11:22:33:44:01"}]})";

    const char* const SectionNames[] =
    {
        "disks", "volumes", "network_adapters", "bluetooth_radios", "baseboards", "motherboards", "chassis", "bios_firmwares", "smbios_tables", "processors",
        "memory_sticks", "batteries", "monitors", "video_controllers", "printers", "users", "operating_systems", "wifis", "routers", "network_signatures",
    };

    // Removes the white space outside of JSON strings.
    std::string Minify(const std::string& InJson)
    {
        std::string Result;
        auto InString = false;

        for (std::size_t I = 0; I < InJson.size(); I++)
        {
            auto Character = InJson[I];

            if (InString)
            {
                Result += Character;

                if (Character == '\\')
                    Result += InJson[++I];
                else if (Character == '"')
                    InString = false;
            }
            else if (Character == '"')
            {
                Result += Character;
                InString = true;
            }
            else if (Character != ' ' && Character != '\r' && Character != '\n')
            {
                Result += Character;
            }
        }

        return Result;
    }

    std::string JsonOfModel(const std::wstring& InModel)
    {
        Hwid Hwid;
        Hwid.Disks.push_back({ .Model = InModel });

        auto Json = ToJson(Hwid);
        auto Start = Json.find("\"model\":") + 8;
        return Json.substr(Start, Json.find(",\"serial_number\"") - Start);
    }

    TEST(Json_MatchesSystemTextJsonByteForByte)
    {
        CHECK_EQ(ToJson(BuildSyntheticHwid()), std::string(DotNetJson));
    }

    TEST(Json_IndentedOutputUsesCrLfAndTwoSpaces)
    {
        std::string Expected = "{";

        for (std::size_t I = 0; I < std::size(SectionNames); I++)
            Expected += std::string(I == 0 ? "" : ",") + "\r\n  \"" + SectionNames[I] + "\": []";

        Expected += "\r\n}";
        CHECK_EQ(ToJson(Hwid{}, JsonFormat::Indented), Expected);

        auto Indented = ToJson(BuildSyntheticHwid(), JsonFormat::Indented);
        CHECK_EQ(Minify(Indented), std::string(DotNetJson));
        CHECK(Indented.find("\r\n    {\r\n      \"id\": 0,\r\n      \"interface\": \"SCSI\",") != std::string::npos);
        CHECK(Indented.find("\"dhcp_servers\": [],") != std::string::npos);
        CHECK(Indented.find("\"Band\": 2.4,") != std::string::npos);

        for (std::size_t I = 0; I < Indented.size(); I++)
        {
            if (Indented[I] == '\n')
                CHECK(I > 0 && Indented[I - 1] == '\r');
        }
    }

    TEST(Json_EscapesLikeSystemTextJson)
    {
        //
        // The expected strings use ` for the backslash: System.Text.Json escapes ` itself, so it never appears raw in its output.
        //

        auto Expected = [](std::string InText)
        {
            std::replace(InText.begin(), InText.end(), '`', '\\');
            return InText;
        };

        std::wstring Characters;

        for (wchar_t Character = 0; Character < 0x80; Character++)
            Characters += Character;

        for (int Character : { 0x00E9, 0x00A0, 0x20AC, 0xD83D, 0xDE00, 0x2028, 0xFFFD })
            Characters += static_cast<wchar_t>(Character);

        CHECK_EQ(JsonOfModel(Characters), Expected(
            R"("`u0000`u0001`u0002`u0003`u0004`u0005`u0006`u0007`b`t`n`u000B`f`r`u000E`u000F`u0010`u0011`u0012`u0013`u0014`u0015`u0016`u0017`u0018`u0019`u001A`u001B`u001C`u001D`u001E`u001F)"
            R"( !`u0022#$%`u0026`u0027()*`u002B,-./0123456789:;`u003C=`u003E?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[``]^_`u0060abcdefghijklmnopqrstuvwxyz{|}~`u007F)"
            R"(`u00E9`u00A0`u20AC`uD83D`uDE00`u2028`uFFFD")"));

        CHECK_EQ(JsonOfModel(std::wstring(L"a") + wchar_t(0xD800) + L"b"), Expected(R"("a`uFFFDb")"));
        CHECK_EQ(JsonOfModel(std::wstring(L"a") + wchar_t(0xDC00)), Expected(R"("a`uFFFD")"));
        CHECK_EQ(JsonOfModel(LR"(\\?\SCSI#Disk&Ven_Synth)"), Expected(R"("````?``SCSI#Disk`u0026Ven_Synth")"));
    }

    TEST(Json_FloatsUseTheShortestRoundTripForm)
    {
        auto BandOf = [](float InBand)
        {
            Hwid Hwid;
            Hwid.Wifis.push_back({ .Band = InBand });

            auto Json = ToJson(Hwid);
            auto Start = Json.find("\"Band\":") + 7;
            return Json.substr(Start, Json.find(",\"Quality\"") - Start);
        };

        CHECK_EQ(BandOf(2.4f), "2.4");
        CHECK_EQ(BandOf(5.0f), "5");
        CHECK_EQ(BandOf(6.0f), "6");
        CHECK_EQ(BandOf(0.0f), "0");
        CHECK_EQ(BandOf(1e20f), "1E+20");
    }

    TEST(Json_CApiReturnsTheSnapshot)
    {
        HardwareIds_Free(nullptr);

        auto Compact = HardwareIds_GetSnapshotJson(nullptr);
        CHECK(Compact != nullptr);
        CHECK(std::string_view(Compact).starts_with("{\"disks\":["));
        CHECK(std::string_view(Compact).ends_with("]}"));
        HardwareIds_Free(Compact);

        HardwareIds_Options Options = {};
        Options.Indented = 1;

        auto Indented = HardwareIds_GetSnapshotJson(&Options);
        CHECK(Indented != nullptr);
        CHECK(std::string_view(Indented).starts_with("{\r\n  \"disks\": ["));
        HardwareIds_Free(Indented);
    }

    //
    // A real scan.
    //

    std::string MaskBootTime(std::string InJson)
    {
        auto Start = InJson.find("\"last_boot_up_time\":\"");

        if (Start != std::string::npos)
        {
            Start += 21;
            InJson.erase(Start, InJson.find('"', Start) - Start);
        }

        return InJson;
    }

    TEST(Scan_CollectsTheMainComponents)
    {
        auto Hwid = GetHwid();

        CHECK(!Hwid.Disks.empty());
        CHECK(!Hwid.Volumes.empty());
        CHECK(!Hwid.Processors.empty());
        CHECK(!Hwid.Motherboards.empty());
        CHECK(!Hwid.SmbiosTables.empty());
        CHECK(!Hwid.Users.empty());
        CHECK_EQ(Hwid.OperatingSystems.size(), 1u);
        CHECK(Hwid.Wifis.empty());
        CHECK(Hwid.Routers.empty());

        for (std::size_t I = 0; I < Hwid.Disks.size(); I++)
        {
            CHECK(!IsNullOrWhiteSpace(Hwid.Disks[I].Model));
            CHECK(Hwid.Disks[I].Duid == std::nullopt || IsHex(Hwid.Disks[I].Duid, 64, true));

            if (I > 0)
                CHECK(Hwid.Disks[I - 1].Id < Hwid.Disks[I].Id);
        }

        const auto& System = Hwid.OperatingSystems[0];
        CHECK(!IsNullOrWhiteSpace(System.Name));
        CHECK(ParseGuid(System.MachineGuid.value_or(L"")).has_value());
        CHECK(System.MachineSid && System.MachineSid->starts_with(L"S-1-5-21-"));
        CHECK(System.LastBootUpTime.IsLocal);
        CHECK(System.InstallDate.Ticks > Ticks(2000, 1, 1));

        for (const auto& Processor : Hwid.Processors)
            CHECK(Processor.NumberOfLogicalProcessors >= Processor.NumberOfCores && Processor.NumberOfCores >= 1);

        for (const auto& Adapter : Hwid.NetworkAdapters)
            CHECK(Adapter.Address.Permanent && Adapter.Address.Permanent->size() == 17);
    }

    TEST(Scan_IsDeterministic)
    {
        CHECK_EQ(MaskBootTime(ToJson(GetHwid())), MaskBootTime(ToJson(GetHwid())));
    }

    TEST(Scan_StopTokenSkipsEverythingWhenAlreadyRequested)
    {
        // Like a cancelled token in .NET: no collector runs.
        std::stop_source Source;
        Source.request_stop();

        HardwareIdsConfig Config;
        Config.ScanNeighborEndpoints = true;
        Config.ScanLocalNetworkDevices = true;

        auto Hwid = GetHwid(Config, Source.get_token());

        CHECK(Hwid.Disks.empty() && Hwid.OperatingSystems.empty() && Hwid.Wifis.empty() && Hwid.Routers.empty());
    }

    TEST(Scan_StopTokenCancelsTheRunningNetworkScans)
    {
        HardwareIdsConfig Config;
        Config.ScanNeighborEndpoints = true;
        Config.ScanLocalNetworkDevices = true;
        Config.DurationOfNetworkScan = std::chrono::seconds(60);
        Config.DurationOfLocalNetworkScan = std::chrono::seconds(60);

        std::stop_source Source;
        std::jthread Canceller([&Source](std::stop_token InStopToken)
        {
            std::mutex Mutex;
            std::unique_lock Lock(Mutex);
            std::condition_variable_any().wait_for(Lock, InStopToken, std::chrono::seconds(5), [] { return false; });
            Source.request_stop();
        });

        auto Start = std::chrono::steady_clock::now();
        auto Hwid = GetHwid(Config, Source.get_token());
        auto Elapsed = std::chrono::steady_clock::now() - Start;

        CHECK(Elapsed < std::chrono::seconds(30));
        CHECK(!Hwid.Disks.empty());
        CHECK_EQ(Hwid.OperatingSystems.size(), 1u);
    }

    TEST(Scan_LocalNetworkEntriesAreWellFormed)
    {
        HardwareIdsConfig Config;
        Config.ScanLocalNetworkDevices = true;

        for (const auto& Router : GetHwid(Config).Routers)
        {
            for (const auto* Devices : { &Router.Gateways, &Router.NetworkDevices })
            {
                for (const auto& Device : *Devices)
                {
                    CHECK(Device.Ip.has_value());
                    CHECK(!Device.MacAddress || Device.MacAddress->size() == 17);
                }
            }
        }
    }
}

int main(int InArgumentCount, char** InArguments)
{
    std::string Filter = InArgumentCount > 1 ? InArguments[1] : "";
    auto Passed = 0, Failed = 0, Skipped = 0;

    for (const auto& Test : GetTests())
    {
        if (!Filter.empty() && std::string(Test.Name).find(Filter) == std::string::npos)
            continue;

        auto Start = std::chrono::steady_clock::now();
        std::string Outcome;

        try
        {
            Test.Function();
            Passed++;
            Outcome = "PASS";
        }
        catch (const TestFailure& Failure)
        {
            Failed++;
            Outcome = "FAIL";
            std::printf("[FAIL] %s\n       %s\n", Test.Name, Failure.Message.c_str());
            continue;
        }
        catch (const TestSkipped& Skip)
        {
            Skipped++;
            std::printf("[SKIP] %s: %s\n", Test.Name, Skip.Reason.c_str());
            continue;
        }
        catch (const std::exception& Exception)
        {
            Failed++;
            std::printf("[FAIL] %s\n       threw %s\n", Test.Name, Exception.what());
            continue;
        }

        auto Milliseconds = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now() - Start).count();
        std::printf("[%s] %s (%lld ms)\n", Outcome.c_str(), Test.Name, static_cast<long long>(Milliseconds));
    }

    std::printf("\n%d passed, %d failed, %d skipped\n", Passed, Failed, Skipped);
    return Failed == 0 && Passed > 0 ? 0 : 1;
}
