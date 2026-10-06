#include "Internal.hpp"

#include <bcrypt.h>

#include <cstdio>
#include <cstring>
#include <mutex>

namespace HardwareIds::Detail
{
    //
    // Text.
    //

    std::wstring FormatMacAddress(ByteSpan InBytes)
    {
        std::wstring Result;
        wchar_t Buffer[4];

        for (std::size_t I = 0; I < InBytes.size(); I++)
        {
            if (I != 0)
                Result += L':';

            swprintf_s(Buffer, L"%02X", InBytes[I]);
            Result += Buffer;
        }

        return Result;
    }

    std::wstring FormatHex(ByteSpan InBytes, bool InUpperCase)
    {
        static constexpr wchar_t Upper[] = L"0123456789ABCDEF";
        static constexpr wchar_t Lower[] = L"0123456789abcdef";
        const wchar_t* Digits = InUpperCase ? Upper : Lower;

        std::wstring Result;
        Result.reserve(InBytes.size() * 2);

        for (auto Byte : InBytes)
        {
            Result += Digits[Byte >> 4];
            Result += Digits[Byte & 0x0F];
        }

        return Result;
    }

    std::wstring Sha256Hex(ByteSpan InBytes)
    {
        std::uint8_t Digest[32] = {};

        if (!BCRYPT_SUCCESS(BCryptHash(BCRYPT_SHA256_ALG_HANDLE, nullptr, 0, const_cast<PUCHAR>(InBytes.data()), static_cast<ULONG>(InBytes.size()), Digest, sizeof(Digest))))
            return {};

        return FormatHex(Digest, false);
    }

    std::wstring DecodeAscii(ByteSpan InBytes)
    {
        std::wstring Result;
        Result.reserve(InBytes.size());

        for (auto Byte : InBytes)
            Result += Byte < 0x80 ? static_cast<wchar_t>(Byte) : L'?';

        return Result;
    }

    std::wstring DecodeLatin1(ByteSpan InBytes)
    {
        return std::wstring(InBytes.begin(), InBytes.end());
    }

    std::wstring DecodeUtf8(ByteSpan InBytes)
    {
        if (InBytes.empty())
            return {};

        auto Length = MultiByteToWideChar(CP_UTF8, 0, reinterpret_cast<const char*>(InBytes.data()), static_cast<int>(InBytes.size()), nullptr, 0);
        std::wstring Result(static_cast<std::size_t>(Length), L'\0');
        MultiByteToWideChar(CP_UTF8, 0, reinterpret_cast<const char*>(InBytes.data()), static_cast<int>(InBytes.size()), Result.data(), Length);
        return Result;
    }

    std::string EncodeUtf8(std::wstring_view InText)
    {
        if (InText.empty())
            return {};

        auto Length = WideCharToMultiByte(CP_UTF8, 0, InText.data(), static_cast<int>(InText.size()), nullptr, 0, nullptr, nullptr);
        std::string Result(static_cast<std::size_t>(Length), '\0');
        WideCharToMultiByte(CP_UTF8, 0, InText.data(), static_cast<int>(InText.size()), Result.data(), Length, nullptr, nullptr);
        return Result;
    }

    bool IsWhiteSpace(wchar_t InCharacter)
    {
        //
        // The characters .NET's char.IsWhiteSpace accepts: the ASCII and Latin-1 spaces, and the Unicode separators (Zs, Zl, Zp).
        //

        if (InCharacter == L' ' || (InCharacter >= 0x09 && InCharacter <= 0x0D) || InCharacter == 0x85 || InCharacter == 0xA0)
            return true;

        return InCharacter == 0x1680
            || (InCharacter >= 0x2000 && InCharacter <= 0x200A)
            || InCharacter == 0x2028
            || InCharacter == 0x2029
            || InCharacter == 0x202F
            || InCharacter == 0x205F
            || InCharacter == 0x3000;
    }

    std::wstring Trim(std::wstring_view InText)
    {
        std::size_t Start = 0;
        std::size_t End = InText.size();

        while (Start < End && IsWhiteSpace(InText[Start]))
            Start++;

        while (End > Start && IsWhiteSpace(InText[End - 1]))
            End--;

        return std::wstring(InText.substr(Start, End - Start));
    }

