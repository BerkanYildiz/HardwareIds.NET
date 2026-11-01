<h1 align="center">HardwareIds.NET</h1>

<p align="center">
  Fingerprint a Windows machine straight from the Windows APIs, with no WMI and no dependencies.
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/HardwareIds.NET"><img alt="NuGet version" src="https://img.shields.io/nuget/v/HardwareIds.NET?logo=nuget&label=NuGet"></a>
  <a href="https://www.nuget.org/packages/HardwareIds.NET"><img alt="NuGet downloads" src="https://img.shields.io/nuget/dt/HardwareIds.NET?logo=nuget&label=Downloads"></a>
  <a href="https://github.com/BerkanYildiz/HardwareIds.NET/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/BerkanYildiz/HardwareIds.NET/actions/workflows/ci.yml/badge.svg"></a>
  <a href="https://github.com/BerkanYildiz/HardwareIds.NET/actions/workflows/publish.yml"><img alt="Publish" src="https://github.com/BerkanYildiz/HardwareIds.NET/actions/workflows/publish.yml/badge.svg"></a>
  <a href="LICENSE"><img alt="License" src="https://img.shields.io/github/license/BerkanYildiz/HardwareIds.NET"></a>
  <img alt="Targets" src="https://img.shields.io/badge/.NET-10.0%20%7C%20Framework%204.8-512BD4?logo=dotnet">
</p>

HardwareIds.NET enumerates every hardware component of the computer it runs on and collects the identifiers that survive
re-installs and component swaps: serial numbers, MAC addresses, firmware and partition table GUIDs, EDID blocks, SMBIOS
data, the Windows installation identifiers, and optionally the Wi-Fi networks and LAN devices around the machine, so a
user can be recognised even on an entirely new computer sitting in the same place.

## Highlights

- **Direct.** Everything is read from the Windows APIs: the SMBIOS table,
  storage and NDIS IOCTLs, the PnP configuration manager, the registry, the print spooler, the display configuration API,
  the battery and Bluetooth APIs, WlanAPI, TPM Base Services and CNG. TPM key reads can add seconds depending on firmware
  and other applications using the TPM. No WMI, no `winmgmt` service, no `WmiPrvSE` host.
- **Dependency-free.** No third-party packages. Only `System.Text.Json` on .NET Framework, which is part of .NET 10.
- **Works without elevation.** Restricted TPM fields and machine keys remain unavailable; network signatures require elevation.
- **WMI-compatible values.** The historical fields hold the exact strings WMI reports, so identifiers collected by
  earlier versions keep matching.
