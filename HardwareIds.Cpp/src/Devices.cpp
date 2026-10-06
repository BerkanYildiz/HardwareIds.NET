#include "Internal.hpp"

#include <winspool.h>
#include <bluetoothapis.h>

#include <algorithm>
#include <cmath>
#include <cstring>

namespace HardwareIds::Detail
{
    static constexpr std::wstring_view NullAndSpace(L"\0 ", 2);

    //
    // EDID and monitors.
    //

    static std::wstring ReadEdidText(ByteSpan InData, std::size_t InStart)
    {
        std::wstring Result;

        for (std::size_t I = 0; I < 13; I++)
        {
            auto Character = InData[InStart + I];

            if (Character == 0x0A || Character == 0x00)
                break;

            Result += static_cast<wchar_t>(Character);
        }

        return Trim(Result);
    }

    std::optional<EdidInfo> ParseEdid(const std::optional<Bytes>& InData)
    {
        static constexpr std::uint8_t Header[] = { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 };

        if (!InData || InData->size() < 128 || !std::equal(std::begin(Header), std::end(Header), InData->begin()))
            return std::nullopt;

        const auto& Data = *InData;

        //
        // Manufacturer: three 5-bit letters, big-endian. Product code and serial number: little-endian.
        //

        auto ManufacturerId = (Data[8] << 8) | Data[9];
        std::wstring Manufacturer;
        Manufacturer += static_cast<wchar_t>(L'A' - 1 + ((ManufacturerId >> 10) & 0x1F));
        Manufacturer += static_cast<wchar_t>(L'A' - 1 + ((ManufacturerId >> 5) & 0x1F));
        Manufacturer += static_cast<wchar_t>(L'A' - 1 + (ManufacturerId & 0x1F));

        wchar_t ProductCode[8];
        swprintf_s(ProductCode, L"%04X", Data[10] | (Data[11] << 8));

        std::uint32_t Serial;
        std::memcpy(&Serial, Data.data() + 12, sizeof(Serial));

        NullableString Name;
        NullableString SerialText;

        for (std::size_t Block = 54; Block + 18 <= 126; Block += 18)
        {
            if (Data[Block] != 0 || Data[Block + 1] != 0 || Data[Block + 2] != 0)
                continue;

            switch (Data[Block + 3])
            {
                case 0xFC:
                    Name = ReadEdidText(Data, Block + 5);
                    break;

                case 0xFF:
                    SerialText = ReadEdidText(Data, Block + 5);
                    break;
            }
        }

        EdidInfo Info;
        Info.Manufacturer = Manufacturer;
        Info.ProductCode = std::wstring(ProductCode);
        Info.SerialNumber = SerialText ? *SerialText : std::to_wstring(Serial);
        Info.Name = Name ? *Name : std::wstring();
        Info.Hash = Sha256Hex(ByteSpan(Data.data(), 128));
        Info.ManufactureWeek = Data[16] == 0xFF ? 0 : Data[16];
        Info.ManufactureYear = Data[17] != 0 ? 1990 + Data[17] : 0;
        return Info;
    }

    void RetrieveMonitors(Hwid& InHwid)
    {
        for (const auto& InterfacePath : GetDeviceInterfaces(GUID_DEVINTERFACE_MONITOR_))
        {
            auto InstanceId = GetInterfaceProperty(InterfacePath, DEVPKEY_Device_InstanceId_);

            if (!InstanceId)
                continue;

            //
            // The EDID block Windows read from the monitor is cached under the device's "Device Parameters" key.
            //

            auto ParametersKey = RegistryKey::OpenLocalMachine(L"SYSTEM\\CurrentControlSet\\Enum\\" + *InstanceId + L"\\Device Parameters");
            auto Info = ParseEdid(ParametersKey.GetBinary(L"EDID"));

            if (!Info)
                continue;

            HwMonitor Entry;
            Entry.Id = static_cast<int>(InHwid.Monitors.size());
            Entry.Manufacturer = Info->Manufacturer;
            Entry.Name = Info->Name;
            Entry.Product = Info->ProductCode;
            Entry.SerialNumber = Info->SerialNumber;
            Entry.InstanceId = InstanceId;
            Entry.EdidHash = Info->Hash;
            Entry.ManufactureWeek = Info->ManufactureWeek;
            Entry.ManufactureYear = Info->ManufactureYear;
            InHwid.Monitors.push_back(std::move(Entry));
        }
    }

