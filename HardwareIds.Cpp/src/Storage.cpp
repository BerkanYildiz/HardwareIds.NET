#include "Internal.hpp"

#include <winioctl.h>

#include <algorithm>
#include <cstring>

namespace HardwareIds::Detail
{
    static constexpr std::wstring_view NullAndSpace(L"\0 ", 2);

    static constexpr DWORD StorageDeviceProperty = 0;
    static constexpr DWORD StorageDeviceIdProperty = 2;
    static constexpr DWORD StorageDeviceUniqueIdProperty = 3;
    static constexpr DWORD StorageDeviceProtocolSpecificProperty = 50;
    static constexpr DWORD ProtocolTypeAta = 2;
    static constexpr DWORD ProtocolTypeNvme = 3;
    static constexpr DWORD IdentifyDataType = 1;
    static constexpr std::uint32_t BusTypeAtapi = 2;
    static constexpr std::uint32_t BusTypeAta = 3;
    static constexpr std::uint32_t BusTypeSata = 11;
    static constexpr std::uint32_t BusTypeNvme = 17;
    static constexpr std::size_t ProtocolSpecificDataSize = 40;
    static constexpr std::size_t DriveLayoutHeaderSize = 48;
    static constexpr std::size_t PartitionInformationSize = 144;
    static constexpr int MaxPartitions = 256;
    static constexpr GUID PartitionMsftReservedGuid = { 0xE3C9E316, 0x0B5C, 0x4DB8, { 0x81, 0x7D, 0xF9, 0x2D, 0xF0, 0x02, 0x15, 0xAE } };

    static std::uint16_t ReadUInt16(ByteSpan InData, std::size_t InOffset)
    {
        std::uint16_t Value;
        std::memcpy(&Value, InData.data() + InOffset, sizeof(Value));
        return Value;
    }

    static std::uint32_t ReadUInt32(ByteSpan InData, std::size_t InOffset)
    {
        std::uint32_t Value;
        std::memcpy(&Value, InData.data() + InOffset, sizeof(Value));
        return Value;
    }

    static std::uint64_t ReadUInt64(ByteSpan InData, std::size_t InOffset)
    {
        std::uint64_t Value;
        std::memcpy(&Value, InData.data() + InOffset, sizeof(Value));
        return Value;
    }

    //
    // Device I/O.
    //

    struct StorageDeviceDescriptor
    {
        bool RemovableMedia = false;
        std::uint32_t BusType = 0;
        NullableString VendorId;
        NullableString ProductId;
        NullableString ProductRevision;
        NullableString SerialNumber;
    };

    static UniqueHandle OpenStorageDevice(const std::wstring& InPath)
    {
        //
        // No access rights requested: enough to query the device, without needing read access to its contents (or elevation).
        //

        return UniqueHandle(CreateFileW(InPath.c_str(), 0, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, 0, nullptr));
    }

    static std::optional<Bytes> DeviceIoControlBytes(HANDLE InHandle, DWORD InCode, const void* InInput, DWORD InInputSize, std::size_t InOutputSize)
    {
        Bytes Output(InOutputSize);
        DWORD Returned = 0;

        if (!DeviceIoControl(InHandle, InCode, const_cast<void*>(InInput), InInputSize, Output.data(), static_cast<DWORD>(Output.size()), &Returned, nullptr))
            return std::nullopt;

        Output.resize(std::min<std::size_t>(Returned, Output.size()));
        return Output;
    }

    static std::optional<Bytes> QueryProperty(HANDLE InHandle, DWORD InPropertyId, ByteSpan InAdditionalParameters, std::size_t InOutputSize)
    {
        //
        // STORAGE_PROPERTY_QUERY: PropertyId, QueryType (standard), then the additional parameters.
        //

        Bytes Query(std::max<std::size_t>(12, 8 + InAdditionalParameters.size()));
        std::memcpy(Query.data(), &InPropertyId, sizeof(InPropertyId));

        if (!InAdditionalParameters.empty())
            std::memcpy(Query.data() + 8, InAdditionalParameters.data(), InAdditionalParameters.size());

        return DeviceIoControlBytes(InHandle, IOCTL_STORAGE_QUERY_PROPERTY, Query.data(), static_cast<DWORD>(Query.size()), InOutputSize);
    }