    std::wstring TrimCharacters(std::wstring_view InText, std::wstring_view InCharacters)
    {
        std::size_t Start = 0;
        std::size_t End = InText.size();

        while (Start < End && InCharacters.find(InText[Start]) != std::wstring_view::npos)
            Start++;

        while (End > Start && InCharacters.find(InText[End - 1]) != std::wstring_view::npos)
            End--;

        return std::wstring(InText.substr(Start, End - Start));
    }

    bool IsNullOrWhiteSpace(const NullableString& InText)
    {
        if (!InText)
            return true;

        for (auto Character : *InText)
        {
            if (!IsWhiteSpace(Character))
                return false;
        }

        return true;
    }

    std::wstring ToUpperInvariant(std::wstring_view InText)
    {
        if (InText.empty())
            return {};

        std::wstring Result(InText.size(), L'\0');

        if (LCMapStringEx(LOCALE_NAME_INVARIANT, LCMAP_UPPERCASE, InText.data(), static_cast<int>(InText.size()), Result.data(), static_cast<int>(Result.size()), nullptr, nullptr, 0) == 0)
            return std::wstring(InText);

        return Result;
    }

    bool EqualsIgnoreCase(std::wstring_view InLeft, std::wstring_view InRight)
    {
        return CompareStringOrdinal(InLeft.data(), static_cast<int>(InLeft.size()), InRight.data(), static_cast<int>(InRight.size()), TRUE) == CSTR_EQUAL;
    }

    std::wstring FormatGuid(const GUID& InGuid)
    {
        wchar_t Buffer[40];
        swprintf_s(Buffer, L"%08lx-%04hx-%04hx-%02x%02x-%02x%02x%02x%02x%02x%02x",
            InGuid.Data1, InGuid.Data2, InGuid.Data3,
            InGuid.Data4[0], InGuid.Data4[1], InGuid.Data4[2], InGuid.Data4[3], InGuid.Data4[4], InGuid.Data4[5], InGuid.Data4[6], InGuid.Data4[7]);
        return Buffer;
    }

    std::wstring FormatGuidBraces(const GUID& InGuid)
    {
        wchar_t Buffer[40];
        swprintf_s(Buffer, L"{%08lX-%04hX-%04hX-%02X%02X-%02X%02X%02X%02X%02X%02X}",
            InGuid.Data1, InGuid.Data2, InGuid.Data3,
            InGuid.Data4[0], InGuid.Data4[1], InGuid.Data4[2], InGuid.Data4[3], InGuid.Data4[4], InGuid.Data4[5], InGuid.Data4[6], InGuid.Data4[7]);
        return Buffer;
    }

    std::optional<GUID> ParseGuid(std::wstring_view InText)
    {
        auto Text = Trim(InText);

        if (Text.size() == 38 && Text.front() == L'{' && Text.back() == L'}')
            Text = Text.substr(1, 36);

        if (Text.size() != 36 || Text[8] != L'-' || Text[13] != L'-' || Text[18] != L'-' || Text[23] != L'-')
            return std::nullopt;

        std::uint8_t Values[16] = {};
        auto Digit = 0;

        for (std::size_t I = 0; I < Text.size(); I++)
        {
            if (I == 8 || I == 13 || I == 18 || I == 23)
                continue;

            auto Character = Text[I];
            int Nibble;

            if (Character >= L'0' && Character <= L'9')
                Nibble = Character - L'0';
            else if (Character >= L'a' && Character <= L'f')
                Nibble = Character - L'a' + 10;
            else if (Character >= L'A' && Character <= L'F')
                Nibble = Character - L'A' + 10;
            else
                return std::nullopt;

            Values[Digit / 2] = static_cast<std::uint8_t>((Values[Digit / 2] << 4) | Nibble);
            Digit++;
        }

        GUID Result = {};
        Result.Data1 = (static_cast<unsigned long>(Values[0]) << 24) | (Values[1] << 16) | (Values[2] << 8) | Values[3];
        Result.Data2 = static_cast<unsigned short>((Values[4] << 8) | Values[5]);
        Result.Data3 = static_cast<unsigned short>((Values[6] << 8) | Values[7]);
        std::memcpy(Result.Data4, Values + 8, 8);
        return Result;
    }

