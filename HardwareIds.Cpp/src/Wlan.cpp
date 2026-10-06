#include "Internal.hpp"

#include <wlanapi.h>

#include <algorithm>
#include <condition_variable>
#include <mutex>

namespace HardwareIds::Detail
{
    int GetWifiChannel(std::uint32_t InFrequencyKHz)
    {
        auto MHz = InFrequencyKHz / 1000;

        if (MHz == 2484)
            return 14;

        if (MHz >= 2412 && MHz <= 2472)
            return static_cast<int>(MHz - 2407) / 5;

        if (MHz >= 5000 && MHz <= 5925)
            return static_cast<int>(MHz - 5000) / 5;

        if (MHz > 5925 && MHz <= 7125)
            return static_cast<int>(MHz - 5950) / 5;

        return 0;
    }

    float GetWifiBand(std::uint32_t InFrequencyKHz)
    {
        auto MHz = InFrequencyKHz / 1000;

        if (MHz >= 2412 && MHz <= 2484)
            return 2.4f;

        if (MHz >= 5000 && MHz <= 5925)
            return 5.0f;

        if (MHz > 5925 && MHz <= 7125)
            return 6.0f;

        return 0.0f;
    }

    namespace
    {
        //
        // wlanapi.dll is only present when the WLAN service is installed (not on every server), so it is loaded at runtime.
        //

        struct WlanLibrary
        {
            using OpenHandleFn = DWORD (WINAPI*)(DWORD, PVOID, PDWORD, PHANDLE);
            using CloseHandleFn = DWORD (WINAPI*)(HANDLE, PVOID);
            using EnumInterfacesFn = DWORD (WINAPI*)(HANDLE, PVOID, PWLAN_INTERFACE_INFO_LIST*);
            using FreeMemoryFn = VOID (WINAPI*)(PVOID);
            using ScanFn = DWORD (WINAPI*)(HANDLE, const GUID*, const PDOT11_SSID, const PWLAN_RAW_DATA, PVOID);
            using RegisterNotificationFn = DWORD (WINAPI*)(HANDLE, DWORD, BOOL, WLAN_NOTIFICATION_CALLBACK, PVOID, PVOID, PDWORD);
            using GetNetworkBssListFn = DWORD (WINAPI*)(HANDLE, const GUID*, const PDOT11_SSID, DOT11_BSS_TYPE, BOOL, PVOID, PWLAN_BSS_LIST*);

            HMODULE Module = nullptr;
            OpenHandleFn OpenHandle = nullptr;
            CloseHandleFn CloseHandle = nullptr;
            EnumInterfacesFn EnumInterfaces = nullptr;
            FreeMemoryFn FreeMemory = nullptr;
            ScanFn Scan = nullptr;
            RegisterNotificationFn RegisterNotification = nullptr;
            GetNetworkBssListFn GetNetworkBssList = nullptr;

            WlanLibrary()
            {
                this->Module = LoadLibraryExW(L"wlanapi.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);

                if (this->Module == nullptr)
                    return;

                this->OpenHandle = reinterpret_cast<OpenHandleFn>(GetProcAddress(this->Module, "WlanOpenHandle"));
                this->CloseHandle = reinterpret_cast<CloseHandleFn>(GetProcAddress(this->Module, "WlanCloseHandle"));
                this->EnumInterfaces = reinterpret_cast<EnumInterfacesFn>(GetProcAddress(this->Module, "WlanEnumInterfaces"));
                this->FreeMemory = reinterpret_cast<FreeMemoryFn>(GetProcAddress(this->Module, "WlanFreeMemory"));
                this->Scan = reinterpret_cast<ScanFn>(GetProcAddress(this->Module, "WlanScan"));
                this->RegisterNotification = reinterpret_cast<RegisterNotificationFn>(GetProcAddress(this->Module, "WlanRegisterNotification"));
                this->GetNetworkBssList = reinterpret_cast<GetNetworkBssListFn>(GetProcAddress(this->Module, "WlanGetNetworkBssList"));
            }

            ~WlanLibrary()
            {
                if (this->Module != nullptr)
                    FreeLibrary(this->Module);
            }

            bool IsLoaded() const
            {
                return this->OpenHandle && this->CloseHandle && this->EnumInterfaces && this->FreeMemory && this->Scan && this->RegisterNotification && this->GetNetworkBssList;
            }
        };

        struct ScanState
        {
            std::mutex Lock;
            std::condition_variable_any Changed;
            std::vector<GUID> Pending;
        };

