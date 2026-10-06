#include "Internal.hpp"

#include <algorithm>
#include <cstring>
#include <intrin.h>

namespace HardwareIds::Detail
{
    //
    // SMBIOS table.
    //

    SmbiosStructure::SmbiosStructure(std::uint8_t InType, std::uint8_t InLength, std::uint16_t InHandle, Bytes InFormatted, std::vector<std::wstring> InStrings)
        : Type(InType), Length(InLength), Handle(InHandle), Strings(std::move(InStrings)), Formatted(std::move(InFormatted))
    {
    }

    std::uint8_t SmbiosStructure::GetByte(int InOffset) const
    {
        return InOffset >= 0 && InOffset + 1 <= this->Length ? this->Formatted[static_cast<std::size_t>(InOffset)] : 0;
    }

    std::uint16_t SmbiosStructure::GetWord(int InOffset) const
    {
        if (InOffset < 0 || InOffset + 2 > this->Length)
            return 0;

        std::uint16_t Value;
        std::memcpy(&Value, this->Formatted.data() + InOffset, sizeof(Value));
        return Value;
    }

    std::uint32_t SmbiosStructure::GetDword(int InOffset) const
    {
        if (InOffset < 0 || InOffset + 4 > this->Length)
            return 0;

        std::uint32_t Value;
        std::memcpy(&Value, this->Formatted.data() + InOffset, sizeof(Value));
        return Value;
    }

    std::optional<Bytes> SmbiosStructure::GetBytes(int InOffset, int InCount) const
    {
        if (InOffset < 0 || InOffset + InCount > this->Length)
            return std::nullopt;

        return Bytes(this->Formatted.begin() + InOffset, this->Formatted.begin() + InOffset + InCount);
    }

    NullableString SmbiosStructure::GetString(int InOffset) const
    {
        auto Index = this->GetByte(InOffset);

        if (Index == 0 || Index > this->Strings.size())
            return std::nullopt;

        return this->Strings[Index - 1u];
    }

    std::optional<SmbiosTable> SmbiosTable::Read()
    {
        constexpr DWORD RSMB = 0x52534D42;

        auto Size = GetSystemFirmwareTable(RSMB, 0, nullptr, 0);

        if (Size < 8)
            return std::nullopt;

        Bytes Buffer(Size);
        auto ReadSize = GetSystemFirmwareTable(RSMB, 0, Buffer.data(), Size);

        if (ReadSize < 8)
            return std::nullopt;

        //
        // RawSMBIOSData header: calling method, major version, minor version, DMI revision, then the table length.
        //

        std::uint32_t TableLength;
        std::memcpy(&TableLength, Buffer.data() + 4, sizeof(TableLength));
        auto Length = std::min<std::size_t>(TableLength, std::min(ReadSize, Size) - 8);

        return FromData(Buffer[1], Buffer[2], Buffer[3], Bytes(Buffer.begin() + 8, Buffer.begin() + 8 + static_cast<std::ptrdiff_t>(Length)));
    }

    SmbiosTable SmbiosTable::FromData(std::uint8_t InMajorVersion, std::uint8_t InMinorVersion, std::uint8_t InDmiRevision, Bytes InData)
    {
        SmbiosTable Table;
        Table.MajorVersion = InMajorVersion;
        Table.MinorVersion = InMinorVersion;
        Table.DmiRevision = InDmiRevision;
        Table.Data = std::move(InData);
        Table.Parse();
        return Table;
    }

    std::vector<const SmbiosStructure*> SmbiosTable::OfType(std::uint8_t InType) const
    {
        std::vector<const SmbiosStructure*> Result;

        for (const auto& Structure : this->Structures)
        {
            if (Structure.Type == InType)
                Result.push_back(&Structure);
        }

        return Result;
    }