    static NullableString ReadOffsetString(ByteSpan InBuffer, std::size_t InOffsetField)
    {
        auto Offset = ReadUInt32(InBuffer, InOffsetField);

        if (Offset == 0 || Offset == 0xFFFFFFFF || Offset >= InBuffer.size())
            return std::nullopt;

        auto End = std::find(InBuffer.begin() + Offset, InBuffer.end(), std::uint8_t{ 0 });
        return DecodeAscii(ByteSpan(InBuffer.data() + Offset, static_cast<std::size_t>(End - (InBuffer.begin() + Offset))));
    }

    static std::optional<StorageDeviceDescriptor> GetDeviceDescriptor(HANDLE InHandle)
    {
        auto Header = QueryProperty(InHandle, StorageDeviceProperty, {}, 8);

        if (!Header || Header->size() < 8)
            return std::nullopt;

        auto Buffer = QueryProperty(InHandle, StorageDeviceProperty, {}, std::max<std::uint32_t>(ReadUInt32(*Header, 4), 36));

        if (!Buffer || Buffer->size() < 36)
            return std::nullopt;

        StorageDeviceDescriptor Descriptor;
        Descriptor.RemovableMedia = (*Buffer)[10] != 0;
        Descriptor.BusType = ReadUInt32(*Buffer, 28);
        Descriptor.VendorId = ReadOffsetString(*Buffer, 12);
        Descriptor.ProductId = ReadOffsetString(*Buffer, 16);
        Descriptor.ProductRevision = ReadOffsetString(*Buffer, 20);
        Descriptor.SerialNumber = ReadOffsetString(*Buffer, 24);
        return Descriptor;
    }

    static std::optional<int> GetDeviceNumber(HANDLE InHandle)
    {
        auto Buffer = DeviceIoControlBytes(InHandle, IOCTL_STORAGE_GET_DEVICE_NUMBER, nullptr, 0, 12);

        if (!Buffer || Buffer->size() < 8)
            return std::nullopt;

        return static_cast<int>(ReadUInt32(*Buffer, 4));
    }

    static std::optional<std::uint64_t> GetSize(HANDLE InHandle)
    {
        auto Buffer = DeviceIoControlBytes(InHandle, IOCTL_DISK_GET_DRIVE_GEOMETRY_EX, nullptr, 0, 1024);

        if (!Buffer || Buffer->size() < 32)
            return std::nullopt;

        //
        // WMI computes the size from the geometry (cylinders * tracks per cylinder * sectors per track * bytes per sector).
        //

        auto Size = ReadUInt64(*Buffer, 0) * ReadUInt32(*Buffer, 12) * ReadUInt32(*Buffer, 16) * ReadUInt32(*Buffer, 20);
        return Size != 0 ? Size : ReadUInt64(*Buffer, 24);
    }

    static std::optional<Bytes> GetDriveLayout(HANDLE InHandle)
    {
        Bytes Buffer(DriveLayoutHeaderSize + MaxPartitions * PartitionInformationSize);
        DWORD Returned = 0;

        if (!DeviceIoControl(InHandle, IOCTL_DISK_GET_DRIVE_LAYOUT_EX, nullptr, 0, Buffer.data(), static_cast<DWORD>(Buffer.size()), &Returned, nullptr))
            return std::nullopt;

        return Buffer;
    }