    GUID GuidFromBytes(ByteSpan InBytes)
    {
        GUID Result = {};

        if (InBytes.size() >= 16)
            std::memcpy(&Result, InBytes.data(), 16);

        return Result;
    }

    static const std::wstring& DecimalSeparator()
    {
        static const std::wstring Separator = []
        {
            wchar_t Buffer[8] = {};

            if (GetLocaleInfoEx(LOCALE_NAME_USER_DEFAULT, LOCALE_SDECIMAL, Buffer, static_cast<int>(std::size(Buffer))) == 0)
                return std::wstring(L".");

            return std::wstring(Buffer);
        }();

        return Separator;
    }

    std::wstring FormatTenths(std::uint32_t InTenths)
    {
        return std::to_wstring(InTenths / 10) + DecimalSeparator() + std::to_wstring(InTenths % 10);
    }

    std::wstring FormatHundredths(std::uint32_t InHundredths)
    {
        auto Fraction = InHundredths % 100;
        return std::to_wstring(InHundredths / 100) + DecimalSeparator() + (Fraction < 10 ? L"0" : L"") + std::to_wstring(Fraction);
    }

    std::vector<std::wstring> SplitMultiString(const wchar_t* InBuffer, std::size_t InLength)
    {
        std::vector<std::wstring> Result;
        std::size_t Start = 0;

        for (std::size_t I = 0; I < InLength; I++)
        {
            if (InBuffer[I] != L'\0')
                continue;

            if (I > Start)
                Result.emplace_back(InBuffer + Start, I - Start);

            Start = I + 1;
        }

        return Result;
    }

    //
    // Dates.
    //

    static std::int64_t DaysFromCivil(std::int64_t InYear, unsigned InMonth, unsigned InDay)
    {
        // Days since 1970-01-01 of a proleptic Gregorian date (Howard Hinnant's algorithm).
        InYear -= InMonth <= 2;
        const std::int64_t Era = (InYear >= 0 ? InYear : InYear - 399) / 400;
        const auto YearOfEra = static_cast<unsigned>(InYear - Era * 400);
        const unsigned DayOfYear = (153 * (InMonth > 2 ? InMonth - 3 : InMonth + 9) + 2) / 5 + InDay - 1;
        const unsigned DayOfEra = YearOfEra * 365 + YearOfEra / 4 - YearOfEra / 100 + DayOfYear;
        return Era * 146097 + static_cast<std::int64_t>(DayOfEra) - 719468;
    }

    static void CivilFromDays(std::int64_t InDays, std::int64_t& OutYear, unsigned& OutMonth, unsigned& OutDay)
    {
        InDays += 719468;
        const std::int64_t Era = (InDays >= 0 ? InDays : InDays - 146096) / 146097;
        const auto DayOfEra = static_cast<unsigned>(InDays - Era * 146097);
        const unsigned YearOfEra = (DayOfEra - DayOfEra / 1460 + DayOfEra / 36524 - DayOfEra / 146096) / 365;
        const unsigned DayOfYear = DayOfEra - (365 * YearOfEra + YearOfEra / 4 - YearOfEra / 100);
        const unsigned MonthIndex = (5 * DayOfYear + 2) / 153;
        OutDay = DayOfYear - (153 * MonthIndex + 2) / 5 + 1;
        OutMonth = MonthIndex < 10 ? MonthIndex + 3 : MonthIndex - 9;
        OutYear = static_cast<std::int64_t>(YearOfEra) + Era * 400 + (OutMonth <= 2);
    }

    static constexpr std::int64_t UnixEpochDays = 719162;   // 1970-01-01, in days since 0001-01-01.