    void SmbiosTable::Parse()
    {
        const auto& Raw = this->Data;
        std::size_t Position = 0;

        while (Position + 4 <= Raw.size())
        {
            auto Type = Raw[Position];
            auto Length = Raw[Position + 1];

            if (Length < 4 || Position + Length > Raw.size())
                break;

            std::uint16_t Handle;
            std::memcpy(&Handle, Raw.data() + Position + 2, sizeof(Handle));

            //
            // The strings follow the formatted area, each terminated by a null byte, the set terminated by another one.
            //

            std::vector<std::wstring> Strings;
            auto Cursor = Position + Length;

            while (Cursor < Raw.size())
            {
                if (Raw[Cursor] == 0)
                {
                    Cursor++;
                    break;
                }

                auto End = std::find(Raw.begin() + static_cast<std::ptrdiff_t>(Cursor), Raw.end(), std::uint8_t{ 0 }) - Raw.begin();
                Strings.push_back(DecodeLatin1(ByteSpan(Raw.data() + Cursor, static_cast<std::size_t>(End) - Cursor)));
                Cursor = static_cast<std::size_t>(End) + 1;
            }

            if (Strings.empty())
                Cursor++;

            this->Structures.emplace_back(Type, Length, Handle, Bytes(Raw.begin() + static_cast<std::ptrdiff_t>(Position), Raw.begin() + static_cast<std::ptrdiff_t>(Position + Length)), std::move(Strings));

            if (Type == 127)
                break;

            Position = Cursor;
        }
    }

    //
    // Helpers.
    //

    std::wstring GetChassisTypeName(int InType)
    {
        static constexpr const wchar_t* Names[] =
        {
            L"Unknown", L"Other", L"Unknown", L"Desktop", L"Low Profile Desktop", L"Pizza Box", L"Mini Tower", L"Tower", L"Portable", L"Laptop",
            L"Notebook", L"Hand Held", L"Docking Station", L"All in One", L"Sub Notebook", L"Space-saving", L"Lunch Box", L"Main Server Chassis",
            L"Expansion Chassis", L"SubChassis", L"Bus Expansion Chassis", L"Peripheral Chassis", L"RAID Chassis", L"Rack Mount Chassis",
            L"Sealed-case PC", L"Multi-system chassis", L"Compact PCI", L"Advanced TCA", L"Blade", L"Blade Enclosure", L"Tablet", L"Convertible",
            L"Detachable", L"IoT Gateway", L"Embedded PC", L"Mini PC", L"Stick PC",
        };

        return InType > 0 && InType < static_cast<int>(std::size(Names)) ? Names[InType] : L"Unknown";
    }

    NullableString FormatProcessorId(const std::optional<Bytes>& InProcessorId)
    {
        if (!InProcessorId || InProcessorId->size() < 8)
            return std::nullopt;

        //
        // The SMBIOS field holds EAX then EDX of CPUID leaf 1; WMI prints EDX first.
        //

        std::uint32_t Eax;
        std::uint32_t Edx;
        std::memcpy(&Eax, InProcessorId->data(), 4);
        std::memcpy(&Edx, InProcessorId->data() + 4, 4);

        wchar_t Buffer[20];
        swprintf_s(Buffer, L"%08X%08X", Edx, Eax);
        return Buffer;
    }

    NullableString GetProcessorIdFromCpuId()
    {
    #if defined(_M_X64) || defined(_M_IX86)
        int Registers[4] = {};
        __cpuid(Registers, 1);

        wchar_t Buffer[20];
        swprintf_s(Buffer, L"%08X%08X", static_cast<std::uint32_t>(Registers[3]), static_cast<std::uint32_t>(Registers[0]));
        return Buffer;
    #else
        return std::nullopt;
    #endif
    }

    std::wstring FormatProcessorVoltage(std::uint8_t InVoltage)
    {
        //
        // Bit 7 set: the current voltage in tenths of volts. Otherwise the bits flag the legacy supported voltages.
        //

        std::uint32_t Tenths = 0;

        if ((InVoltage & 0x80) != 0)
            Tenths = InVoltage & 0x7Fu;
        else if ((InVoltage & 0x01) != 0)
            Tenths = 50;
        else if ((InVoltage & 0x02) != 0)
            Tenths = 33;
        else if ((InVoltage & 0x04) != 0)
            Tenths = 29;

        return FormatTenths(Tenths) + L" V";
    }

