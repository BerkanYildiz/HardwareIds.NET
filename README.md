<h1 align="center">HardwareIds.NET</h1>

<p align="center">
  Fingerprint a Windows machine in about 10 milliseconds, straight from the Windows APIs, with no WMI and no dependencies.
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

- **Fast.** A full hardware scan takes about 10 ms. Everything is read from the Windows APIs directly: the SMBIOS table,
  storage and NDIS IOCTLs, the PnP configuration manager, the registry, the print spooler, the display configuration API,
  the battery and Bluetooth APIs and WlanAPI. No WMI, no `winmgmt` service, no `WmiPrvSE` host.
- **Dependency-free.** No third-party packages. Only `System.Text.Json` on .NET Framework, which is part of .NET 10.
- **No elevation needed.** Every collector works from a standard user account.
- **WMI-compatible values.** The historical fields hold the exact strings WMI reports, so identifiers collected by
  earlier versions keep matching.
- **Serializable.** The result is a plain object with `System.Text.Json` attributes; serialize it and store it as is.
- **Tested.** 130+ unit and integration tests, the latter checking every collector against WMI as an independent oracle.

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

// Hardware only: about 10 ms.
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
`Hwid.OperatingSystem`, ...). Collectors never throw: a component that cannot be read simply yields an empty list.

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

## Notes on the identifiers

- `ProcessorId` is the CPUID signature: identical for every CPU of the same model and stepping, not unique per chip.
- `network_signatures` needs an elevated process; it is empty otherwise.
- `routers` lists only devices inside the subnets of the interface and never devices answering with a gateway's MAC,
  which filters out the proxy-ARP artefacts of statically routed hosts.
- The Windows product key is deliberately not collected.

## Building and testing

```bash
dotnet build
dotnet test
```

The test project runs on both target frameworks. Unit tests exercise the SMBIOS, EDID, NVMe, ATA and SCSI parsers on
synthetic data; integration tests run every collector on the local machine and compare the results with WMI. Tests that
need hardware or privileges the machine lacks (Wi-Fi, a battery, a Bluetooth radio, monitor EDID, elevation) are
skipped, not failed.

## Releasing

Pushing a version tag publishes the package:

```bash
git tag v2.1.0
git push origin v2.1.0
```

The `Publish` workflow builds and tests the solution, packs the library with the tag as its version, pushes it to
NuGet.org and GitHub Packages, and attaches the packages to a GitHub release with generated notes. A tag with a
pre-release suffix (`v2.1.0-beta.1`) produces a pre-release.

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