    static std::optional<int> GetPartitionCount(const std::optional<Bytes>& InLayout)
    {
        if (!InLayout)
            return std::nullopt;

        const auto& Buffer = *InLayout;
        auto Style = ReadUInt32(Buffer, 0);
        auto Count = std::min(static_cast<int>(ReadUInt32(Buffer, 4)), MaxPartitions);
        auto Result = 0;

        for (auto I = 0; I < Count; I++)
        {
            auto Entry = DriveLayoutHeaderSize + static_cast<std::size_t>(I) * PartitionInformationSize;

            if (Style == 0)
            {
                // MBR: unused entries and extended partition containers are not partitions.
                auto PartitionType = Buffer[Entry + 32];

                if (PartitionType != 0x00 && PartitionType != 0x05 && PartitionType != 0x0F && PartitionType != 0x85)
                    Result++;
            }
            else if (Style == 1)
            {
                // GPT: WMI does not report empty entries nor the Microsoft Reserved partition.
                auto TypeGuid = GuidFromBytes(ByteSpan(Buffer.data() + Entry + 32, 16));

                if (TypeGuid != GUID{} && TypeGuid != PartitionMsftReservedGuid)
                    Result++;
            }
        }

        return Result;
    }

    static bool SupportsFailurePrediction(HANDLE InHandle)
    {
        return DeviceIoControlBytes(InHandle, IOCTL_STORAGE_PREDICT_FAILURE, nullptr, 0, 516).has_value();
    }

    static std::optional<Bytes> QueryProtocolData(HANDLE InHandle, DWORD InProtocolType, DWORD InDataType, DWORD InRequestValue, DWORD InRequestSubValue, std::size_t InDataLength)
    {
        //
        // STORAGE_PROTOCOL_SPECIFIC_DATA: the data follows the structure in the output buffer.
        //

        std::uint32_t Parameters[10] = {};
        Parameters[0] = InProtocolType;
        Parameters[1] = InDataType;
        Parameters[2] = InRequestValue;
        Parameters[3] = InRequestSubValue;
        Parameters[4] = static_cast<std::uint32_t>(ProtocolSpecificDataSize);
        Parameters[5] = static_cast<std::uint32_t>(InDataLength);

        auto Output = QueryProperty(InHandle, StorageDeviceProtocolSpecificProperty, ByteSpan(reinterpret_cast<const std::uint8_t*>(Parameters), sizeof(Parameters)), 8 + ProtocolSpecificDataSize + InDataLength);

        if (!Output || Output->size() < 8 + ProtocolSpecificDataSize)
            return std::nullopt;

        auto DataOffset = 8 + static_cast<std::size_t>(ReadUInt32(*Output, 8 + 16));
        auto DataLength = static_cast<std::size_t>(ReadUInt32(*Output, 8 + 20));

        if (DataLength == 0 || DataOffset + DataLength > Output->size())
            return std::nullopt;

        return Bytes(Output->begin() + static_cast<std::ptrdiff_t>(DataOffset), Output->begin() + static_cast<std::ptrdiff_t>(DataOffset + DataLength));
    }

    static NullableString GetUniqueId(HANDLE InHandle)
    {
        auto Buffer = QueryProperty(InHandle, StorageDeviceUniqueIdProperty, {}, 4096);

        if (!Buffer || Buffer->size() < 20)
            return std::nullopt;

        //
        // The DUID is a few hundred bytes (it embeds the device descriptor and the SCSI identifiers), so only its hash is kept.
        //

        auto Size = std::min<std::size_t>(ReadUInt32(*Buffer, 4), Buffer->size());

        if (Size <= 20)
            return std::nullopt;

        return Sha256Hex(ByteSpan(Buffer->data(), Size));
    }

    //
    // Parsers.
    //

    std::wstring ScsiDeviceIdentifier::Text() const
    {
        switch (this->CodeSet)
        {
            case ScsiCodeSetAscii:
                return TrimCharacters(DecodeAscii(this->Value), NullAndSpace);

            case ScsiCodeSetUtf8:
                return TrimCharacters(DecodeUtf8(this->Value), NullAndSpace);

            default:
                return FormatHex(this->Value);
        }
    }