    std::uint64_t GetMemoryDeviceCapacity(const SmbiosStructure& InMemoryDevice)
    {
        auto Size = InMemoryDevice.GetWord(0x0C);

        switch (Size)
        {
            case 0x0000:
            case 0xFFFF:
                return 0;

            case 0x7FFF:
                return static_cast<std::uint64_t>(InMemoryDevice.GetDword(0x1C)) * 1024 * 1024;

            default:
                return (Size & 0x8000) != 0 ? static_cast<std::uint64_t>(Size & 0x7FFF) * 1024 : static_cast<std::uint64_t>(Size) * 1024 * 1024;
        }
    }

    ProcessorTopology GetProcessorTopology()
    {
        DWORD Length = 0;
        GetLogicalProcessorInformationEx(RelationAll, nullptr, &Length);

        if (Length == 0)
            return {};

        Bytes Buffer(Length);
        auto Information = reinterpret_cast<SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX*>(Buffer.data());

        if (!GetLogicalProcessorInformationEx(RelationAll, Information, &Length))
            return {};

        ProcessorTopology Result;
        DWORD Offset = 0;

        while (Offset + 8 <= Length)
        {
            auto Entry = reinterpret_cast<const SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX*>(Buffer.data() + Offset);

            if (Entry->Size == 0)
                break;

            if (Entry->Relationship == RelationProcessorCore)
            {
                Result.Cores++;

                for (WORD Group = 0; Group < Entry->Processor.GroupCount; Group++)
                    Result.LogicalProcessors += static_cast<int>(__popcnt64(Entry->Processor.GroupMask[Group].Mask));
            }
            else if (Entry->Relationship == RelationProcessorPackage)
            {
                Result.Packages++;
            }

            Offset += Entry->Size;
        }

        return Result;
    }

    //
    // Collectors.
    //

    void RetrieveBaseBoards(Hwid& InHwid, const SmbiosTable* InSmbios)
    {
        if (InSmbios == nullptr)
            return;

        //
        // SMBIOS type 2: Baseboard (or Module) Information.
        //

        for (auto Baseboard : InSmbios->OfType(2))
        {
            HwBaseboard Entry;
            Entry.Id = static_cast<int>(InHwid.Baseboards.size());
            Entry.Manufacturer = Baseboard->GetString(0x04);
            Entry.Model = Baseboard->GetString(0x05);
            Entry.Version = Baseboard->GetString(0x06);
            Entry.SerialNumber = Baseboard->GetString(0x07);
            InHwid.Baseboards.push_back(std::move(Entry));
        }
    }

    void RetrieveMotherBoards(Hwid& InHwid, const SmbiosTable* InSmbios)
    {
        if (InSmbios == nullptr)
            return;

        //
        // SMBIOS type 1: System Information (what WMI exposes as Win32_ComputerSystemProduct).
        //

        for (auto SystemInfo : InSmbios->OfType(1))
        {
            HwMotherboard Entry;
            Entry.Id = static_cast<int>(InHwid.Motherboards.size());
            Entry.Name = SystemInfo->GetString(0x05);
            Entry.Vendor = SystemInfo->GetString(0x04);
            Entry.Version = SystemInfo->GetString(0x06);

            if (auto Uuid = SystemInfo->GetBytes(0x08, 16))
                Entry.UUID = FormatGuid(GuidFromBytes(*Uuid));

            InHwid.Motherboards.push_back(std::move(Entry));
        }
    }

