namespace HardwareIds.NET
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    using WindowsMonitor.Hardware;
    using WindowsMonitor.Hardware.Bios;
    using WindowsMonitor.Hardware.Drives.Win32DiskDrives;
    using WindowsMonitor.Hardware.Network;
    using WindowsMonitor.Hardware.OnBoard;
    using WindowsMonitor.Hardware.Printers;
    using WindowsMonitor.Hardware.Processors;
    using WindowsMonitor.Hardware.Storage.Volumes;
    using WindowsMonitor.Hardware.Video;
    using WindowsMonitor.Hardware.Video.WmiMonitor;
    using WindowsMonitor.Windows;
    using WindowsMonitor.Windows.Users;

    using Driver.NET.Device;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using ManagedNativeWifi;

    using PhysicalMemory = WindowsMonitor.Hardware.Memories.PhysicalMemory;

    public static class HardwareIds
    {
        /// <summary>
        /// Gets the current hardware information of this (local) computer.
        /// </summary>
        /// <param name="InConfig">The configuration.</param>
        [Obsolete]
        public static async ValueTask<Hwid> GetHwidAsync(HardwareIdsConfig InConfig = null)
        {
            if (InConfig == null)
                InConfig = new HardwareIdsConfig();

            var Hwid = new Hwid();
            var HwidTasks = new List<Task>();

            // 
            // Retrieve the disk devices connected to this computer.
            // 

            try
            {
                foreach (var Disk in Win32DiskDrive.Retrieve().OrderBy(T => T.Index))
                {
                    Hwid.Disks.Add(new HwDisk
                    {
                        Id = (int) Disk.Index,
                        Interface = Disk.InterfaceType,
                        Model = Disk.Model,
                        SerialNumber = Disk.SerialNumber,
                        Capacity = (Disk.Size / 1024 / 1024 / 1024) + " GB",
                        Partitions = (int) Disk.Partitions,
                        IsRemovable = Disk.CapabilityDescriptions.Any(T => T.Contains("Removable")),
                        IsSMART = Disk.CapabilityDescriptions.Any(T => T.Contains("SMART") || T.Contains("S.M.A.R.T")),
                    });
                }
            }
            catch (Exception)
            {
                // ...
            }

            // 
            // Retrieve the volumes configured on each disks plugged into this computer.
            // 

            try
            {
                foreach (var Volume in Volume.Retrieve())
                {
                    Hwid.Volumes.Add(new HwVolume
                    {
                        Id = (int) Hwid.Volumes.Count,
                        Path = Volume.DeviceId,
                        Letter = Volume.DriveLetter,
                        SerialNumber = Volume.SerialNumber,
                    });
                }
            }
            catch (Exception)
            {
                // ...
            }

            // 
            // Retrieve the network adapters installed (and enabled) on this computer.
            // 

            try
            {
                foreach (var NetworkAdapter in Win32NetworkAdapter.Retrieve().Where(T => T.Installed /* && T.NetEnabled */ && T.PhysicalAdapter).OrderBy(T => T.Index).ThenByDescending(T => T.PhysicalAdapter))
                {
                    var Entry = new HwNetworkAdapter
                    {
                        Id = (int) NetworkAdapter.Index,
                        InterfaceId = (int) NetworkAdapter.InterfaceIndex,
                        Name = NetworkAdapter.ProductName,
                        InterfaceGuid = NetworkAdapter.Guid,
                        ServiceName = NetworkAdapter.ServiceName,
                        IsPhysical = NetworkAdapter.PhysicalAdapter,
                        Address = { Current = NetworkAdapter.MacAddress, Permanent = NetworkAdapter.PermanentAddress },
                    };

                    if (DeviceIoControl.Exists($@"\\.\{NetworkAdapter.Guid}"))
                    {
                        var DeviceIo = new DeviceIoControl($@"\\.\{NetworkAdapter.Guid}");

                        { DeviceIo.Connect();
                            {
                                var InputValue = 0x01010101;
                                var OutputValue = new byte[6];
                                unsafe { fixed (byte* OutputBuffer = OutputValue) { DeviceIo.TryIoControl(0x170002, &InputValue, 4, OutputBuffer, 6); }}
                                Entry.Address.Current = string.Join(":", OutputValue.Select(T => T.ToString("X2")));
                            }

                            {
                                var InputValue = 0x01010102;
                                var OutputValue = new byte[6];
                                unsafe { fixed (byte* OutputBuffer = OutputValue) { DeviceIo.TryIoControl(0x170002, &InputValue, 4, OutputBuffer, 6); } }
                                Entry.Address.Permanent = string.Join(":", OutputValue.Select(T => T.ToString("X2")));
                            }
                        } DeviceIo.Close();
                    }

                    Hwid.NetworkAdapters.Add(Entry);
                }
            }
            catch (Exception)
            {
                // ...
            }

            // 
            // Retrieve the baseboard(s) installed on this computer.
            // 

            try
            {
                foreach (var Baseboard in BaseBoard.Retrieve())
                {
                    Hwid.Baseboards.Add(new HwBaseboard
                    {
                        Id = (int) Hwid.Baseboards.Count,
                        Manufacturer = Baseboard.Manufacturer,
                        Model = Baseboard.Product,
                        Version = Baseboard.Version,
                        SerialNumber = Baseboard.SerialNumber,
                        PartNumber = Baseboard.PartNumber,
                    });
                }
            }
            catch (Exception)
            {
                // ...
            }

            // 
            // Retrieve the motherboard(s) installed on this computer.
            // 

            try
            {
                foreach (var Motherboard in Win32ComputerSystemProduct.Retrieve())
                {
                    Hwid.Motherboards.Add(new HwMotherboard
                    {
                        Id = (int) Hwid.Motherboards.Count,
                        Name = Motherboard.Name,
                        Vendor = Motherboard.Vendor,
                        Version = Motherboard.Version,
                        UUID = Guid.Parse(Motherboard.Uuid),
                    });
                }
            }
            catch (Exception)
            {
                // ...
            }

            // 
            // Retrieve the BIOS firmwares installed on this computer's motherboard(s).
            // 

            try
            {
                foreach (var BiosFirmware in Bios.Retrieve())
                {
                    Hwid.BiosFirmwares.Add(new HwBios
                    {
                        Id = (int) Hwid.BiosFirmwares.Count,
                        Manufacturer = BiosFirmware.Manufacturer,
                        Version = BiosFirmware.Version,
                        SerialNumber = BiosFirmware.SerialNumber,
                    });
                }
            }
            catch (Exception)
            {
                // ...
            }

            // 
            // Retrieve the SMBIOS Table(s) configured on this computer's motherboard's bios(es).
            // 

            try
            {
                foreach (var SmbiosTable in SmBiosRawSmBiosTables.Retrieve())
                {
                    using (SHA256 sha256 = SHA256.Create())
                    {
                        Hwid.SmbiosTables.Add(new HwSmbios
                        {
                            Id = (int)Hwid.SmbiosTables.Count,
                            Version = $"{SmbiosTable.SmbiosMajorVersion}.{SmbiosTable.SmbiosMinorVersion}.{SmbiosTable.DmiRevision}",
                            Hash = string.Join(string.Empty, sha256.ComputeHash(SmbiosTable.SmBiosData).Select(T => T.ToString("x2"))),

                            Length = SmbiosTable.Size,
                        });
                    }
                }
            }
            catch (Exception)
            {
                // ...
            }

            // 
            // Retrieve the processor(s) installed on this computer's motherboard(s).
            // 

            try
            {
                foreach (var Processor in Win32Processor.Retrieve())
                {
                    Hwid.Processors.Add(new HwProcessor
                    {
                        Id = (int) Hwid.Processors.Count,
                        Manufacturer = Processor.Manufacturer,
                        Model = Processor.Name,
                        ModelNumber = Processor.ProcessorId,
                        Socket = Processor.SocketDesignation,
                        SerialNumber = Processor.SerialNumber,
                        PartNumber = Processor.PartNumber,
                        ClockSpeed = $"{Processor.CurrentClockSpeed} MHz",
                        Voltage = $"{Processor.CurrentVoltage} V",
                        Channel = Processor.DeviceId,
                        NumberOfCores = Processor.NumberOfCores,
                        NumberOfLogicalProcessors = Processor.NumberOfLogicalProcessors,
                    });
                }
            }
            catch (Exception)
            {
                // ...
            }

            // 
            // Retrieve the memory sticks installed on this computer's motherboard(s).
            // 

            try
            {
                foreach (var MemoryStick in PhysicalMemory.Retrieve())
                {
                    Hwid.MemorySticks.Add(new HwMemoryStick
                    {
                        Id = (int) Hwid.MemorySticks.Count,
                        Manufacturer = MemoryStick.Manufacturer,
                        Capacity = (MemoryStick.Capacity / 1024 / 1024 / 1024) + " GB",
                        ClockSpeed = $"{MemoryStick.ConfiguredClockSpeed} MHz",
                        Voltage = $"{MemoryStick.ConfiguredVoltage / 1000:0.00} V",
                        SerialNumber = MemoryStick.SerialNumber,
                        PartNumber = MemoryStick.PartNumber,
                        Channel = MemoryStick.DeviceLocator,
                    });
                }
            }
            catch (Exception)
            {
                // ..
            }

            // 
            // Retrieve the monitors plugged into this computer.
            // 

            try
            {
                foreach (var Monitor in WmiMonitorId.Retrieve())
                {
                    Hwid.Monitors.Add(new HwMonitor
                    {
                        Id = (int) Hwid.Monitors.Count,
                        Manufacturer = HwMonitor.ArrayToString(Monitor.ManufacturerName),
                        Name = HwMonitor.ArrayToString(Monitor.UserFriendlyName),
                        Product = HwMonitor.ArrayToString(Monitor.ProductCodeId),
                        SerialNumber = HwMonitor.ArrayToString(Monitor.SerialNumberId),
                    });
                }
            }
            catch (Exception)
            {
                // ...
            }

            // 
            // Retrieve the video controllers plugged into this computer.
            // 

            try
            {
                foreach (var VideoController in Win32VideoController.Retrieve())
                {
                    Hwid.VideoControllers.Add(new HwVideo
                    {
                        Id = (int) Hwid.VideoControllers.Count,
                        Name = VideoController.Name,
                        Width = VideoController.CurrentHorizontalResolution,
                        Height = VideoController.CurrentVerticalResolution,
                        RefreshRate = VideoController.CurrentRefreshRate,
                        DriverDate = VideoController.DriverDate,
                        DriverVersion = VideoController.DriverVersion,
                    });
                }
            }
            catch (Exception)
            {
                // ...
            }

            // 
            // Retrieve the printers ever connected to this computer.
            // 

            try
            {
                foreach (var Printer in Win32Printer.Retrieve())
                {
                    Hwid.Printers.Add(new HwPrinter
                    {
                        Id = (int) Hwid.Printers.Count,
                        Name = Printer.Name,
                        PortName = Printer.PortName,
                        Location = Printer.Location,
                        Width = Printer.HorizontalResolution,
                        Height = Printer.VerticalResolution,
                    });
                }
            }
            catch (Exception)
            {
                // ...
            }

            // 
            // Retrieve the users that have ever logged into this computer.
            // 

            try
            {
                foreach (var UserAccount in UserAccount.Retrieve().Where(T => !T.Disabled))
                {
                    Hwid.Users.Add(new HwUser
                    {
                        Id = (int) Hwid.Users.Count,
                        Username = UserAccount.Name,
                        FullName = UserAccount.FullName,
                        SID = UserAccount.Sid,
                        Domain = UserAccount.Domain,
                        InstallDate = UserAccount.InstallDate,
                    });
                }
            }
            catch (Exception)
            {
                // ...
            }

            // 
            // Retrieve the operating systems configuration.
            // 

            try
            {
                foreach (var OperatingSystem in Win32OperatingSystem.Retrieve())
                {
                    Hwid.OperatingSystems.Add(new HwOperatingSystem
                    {
                        Id = (int) Hwid.OperatingSystems.Count,
                        Name = OperatingSystem.Caption,
                        Version = OperatingSystem.Version,
                        Architecture = OperatingSystem.OsArchitecture,
                        RegisteredUser = OperatingSystem.RegisteredUser,
                        SerialNumber = OperatingSystem.SerialNumber,
                        InstallDate = OperatingSystem.InstallDate,
                        LastBootUpTime = OperatingSystem.LastBootUpTime,
                    });
                }
            }
            catch (Exception)
            {
                // ...
            }

            // 
            // Retrieve the WIFI endpoints currently available around the computer.
            // 

            if (InConfig.ScanNeighborEndpoints)
            {
                HwidTasks.Add(Task.Run(async () =>
                {
                    // 
                    // Scan for the available WIFI endpoints around this computer.
                    // 

                    var NeighborEndpoints = (IEnumerable<Guid>) null;

                    try
                    {
                        NeighborEndpoints = await NativeWifi.ScanNetworksAsync(InConfig.DurationOfNetworkScan);
                    }
                    catch (Exception)
                    {
                        // ...
                    }

                    // 
                    // Retrieve every available WIFI endpoints that have been previously scanned.
                    // 

                    if (NeighborEndpoints != null)
                    {
                        var AvailableEndpoints = (IEnumerable<BssNetworkPack>) null;

                        try
                        {
                            AvailableEndpoints = NativeWifi.EnumerateBssNetworks();
                        }
                        catch (Exception)
                        {
                            // ...
                        }

                        // 
                        // For each available WIFI endpoint...
                        // 

                        if (AvailableEndpoints != null)
                        {
                            foreach (var Wifi in AvailableEndpoints)
                            {
                                Hwid.Wifis.Add(new HwWifi
                                {
                                    Id = (int) Hwid.Wifis.Count,
                                    Ssid = Wifi.Ssid.ToString(),
                                    Bssid = string.Join(":", Wifi.Bssid.ToBytes().Select(T => T.ToString("X2"))),
                                    Strength = Wifi.SignalStrength,
                                    Channel = Wifi.Channel,
                                    Frequency = Wifi.Frequency,
                                    Band = Wifi.Band,
                                    Quality = Wifi.LinkQuality,
                                });
                            }
                        }
                    }
                }));
            }

            // 
            // Retrieve the WIFI endpoints this computer has connected to in the past.
            // 

            /* try
            {
                var SelectedWifiProfile = RegistryUtils.GetValue<string>(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\Settings\Network\Preferences\", "SelectedWiFiProfile");
                
                foreach (var Wifi in Wifi)
                {
                    Hwid.Wifis.Add(new HwWifi
                    {
                        Id = (int) Hwid.Wifis.Count,
                        Name = "",
                    });
                }
            }
            catch (Exception)
            {
                // ...
            } */

            // 
            // Retrieve information about the routers.
            // 

            if (InConfig.ScanLocalNetworkDevices)
            {
                HwidTasks.Add(Task.Run(() =>
                {
                    var Interfaces = NetworkInterface.GetAllNetworkInterfaces().Where(T => T.OperationalStatus == OperationalStatus.Up && T.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                    foreach (var Interface in Interfaces)
                    {
                        var Entry = new HwRouter() { Id = Hwid.Routers.Count };
                        var IpProperties = Interface.GetIPProperties();

                        // 
                        // Retrieve the gateway and IP ranges.
                        // 

                        foreach (var GatewayAddress in IpProperties.GatewayAddresses.Where(T => T.Address.AddressFamily == AddressFamily.InterNetwork))
                            Entry.Gateways.Add(new HwRouter.NetworkDevice() { Address = GatewayAddress.Address, MacAddress = null } );

                        foreach (var DnsAddress in IpProperties.DnsAddresses.Where(T => T.AddressFamily == AddressFamily.InterNetwork || T.AddressFamily == AddressFamily.InterNetworkV6))
                            Entry.DnsServers.Add(DnsAddress.ToString());

                        foreach (var DhcpServerAddress in IpProperties.DhcpServerAddresses.Where(T => T.AddressFamily == AddressFamily.InterNetwork))
                            Entry.DhcpServers.Add(DhcpServerAddress.ToString());

                        // 
                        // Scan each DHCP servers for devices connected to the router.
                        // 

                        foreach (var DhcpServerAddress in Entry.DhcpServers)
                        {
                            var IpSections = DhcpServerAddress.Split('.');
                            var DhcpStartIndex = int.Parse(IpSections.Last());

                            Parallel.For(DhcpStartIndex, 40, new ParallelOptions { MaxDegreeOfParallelism = 32 }, DhcpIndex =>
                            {
                                var MacAddressLength = 6;
                                var MacAddress = new byte[6];
                                var DhcpAddress = string.Join(".", IpSections[0], IpSections[1], IpSections[2], DhcpIndex);
                                var IpAddress = IPAddress.Parse(DhcpAddress);
                                var DhcpStatus = SendARP((uint) IpAddress.Address, 0, MacAddress, ref MacAddressLength);

                                if (DhcpStatus == 0)
                                {
                                    var Gateway = Entry.Gateways.FirstOrDefault(T => T.Address.Equals(IpAddress));

                                    if (Gateway != null)
                                    {
                                        Gateway.MacAddress = string.Join(":", MacAddress.Select(T => T.ToString("X2")));
                                    }
                                    else
                                    {
                                        Entry.NetworkDevices.Add(new HwRouter.NetworkDevice() { Address = IpAddress, MacAddress = string.Join(":", MacAddress.Select(T => T.ToString("X2"))) });
                                    }
                                }
                            });
                        }

                        Hwid.Routers.Add(Entry);
                    }
                }));
            }

            // 
            // Wait for every running tasks to terminate.
            // 

            if (HwidTasks.Count > 0)
                await Task.WhenAll(HwidTasks).ConfigureAwait(false);

            return Hwid;
        }

        /// <summary>
        /// Gets the current hardware information of this (local) computer.
        /// </summary>
        /// <param name="InConfig">The configuration.</param>
        [Obsolete]
        public static Hwid GetHwid(HardwareIdsConfig InConfig = null)
        {
            return GetHwidAsync(InConfig).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(uint DestinationIp, uint SourceIp, byte[] MacAddress, ref int MacAddressLength);
    }
}