- **Serializable.** The result is a plain object with `System.Text.Json` attributes; serialize it and store it as is.
- **Tested.** 130+ unit and integration tests, the latter checking every collector against WMI as an independent oracle.
- **C and C++ too.** [`HardwareIds.Cpp`](#c-and-c) is a native port that writes the very same JSON, byte for byte.

## What is collected

| Section | Source | Identifiers |
|---|---|---|
| `disks` | Storage IOCTLs, NVMe / ATA identify, SCSI VPD page 0x83, PnP | Model, descriptor serial, NVMe serial / EUI-64 / NGUID / FGUID, ATA serial / WWN, VPD identifiers, Windows DUID, firmware, partition table GUID, instance path |
| `volumes` | Volume management API | Volume GUID path, drive letter, NTFS/FAT serial |
| `network_adapters` | `GetIfTable2`, registry, PnP | Current and permanent MAC, interface GUID, driver service, install date, instance path |
| `bluetooth_radios` | Bluetooth API | Radio address, name, manufacturer, class of device |
| `baseboards`, `motherboards`, `chassis`, `bios_firmwares`, `smbios_tables` | SMBIOS (`GetSystemFirmwareTable`) | Board serial, system UUID, chassis serial and asset tag, BIOS version, hash of the whole table |
| `processors` | SMBIOS, CPUID, registry | Model, ProcessorId, socket, cores and threads |
| `memory_sticks` | SMBIOS | Manufacturer, part number, serial, capacity, speed, slot |
| `batteries` | Battery IOCTLs | Serial, unique ID, chemistry, capacities, manufacture date |
| `monitors` | EDID (PnP registry) | Manufacturer, product code, serial, name, EDID hash, manufacture week and year, instance path |
| `video_controllers` | PnP, display configuration API | Name, driver version and date, current mode, instance path |
| `printers` | Print spooler | Name, port, location, resolution |
| `users` | `NetUserEnum` | Local accounts with their SIDs |
| `operating_systems` | Registry, branding API | Edition, version, product ID, install date and time, machine GUID, hardware profile GUID, machine SID |
| `wifis` (optional) | WlanAPI scan | SSID, BSSID, signal, channel, band |
| `routers` (optional) | Neighbour cache, ARP sweep | Gateways with their MAC, DNS and DHCP servers, every device on the LAN |
| `network_signatures` | Registry (elevated only) | Networks the machine connected to, with their gateway MAC |
| `tpms` | TBS, TPM2_GetCapability, Platform Crypto Provider, certificate stores | TPM version, manufacturer, firmware, specification, flags, lockout state, raw properties, algorithms, PCR banks, public EK/SRK and persisted keys, endorsement and associated certificates |

## Installation

```bash
dotnet add package HardwareIds.NET
```

```powershell
PM> Install-Package HardwareIds.NET
```

Requirements: Windows, and .NET 10 or .NET Framework 4.8.

## Usage

```csharp
using HardwareIds.NET;

// Hardware only, including the TPM's available public key material.
var Hwid = HardwareIds.GetHwid();

Console.WriteLine(Hwid.Motherboard?.UUID);
Console.WriteLine(Hwid.Disk?.NvmeSerial ?? Hwid.Disk?.AtaSerial ?? Hwid.Disk?.SerialNumber);
Console.WriteLine(Hwid.OperatingSystem?.MachineGuid);
```

```csharp
// With the network probes: a few seconds, bound by the Wi-Fi scan.
var Hwid = await HardwareIds.GetHwidAsync(new HardwareIdsConfig
{
    ScanNeighborEndpoints = true,                                 // Wi-Fi networks around the computer
    DurationOfNetworkScan = TimeSpan.FromSeconds(3),              // how long to wait for the Wi-Fi scan
    ScanLocalNetworkDevices = true,                               // devices on the LAN, with their MAC addresses
    DurationOfLocalNetworkScan = TimeSpan.FromSeconds(1),         // how long to wait for the devices to answer
}, cancellationToken);

foreach (var Wifi in Hwid.Wifis.OrderByDescending(T => T.Strength))
    Console.WriteLine($"{Wifi.Ssid,-32} {Wifi.Bssid}  {Wifi.Strength} dBm  channel {Wifi.Channel}");

foreach (var Device in Hwid.Router?.NetworkDevices ?? [])
    Console.WriteLine($"{Device.Ip,-16} {Device.MacAddress}");
```

```csharp
// The result serializes as is, with stable snake_case property names.
var Json = JsonSerializer.Serialize(Hwid);
```

Every section is a list, and each has a convenience getter for its first entry (`Hwid.Disk`, `Hwid.Processor`,
`Hwid.OperatingSystem`, `Hwid.Tpm`, ...). Collectors never throw. Missing components yield empty lists; TPM queries
retain partial results and expose individual failures in `Tpm.Errors`.

### TPM information

```csharp
var Tpm = HardwareIds.GetHwid().Tpm;

if (Tpm != null)
{
    Console.WriteLine($"TPM {Tpm.Version}: {Tpm.Manufacturer}");

    foreach (var Key in Tpm.Keys)
        Console.WriteLine($"{Key.Kind} / {Key.Scope} / {Key.Name}: {Key.PublicKeySha256}");

    foreach (var Certificate in Tpm.Certificates)
        Console.WriteLine($"{Certificate.Source}: {Certificate.Subject} ({Certificate.Sha256})");

    foreach (var Error in Tpm.Errors)
        Console.WriteLine($"{Error.Operation}: {Error.Source} 0x{Error.Code:X8}");
}
```

- `Keys` includes the default, RSA and ECC endorsement public keys, the storage-root public key, and accessible
  persisted keys from the Microsoft Platform Crypto Provider in the current-user and local-machine scopes. Identity
  keys (AIKs) can be identified by bit `0x8` of `TpmUsage`. Enumeration follows the caller's access rights.
- `PublicKeyBlob` is Base64-encoded BCRYPT public-key data (RSA, ECC or full ECC); `PublicKeySha256` hashes those exact bytes, not
  SubjectPublicKeyInfo or the certificate. `Length` is in bits, or null if the curve size is unknown. Malformed public
  blobs are rejected. Private keys and authorization secrets are not read.
- `Certificates` contains available PCP endorsement certificate stores, including the NV-backed stores. `Source`
  identifies each store, so the same certificate can occur under several sources. Each entry includes complete DER
  in Base64, SHA-256, subject, issuer, serial number, UTC validity and algorithm OIDs. `Keys[i].Certificates` also
  includes certificates returned by the key property or matched by provider/container in the Personal (`My`) stores.
- TPM 2.0 exposes fixed and variable properties, specification and firmware words, supported algorithm IDs, and PCR
  selection bitmaps. A set bit in `PcrBanks[i].Selection` selects that PCR (least-significant bit first within each
  byte); PCR digest values are not collected. Firmware words retain the vendor's encoding. TPM 1.2 receives TBS and
  available PCP information; TPM 2.0-only fields stay null or empty.
- Reads use [TBS](https://learn.microsoft.com/en-us/windows/win32/api/tbs/nf-tbs-tbsi_getdeviceinfo) and silent
  [CNG enumeration](https://learn.microsoft.com/en-us/windows/win32/api/ncrypt/nf-ncrypt-ncryptenumkeys). The collector
  does not create, clear, provision or modify keys, prompt for authorization, download certificates, or validate
  certificate trust. TPM access is serialized within each process, with bounded retries for transient read failures.
- A missing TPM yields `Tpms = []`. Unsupported properties, denied access and failed queries leave their fields null
  or lists empty, with raw codes in `Errors` (`TBS`, `TPM`, `NCrypt` or `Win32`). A null boolean means unknown.
  Each PCP certificate store and persisted-key scope is limited to 1,024 entries; truncation is reported in `Errors`.
  Cancellation is checked between queries and enumeration steps; an in-flight Windows API call finishes first.

### A disk, for example

```json
{
  "id": 1,
  "interface": "SCSI",
  "model": "Samsung SSD 980 PRO 2TB",
  "serial_number": "0025_384A_471F_58A7.",
  "capacity": "1863 GB",
  "partitions": 3,
  "firmware": "5B2QGXA7",
  "world_wide_name": "0025384A471F58A7",
  "disk_guid": "95002274-501c-4590-8f6c-6b45c75a3a0c",
  "nvme_serial": "S69ENC5C753184B",
  "nvme_eui64": "0025384A471F58A7",
  "vpd_scsi_name": "eui.0025384A471F58A7",
  "duid": "3f1c…"
}
```

A drive legitimately reports different serial numbers depending on the layer asked. `serial_number` is the value WMI
reports (for NVMe drives, Windows derives it from the EUI-64), while `nvme_serial` or `ata_serial` is the serial printed
on the label. All of them are kept, each in its own field.

## C and C++

[`HardwareIds.Cpp`](HardwareIds.Cpp) is a port of the library to C++20 for native programs. It runs the same collectors
with the same rules and serializes the result to the JSON `JsonSerializer.Serialize(Hwid)` produces, byte for byte, so a
snapshot taken by a native program can be compared directly with one taken from .NET. CI checks this on every push by
diffing the output of both libraries on the runner. It needs MSVC and CMake 3.21 or later, and nothing else.

### Adding it to a CMake project

```cmake
include(FetchContent)
FetchContent_Declare(HardwareIds
    GIT_REPOSITORY https://github.com/BerkanYildiz/HardwareIds.NET.git
    GIT_TAG        master                # or a release tag
    SOURCE_SUBDIR  HardwareIds.Cpp)
FetchContent_MakeAvailable(HardwareIds)

target_link_libraries(MyApp PRIVATE HardwareIds::HardwareIds)   # static library: C++ and C API
```

`add_subdirectory(path/to/HardwareIds.Cpp)` works the same way. Each release also attaches
`HardwareIds.Cpp-<version>-win-x64.zip`, a prebuilt install tree for `find_package(HardwareIds CONFIG REQUIRED)`: it
holds `HardwareIds::Shared` (`HardwareIds.dll`, the C API, usable from any configuration and any compiler) and
`HardwareIds::HardwareIds` (the static library, built in Release with `/MD`).

### C++

```cpp
#include <HardwareIds/HardwareIds.hpp>

// Hardware only, including the TPM's available public key material.
auto Hwid = HardwareIds::GetHwid();

std::string Json = HardwareIds::ToJson(Hwid);                                     // JsonSerializer.Serialize(Hwid)
std::string Pretty = HardwareIds::ToJson(Hwid, HardwareIds::JsonFormat::Indented); // ... with WriteIndented = true

for (const auto& Disk : Hwid.Disks)
    std::wcout << Disk.Model.value_or(L"?") << L"  " << Disk.NvmeSerial.value_or(Disk.SerialNumber.value_or(L"")) << L'\n';
```

```cpp
// With the network probes. Requesting a stop ends the scans early, like the .NET cancellation token.
HardwareIds::HardwareIdsConfig Config;
Config.ScanNeighborEndpoints = true;
Config.DurationOfNetworkScan = std::chrono::seconds(3);
Config.ScanLocalNetworkDevices = true;

std::stop_source Stop;
auto Hwid = HardwareIds::GetHwid(Config, Stop.get_token());
```

The structures mirror the .NET ones: the same sections and fields, strings as `std::wstring`, and missing values as
`std::nullopt` (JSON `null`).

### C

```c
#include <HardwareIds/HardwareIds.h>

HardwareIds_Options Options = { 0 };            /* or pass NULL: hardware only, compact JSON */
Options.ScanLocalNetworkDevices = 1;

char* Json = HardwareIds_GetSnapshotJson(&Options);

if (Json != NULL)
{
    puts(Json);
    HardwareIds_Free(Json);
}
```

Link against `HardwareIds.lib` and ship `HardwareIds.dll` next to the program, or link the static library and define
`HARDWAREIDS_STATIC` (the CMake target defines it for you).

### Snapshot tool

`HardwareIdsSnapshot` prints a snapshot from the command line:

```bash
HardwareIdsSnapshot --indented --wifi=3 --lan --output snapshot.json
```

## Notes on the identifiers

- `ProcessorId` is the CPUID signature the firmware records in SMBIOS, as WMI reports it: identical for every CPU of the
  same model and stepping, not unique per chip, and all zeros under some hypervisors (Hyper-V, Azure).
- `network_signatures` needs an elevated process; it is empty otherwise.
- `routers` lists only devices inside the subnets of the interface and never devices answering with a gateway's MAC,
  which filters out the proxy-ARP artefacts of statically routed hosts.
- The Windows product key is deliberately not collected.

## Building and testing

```bash
dotnet build
dotnet test
```

The test project runs on both target frameworks. Unit tests exercise the SMBIOS, EDID, NVMe, ATA, SCSI and TPM parsers on
synthetic data; integration tests run every collector on the local machine and compare the results with WMI. Tests that
need hardware or privileges the machine lacks (Wi-Fi, a battery, a Bluetooth radio, monitor EDID, elevation) are
skipped, not failed.

The C++ library has its own tests, and a script that checks both libraries print the same snapshot. Boot time and
volatile TPM state are masked during live comparisons; key material, certificates and fixed TPM capabilities are compared:

```bash
cmake -S HardwareIds.Cpp -B HardwareIds.Cpp/build
cmake --build HardwareIds.Cpp/build --config Release
ctest --test-dir HardwareIds.Cpp/build --build-config Release
```

```powershell
./.github/scripts/parity.ps1 -Snapshot HardwareIds.Cpp/build/Release/HardwareIdsSnapshot.exe
```

## Releasing

Pushing a version tag publishes the package:

```bash
git tag v2.1.0
git push origin v2.1.0
```

The `Publish` workflow builds and tests the solution, packs the library with the tag as its version, pushes it to
NuGet.org and GitHub Packages, and attaches the packages to a GitHub release with generated notes, along with the zip of
the C++ library. A tag with a pre-release suffix (`v2.1.0-beta.1`) produces a pre-release.

No secret is involved: NuGet.org is reached through [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing),
which exchanges the workflow's OIDC token for a one-hour API key, and GitHub Packages uses the built-in token. The
nuget.org account owning the package needs one Trusted Publishing policy (profile menu, *Trusted Publishing*):

| Field | Value |
|---|---|
| Package Owner | the nuget.org profile that owns `HardwareIds.NET` |
| CI/CD Provider | GitHub Actions |
| Repository Owner | `BerkanYildiz` |
| Repository | `HardwareIds.NET` |
| Workflow File | `publish.yml` |
| Environment | leave empty |
| Scopes | Push, new packages and package versions |
| Glob Patterns and Packages | `HardwareIds.NET` |

## License

MIT. See [LICENSE](LICENSE).
