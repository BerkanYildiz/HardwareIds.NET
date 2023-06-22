# HardwareIds.NET
Simple .NET library capable of tracking users across re-installs and hardware swapping,  
and even entirely new computers using the neighborhood's network endpoints.

## How does it works ?

HardwareIds.NET will enumerate every single hardware components plugged-in/installed on the computer,  
query their unique identifiers (like serial numbers, mac addresses, etc...) and go even beyond that,  
by scanning network endpoints (WiFi) and routers available that are transmitting packets close to the computer.  
  
This library will also take care of scanning the current network (WiFi/Ethernet) the computer is connected to,  
and retrieve the MAC address of every devices connected, like printers, Smart TVs, phones, etc...  

## Installation

    PM> Install-Package HardwareIds.NET

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
```

# Licence
This work is licensed under the MIT License.