    std::vector<ScsiDeviceIdentifier> ParseDeviceIdentifiers(ByteSpan InDescriptor)
    {
        //
        // STORAGE_DEVICE_ID_DESCRIPTOR: a header followed by STORAGE_IDENTIFIER structures, one per SCSI VPD page 0x83 descriptor.
        //

        constexpr std::size_t HeaderSize = 12;
        constexpr std::size_t IdentifierHeaderSize = 16;
        std::vector<ScsiDeviceIdentifier> Result;

        if (InDescriptor.size() < HeaderSize)
            return Result;

        auto Count = static_cast<int>(ReadUInt32(InDescriptor, 8));
        std::size_t Offset = HeaderSize;

        for (auto I = 0; I < Count && Offset + IdentifierHeaderSize <= InDescriptor.size(); I++)
        {
            auto Size = ReadUInt16(InDescriptor, Offset + 8);
            auto NextOffset = ReadUInt16(InDescriptor, Offset + 10);
            auto Available = std::min<std::size_t>(Size, InDescriptor.size() - Offset - IdentifierHeaderSize);

            ScsiDeviceIdentifier Identifier;
            Identifier.CodeSet = static_cast<int>(ReadUInt32(InDescriptor, Offset));
            Identifier.Type = static_cast<int>(ReadUInt32(InDescriptor, Offset + 4));
            Identifier.Association = static_cast<int>(ReadUInt32(InDescriptor, Offset + 12));
            Identifier.Value.assign(InDescriptor.begin() + static_cast<std::ptrdiff_t>(Offset + IdentifierHeaderSize), InDescriptor.begin() + static_cast<std::ptrdiff_t>(Offset + IdentifierHeaderSize + Available));
            Result.push_back(std::move(Identifier));

            if (NextOffset == 0)
                break;

            Offset += NextOffset;
        }

        return Result;
    }

    static NullableString ReadAsciiField(ByteSpan InData, std::size_t InOffset, std::size_t InLength)
    {
        auto Value = TrimCharacters(DecodeAscii(InData.subspan(InOffset, InLength)), NullAndSpace);
        return Value.empty() ? NullableString{} : NullableString{ Value };
    }

    static NullableString ReadHexField(ByteSpan InData, std::size_t InOffset, std::size_t InLength)
    {
        auto Field = InData.subspan(InOffset, InLength);
        return std::any_of(Field.begin(), Field.end(), [](auto InByte) { return InByte != 0; }) ? NullableString{ FormatHex(Field) } : NullableString{};
    }

    std::optional<NvmeControllerIdentity> ParseNvmeControllerIdentity(ByteSpan InData)
    {
        if (InData.size() < 128)
            return std::nullopt;

        NvmeControllerIdentity Identity;
        Identity.SerialNumber = ReadAsciiField(InData, 4, 20);
        Identity.ModelNumber = ReadAsciiField(InData, 24, 40);
        Identity.FirmwareRevision = ReadAsciiField(InData, 64, 8);
        Identity.FruGuid = ReadHexField(InData, 112, 16);
        return Identity;
    }

    std::optional<NvmeNamespaceIdentity> ParseNvmeNamespaceIdentity(ByteSpan InData)
    {
        if (InData.size() < 128)
            return std::nullopt;

        NvmeNamespaceIdentity Identity;
        Identity.Nguid = ReadHexField(InData, 104, 16);
        Identity.Eui64 = ReadHexField(InData, 120, 8);
        return Identity;
    }

    static NullableString ReadAtaString(ByteSpan InData, std::size_t InWord, std::size_t InWordCount)
    {
        //
        // ATA strings store their characters swapped within each 16-bit word.
        //

        Bytes Value(InWordCount * 2);

        for (std::size_t I = 0; I < InWordCount; I++)
        {
            Value[I * 2] = InData[(InWord + I) * 2 + 1];
            Value[I * 2 + 1] = InData[(InWord + I) * 2];
        }

        auto Text = TrimCharacters(DecodeAscii(Value), NullAndSpace);
        return Text.empty() ? NullableString{} : NullableString{ Text };
    }