        void WINAPI OnNotification(PWLAN_NOTIFICATION_DATA InData, PVOID InContext)
        {
            if (InData == nullptr || InData->NotificationSource != WLAN_NOTIFICATION_SOURCE_ACM)
                return;

            if (InData->NotificationCode != wlan_notification_acm_scan_complete && InData->NotificationCode != wlan_notification_acm_scan_fail)
                return;

            auto State = static_cast<ScanState*>(InContext);

            {
                std::lock_guard Guard(State->Lock);
                auto Found = std::find(State->Pending.begin(), State->Pending.end(), InData->InterfaceGuid);

                if (Found == State->Pending.end())
                    return;

                State->Pending.erase(Found);
            }

            State->Changed.notify_all();
        }
    }

    void ScanNetworkEndpoints(Hwid& InHwid, std::chrono::milliseconds InTimeout, std::stop_token InStopToken)
    {
        WlanLibrary Wlan;

        if (!Wlan.IsLoaded())
            return;

        HANDLE Session = nullptr;
        DWORD NegotiatedVersion = 0;

        if (Wlan.OpenHandle(2, nullptr, &NegotiatedVersion, &Session) != ERROR_SUCCESS || Session == nullptr)
            return;

        //
        // Enumerate the wireless interfaces.
        //

        std::vector<GUID> Interfaces;
        PWLAN_INTERFACE_INFO_LIST InterfaceList = nullptr;

        if (Wlan.EnumInterfaces(Session, nullptr, &InterfaceList) == ERROR_SUCCESS && InterfaceList != nullptr)
        {
            for (DWORD I = 0; I < InterfaceList->dwNumberOfItems; I++)
                Interfaces.push_back(InterfaceList->InterfaceInfo[I].InterfaceGuid);

            Wlan.FreeMemory(InterfaceList);
        }

        if (Interfaces.empty())
        {
            Wlan.CloseHandle(Session, nullptr);
            return;
        }

        //
        // Ask every wireless interface to scan for the Wi-Fi endpoints around this computer, and wait for them to finish.
        // A timeout keeps what the interfaces have seen so far; a cancellation returns nothing.
        //

        ScanState State;
        auto Cancelled = false;

        if (Wlan.RegisterNotification(Session, WLAN_NOTIFICATION_SOURCE_ACM, TRUE, OnNotification, &State, nullptr, nullptr) == ERROR_SUCCESS)
        {
            for (const auto& Interface : Interfaces)
            {
                {
                    std::lock_guard Guard(State.Lock);
                    State.Pending.push_back(Interface);
                }

                if (Wlan.Scan(Session, &Interface, nullptr, nullptr, nullptr) != ERROR_SUCCESS)
                {
                    std::lock_guard Guard(State.Lock);
                    State.Pending.erase(std::find(State.Pending.begin(), State.Pending.end(), Interface));
                }
            }

            {
                std::unique_lock Guard(State.Lock);
                State.Changed.wait_for(Guard, InStopToken, InTimeout, [&] { return State.Pending.empty(); });
                Cancelled = InStopToken.stop_requested();
            }

            Wlan.RegisterNotification(Session, WLAN_NOTIFICATION_SOURCE_NONE, TRUE, nullptr, nullptr, nullptr, nullptr);
        }

        //
        // Retrieve every Wi-Fi endpoint the interfaces have seen.
        //

        if (!Cancelled)
        {
            for (const auto& Interface : Interfaces)
            {
                PWLAN_BSS_LIST List = nullptr;

                if (Wlan.GetNetworkBssList(Session, &Interface, nullptr, dot11_BSS_type_any, FALSE, nullptr, &List) != ERROR_SUCCESS || List == nullptr)
                    continue;

                for (DWORD I = 0; I < List->dwNumberOfItems; I++)
                {
                    const auto& Network = List->wlanBssEntries[I];

                    HwWifi Entry;
                    Entry.Id = static_cast<int>(InHwid.Wifis.size());
                    Entry.Ssid = DecodeUtf8(ByteSpan(Network.dot11Ssid.ucSSID, std::min<ULONG>(Network.dot11Ssid.uSSIDLength, DOT11_SSID_MAX_LENGTH)));
                    Entry.Bssid = FormatMacAddress(ByteSpan(Network.dot11Bssid, 6));
                    Entry.Strength = Network.lRssi;
                    Entry.Channel = GetWifiChannel(Network.ulChCenterFrequency);
                    Entry.Frequency = static_cast<int>(Network.ulChCenterFrequency);
                    Entry.Band = GetWifiBand(Network.ulChCenterFrequency);
                    Entry.Quality = static_cast<int>(Network.uLinkQuality);
                    InHwid.Wifis.push_back(std::move(Entry));
                }

                Wlan.FreeMemory(List);
            }
        }

        Wlan.CloseHandle(Session, nullptr);
    }
}