    static std::int64_t SystemTimeToTicks(const SYSTEMTIME& InTime)
    {
        auto Days = DaysFromCivil(InTime.wYear, InTime.wMonth, InTime.wDay) + UnixEpochDays;
        return Days * TicksPerDay
             + InTime.wHour * 60 * TicksPerMinute
             + InTime.wMinute * TicksPerMinute
             + InTime.wSecond * TicksPerSecond
             + InTime.wMilliseconds * 10'000LL;
    }

    DateTime MakeDate(int InYear, int InMonth, int InDay)
    {
        DateTime Result;
        Result.Ticks = (DaysFromCivil(InYear, static_cast<unsigned>(InMonth), static_cast<unsigned>(InDay)) + UnixEpochDays) * TicksPerDay;
        return Result;
    }

    std::optional<DateTime> FromFileTime(std::int64_t InFileTime)
    {
        if (InFileTime < 0 || InFileTime > MaxDateTimeTicks - FileTimeEpochTicks)
            return std::nullopt;

        //
        // Find the UTC offset that applies at that moment (daylight saving included), as .NET's ToLocalTime does.
        //

        static const DYNAMIC_TIME_ZONE_INFORMATION TimeZone = []
        {
            DYNAMIC_TIME_ZONE_INFORMATION Information = {};
            GetDynamicTimeZoneInformation(&Information);
            return Information;
        }();

        FILETIME FileTime;
        FileTime.dwLowDateTime = static_cast<DWORD>(InFileTime & 0xFFFFFFFF);
        FileTime.dwHighDateTime = static_cast<DWORD>(InFileTime >> 32);

        SYSTEMTIME Utc;
        SYSTEMTIME Local;
        std::int64_t OffsetMinutes = 0;

        if (FileTimeToSystemTime(&FileTime, &Utc) && SystemTimeToTzSpecificLocalTimeEx(&TimeZone, &Utc, &Local))
            OffsetMinutes = (SystemTimeToTicks(Local) - SystemTimeToTicks(Utc)) / TicksPerMinute;

        DateTime Result;
        Result.Ticks = InFileTime + FileTimeEpochTicks + OffsetMinutes * TicksPerMinute;
        Result.IsLocal = true;
        Result.UtcOffsetMinutes = static_cast<std::int32_t>(OffsetMinutes);
        return Result;
    }

    DateTime FromUnixTimeSeconds(std::int64_t InSeconds)
    {
        static constexpr std::int64_t UnixEpochFileTime = 116'444'736'000'000'000;
        return FromFileTime(InSeconds * TicksPerSecond + UnixEpochFileTime).value_or(DateTime{});
    }

    DateTime LocalNowMinus(std::uint64_t InMilliseconds)
    {
        FILETIME Now;
        GetSystemTimePreciseAsFileTime(&Now);

        auto FileTime = (static_cast<std::int64_t>(Now.dwHighDateTime) << 32) | Now.dwLowDateTime;
        return FromFileTime(FileTime - static_cast<std::int64_t>(InMilliseconds) * 10'000).value_or(DateTime{});
    }

    std::string FormatDateTime(const DateTime& InDateTime)
    {
        auto Ticks = InDateTime.Ticks < 0 ? 0 : InDateTime.Ticks;
        auto TimeOfDay = Ticks % TicksPerDay;

        std::int64_t Year;
        unsigned Month;
        unsigned Day;
        CivilFromDays(Ticks / TicksPerDay - UnixEpochDays, Year, Month, Day);

        char Buffer[64];
        auto Length = std::snprintf(Buffer, sizeof(Buffer), "%04lld-%02u-%02uT%02lld:%02lld:%02lld",
            static_cast<long long>(Year), Month, Day,
            static_cast<long long>(TimeOfDay / (60 * TicksPerMinute)),
            static_cast<long long>(TimeOfDay / TicksPerMinute % 60),
            static_cast<long long>(TimeOfDay / TicksPerSecond % 60));

        std::string Result(Buffer, static_cast<std::size_t>(Length));

        //
        // Fractional seconds: seven digits, without the trailing zeros, and nothing at all when they are all zero.
        //

        if (auto Fraction = TimeOfDay % TicksPerSecond; Fraction != 0)
        {
            std::snprintf(Buffer, sizeof(Buffer), "%07lld", static_cast<long long>(Fraction));
            std::string Digits(Buffer);
            Digits.erase(Digits.find_last_not_of('0') + 1);
            Result += '.' + Digits;
        }

        if (InDateTime.IsLocal)
        {
            auto Offset = InDateTime.UtcOffsetMinutes;
            std::snprintf(Buffer, sizeof(Buffer), "%c%02d:%02d", Offset < 0 ? '-' : '+', std::abs(Offset) / 60, std::abs(Offset) % 60);
            Result += Buffer;
        }

        return Result;
    }

    //
    // Registry.
    //

    RegistryKey::RegistryKey(RegistryKey&& InOther) noexcept : Handle(InOther.Handle)
    {
        InOther.Handle = nullptr;
    }

    RegistryKey& RegistryKey::operator=(RegistryKey&& InOther) noexcept
    {
        if (this != &InOther)
        {
            if (this->Handle != nullptr)
                RegCloseKey(this->Handle);

            this->Handle = InOther.Handle;
            InOther.Handle = nullptr;
        }

        return *this;
    }

    RegistryKey::~RegistryKey()
    {
        if (this->Handle != nullptr)
            RegCloseKey(this->Handle);
    }

    RegistryKey RegistryKey::OpenLocalMachine(std::wstring_view InPath)
    {
        HKEY Key = nullptr;
        std::wstring Path(InPath);

        if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, Path.c_str(), 0, KEY_READ | KEY_WOW64_64KEY, &Key) != ERROR_SUCCESS)
            return {};

