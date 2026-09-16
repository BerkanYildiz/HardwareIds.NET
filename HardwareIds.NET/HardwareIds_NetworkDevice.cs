namespace HardwareIds.NET
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Threading;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    public static partial class HardwareIds
    {
        /// <summary>
        /// The number of host addresses probed at the start of each local subnet, on top of the neighbour cache (a whole /24).
        /// </summary>
        internal const int NetworkProbeCount = 254;

        /// <summary>
        /// How long the scan waits for probed devices to answer when the configuration does not say.
        /// </summary>
        internal static readonly TimeSpan DefaultNetworkProbeWait = TimeSpan.FromSeconds(1);

        internal static void ScanNetworkDevices(Hwid InHwid, TimeSpan? InProbeWait = null, CancellationToken InCancellationToken = default)
        {
            var Interfaces = NetworkInterface.GetAllNetworkInterfaces().Where(T => T.OperationalStatus == OperationalStatus.Up && T.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            var Neighbors = new List<NeighborInfo>();

            //
            // The neighbour cache already holds every device the computer recently exchanged packets with; no probing needed.
            //

            try
            {
                Neighbors = IpHlpApi.GetNeighbors();
            }
            catch (Exception)
            {
                // ...
            }

            foreach (var Interface in Interfaces)
            {
                IPInterfaceProperties IpProperties;

                try
                {
                    IpProperties = Interface.GetIPProperties();
                }
                catch (Exception)
                {
                    continue;
                }

                //
                // NDIS filter modules (QoS scheduler, WFP filters, ...) show up as "Up" interfaces without any address; they are not networks.
                //

                var UnicastAddresses = IpProperties.UnicastAddresses.Where(T => T.Address.AddressFamily == AddressFamily.InterNetwork || T.Address.AddressFamily == AddressFamily.InterNetworkV6).ToList();

                if (!HasUsableUnicastAddress(UnicastAddresses.Select(T => T.Address)))
                    continue;

                var Entry = new HwRouter { Id = InHwid.Routers.Count };
                var InterfaceIndex = GetInterfaceIndex(IpProperties);
                var Subnets = GetSubnets(UnicastAddresses);
                var OwnAddresses = new HashSet<IPAddress>(UnicastAddresses.Select(T => T.Address));
                var KnownDevices = new Dictionary<IPAddress, byte[]>();

                foreach (var Neighbor in Neighbors.Where(T => T.InterfaceIndex == InterfaceIndex && !OwnAddresses.Contains(T.Address)))
                    KnownDevices[Neighbor.Address] = Neighbor.PhysicalAddress;

                //
                // Retrieve the gateways, DNS and DHCP servers. Gateways get their MAC address from the cache when it has it.
                //

                foreach (var GatewayAddress in IpProperties.GatewayAddresses.Where(T => T.Address.AddressFamily == AddressFamily.InterNetwork))
                    Entry.Gateways.Add(new HwNetworkDevice { Address = GatewayAddress.Address, MacAddress = KnownDevices.TryGetValue(GatewayAddress.Address, out var CachedMac) ? FormatMacAddress(CachedMac) : null });

                foreach (var DnsAddress in IpProperties.DnsAddresses.Where(T => T.AddressFamily == AddressFamily.InterNetwork || T.AddressFamily == AddressFamily.InterNetworkV6))
                    Entry.DnsServers.Add(DnsAddress.ToString());

                foreach (var DhcpServerAddress in IpProperties.DhcpServerAddresses.Where(T => T.AddressFamily == AddressFamily.InterNetwork))
                    Entry.DhcpServers.Add(DhcpServerAddress.ToString());

                //
                // Probe the subnets and the unresolved gateways, then list every device known for this interface.
                // A device must live in one of the subnets of the interface, and must not answer with a gateway's MAC
                // address: on-link routing and proxy ARP make off-link destinations look like neighbours otherwise.
                //

                var Cancelled = false;

                if (!InCancellationToken.IsCancellationRequested)
                {
                    Cancelled = !ProbeNetworkDevices(Entry, Subnets, OwnAddresses, KnownDevices, InterfaceIndex, InProbeWait ?? DefaultNetworkProbeWait, InCancellationToken);

                    foreach (var Gateway in Entry.Gateways.Where(T => T.MacAddress == null && T.Address != null))
                    {
                        if (KnownDevices.TryGetValue(Gateway.Address!, out var ProbedMac))
                            Gateway.MacAddress = FormatMacAddress(ProbedMac);
                        else if (!Cancelled && ResolveMacAddress(Gateway.Address!) is byte[] ResolvedMac)
                            Gateway.MacAddress = FormatMacAddress(ResolvedMac);
                    }

                    var GatewayMacs = new HashSet<string>(Entry.Gateways.Where(T => T.MacAddress != null).Select(T => T.MacAddress!), StringComparer.OrdinalIgnoreCase);
                    var GatewayAddresses = new HashSet<IPAddress>(Entry.Gateways.Where(T => T.Address != null).Select(T => T.Address!));

                    foreach (var Known in KnownDevices)
                    {
                        var MacAddress = FormatMacAddress(Known.Value);

                        if (GatewayAddresses.Contains(Known.Key) || GatewayMacs.Contains(MacAddress))
                            continue;

                        if (Subnets.Any(T => IsInSubnet(Known.Key, T.Address, T.Mask)))
                            Entry.NetworkDevices.Add(new HwNetworkDevice { Address = Known.Key, MacAddress = MacAddress });
                    }
                }

                Entry.NetworkDevices.Sort((InLeft, InRight) => CompareAddresses(InLeft.Address, InRight.Address));

                if (Entry.Gateways.Count + Entry.DnsServers.Count + Entry.DhcpServers.Count + Entry.NetworkDevices.Count > 0)
                    InHwid.Routers.Add(Entry);

                if (Cancelled)
                    break;
            }
        }

        /// <summary>
        /// Makes the operating system resolve the first hosts of each IPv4 subnet of the interface: one UDP datagram
        /// to the discard port of every host triggers an ARP request without blocking, and after a short wait the
        /// neighbour cache holds the MAC address of every host that answered. The devices found are merged into
        /// <paramref name="InKnownDevices"/>.
        /// </summary>
        /// <returns>False when the scan was cancelled.</returns>
        private static bool ProbeNetworkDevices(HwRouter InEntry, List<(IPAddress Address, IPAddress Mask)> InSubnets, HashSet<IPAddress> InOwnAddresses, Dictionary<IPAddress, byte[]> InKnownDevices, int InInterfaceIndex, TimeSpan InWait, CancellationToken InCancellationToken)
        {
            var Targets = new HashSet<IPAddress>();

            foreach (var Subnet in InSubnets)
                Targets.UnionWith(GetProbeAddresses(Subnet.Address, Subnet.Mask, NetworkProbeCount));

            foreach (var Gateway in InEntry.Gateways.Where(T => T.MacAddress == null && T.Address != null))
                Targets.Add(Gateway.Address!);

            Targets.ExceptWith(InOwnAddresses);
            Targets.ExceptWith(InKnownDevices.Keys);

            if (Targets.Count == 0)
                return true;

            try
            {
                using var Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                var Source = InSubnets.Select(T => T.Address).FirstOrDefault();

                if (Source != null)
                    Socket.Bind(new IPEndPoint(Source, 0));

                foreach (var Target in Targets)
                {
                    if (InCancellationToken.IsCancellationRequested)
                        return false;

                    try
                    {
                        Socket.SendTo([0], new IPEndPoint(Target, 9));
                    }
                    catch (SocketException)
                    {
                        // Unreachable or filtered; nothing to resolve.
                    }
                }
            }
            catch (Exception)
            {
                return !InCancellationToken.IsCancellationRequested;
            }

            if (InCancellationToken.WaitHandle.WaitOne(InWait))
                return false;

            try
            {
                foreach (var Neighbor in IpHlpApi.GetNeighbors().Where(T => T.InterfaceIndex == InInterfaceIndex && Targets.Contains(T.Address)))
                    InKnownDevices[Neighbor.Address] = Neighbor.PhysicalAddress;
            }
            catch (Exception)
            {
                // ...
            }

            return true;
        }

        /// <summary>
        /// Resolves the MAC address of an on-link IPv4 address with a blocking ARP request (used for gateways only).
        /// </summary>
        /// <param name="InAddress">The IPv4 address.</param>
        internal static byte[]? ResolveMacAddress(IPAddress InAddress)
        {
            var MacAddress = new byte[6];
            var MacAddressLength = MacAddress.Length;

            if (InAddress.AddressFamily != AddressFamily.InterNetwork)
                return null;

            //
            // Unspecified, loopback, multicast and broadcast addresses are not devices; Windows answers some of them with our own MAC.
            //

            var Bytes = InAddress.GetAddressBytes();

            if (Bytes[0] == 0 || Bytes[0] == 127 || Bytes[0] >= 224)
                return null;

            if (IpHlpApi.SendARP(BitConverter.ToUInt32(Bytes, 0), 0, MacAddress, ref MacAddressLength) != 0 || MacAddressLength != 6)
                return null;

            //
            // Unresolved and broadcast answers are not devices.
            //

            return MacAddress.All(T => T == 0x00) || MacAddress.All(T => T == 0xFF) ? null : MacAddress;
        }

        /// <summary>
        /// Tells whether an interface has an address that makes it a network worth describing (any IPv4 address, or a non link-local IPv6 address).
        /// </summary>
        /// <param name="InAddresses">The unicast addresses of the interface.</param>
        internal static bool HasUsableUnicastAddress(IEnumerable<IPAddress> InAddresses)
        {
            return InAddresses.Any(T => T.AddressFamily == AddressFamily.InterNetwork || (T.AddressFamily == AddressFamily.InterNetworkV6 && !T.IsIPv6LinkLocal));
        }

        /// <summary>
        /// Tells whether an IPv4 address belongs to the subnet of another IPv4 address.
        /// </summary>
        /// <param name="InAddress">The address to test.</param>
        /// <param name="InSubnetAddress">Any address of the subnet.</param>
        /// <param name="InMask">The subnet mask.</param>
        internal static bool IsInSubnet(IPAddress InAddress, IPAddress InSubnetAddress, IPAddress InMask)
        {
            if (InAddress.AddressFamily != AddressFamily.InterNetwork || InSubnetAddress.AddressFamily != AddressFamily.InterNetwork || InMask.AddressFamily != AddressFamily.InterNetwork)
                return false;

            var Mask = ToUInt32(InMask);
            return (ToUInt32(InAddress) & Mask) == (ToUInt32(InSubnetAddress) & Mask);
        }

        /// <summary>
        /// Gets the first host addresses of the IPv4 subnet an address belongs to.
        /// Link-local (169.254.0.0/16) subnets yield nothing: their hosts pick random addresses, so probing the first ones is pointless.
        /// </summary>
        /// <param name="InAddress">An address of the subnet.</param>
        /// <param name="InMask">The subnet mask.</param>
        /// <param name="InCount">The maximum number of addresses to return.</param>
        internal static IEnumerable<IPAddress> GetProbeAddresses(IPAddress InAddress, IPAddress InMask, int InCount)
        {
            if (InAddress.AddressFamily != AddressFamily.InterNetwork || InMask.AddressFamily != AddressFamily.InterNetwork)
                yield break;

            var Bytes = InAddress.GetAddressBytes();

            if (Bytes[0] == 169 && Bytes[1] == 254)
                yield break;

            var Address = ToUInt32(InAddress);
            var Mask = ToUInt32(InMask);
            var Network = Address & Mask;
            var Broadcast = Network | ~Mask;

            for (var Host = Network + 1; Host < Broadcast && Host - Network <= (uint) InCount; Host++)
                yield return FromUInt32(Host);
        }

        private static List<(IPAddress Address, IPAddress Mask)> GetSubnets(IEnumerable<UnicastIPAddressInformation> InUnicastAddresses)
        {
            var Result = new List<(IPAddress Address, IPAddress Mask)>();

            foreach (var Unicast in InUnicastAddresses.Where(T => T.Address.AddressFamily == AddressFamily.InterNetwork))
            {
                try
                {
                    if (Unicast.IPv4Mask is IPAddress Mask)
                        Result.Add((Unicast.Address, Mask));
                }
                catch (Exception)
                {
                    // ...
                }
            }

            return Result;
        }

        private static int GetInterfaceIndex(IPInterfaceProperties InProperties)
        {
            try
            {
                return InProperties.GetIPv4Properties()?.Index ?? InProperties.GetIPv6Properties()?.Index ?? -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private static int CompareAddresses(IPAddress? InLeft, IPAddress? InRight)
        {
            if (InLeft is null || InRight is null)
                return InLeft is null ? (InRight is null ? 0 : -1) : 1;

            var Family = InLeft.AddressFamily.CompareTo(InRight.AddressFamily);
            return Family != 0 ? Family : ToUInt32(InLeft).CompareTo(ToUInt32(InRight));
        }

        private static uint ToUInt32(IPAddress InAddress)
        {
            var Bytes = InAddress.GetAddressBytes();
            return Bytes.Length == 4 ? ((uint) Bytes[0] << 24) | ((uint) Bytes[1] << 16) | ((uint) Bytes[2] << 8) | Bytes[3] : 0;
        }

        private static IPAddress FromUInt32(uint InValue)
        {
            return new IPAddress([(byte) (InValue >> 24), (byte) (InValue >> 16), (byte) (InValue >> 8), (byte) InValue]);
        }
    }
}
