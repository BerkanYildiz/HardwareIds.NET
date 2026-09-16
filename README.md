# HardwareIds.NET
Simple .NET library capable of tracking users across re-installs and hardware swapping,  
and even entirely new computers using the neighborhood's network endpoints.

## How does it work?

HardwareIds.NET will enumerate every single hardware component plugged-in/installed on the computer,  
query their unique identifiers (like serial numbers, mac addresses, etc...) and go even beyond that,  
by scanning network endpoints (WiFi) and routers available that are transmitting packets close to the computer.  
  
This library will also take care of scanning the current network (WiFi/Ethernet) the computer is connected to,  
and retrieve the MAC address of every device connected, like printers, Smart TVs, phones, etc...  

Everything is read straight from the Windows APIs, without WMI: the SMBIOS table (`GetSystemFirmwareTable`),  
storage and NDIS device IOCTLs, the PnP configuration manager, the EDID blocks of the monitors, the registry,  
`NetUserEnum`, the print spooler, the display configuration API and the native Wi-Fi API (WlanAPI). A full scan (without the network scans)  
takes about 10 ms, does not need administrator rights, does not depend on the WMI service, and the library has no third-party dependencies.

The values returned are the same ones WMI reports, so identifiers collected with previous versions keep matching.

## Requirements

- Windows (the library relies on the SMBIOS table, the PnP manager, the registry and the native Wi-Fi API).
- .NET 10 or .NET Framework 4.8.

## Installation

    PM> Install-Package HardwareIds.NET

or

    dotnet add package HardwareIds.NET

## Example

```csharp
using HardwareIds.NET;

var Hwid = await HardwareIds.GetHwidAsync(new HardwareIdsConfig {
  ScanLocalNetworkDevices = false,
  ScanNeighborEndpoints = true,
  DurationOfNetworkScan = TimeSpan.FromSeconds(5)
});

foreach (var Disk in Hwid.Disks)
{
    Console.WriteLine($"  DISK->ID:       {Disk.Id}");
    Console.WriteLine($"  DISK->NAME:     {Disk.Model}");
    Console.WriteLine($"  DISK->SN:       {Disk.SerialNumber}");
}

// The result can be serialized as-is with System.Text.Json.
var Json = JsonSerializer.Serialize(Hwid);
```

## Tests

The `HardwareIds.NET.Tests` project holds unit tests for the SMBIOS and EDID parsers (using synthetic tables) and integration tests that run every collector on the local machine and compare the results with WMI. Run them with:

    dotnet test

Tests that depend on hardware or privileges that are missing (no Wi-Fi interface, no monitor EDID, non-elevated process) are skipped rather than failed.

# Licence
This work is licensed under the MIT License.