    void RetrieveChassis(Hwid& InHwid, const SmbiosTable* InSmbios)
    {
        if (InSmbios == nullptr)
            return;

        //
        // SMBIOS type 3: System Enclosure or Chassis. Bit 7 of the type is the lock flag.
        //

        for (auto Chassis : InSmbios->OfType(3))
        {
            HwChassis Entry;
            Entry.Id = static_cast<int>(InHwid.Chassis.size());
            Entry.Manufacturer = Chassis->GetString(0x04);
            Entry.Type = Chassis->GetByte(0x05) & 0x7F;
            Entry.TypeName = GetChassisTypeName(Entry.Type);
            Entry.Version = Chassis->GetString(0x06);
            Entry.SerialNumber = Chassis->GetString(0x07);
            Entry.AssetTag = Chassis->GetString(0x08);
            InHwid.Chassis.push_back(std::move(Entry));
        }
    }

    void RetrieveFirmwares(Hwid& InHwid, const SmbiosTable* InSmbios)
    {
        if (InSmbios == nullptr)
            return;

        //
        // WMI reports the first entry of the SystemBiosVersion registry value as the BIOS version,
        // and the system serial number (SMBIOS type 1) as the BIOS serial number.
        //

        NullableString RegistryVersion;

        if (auto Versions = RegistryKey::OpenLocalMachine(L"HARDWARE\\DESCRIPTION\\System").GetMultiString(L"SystemBiosVersion"); Versions && !Versions->empty())
            RegistryVersion = Versions->front();

        NullableString SystemSerialNumber;

        if (auto Systems = InSmbios->OfType(1); !Systems.empty())
            SystemSerialNumber = Systems.front()->GetString(0x07);

        //
        // SMBIOS type 0: BIOS Information.
        //

        for (auto Bios : InSmbios->OfType(0))
        {
            HwBios Entry;
            Entry.Id = static_cast<int>(InHwid.BiosFirmwares.size());
            Entry.Manufacturer = Bios->GetString(0x04);
            Entry.Version = RegistryVersion ? RegistryVersion : Bios->GetString(0x05);
            Entry.SerialNumber = SystemSerialNumber;
            InHwid.BiosFirmwares.push_back(std::move(Entry));
        }
    }

    void RetrieveSmbiosTables(Hwid& InHwid, const SmbiosTable* InSmbios)
    {
        if (InSmbios == nullptr)
            return;

        HwSmbios Entry;
        Entry.Id = static_cast<int>(InHwid.SmbiosTables.size());
        Entry.Version = std::to_wstring(InSmbios->MajorVersion) + L"." + std::to_wstring(InSmbios->MinorVersion) + L"." + std::to_wstring(InSmbios->DmiRevision);
        Entry.Hash = Sha256Hex(InSmbios->Data);
        Entry.Length = static_cast<std::uint32_t>(InSmbios->Data.size());
        InHwid.SmbiosTables.push_back(std::move(Entry));
    }

