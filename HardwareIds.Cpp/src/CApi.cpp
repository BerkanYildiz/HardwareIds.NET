#include <HardwareIds/HardwareIds.h>
#include <HardwareIds/HardwareIds.hpp>

#include <cstdlib>
#include <cstring>

extern "C" HARDWAREIDS_API char* HardwareIds_GetSnapshotJson(const HardwareIds_Options* InOptions)
{
    try
    {
        HardwareIds::HardwareIdsConfig Config;
        auto Format = HardwareIds::JsonFormat::Compact;

        if (InOptions != nullptr)
        {
            Config.ScanNeighborEndpoints = InOptions->ScanNeighborEndpoints != 0;
            Config.ScanLocalNetworkDevices = InOptions->ScanLocalNetworkDevices != 0;

            if (InOptions->DurationOfNetworkScanMs != 0)
                Config.DurationOfNetworkScan = std::chrono::milliseconds(InOptions->DurationOfNetworkScanMs);

            if (InOptions->DurationOfLocalNetworkScanMs != 0)
                Config.DurationOfLocalNetworkScan = std::chrono::milliseconds(InOptions->DurationOfLocalNetworkScanMs);

            if (InOptions->Indented != 0)
                Format = HardwareIds::JsonFormat::Indented;
        }

        auto Json = HardwareIds::ToJson(HardwareIds::GetHwid(Config), Format);
        auto Result = static_cast<char*>(std::malloc(Json.size() + 1));

        if (Result == nullptr)
            return nullptr;

        std::memcpy(Result, Json.c_str(), Json.size() + 1);
        return Result;
    }
    catch (...)
    {
        return nullptr;
    }
}

extern "C" HARDWAREIDS_API void HardwareIds_Free(char* InJson)
{
    std::free(InJson);
}