        return RegistryKey(Key);
    }

    RegistryKey RegistryKey::OpenSubKey(std::wstring_view InName) const
    {
        if (this->Handle == nullptr)
            return {};

        HKEY Key = nullptr;
        std::wstring Name(InName);

        if (RegOpenKeyExW(this->Handle, Name.c_str(), 0, KEY_READ | KEY_WOW64_64KEY, &Key) != ERROR_SUCCESS)
            return {};

        return RegistryKey(Key);
    }

    bool RegistryKey::Query(const wchar_t* InName, DWORD& OutType, Bytes& OutData) const
    {
        if (this->Handle == nullptr)
            return false;

        for (auto Attempt = 0; Attempt < 4; Attempt++)
        {
            DWORD Size = 0;

            if (RegQueryValueExW(this->Handle, InName, nullptr, &OutType, nullptr, &Size) != ERROR_SUCCESS)
                return false;

            OutData.resize(Size);
            auto Status = RegQueryValueExW(this->Handle, InName, nullptr, &OutType, OutData.data(), &Size);

            if (Status == ERROR_MORE_DATA)
                continue;

            if (Status != ERROR_SUCCESS)
                return false;

            OutData.resize(Size);
            return true;
        }

        return false;
    }

    NullableString RegistryKey::GetString(const wchar_t* InName) const
    {
        DWORD Type = 0;
        Bytes Data;

        if (!this->Query(InName, Type, Data) || (Type != REG_SZ && Type != REG_EXPAND_SZ))
            return std::nullopt;

        //
        // Like .NET: only the last character is dropped when it is a null character.
        //

        std::wstring Value(reinterpret_cast<const wchar_t*>(Data.data()), Data.size() / sizeof(wchar_t));

        if (!Value.empty() && Value.back() == L'\0')
            Value.pop_back();

        if (Type == REG_EXPAND_SZ)
        {
            auto Length = ExpandEnvironmentStringsW(Value.c_str(), nullptr, 0);

            if (Length > 0)
            {
                std::wstring Expanded(Length, L'\0');
                ExpandEnvironmentStringsW(Value.c_str(), Expanded.data(), Length);
                Expanded.resize(Length - 1);
                Value = std::move(Expanded);
            }
        }

        return Value;
    }

    std::optional<std::int32_t> RegistryKey::GetDword(const wchar_t* InName) const
    {
        DWORD Type = 0;
        Bytes Data;

        if (!this->Query(InName, Type, Data) || Type != REG_DWORD || Data.size() < 4)
            return std::nullopt;

        std::int32_t Value;
        std::memcpy(&Value, Data.data(), sizeof(Value));
        return Value;
    }

    std::optional<std::int64_t> RegistryKey::GetQword(const wchar_t* InName) const
    {
        DWORD Type = 0;
        Bytes Data;

        if (!this->Query(InName, Type, Data) || Type != REG_QWORD || Data.size() < 8)
            return std::nullopt;

        std::int64_t Value;
        std::memcpy(&Value, Data.data(), sizeof(Value));
        return Value;
    }

    std::optional<Bytes> RegistryKey::GetBinary(const wchar_t* InName) const
    {
        DWORD Type = 0;
        Bytes Data;

        if (!this->Query(InName, Type, Data) || (Type != REG_BINARY && Type != REG_NONE))
            return std::nullopt;

        return Data;
    }

    std::optional<std::vector<std::wstring>> RegistryKey::GetMultiString(const wchar_t* InName) const
    {
        DWORD Type = 0;
        Bytes Data;

        if (!this->Query(InName, Type, Data) || Type != REG_MULTI_SZ)
            return std::nullopt;

        //
        // Split like .NET: empty strings between two separators are kept, the final terminator is not a string.
        //

        auto Blob = reinterpret_cast<const wchar_t*>(Data.data());
        auto Length = Data.size() / sizeof(wchar_t);
        std::vector<std::wstring> Result;
        std::size_t Current = 0;

        while (Current < Length)
        {
            auto NextNull = Current;

            while (NextNull < Length && Blob[NextNull] != L'\0')
                NextNull++;

            if (NextNull < Length)
            {
                if (NextNull > Current)
                    Result.emplace_back(Blob + Current, NextNull - Current);
                else if (NextNull != Length - 1)
                    Result.emplace_back();
            }
            else
            {
                Result.emplace_back(Blob + Current, Length - Current);
            }

            Current = NextNull + 1;
        }

        return Result;
    }

    std::vector<std::wstring> RegistryKey::GetSubKeyNames() const
    {
        std::vector<std::wstring> Result;

        if (this->Handle == nullptr)
            return Result;

        for (DWORD Index = 0;; Index++)
        {
            wchar_t Name[256];
            DWORD Length = static_cast<DWORD>(std::size(Name));

            auto Status = RegEnumKeyExW(this->Handle, Index, Name, &Length, nullptr, nullptr, nullptr, nullptr);

            if (Status == ERROR_NO_MORE_ITEMS || (Status != ERROR_SUCCESS && Status != ERROR_MORE_DATA))
                break;

            if (Status == ERROR_SUCCESS)
                Result.emplace_back(Name, Length);
        }

        return Result;
    }

    //
    // Plug and Play configuration manager.
    //

    std::vector<std::wstring> GetDeviceInterfaces(const GUID& InInterfaceClass)
    {
        auto InterfaceClass = InInterfaceClass;

        for (auto Attempt = 0; Attempt < 3; Attempt++)
        {
            ULONG Length = 0;

            if (CM_Get_Device_Interface_List_SizeW(&Length, &InterfaceClass, nullptr, CM_GET_DEVICE_INTERFACE_LIST_PRESENT) != CR_SUCCESS || Length == 0)
                return {};

            std::vector<wchar_t> Buffer(Length);
            auto Result = CM_Get_Device_Interface_ListW(&InterfaceClass, nullptr, Buffer.data(), Length, CM_GET_DEVICE_INTERFACE_LIST_PRESENT);

            if (Result == CR_SUCCESS)
                return SplitMultiString(Buffer.data(), Buffer.size());

            if (Result != CR_BUFFER_SMALL)
                return {};
        }

        return {};
    }

    std::vector<std::wstring> GetDeviceIds(const GUID& InSetupClass)
    {
        auto Filter = FormatGuidBraces(InSetupClass);
        constexpr ULONG Flags = CM_GETIDLIST_FILTER_CLASS | CM_GETIDLIST_FILTER_PRESENT;

        for (auto Attempt = 0; Attempt < 3; Attempt++)
        {
            ULONG Length = 0;

            if (CM_Get_Device_ID_List_SizeW(&Length, Filter.c_str(), Flags) != CR_SUCCESS || Length == 0)
                return {};

            std::vector<wchar_t> Buffer(Length);
            auto Result = CM_Get_Device_ID_ListW(Filter.c_str(), Buffer.data(), Length, Flags);

            if (Result == CR_SUCCESS)
                return SplitMultiString(Buffer.data(), Buffer.size());

            if (Result != CR_BUFFER_SMALL)
                return {};
        }

        return {};
    }

    std::optional<DEVINST> LocateDevNode(const std::wstring& InInstanceId)
    {
        DEVINST DevInst = 0;

        if (CM_Locate_DevNodeW(&DevInst, const_cast<DEVINSTID_W>(InInstanceId.c_str()), CM_LOCATE_DEVNODE_NORMAL) != CR_SUCCESS)
            return std::nullopt;

        return DevInst;
    }

    template <typename TReader>
    static std::optional<Bytes> ReadProperty(TReader InReader, DEVPROPTYPE& OutType)
    {
        ULONG Size = 0;

        if (InReader(nullptr, Size, OutType) != CR_BUFFER_SMALL || Size == 0)
            return std::nullopt;

        Bytes Buffer(Size);

        if (InReader(Buffer.data(), Size, OutType) != CR_SUCCESS)
            return std::nullopt;

        return Buffer;
    }

    static NullableString DecodePropertyString(const std::optional<Bytes>& InValue, DEVPROPTYPE InType)
    {
        if (!InValue || InType != DEVPROP_TYPE_STRING)
            return std::nullopt;

        std::wstring Value(reinterpret_cast<const wchar_t*>(InValue->data()), InValue->size() / sizeof(wchar_t));

        if (auto End = Value.find(L'\0'); End != std::wstring::npos)
            Value.resize(End);

        return Value;
    }

    NullableString GetInterfaceProperty(const std::wstring& InInterfacePath, const DEVPROPKEY& InKey)
    {
        DEVPROPTYPE Type = 0;
        auto Value = ReadProperty([&](std::uint8_t* InBuffer, ULONG& InSize, DEVPROPTYPE& OutType)
        {
            return CM_Get_Device_Interface_PropertyW(InInterfacePath.c_str(), &InKey, &OutType, InBuffer, &InSize, 0);
        }, Type);

        return DecodePropertyString(Value, Type);
    }

    NullableString GetDevNodeProperty(DEVINST InDevInst, const DEVPROPKEY& InKey)
    {
        DEVPROPTYPE Type = 0;
        auto Value = ReadProperty([&](std::uint8_t* InBuffer, ULONG& InSize, DEVPROPTYPE& OutType)
        {
            return CM_Get_DevNode_PropertyW(InDevInst, &InKey, &OutType, InBuffer, &InSize, 0);
        }, Type);

        return DecodePropertyString(Value, Type);
    }

    std::optional<DateTime> GetDevNodeDateProperty(DEVINST InDevInst, const DEVPROPKEY& InKey)
    {
        DEVPROPTYPE Type = 0;
        auto Value = ReadProperty([&](std::uint8_t* InBuffer, ULONG& InSize, DEVPROPTYPE& OutType)
        {
            return CM_Get_DevNode_PropertyW(InDevInst, &InKey, &OutType, InBuffer, &InSize, 0);
        }, Type);

        if (!Value || Type != DEVPROP_TYPE_FILETIME || Value->size() < 8)
            return std::nullopt;

        std::int64_t FileTime;
        std::memcpy(&FileTime, Value->data(), sizeof(FileTime));
        return FromFileTime(FileTime);
    }

    NullableString InterfaceNameToInstanceId(const NullableString& InInterfaceName)
    {
        if (!InInterfaceName || InInterfaceName->empty())
            return std::nullopt;

        std::wstring Value = *InInterfaceName;

        if (Value.starts_with(L"\\\\?\\"))
            Value = Value.substr(4);

        if (auto End = Value.find(L"#{"); End != std::wstring::npos && End > 0)
            Value.resize(End);

        for (auto& Character : Value)
        {
            if (Character == L'#')
                Character = L'\\';
        }

        return Value;
    }
}