    //
    // Video controllers.
    //

    struct DisplayMode
    {
        std::wstring InstanceId;
        std::uint32_t Width = 0;
        std::uint32_t Height = 0;
        std::uint32_t RefreshRate = 0;
    };

    static NullableString GetAdapterDevicePath(LUID InAdapterId)
    {
        DISPLAYCONFIG_ADAPTER_NAME Request = {};
        Request.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME;
        Request.header.size = sizeof(Request);
        Request.header.adapterId = InAdapterId;

        if (DisplayConfigGetDeviceInfo(&Request.header) != ERROR_SUCCESS)
            return std::nullopt;

        return std::wstring(Request.adapterDevicePath);
    }

    static std::vector<DisplayMode> GetActiveDisplayModes()
    {
        std::vector<DisplayMode> Result;

        for (auto Attempt = 0; Attempt < 3; Attempt++)
        {
            UINT32 PathCount = 0;
            UINT32 ModeCount = 0;

            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, &PathCount, &ModeCount) != ERROR_SUCCESS || PathCount == 0)
                return Result;

            std::vector<DISPLAYCONFIG_PATH_INFO> Paths(PathCount);
            std::vector<DISPLAYCONFIG_MODE_INFO> Modes(std::max<UINT32>(ModeCount, 1));
            auto Status = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, &PathCount, Paths.data(), &ModeCount, Modes.data(), nullptr);

            if (Status == ERROR_INSUFFICIENT_BUFFER)
                continue;

            if (Status != ERROR_SUCCESS)
                return Result;

            for (UINT32 I = 0; I < PathCount; I++)
            {
                const auto& Path = Paths[I];

                if ((Path.flags & DISPLAYCONFIG_PATH_ACTIVE) == 0)
                    continue;

                auto InstanceId = InterfaceNameToInstanceId(GetAdapterDevicePath(Path.sourceInfo.adapterId));

                if (!InstanceId || std::any_of(Result.begin(), Result.end(), [&](const DisplayMode& InMode) { return EqualsIgnoreCase(InMode.InstanceId, *InstanceId); }))
                    continue;

                DisplayMode Mode;
                Mode.InstanceId = *InstanceId;

                // Rounded like .NET's Math.Round: to the nearest integer, halves to even.
                if (Path.targetInfo.refreshRate.Denominator != 0)
                    Mode.RefreshRate = static_cast<std::uint32_t>(std::nearbyint(static_cast<double>(Path.targetInfo.refreshRate.Numerator) / Path.targetInfo.refreshRate.Denominator));

                auto SourceModeIndex = Path.sourceInfo.modeInfoIdx;

                if (SourceModeIndex != DISPLAYCONFIG_PATH_MODE_IDX_INVALID && SourceModeIndex < ModeCount && Modes[SourceModeIndex].infoType == DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
                {
                    Mode.Width = Modes[SourceModeIndex].sourceMode.width;
                    Mode.Height = Modes[SourceModeIndex].sourceMode.height;
                }

                Result.push_back(std::move(Mode));
            }

            return Result;
        }

        return Result;
    }

    void RetrieveVideoControllers(Hwid& InHwid)
    {
        auto DisplayModes = GetActiveDisplayModes();

        for (const auto& InstanceId : GetDeviceIds(GUID_DEVCLASS_DISPLAY_))
        {
            auto DevNode = LocateDevNode(InstanceId);

            if (!DevNode)
                continue;

            auto Mode = std::find_if(DisplayModes.begin(), DisplayModes.end(), [&](const DisplayMode& InMode) { return EqualsIgnoreCase(InMode.InstanceId, InstanceId); });
            auto HasMode = Mode != DisplayModes.end();

            HwVideo Entry;
            Entry.Id = static_cast<int>(InHwid.VideoControllers.size());
            Entry.Name = GetDevNodeProperty(*DevNode, DEVPKEY_Device_DeviceDesc_);
            Entry.Width = HasMode ? Mode->Width : 0;
            Entry.Height = HasMode ? Mode->Height : 0;
            Entry.RefreshRate = HasMode ? Mode->RefreshRate : 0;
            Entry.DriverDate = GetDevNodeDateProperty(*DevNode, DEVPKEY_Device_DriverDate_).value_or(DateTime{});
            Entry.DriverVersion = GetDevNodeProperty(*DevNode, DEVPKEY_Device_DriverVersion_);
            Entry.InstanceId = InstanceId;
            InHwid.VideoControllers.push_back(std::move(Entry));
        }
    }

    //
    // Printers.
    //

    static NullableString FromPointer(const wchar_t* InText)
    {
        return InText != nullptr ? NullableString{ InText } : NullableString{};
    }

    void RetrievePrinters(Hwid& InHwid)
    {
        constexpr DWORD Flags = PRINTER_ENUM_LOCAL | PRINTER_ENUM_CONNECTIONS;
        DWORD Needed = 0;
        DWORD Returned = 0;

        EnumPrintersW(Flags, nullptr, 2, nullptr, 0, &Needed, &Returned);

        if (Needed == 0)
            return;

        Bytes Buffer(Needed);

        if (!EnumPrintersW(Flags, nullptr, 2, Buffer.data(), Needed, &Needed, &Returned))
            return;

        auto Printers = reinterpret_cast<const PRINTER_INFO_2W*>(Buffer.data());

        for (DWORD I = 0; I < Returned; I++)
        {
            const auto& Printer = Printers[I];

            HwPrinter Entry;
            Entry.Id = static_cast<int>(InHwid.Printers.size());
            Entry.Name = FromPointer(Printer.pPrinterName);
            Entry.PortName = FromPointer(Printer.pPortName);
            Entry.Location = FromPointer(Printer.pLocation);

            if (Printer.pDevMode != nullptr)
            {
                if ((Printer.pDevMode->dmFields & DM_PRINTQUALITY) != 0 && Printer.pDevMode->dmPrintQuality > 0)
                    Entry.Width = static_cast<std::uint32_t>(Printer.pDevMode->dmPrintQuality);

                if ((Printer.pDevMode->dmFields & DM_YRESOLUTION) != 0 && Printer.pDevMode->dmYResolution > 0)
                    Entry.Height = static_cast<std::uint32_t>(Printer.pDevMode->dmYResolution);

                if (Entry.Height == 0)
                    Entry.Height = Entry.Width;
            }

            InHwid.Printers.push_back(std::move(Entry));
        }
    }

    //
    // Bluetooth radios.
    //

    Bytes BluetoothAddressToBytes(std::uint64_t InAddress)
    {
        return
        {
            static_cast<std::uint8_t>(InAddress >> 40),
            static_cast<std::uint8_t>(InAddress >> 32),
            static_cast<std::uint8_t>(InAddress >> 24),
            static_cast<std::uint8_t>(InAddress >> 16),
            static_cast<std::uint8_t>(InAddress >> 8),
            static_cast<std::uint8_t>(InAddress),
        };
    }

    void RetrieveBluetoothRadios(Hwid& InHwid)
    {
        //
        // The Bluetooth stack is optional (absent on most servers), so its library is loaded at runtime.
        //

        using FindFirstRadioFn = HBLUETOOTH_RADIO_FIND (WINAPI*)(const BLUETOOTH_FIND_RADIO_PARAMS*, HANDLE*);
        using FindNextRadioFn = BOOL (WINAPI*)(HBLUETOOTH_RADIO_FIND, HANDLE*);
        using FindRadioCloseFn = BOOL (WINAPI*)(HBLUETOOTH_RADIO_FIND);
        using GetRadioInfoFn = DWORD (WINAPI*)(HANDLE, PBLUETOOTH_RADIO_INFO);

        auto Module = LoadLibraryExW(L"bthprops.cpl", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);

        if (Module == nullptr)
            return;

        auto FindFirstRadio = reinterpret_cast<FindFirstRadioFn>(GetProcAddress(Module, "BluetoothFindFirstRadio"));
        auto FindNextRadio = reinterpret_cast<FindNextRadioFn>(GetProcAddress(Module, "BluetoothFindNextRadio"));
        auto FindRadioClose = reinterpret_cast<FindRadioCloseFn>(GetProcAddress(Module, "BluetoothFindRadioClose"));
        auto GetRadioInfo = reinterpret_cast<GetRadioInfoFn>(GetProcAddress(Module, "BluetoothGetRadioInfo"));

        if (FindFirstRadio && FindNextRadio && FindRadioClose && GetRadioInfo)
        {
            BLUETOOTH_FIND_RADIO_PARAMS Parameters = { sizeof(BLUETOOTH_FIND_RADIO_PARAMS) };
            HANDLE Radio = nullptr;
            auto Find = FindFirstRadio(&Parameters, &Radio);

            if (Find != nullptr)
            {
                do
                {
                    BLUETOOTH_RADIO_INFO Info = {};
                    Info.dwSize = sizeof(Info);

                    if (GetRadioInfo(Radio, &Info) == ERROR_SUCCESS)
                    {
                        HwBluetoothRadio Entry;
                        Entry.Id = static_cast<int>(InHwid.BluetoothRadios.size());
                        Entry.Address = FormatMacAddress(BluetoothAddressToBytes(Info.address.ullLong));
                        Entry.Name = std::wstring(Info.szName);
                        Entry.Manufacturer = Info.manufacturer;
                        Entry.ClassOfDevice = Info.ulClassofDevice;
                        Entry.LmpSubversion = Info.lmpSubversion;
                        InHwid.BluetoothRadios.push_back(std::move(Entry));
                    }

                    CloseHandle(Radio);
                }
                while (FindNextRadio(Find, &Radio));

                FindRadioClose(Find);
            }
        }

        FreeLibrary(Module);
    }

    //
    // Batteries.
    //

    std::optional<DateTime> ParseBatteryManufactureDate(std::uint8_t InDay, std::uint8_t InMonth, std::uint16_t InYear)
    {
        static constexpr int DaysInMonth[] = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

        if (InYear < 1980 || InYear > 9999 || InMonth < 1 || InMonth > 12 || InDay < 1)
            return std::nullopt;

        auto IsLeapYear = (InYear % 4 == 0 && InYear % 100 != 0) || InYear % 400 == 0;
        auto Days = DaysInMonth[InMonth - 1] + (InMonth == 2 && IsLeapYear ? 1 : 0);

        if (InDay > Days)
            return std::nullopt;

        return MakeDate(InYear, InMonth, InDay);
    }

    static std::optional<Bytes> QueryBatteryInformation(HANDLE InHandle, std::uint32_t InTag, std::uint32_t InLevel, std::size_t InOutputSize)
    {
        constexpr DWORD IOCTL_BATTERY_QUERY_INFORMATION = 0x00294044;

        // BATTERY_QUERY_INFORMATION: BatteryTag, InformationLevel, AtRate.
        std::uint32_t Query[3] = { InTag, InLevel, 0 };
        Bytes Output(InOutputSize);
        DWORD Returned = 0;

        if (!DeviceIoControl(InHandle, IOCTL_BATTERY_QUERY_INFORMATION, Query, sizeof(Query), Output.data(), static_cast<DWORD>(Output.size()), &Returned, nullptr))
            return std::nullopt;

        Output.resize(std::min<std::size_t>(Returned, Output.size()));
        return Output;
    }

    static NullableString QueryBatteryString(HANDLE InHandle, std::uint32_t InTag, std::uint32_t InLevel)
    {
        auto Output = QueryBatteryInformation(InHandle, InTag, InLevel, 1024);

        if (!Output)
            return std::nullopt;

        auto Value = TrimCharacters(std::wstring_view(reinterpret_cast<const wchar_t*>(Output->data()), Output->size() / sizeof(wchar_t)), NullAndSpace);
        return Value.empty() ? NullableString{} : NullableString{ Value };
    }

    void RetrieveBatteries(Hwid& InHwid)
    {
        constexpr DWORD IOCTL_BATTERY_QUERY_TAG = 0x00294040;
        constexpr std::uint32_t BatteryInformation = 0;
        constexpr std::uint32_t BatteryDeviceName = 4;
        constexpr std::uint32_t BatteryManufactureDate = 5;
        constexpr std::uint32_t BatteryManufactureName = 6;
        constexpr std::uint32_t BatteryUniqueID = 7;
        constexpr std::uint32_t BatterySerialNumber = 8;

        for (const auto& InterfacePath : GetDeviceInterfaces(GUID_DEVICE_BATTERY_))
        {
            UniqueHandle Handle(CreateFileW(InterfacePath.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, 0, nullptr));

            if (!Handle)
                continue;

            //
            // The tag identifies the battery currently in the slot; 0 (BATTERY_TAG_INVALID) means the slot is empty.
            //

            DWORD Wait = 0;
            DWORD Tag = 0;
            DWORD Returned = 0;

            if (!DeviceIoControl(Handle.Get(), IOCTL_BATTERY_QUERY_TAG, &Wait, sizeof(Wait), &Tag, sizeof(Tag), &Returned, nullptr) || Tag == 0 || Tag == 0xFFFFFFFF)
                continue;

            HwBattery Entry;
            Entry.Id = static_cast<int>(InHwid.Batteries.size());
            Entry.DeviceName = QueryBatteryString(Handle.Get(), Tag, BatteryDeviceName);
            Entry.Manufacturer = QueryBatteryString(Handle.Get(), Tag, BatteryManufactureName);
            Entry.SerialNumber = QueryBatteryString(Handle.Get(), Tag, BatterySerialNumber);
            Entry.UniqueId = QueryBatteryString(Handle.Get(), Tag, BatteryUniqueID);

            // BATTERY_INFORMATION: Capabilities, Technology, Reserved[3], Chemistry[4], DesignedCapacity, FullChargedCapacity, ...
            if (auto Information = QueryBatteryInformation(Handle.Get(), Tag, BatteryInformation, 36); Information && Information->size() >= 20)
            {
                Entry.Chemistry = TrimCharacters(DecodeAscii(ByteSpan(Information->data() + 8, 4)), NullAndSpace);
                std::memcpy(&Entry.DesignedCapacity, Information->data() + 12, 4);
                std::memcpy(&Entry.FullChargedCapacity, Information->data() + 16, 4);
            }

            // BATTERY_MANUFACTURE_DATE: Day, Month, Year.
            if (auto Date = QueryBatteryInformation(Handle.Get(), Tag, BatteryManufactureDate, 4); Date && Date->size() >= 4)
            {
                std::uint16_t Year;
                std::memcpy(&Year, Date->data() + 2, 2);
                Entry.ManufactureDate = ParseBatteryManufactureDate((*Date)[0], (*Date)[1], Year);
            }

            InHwid.Batteries.push_back(std::move(Entry));
        }
    }
}
