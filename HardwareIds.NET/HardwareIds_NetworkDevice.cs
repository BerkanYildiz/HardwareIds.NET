namespace HardwareIds.NET
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    public static partial class HardwareIds
    {
        private static void ScanNetworkDevices(Hwid InHwid, CancellationToken InCancellationToken = default)
        {
            var Interfaces = NetworkInterface.GetAllNetworkInterfaces().Where(T => T.OperationalStatus == OperationalStatus.Up && T.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var Interface in Interfaces)
            {
                var Entry = new HwRouter() { Id = InHwid.Routers.Count };
                var IpProperties = Interface.GetIPProperties();

                // 
                // Retrieve the gateway and IP ranges.
                // 

                foreach (var GatewayAddress in IpProperties.GatewayAddresses.Where(T => T.Address.AddressFamily == AddressFamily.InterNetwork))
                    Entry.Gateways.Add(new HwNetworkDevice() { Address = GatewayAddress.Address, MacAddress = null });

                foreach (var DnsAddress in IpProperties.DnsAddresses.Where(T => T.AddressFamily == AddressFamily.InterNetwork || T.AddressFamily == AddressFamily.InterNetworkV6))
                    Entry.DnsServers.Add(DnsAddress.ToString());

                foreach (var DhcpServerAddress in IpProperties.DhcpServerAddresses.Where(T => T.AddressFamily == AddressFamily.InterNetwork))
                    Entry.DhcpServers.Add(DhcpServerAddress.ToString());

                // 
                // Scan each DHCP servers for devices connected to the router.
                // 

                if (!InCancellationToken.IsCancellationRequested)
                {
                    foreach (var DhcpServerAddress in Entry.DhcpServers)
                    {
                        var IpSections = DhcpServerAddress.Split('.');
                        var DhcpStartIndex = int.Parse(IpSections.Last());

                        try
                        {
                            Parallel.For(DhcpStartIndex, 20, new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = InCancellationToken }, DhcpIndex =>
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
                                        Entry.NetworkDevices.Add(new HwNetworkDevice() { Address = IpAddress, MacAddress = string.Join(":", MacAddress.Select(T => T.ToString("X2"))) });
                                    }
                                }
                            });
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }

                InHwid.Routers.Add(Entry);
            }
        }
    }
}