    std::optional<AtaIdentity> ParseAtaIdentity(ByteSpan InData)
    {
        if (InData.size() < 512)
            return std::nullopt;

        auto WwnSupported = (ReadUInt16(InData, 84 * 2) & 0x0100) != 0 || (ReadUInt16(InData, 87 * 2) & 0x0100) != 0;
        auto WorldWideName = (static_cast<std::uint64_t>(ReadUInt16(InData, 108 * 2)) << 48)
                           | (static_cast<std::uint64_t>(ReadUInt16(InData, 109 * 2)) << 32)
                           | (static_cast<std::uint64_t>(ReadUInt16(InData, 110 * 2)) << 16)
                           | ReadUInt16(InData, 111 * 2);

        AtaIdentity Identity;
        Identity.SerialNumber = ReadAtaString(InData, 10, 10);
        Identity.FirmwareRevision = ReadAtaString(InData, 23, 4);
        Identity.ModelNumber = ReadAtaString(InData, 27, 20);

        if (WwnSupported && WorldWideName != 0)
        {
            wchar_t Buffer[20];
            swprintf_s(Buffer, L"%016llX", static_cast<unsigned long long>(WorldWideName));
            Identity.WorldWideName = Buffer;
        }

        return Identity;
    }

    NullableString ParseDiskIdentifier(ByteSpan InLayout)
    {
        if (InLayout.size() < 24)
            return std::nullopt;

        switch (ReadUInt32(InLayout, 0))
        {
            case 0:
            {
                // MBR: the disk signature.
                auto Signature = ReadUInt32(InLayout, 8);

                if (Signature == 0)
                    return std::nullopt;

                wchar_t Buffer[16];
                swprintf_s(Buffer, L"0x%08X", Signature);
                return Buffer;
            }

            case 1:
            {
                // GPT: the disk GUID.
                auto DiskId = GuidFromBytes(InLayout.subspan(8, 16));
                return DiskId != GUID{} ? NullableString{ FormatGuid(DiskId) } : NullableString{};
            }

            default:
                return std::nullopt;
        }
    }

    std::wstring GetDiskInterfaceType(const NullableString& InInstanceId, std::optional<std::uint32_t> InBusType)
    {
        NullableString Enumerator;

        if (InInstanceId)
            Enumerator = ToUpperInvariant(InInstanceId->substr(0, InInstanceId->find(L'\\')));

        if (Enumerator)
        {
            if (*Enumerator == L"USBSTOR")
                return L"USB";

            if (*Enumerator == L"SCSI" || *Enumerator == L"IDE" || *Enumerator == L"1394")
                return *Enumerator;
        }

        switch (InBusType.value_or(0xFFFFFFFF))
        {
            case 1: case 6: case 8: case 9: case 10: case 11: case 14: case 15: case 16: case 17:
                return L"SCSI";

            case 2: case 3:
                return L"IDE";

            case 4:
                return L"1394";

            case 7:
                return L"USB";

            default:
                return Enumerator ? *Enumerator : L"Unknown";
        }
    }

    //
    // Disks.
    //

    static NullableString Clean(const NullableString& InValue)
    {
        return IsNullOrWhiteSpace(InValue) ? NullableString{} : InValue;
    }

