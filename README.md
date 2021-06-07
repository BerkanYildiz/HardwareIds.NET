# HardwareIds.NET
Simple .NET library capable of tracking users across re-installs and hardware components swapping,  
and even entirely new computer using the neighborhood's network endpoints.

## How does it works ?

HardwareIds.NET will enumerate every single hardware components plugged-in/installed on the computer,  
query their unique identifiers (like serial numbers, mac addresses, etc...) and go even beyond that,  
by scanning network endpoints (WiFi) and routers available and currently transmitting packets close to the computer.  
  
This library will also take care of scanning the current network (WiFi/Ethernet) the computer is connected to,  
and retrieve the MAC address of every devices connected, like printers, Smart TVs, phones, etc...  
  
Even worse (or better), we will scan and retrieve the location of the computer, whether from the GPS chipset  
if the computer has one, or WiFi triangulation, or if connected using Ethernet, the location being set by the ISP.  
  
No one can escape this type of hardware tracking.  
Might aswell go live in a bunker; use ethernet; and only browse the internet using a VPN/TOR.  
  
May God be with you my brothers.  

## Installation

    PM> Install-Package HardwareIds.NET

## Example

```csharp
soon :-)
```

# Licence
This work is licensed under the MIT License.
