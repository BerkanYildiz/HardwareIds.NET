#include "Internal.hpp"

#include <lm.h>
#include <sddl.h>

namespace HardwareIds::Detail
{
    static std::wstring GetMachineName()
    {
        wchar_t Buffer[MAX_COMPUTERNAME_LENGTH + 1] = {};
        DWORD Length = static_cast<DWORD>(std::size(Buffer));

        if (!GetComputerNameW(Buffer, &Length))
            return {};

        return std::wstring(Buffer, Length);
    }

    static NullableString GetMachineSid()
    {
        //
        // Looking the computer name up as an account gives the SID of the local machine (the domain part of every local account SID).
        //

        auto MachineName = GetMachineName();
        DWORD SidSize = 0;
        DWORD DomainSize = 0;
        SID_NAME_USE Use;

        LookupAccountNameW(nullptr, MachineName.c_str(), nullptr, &SidSize, nullptr, &DomainSize, &Use);

        if (SidSize == 0)
            return std::nullopt;

        Bytes Sid(SidSize);
        std::wstring Domain(std::max<DWORD>(DomainSize, 1), L'\0');

        if (!LookupAccountNameW(nullptr, MachineName.c_str(), Sid.data(), &SidSize, Domain.data(), &DomainSize, &Use))
            return std::nullopt;

        LPWSTR Text = nullptr;

        if (!ConvertSidToStringSidW(Sid.data(), &Text))
            return std::nullopt;

        std::wstring Result(Text);
        LocalFree(Text);
        return Result;
    }

    //
    // Users.
    //

    void RetrieveUserAccounts(Hwid& InHwid)
    {
        auto MachineName = GetMachineName();
        auto MachineSid = GetMachineSid();
        DWORD Resume = 0;

        while (true)
        {
            USER_INFO_20* Buffer = nullptr;
            DWORD Read = 0;
            DWORD Total = 0;
            auto Status = NetUserEnum(nullptr, 20, FILTER_NORMAL_ACCOUNT, reinterpret_cast<LPBYTE*>(&Buffer), MAX_PREFERRED_LENGTH, &Read, &Total, &Resume);

            if (Status != NERR_Success && Status != ERROR_MORE_DATA)
                break;

            for (DWORD I = 0; I < Read; I++)
            {
                const auto& User = Buffer[I];

                if ((User.usri20_flags & UF_ACCOUNTDISABLE) != 0)
                    continue;

                HwUser Entry;
                Entry.Id = static_cast<int>(InHwid.Users.size());
                Entry.Username = User.usri20_name != nullptr ? NullableString{ User.usri20_name } : NullableString{};
                Entry.FullName = User.usri20_full_name != nullptr ? NullableString{ User.usri20_full_name } : NullableString{};
                Entry.SID = MachineSid ? NullableString{ *MachineSid + L"-" + std::to_wstring(User.usri20_user_id) } : NullableString{};
                Entry.Domain = MachineName;
                InHwid.Users.push_back(std::move(Entry));
            }

            if (Buffer != nullptr)
                NetApiBufferFree(Buffer);

            if (Status != ERROR_MORE_DATA)
                break;
        }
    }

    //
    // Operating system.
    //

    static NullableString GetBrandingString(const wchar_t* InFormat)
    {
        using BrandingFormatStringFn = LPWSTR (WINAPI*)(LPCWSTR);

        auto Module = LoadLibraryExW(L"winbrand.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);

        if (Module == nullptr)
            return std::nullopt;

        NullableString Result;

        if (auto BrandingFormatString = reinterpret_cast<BrandingFormatStringFn>(GetProcAddress(Module, "BrandingFormatString")))
        {
            if (auto Value = BrandingFormatString(InFormat))
            {
                Result = std::wstring(Value);
                GlobalFree(Value);
            }
        }

        FreeLibrary(Module);
        return Result;
    }

    static std::wstring GetOperatingSystemArchitecture()
    {
        SYSTEM_INFO Information = {};
        GetNativeSystemInfo(&Information);

        switch (Information.wProcessorArchitecture)
        {
            case PROCESSOR_ARCHITECTURE_AMD64:
                return L"64-bit";

            case PROCESSOR_ARCHITECTURE_ARM64:
                return L"ARM 64-bit";

            case PROCESSOR_ARCHITECTURE_ARM:
                return L"ARM 32-bit";

            case PROCESSOR_ARCHITECTURE_INTEL:
                return L"32-bit";

            default:
                return sizeof(void*) == 8 ? L"64-bit" : L"32-bit";
        }
    }

    void RetrieveOperatingSystems(Hwid& InHwid)
    {
        auto VersionKey = RegistryKey::OpenLocalMachine(L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion");

        if (!VersionKey)
            return;

        //
        // The registry still says "Windows 10" on Windows 11; the branding API returns the marketed name, like WMI does.
        //

        auto ProductName = GetBrandingString(L"%WINDOWS_LONG%");

        if (!ProductName)
            ProductName = VersionKey.GetString(L"ProductName");

        if (ProductName && !(ProductName->size() >= 9 && EqualsIgnoreCase(std::wstring_view(*ProductName).substr(0, 9), L"Microsoft")))
            ProductName = L"Microsoft " + *ProductName;

        auto MajorVersion = VersionKey.GetDword(L"CurrentMajorVersionNumber");
        auto MinorVersion = VersionKey.GetDword(L"CurrentMinorVersionNumber");
        auto BuildNumber = VersionKey.GetString(L"CurrentBuildNumber").value_or(L"");
        auto Version = MajorVersion && MinorVersion
            ? std::to_wstring(*MajorVersion) + L"." + std::to_wstring(*MinorVersion) + L"." + BuildNumber
            : VersionKey.GetString(L"CurrentVersion").value_or(L"") + L"." + BuildNumber;

        HwOperatingSystem Entry;
        Entry.Id = static_cast<int>(InHwid.OperatingSystems.size());
        Entry.Name = ProductName;
        Entry.Version = Version;
        Entry.Architecture = GetOperatingSystemArchitecture();
        Entry.RegisteredUser = VersionKey.GetString(L"RegisteredOwner");
        Entry.SerialNumber = VersionKey.GetString(L"ProductId");

        if (auto InstallDate = VersionKey.GetDword(L"InstallDate"))
            Entry.InstallDate = FromUnixTimeSeconds(static_cast<std::uint32_t>(*InstallDate));

        Entry.LastBootUpTime = LocalNowMinus(GetTickCount64());

        if (auto InstallTime = VersionKey.GetQword(L"InstallTime"); InstallTime && *InstallTime > 0)
            Entry.InstallTime = FromFileTime(*InstallTime);

        Entry.MachineGuid = RegistryKey::OpenLocalMachine(L"SOFTWARE\\Microsoft\\Cryptography").GetString(L"MachineGuid");
        Entry.SqmMachineId = RegistryKey::OpenLocalMachine(L"SOFTWARE\\Microsoft\\SQMClient").GetString(L"MachineId");
        Entry.HardwareProfileGuid = RegistryKey::OpenLocalMachine(L"SYSTEM\\CurrentControlSet\\Control\\IDConfigDB\\Hardware Profiles\\0001").GetString(L"HwProfileGuid");
        Entry.MachineSid = GetMachineSid();
        InHwid.OperatingSystems.push_back(std::move(Entry));
    }
}