    static void RetrieveDiskIdentifiers(HwDisk& InDisk, HANDLE InHandle, const std::optional<StorageDeviceDescriptor>& InDescriptor)
    {
        //
        // A drive can legitimately report several serial numbers depending on the layer asked, so each one is kept in its own field.
        //

        auto BusType = InDescriptor ? InDescriptor->BusType : 0xFFFFFFFF;

        if (BusType == BusTypeNvme)
        {
            auto ControllerData = QueryProtocolData(InHandle, ProtocolTypeNvme, IdentifyDataType, 1, 0, 4096);
            auto NamespaceData = QueryProtocolData(InHandle, ProtocolTypeNvme, IdentifyDataType, 0, 1, 4096);
            auto Controller = ControllerData ? ParseNvmeControllerIdentity(*ControllerData) : std::nullopt;
            auto Namespace = NamespaceData ? ParseNvmeNamespaceIdentity(*NamespaceData) : std::nullopt;

            InDisk.NvmeSerial = Clean(Controller ? Controller->SerialNumber : std::nullopt);
            InDisk.NvmeFguid = Clean(Controller ? Controller->FruGuid : std::nullopt);
            InDisk.NvmeNguid = Clean(Namespace ? Namespace->Nguid : std::nullopt);
            InDisk.NvmeEui64 = Clean(Namespace ? Namespace->Eui64 : std::nullopt);

            if (auto Firmware = Clean(Controller ? Controller->FirmwareRevision : std::nullopt))
                InDisk.Firmware = Firmware;

            if (!InDisk.WorldWideName)
                InDisk.WorldWideName = InDisk.NvmeEui64 ? InDisk.NvmeEui64 : InDisk.NvmeNguid;
        }
        else if (BusType == BusTypeAta || BusType == BusTypeSata || BusType == BusTypeAtapi)
        {
            auto Data = QueryProtocolData(InHandle, ProtocolTypeAta, IdentifyDataType, 0, 0, 512);
            auto Ata = Data ? ParseAtaIdentity(*Data) : std::nullopt;

            InDisk.AtaSerial = Clean(Ata ? Ata->SerialNumber : std::nullopt);
            InDisk.AtaWwn = Clean(Ata ? Ata->WorldWideName : std::nullopt);

            if (auto Firmware = Clean(Ata ? Ata->FirmwareRevision : std::nullopt))
                InDisk.Firmware = Firmware;

            if (!InDisk.WorldWideName)
                InDisk.WorldWideName = InDisk.AtaWwn;
        }

        auto Descriptor = QueryProperty(InHandle, StorageDeviceIdProperty, {}, 4096);

        for (const auto& Identifier : Descriptor ? ParseDeviceIdentifiers(*Descriptor) : std::vector<ScsiDeviceIdentifier>{})
        {
            if (Identifier.Association != ScsiAssociationLogicalUnit)
                continue;

            auto Text = Clean(Identifier.Text());

            if (!Text)
                continue;

            switch (Identifier.Type)
            {
                case ScsiTypeVendorSpecific:
                    if (!InDisk.VpdVendor) InDisk.VpdVendor = Text;
                    break;

                case ScsiTypeT10VendorId:
                    if (!InDisk.VpdT10) InDisk.VpdT10 = Text;
                    break;

                case ScsiTypeEui64:
                    if (Identifier.Value.size() == 16)
                    {
                        if (!InDisk.VpdNguid) InDisk.VpdNguid = Text;
                    }
                    else if (!InDisk.VpdEui64)
                    {
                        InDisk.VpdEui64 = Text;
                    }
                    break;

                case ScsiTypeNaa:
                    if (!InDisk.VpdNaa) InDisk.VpdNaa = Text;
                    break;

                case ScsiTypeScsiNameString:
                    if (!InDisk.VpdScsiName) InDisk.VpdScsiName = Text;
                    break;
            }
        }

        if (!InDisk.WorldWideName)
            InDisk.WorldWideName = InDisk.VpdNaa ? InDisk.VpdNaa : InDisk.VpdEui64;

        InDisk.Duid = GetUniqueId(InHandle);
    }