    void RetrieveProcessors(Hwid& InHwid, const SmbiosTable* InSmbios)
    {
        if (InSmbios == nullptr)
            return;

        //
        // SMBIOS type 4: Processor Information, keeping only populated sockets (status bit 6).
        //

        std::vector<const SmbiosStructure*> Processors;

        for (auto Processor : InSmbios->OfType(4))
        {
            if (Processor->Length <= 0x18 || (Processor->GetByte(0x18) & 0x40) != 0)
                Processors.push_back(Processor);
        }

        if (Processors.empty())
            return;

        //
        // WMI takes the vendor, the name and the clock speed from the registry (the CPUID strings and the boot-time calibration), and the
        // identifier from the SMBIOS record (which hypervisors may leave zeroed). CPUID is only used when the record has no identifier.
        // The name is not trimmed: AMD pads it to 48 characters, and WMI keeps the padding.
        //

        auto ProcessorKey = RegistryKey::OpenLocalMachine(L"HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\0");
        auto RegistryName = ProcessorKey.GetString(L"ProcessorNameString");
        auto RegistryVendor = ProcessorKey.GetString(L"VendorIdentifier");
        auto RegistrySpeed = ProcessorKey.GetDword(L"~MHz");

        auto CpuId = GetProcessorIdFromCpuId();
        auto Topology = GetProcessorTopology();
        auto PackageCount = std::max(Topology.Packages, 1);
        auto ProcessorCount = GetActiveProcessorCount(ALL_PROCESSOR_GROUPS);

        for (auto Processor : Processors)
        {
            auto Index = static_cast<int>(InHwid.Processors.size());
            auto CurrentSpeed = Processor->GetWord(0x16);
            std::uint32_t CoreCount = Processor->GetByte(0x23) == 0xFF ? Processor->GetWord(0x2A) : Processor->GetByte(0x23);
            std::uint32_t ThreadCount = Processor->GetByte(0x25) == 0xFF ? Processor->GetWord(0x2E) : Processor->GetByte(0x25);

            HwProcessor Entry;
            Entry.Id = Index;
            Entry.Manufacturer = RegistryVendor ? RegistryVendor : Processor->GetString(0x07);

            if (RegistryName)
                Entry.Model = RegistryName;
            else if (auto Version = Processor->GetString(0x10))
                Entry.Model = Trim(*Version);

            Entry.ModelNumber = FormatProcessorId(Processor->GetBytes(0x08, 8));

            if (!Entry.ModelNumber)
                Entry.ModelNumber = CpuId;

            Entry.Socket = Processor->GetString(0x04);
            Entry.SerialNumber = Processor->GetString(0x20);
            Entry.PartNumber = Processor->GetString(0x22);
            Entry.ClockSpeed = std::to_wstring(RegistrySpeed && *RegistrySpeed > 0 ? *RegistrySpeed : static_cast<std::int32_t>(CurrentSpeed)) + L" MHz";
            Entry.Voltage = FormatProcessorVoltage(Processor->GetByte(0x11));
            Entry.Channel = L"CPU" + std::to_wstring(Index);
            Entry.NumberOfCores = Topology.Cores > 0 ? static_cast<std::uint32_t>(std::max(Topology.Cores / PackageCount, 1)) : (CoreCount != 0 ? CoreCount : ProcessorCount);
            Entry.NumberOfLogicalProcessors = Topology.LogicalProcessors > 0 ? static_cast<std::uint32_t>(std::max(Topology.LogicalProcessors / PackageCount, 1)) : (ThreadCount != 0 ? ThreadCount : ProcessorCount);
            InHwid.Processors.push_back(std::move(Entry));
        }
    }

    void RetrieveMemorySticks(Hwid& InHwid, const SmbiosTable* InSmbios)
    {
        if (InSmbios == nullptr)
            return;

        //
        // SMBIOS type 17: Memory Device, skipping empty slots like WMI does.
        //

        for (auto MemoryStick : InSmbios->OfType(17))
        {
            auto Capacity = GetMemoryDeviceCapacity(*MemoryStick);

            if (Capacity == 0)
                continue;

            auto ClockSpeed = MemoryStick->Length > 0x21 ? MemoryStick->GetWord(0x20) : MemoryStick->GetWord(0x15);

            //
            // The configured voltage is in millivolts; .NET prints it with two decimals, rounding halves away from zero.
            //

            auto Millivolts = static_cast<std::uint32_t>(MemoryStick->GetWord(0x26));

            HwMemoryStick Entry;
            Entry.Id = static_cast<int>(InHwid.MemorySticks.size());
            Entry.Manufacturer = MemoryStick->GetString(0x17);
            Entry.Capacity = std::to_wstring(Capacity / 1024 / 1024 / 1024) + L" GB";
            Entry.ClockSpeed = std::to_wstring(ClockSpeed) + L" MHz";
            Entry.Voltage = FormatHundredths((Millivolts + 5) / 10) + L" V";
            Entry.SerialNumber = MemoryStick->GetString(0x18);
            Entry.PartNumber = MemoryStick->GetString(0x1A);
            Entry.Channel = MemoryStick->GetString(0x10);
            InHwid.MemorySticks.push_back(std::move(Entry));
        }
    }
}
