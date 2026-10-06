//
// HardwareIdsSnapshot: prints a snapshot of the local computer as JSON, the same JSON HardwareIds.NET writes.
//
//   HardwareIdsSnapshot [--indented] [--wifi[=seconds]] [--lan[=seconds]] [--output <file>]
//

#include <HardwareIds/HardwareIds.hpp>

#include <cstdio>
#include <cwchar>
#include <string>

#include <fcntl.h>
#include <io.h>

static void PrintUsage()
{
    std::fwprintf(stderr,
        L"Usage: HardwareIdsSnapshot [options]\n"
        L"\n"
        L"  --indented          Indent the JSON.\n"
        L"  --wifi[=seconds]    Also scan the Wi-Fi networks around the computer (7 seconds by default).\n"
        L"  --lan[=seconds]     Also scan the local networks for devices (waits 1 second by default).\n"
        L"  --output <file>     Write the JSON to a file instead of the standard output.\n");
}

static std::optional<std::chrono::milliseconds> ParseSeconds(const std::wstring& InArgument)
{
    auto Equals = InArgument.find(L'=');

    if (Equals == std::wstring::npos)
        return std::nullopt;

    return std::chrono::milliseconds(static_cast<long long>(std::wcstod(InArgument.c_str() + Equals + 1, nullptr) * 1000));
}

int wmain(int InArgumentCount, wchar_t** InArguments)
{
    HardwareIds::HardwareIdsConfig Config;
    auto Format = HardwareIds::JsonFormat::Compact;
    std::wstring OutputPath;

    for (auto I = 1; I < InArgumentCount; I++)
    {
        std::wstring Argument = InArguments[I];

        if (Argument == L"--indented")
        {
            Format = HardwareIds::JsonFormat::Indented;
        }
        else if (Argument.starts_with(L"--wifi"))
        {
            Config.ScanNeighborEndpoints = true;
            Config.DurationOfNetworkScan = ParseSeconds(Argument);
        }
        else if (Argument.starts_with(L"--lan"))
        {
            Config.ScanLocalNetworkDevices = true;
            Config.DurationOfLocalNetworkScan = ParseSeconds(Argument);
        }
        else if (Argument == L"--output" && I + 1 < InArgumentCount)
        {
            OutputPath = InArguments[++I];
        }
        else
        {
            PrintUsage();
            return Argument == L"--help" || Argument == L"-h" ? 0 : 2;
        }
    }

    auto Json = HardwareIds::ToJson(HardwareIds::GetHwid(Config), Format);

    if (OutputPath.empty())
    {
        // Binary mode, so the CRLF line endings of the indented JSON are not turned into CR CR LF.
        _setmode(_fileno(stdout), _O_BINARY);
        std::fwrite(Json.data(), 1, Json.size(), stdout);
        std::fflush(stdout);
        return 0;
    }

    FILE* File = nullptr;

    if (_wfopen_s(&File, OutputPath.c_str(), L"wb") != 0 || File == nullptr)
    {
        std::fwprintf(stderr, L"Cannot write %ls\n", OutputPath.c_str());
        return 1;
    }

    std::fwrite(Json.data(), 1, Json.size(), File);
    std::fclose(File);
    return 0;
}