    void RetrieveDiskDrives(Hwid& InHwid)
    {
        std::vector<HwDisk> Entries;

        for (const auto& InterfacePath : GetDeviceInterfaces(GUID_DEVINTERFACE_DISK_))
        {
            auto Handle = OpenStorageDevice(InterfacePath);

            if (!Handle)
                continue;

            auto Descriptor = GetDeviceDescriptor(Handle.Get());
            auto InstanceId = GetInterfaceProperty(InterfacePath, DEVPKEY_Device_InstanceId_);
            auto DevNode = InstanceId ? LocateDevNode(*InstanceId) : std::nullopt;
            auto FriendlyName = DevNode ? GetDevNodeProperty(*DevNode, DEVPKEY_Device_FriendlyName_) : std::nullopt;
            auto Size = GetSize(Handle.Get()).value_or(0);
            auto Layout = GetDriveLayout(Handle.Get());

            HwDisk Entry;
            Entry.Id = GetDeviceNumber(Handle.Get()).value_or(static_cast<int>(Entries.size()));
            Entry.Interface = GetDiskInterfaceType(InstanceId, Descriptor ? std::optional<std::uint32_t>(Descriptor->BusType) : std::nullopt);

            if (FriendlyName)
            {
                Entry.Model = FriendlyName;
            }
            else
            {
                std::wstring Model;

                for (const auto& Part : { Descriptor ? Descriptor->VendorId : std::nullopt, Descriptor ? Descriptor->ProductId : std::nullopt })
                {
                    if (!Part)
                        continue;

                    auto Trimmed = Trim(*Part);

                    if (Trimmed.empty())
                        continue;

                    if (!Model.empty())
                        Model += L' ';

                    Model += Trimmed;
                }

                Entry.Model = Model;
            }

            Entry.SerialNumber = Descriptor ? Descriptor->SerialNumber : std::nullopt;
            Entry.Capacity = std::to_wstring(Size / 1024 / 1024 / 1024) + L" GB";
            Entry.Partitions = GetPartitionCount(Layout).value_or(0);
            Entry.IsRemovable = Descriptor ? Descriptor->RemovableMedia : false;
            Entry.IsSMART = SupportsFailurePrediction(Handle.Get());

            if (Descriptor && Descriptor->ProductRevision)
                Entry.Firmware = Trim(*Descriptor->ProductRevision);

            Entry.DiskGuid = Layout ? ParseDiskIdentifier(*Layout) : std::nullopt;
            Entry.InstanceId = InstanceId;

            RetrieveDiskIdentifiers(Entry, Handle.Get(), Descriptor);
            Entries.push_back(std::move(Entry));
        }

        std::stable_sort(Entries.begin(), Entries.end(), [](const HwDisk& InLeft, const HwDisk& InRight) { return InLeft.Id < InRight.Id; });
        InHwid.Disks.insert(InHwid.Disks.end(), std::make_move_iterator(Entries.begin()), std::make_move_iterator(Entries.end()));
    }

    //
    // Volumes.
    //

    static NullableString GetVolumeDriveLetter(const std::wstring& InVolumePath)
    {
        wchar_t Buffer[1024];
        DWORD Length = 0;

        if (!GetVolumePathNamesForVolumeNameW(InVolumePath.c_str(), Buffer, static_cast<DWORD>(std::size(Buffer)), &Length))
            return std::nullopt;

        for (const auto& Path : SplitMultiString(Buffer, std::min<std::size_t>(Length, std::size(Buffer))))
        {
            if (Path.size() == 3 && Path[1] == L':' && Path[2] == L'\\')
                return Path.substr(0, 2);
        }

        return std::nullopt;
    }

    void RetrieveDiskVolumes(Hwid& InHwid)
    {
        //
        // Prevent Windows from showing an "insert a disk" dialog for removable drives without media.
        //

        DWORD PreviousErrorMode = 0;
        SetThreadErrorMode(SEM_FAILCRITICALERRORS, &PreviousErrorMode);

        wchar_t VolumeName[260];
        auto Find = FindFirstVolumeW(VolumeName, static_cast<DWORD>(std::size(VolumeName)));

        if (Find != INVALID_HANDLE_VALUE)
        {
            do
            {
                std::wstring Path = VolumeName;
                DWORD SerialNumber = 0;

                if (!GetVolumeInformationW(Path.c_str(), nullptr, 0, &SerialNumber, nullptr, nullptr, nullptr, 0))
                    SerialNumber = 0;

                HwVolume Entry;
                Entry.Id = static_cast<int>(InHwid.Volumes.size());
                Entry.Path = Path;
                Entry.Letter = GetVolumeDriveLetter(Path);
                Entry.SerialNumber = SerialNumber;
                InHwid.Volumes.push_back(std::move(Entry));
            }
            while (FindNextVolumeW(Find, VolumeName, static_cast<DWORD>(std::size(VolumeName))));

            FindVolumeClose(Find);
        }

        SetThreadErrorMode(PreviousErrorMode, nullptr);
    }
}
