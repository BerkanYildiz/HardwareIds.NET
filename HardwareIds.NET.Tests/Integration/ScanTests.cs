namespace HardwareIds.NET.Tests.Integration
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Threading;
    using System.Threading.Tasks;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;

    using Xunit;

    public class ScanTests
    {
        private const string MacPattern = "^([0-9A-F]{2}:){5}[0-9A-F]{2}$";

        private static CancellationToken TestToken => TestContext.Current.CancellationToken;

        private static bool HasWifiInterface()
        {
            using var Session = WlanSession.Open();
            return Session != null && Session.EnumerateInterfaces().Count > 0;
        }

        [Fact]
        public void GetHwid_WithNullConfig_UsesDefaults()
        {
            var Hwid = HardwareIds.GetHwid(null, TestToken);

            Assert.NotNull(Hwid);
            Assert.NotEmpty(Hwid.Disks);
            Assert.Empty(Hwid.Wifis);
            Assert.Empty(Hwid.Routers);
        }

        [Fact]
        public async Task GetHwidAsync_AgreesWithGetHwid()
        {
            var Synchronous = HardwareIds.GetHwid(new HardwareIdsConfig(), TestToken);
            var Asynchronous = await HardwareIds.GetHwidAsync(new HardwareIdsConfig(), TestToken);

            Assert.Equal(HwidFixture.ToComparableJson(Synchronous), HwidFixture.ToComparableJson(Asynchronous));
        }

        [Fact]
        public void GetHwid_IsStableAcrossRuns()
        {
            var First = HardwareIds.GetHwid(new HardwareIdsConfig(), TestToken);
            var Second = HardwareIds.GetHwid(new HardwareIdsConfig(), TestToken);

            Assert.Equal(HwidFixture.ToComparableJson(First), HwidFixture.ToComparableJson(Second));
        }

        [Fact]
        public void GetHwid_WithCancelledToken_ReturnsAnEmptyResult()
        {
            using var Cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestToken);
            Cancellation.Cancel();

            var Hwid = HardwareIds.GetHwid(new HardwareIdsConfig { ScanNeighborEndpoints = true, ScanLocalNetworkDevices = true }, Cancellation.Token);

            Assert.NotNull(Hwid);
            Assert.All(typeof(Hwid).GetProperties().Where(T => T.PropertyType.IsGenericType), T => Assert.Empty((System.Collections.IEnumerable) T.GetValue(Hwid)!));
        }

        [Fact]
        public void GetHwid_CompletesQuickly()
        {
            HardwareIds.GetHwid(new HardwareIdsConfig(), TestToken);

            var Timer = Stopwatch.StartNew();
            HardwareIds.GetHwid(new HardwareIdsConfig(), TestToken);
            Timer.Stop();

            Assert.True(Timer.ElapsedMilliseconds < 500, $"A warm scan took {Timer.ElapsedMilliseconds} ms.");
        }

        [Fact]
        public void GetHwid_SupportsConcurrentCalls()
        {
            var Results = new Hwid[8];

            Parallel.For(0, Results.Length, I => Results[I] = HardwareIds.GetHwid(new HardwareIdsConfig(), TestToken));

            var Expected = HwidFixture.ToComparableJson(Results[0]);
            Assert.All(Results, T => Assert.Equal(Expected, HwidFixture.ToComparableJson(T)));
        }

        [Fact]
        public async Task NeighborEndpointsScan_ReturnsWellFormedNetworks()
        {
            Assert.SkipUnless(HasWifiInterface(), "No Wi-Fi interface on this machine.");

            var Hwid = await HardwareIds.GetHwidAsync(new HardwareIdsConfig { ScanNeighborEndpoints = true, DurationOfNetworkScan = TimeSpan.FromSeconds(3) }, TestToken);

            Assert.Equal(Enumerable.Range(0, Hwid.Wifis.Count), Hwid.Wifis.Select(T => T.Id));
            Assert.All(Hwid.Wifis, Wifi =>
            {
                Assert.Matches(MacPattern, Wifi.Bssid);
                Assert.NotNull(Wifi.Ssid);
                Assert.True(Wifi.Channel > 0);
                Assert.True(Wifi.Frequency > 0);
            });
        }

        [Fact]
        public async Task NeighborEndpointsScan_WithoutWifi_ReturnsNothingAndDoesNotThrow()
        {
            Assert.SkipWhen(HasWifiInterface(), "This machine has a Wi-Fi interface.");

            var Hwid = await HardwareIds.GetHwidAsync(new HardwareIdsConfig { ScanNeighborEndpoints = true, DurationOfNetworkScan = TimeSpan.FromSeconds(1) }, TestToken);

            Assert.Empty(Hwid.Wifis);
        }

        //
        // The ARP probes take a few seconds, so the local network scan runs once and is shared by the tests below.
        //

        private static readonly Lazy<Task<Hwid>> LocalNetworkScan = new(() => Task.Run(async () =>
        {
            using var Cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            return await HardwareIds.GetHwidAsync(new HardwareIdsConfig { ScanLocalNetworkDevices = true }, Cancellation.Token);
        }));

        private static Task<Hwid> ScanLocalNetworkAsync()
        {
            return LocalNetworkScan.Value;
        }

        [Fact]
        public async Task LocalNetworkScan_DescribesOnlyInterfacesWithAnAddress()
        {
            var Hwid = await ScanLocalNetworkAsync();

            var Configured = NetworkInterface.GetAllNetworkInterfaces()
                .Where(T => T.OperationalStatus == OperationalStatus.Up && T.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(T => T.GetIPProperties())
                .Where(T => HardwareIds.HasUsableUnicastAddress(T.UnicastAddresses.Select(A => A.Address)))
                .ToList();

            var WithServers = Configured.Count(T => T.GatewayAddresses.Count > 0 || T.DnsAddresses.Count > 0 || T.DhcpServerAddresses.Count > 0);

            Assert.InRange(Hwid.Routers.Count, WithServers, Configured.Count);
            Assert.Equal(Enumerable.Range(0, Hwid.Routers.Count), Hwid.Routers.Select(T => T.Id));
        }

        [Fact]
        public async Task LocalNetworkScan_NeverReturnsEmptyRouterEntries()
        {
            var Hwid = await ScanLocalNetworkAsync();

            Assert.All(Hwid.Routers, Router => Assert.True(Router.Gateways.Count + Router.DnsServers.Count + Router.DhcpServers.Count + Router.NetworkDevices.Count > 0, $"Router {Router.Id} is empty."));
        }

        [Fact]
        public async Task LocalNetworkScan_ReturnsWellFormedEntries()
        {
            var Hwid = await ScanLocalNetworkAsync();

            Assert.All(Hwid.Routers, Router =>
            {
                Assert.All(Router.Gateways, Gateway => Assert.NotNull(Gateway.Address));
                Assert.All(Router.DnsServers, Server => Assert.True(IPAddress.TryParse(Server, out _)));
                Assert.All(Router.DhcpServers, Server => Assert.True(IPAddress.TryParse(Server, out _)));
                Assert.All(Router.NetworkDevices, Device =>
                {
                    Assert.NotNull(Device.Address);
                    Assert.Matches(MacPattern, Device.MacAddress);
                });
                Assert.All(Router.Gateways.Where(T => T.MacAddress != null), Gateway => Assert.Matches(MacPattern, Gateway.MacAddress));

                //
                // A device is listed once, and never under its gateway's address.
                //

                var Addresses = Router.NetworkDevices.Select(T => T.Address!).ToList();
                Assert.Equal(Addresses.Count, Addresses.Distinct().Count());
                Assert.DoesNotContain(Router.NetworkDevices, T => Router.Gateways.Any(G => G.Address!.Equals(T.Address)));

                //
                // Off-link destinations answered by proxy ARP carry the gateway's MAC; they must not be reported as devices.
                //

                var GatewayMacs = Router.Gateways.Where(T => T.MacAddress != null).Select(T => T.MacAddress!).ToList();
                Assert.DoesNotContain(Router.NetworkDevices, T => GatewayMacs.Contains(T.MacAddress!));
            });
        }

        [Fact]
        public async Task LocalNetworkScan_OnlyReportsDevicesInsideTheInterfaceSubnets()
        {
            var Hwid = await ScanLocalNetworkAsync();

            var Subnets = NetworkInterface.GetAllNetworkInterfaces()
                .Where(T => T.OperationalStatus == OperationalStatus.Up)
                .SelectMany(T => T.GetIPProperties().UnicastAddresses)
                .Where(T => T.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(T => (T.Address, T.IPv4Mask))
                .ToList();

            Assert.All(Hwid.Routers.SelectMany(T => T.NetworkDevices), Device => Assert.Contains(Subnets, T => HardwareIds.IsInSubnet(Device.Address!, T.Address, T.IPv4Mask)));
        }

        [Fact]
        public async Task LocalNetworkScan_ResolvesTheGatewayMacAddress()
        {
            var Hwid = await ScanLocalNetworkAsync();
            var Gateways = Hwid.Routers.SelectMany(T => T.Gateways).ToList();

            Assert.SkipWhen(Gateways.Count == 0, "No IPv4 gateway on this machine.");
            Assert.Contains(Gateways, T => T.MacAddress != null);
        }

        [Fact]
        public async Task LocalNetworkScan_IncludesTheNeighbourCache()
        {
            var Hwid = await ScanLocalNetworkAsync();
            var Neighbors = IpHlpApi.GetNeighbors();

            Assert.SkipWhen(Neighbors.Count == 0, "The neighbour cache is empty.");

            var Reported = Hwid.Routers.SelectMany(T => T.Gateways.Concat(T.NetworkDevices)).Select(T => T.Address!).ToList();
            Assert.Contains(Neighbors, T => Reported.Contains(T.Address));
        }

        [Fact]
        public async Task LocalNetworkScan_CompletesWithinTheConfiguredWait()
        {
            //
            // Probing must not block on silent hosts: a whole subnet is swept within the configured wait plus a small overhead.
            //

            using var Cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestToken);
            Cancellation.CancelAfter(TimeSpan.FromSeconds(30));

            var Timer = Stopwatch.StartNew();
            var Hwid = await HardwareIds.GetHwidAsync(new HardwareIdsConfig { ScanLocalNetworkDevices = true, DurationOfLocalNetworkScan = TimeSpan.FromMilliseconds(500) }, Cancellation.Token);
            Timer.Stop();

            var Subnets = NetworkInterface.GetAllNetworkInterfaces()
                .Where(T => T.OperationalStatus == OperationalStatus.Up && T.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(T => T.GetIPProperties().UnicastAddresses)
                .Count(T => T.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && HardwareIds.GetProbeAddresses(T.Address, T.IPv4Mask, HardwareIds.NetworkProbeCount).Any());

            Assert.NotNull(Hwid);
            Assert.True(Timer.Elapsed < TimeSpan.FromSeconds(3 + Subnets), $"The scan of {Subnets} subnet(s) took {Timer.ElapsedMilliseconds} ms.");
        }

        [Fact]
        public void Config_LocalNetworkScanWaitDefaultsToOneSecond()
        {
            Assert.Null(new HardwareIdsConfig().DurationOfLocalNetworkScan);
            Assert.Equal(TimeSpan.FromSeconds(1), HardwareIds.DefaultNetworkProbeWait);
            Assert.Equal(254, HardwareIds.NetworkProbeCount);
        }

        [Fact]
        public async Task LocalNetworkScan_HonoursCancellation()
        {
            using var Cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestToken);
            Cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));

            var Timer = Stopwatch.StartNew();
            var Hwid = await HardwareIds.GetHwidAsync(new HardwareIdsConfig { ScanLocalNetworkDevices = true }, Cancellation.Token);
            Timer.Stop();

            Assert.NotNull(Hwid);
            Assert.True(Timer.Elapsed < TimeSpan.FromSeconds(10), $"A cancelled scan took {Timer.ElapsedMilliseconds} ms.");
        }
    }
}
