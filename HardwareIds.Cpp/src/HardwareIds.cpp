#include "Internal.hpp"

#include <thread>

namespace HardwareIds
{
    using namespace Detail;

    template <typename TCollector>
    static void Run(const std::stop_token& InStopToken, TCollector&& InCollector)
    {
        //
        // A collector never takes the whole scan down: whatever goes wrong only leaves its own list empty.
        //

        if (InStopToken.stop_requested())
            return;

        try
        {
            InCollector();
        }
        catch (...)
        {
            // ...
        }
    }

    Hwid GetHwid(const HardwareIdsConfig& InConfig, std::stop_token InStopToken)
    {
        Hwid Result;

        //
        // Read the SMBIOS table once; the baseboard, system, chassis, BIOS, processor and memory collectors are all built from it.
        //

        std::optional<SmbiosTable> Smbios;

        try
        {
            Smbios = SmbiosTable::Read();
        }
        catch (...)
        {
            // ...
        }

        const SmbiosTable* Table = Smbios ? &*Smbios : nullptr;

        //
        // The network scans run in the background while the hardware is read. Each one writes to its own list.
        //

        std::stop_source ScanStop;
        std::stop_callback LinkStop(InStopToken, [&ScanStop] { ScanStop.request_stop(); });
        std::vector<std::thread> Scans;

        if (!InStopToken.stop_requested() && InConfig.ScanNeighborEndpoints)
        {
            Scans.emplace_back([&Result, &InConfig, Token = ScanStop.get_token()]
            {
                Run(Token, [&] { ScanNetworkEndpoints(Result, InConfig.DurationOfNetworkScan.value_or(DefaultWifiScanDuration), Token); });
            });
        }

        if (!InStopToken.stop_requested() && InConfig.ScanLocalNetworkDevices)
        {
            Scans.emplace_back([&Result, &InConfig, Token = ScanStop.get_token()]
            {
                Run(Token, [&] { ScanNetworkDevices(Result, InConfig.DurationOfLocalNetworkScan.value_or(DefaultNetworkProbeWait), Token); });
            });
        }

        Run(InStopToken, [&] { RetrieveNetworkSignatures(Result); });
        Run(InStopToken, [&] { RetrieveDiskDrives(Result); });
        Run(InStopToken, [&] { RetrieveDiskVolumes(Result); });
        Run(InStopToken, [&] { RetrieveNetworkAdapters(Result); });
        Run(InStopToken, [&] { RetrieveBluetoothRadios(Result); });
        Run(InStopToken, [&] { RetrieveBaseBoards(Result, Table); });
        Run(InStopToken, [&] { RetrieveMotherBoards(Result, Table); });
        Run(InStopToken, [&] { RetrieveChassis(Result, Table); });
        Run(InStopToken, [&] { RetrieveFirmwares(Result, Table); });
        Run(InStopToken, [&] { RetrieveSmbiosTables(Result, Table); });
        Run(InStopToken, [&] { RetrieveProcessors(Result, Table); });
        Run(InStopToken, [&] { RetrieveMemorySticks(Result, Table); });
        Run(InStopToken, [&] { RetrieveBatteries(Result); });
        Run(InStopToken, [&] { RetrieveMonitors(Result); });
        Run(InStopToken, [&] { RetrieveVideoControllers(Result); });
        Run(InStopToken, [&] { RetrievePrinters(Result); });
        Run(InStopToken, [&] { RetrieveUserAccounts(Result); });
        Run(InStopToken, [&] { RetrieveOperatingSystems(Result); });

        //
        // Wait for the network scans; a cancellation makes them stop early.
        //

        for (auto& Scan : Scans)
            Scan.join();

        return Result;
    }
}
