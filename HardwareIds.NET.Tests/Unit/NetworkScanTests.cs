namespace HardwareIds.NET.Tests.Unit
{
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;

    using global::HardwareIds.NET.Native;

    using Xunit;

    public class NetworkScanTests
    {
        [Theory]
        [InlineData("192.168.1.37", "255.255.255.0", 20, "192.168.1.1", "192.168.1.20", 20)]
        [InlineData("192.168.1.254", "255.255.255.0", 20, "192.168.1.1", "192.168.1.20", 20)]
        [InlineData("172.16.5.9", "255.255.0.0", 3, "172.16.0.1", "172.16.0.3", 3)]
        [InlineData("10.0.0.5", "255.255.255.252", 20, "10.0.0.5", "10.0.0.6", 2)]
        [InlineData("10.0.0.1", "255.255.255.248", 20, "10.0.0.1", "10.0.0.6", 6)]
        public void GetProbeAddresses_ReturnsTheFirstHostsOfTheSubnet(string InAddress, string InMask, int InCount, string InFirst, string InLast, int InExpectedCount)
        {
            var Addresses = HardwareIds.GetProbeAddresses(IPAddress.Parse(InAddress), IPAddress.Parse(InMask), InCount).ToList();

            Assert.Equal(InExpectedCount, Addresses.Count);
            Assert.Equal(IPAddress.Parse(InFirst), Addresses.First());
            Assert.Equal(IPAddress.Parse(InLast), Addresses.Last());
            Assert.Equal(Addresses.Count, Addresses.Distinct().Count());
        }

        [Theory]
        [InlineData("62.210.1.5", "255.255.255.255")]
        [InlineData("10.0.0.5", "255.255.255.254")]
        [InlineData("169.254.10.20", "255.255.0.0")]
        public void GetProbeAddresses_ReturnsNothingForSubnetsWithoutHostsOrLinkLocalSubnets(string InAddress, string InMask)
        {
            Assert.Empty(HardwareIds.GetProbeAddresses(IPAddress.Parse(InAddress), IPAddress.Parse(InMask), 20));
        }

        [Theory]
        [InlineData("192.168.1.20", "192.168.1.37", "255.255.255.0", true)]
        [InlineData("192.168.2.20", "192.168.1.37", "255.255.255.0", false)]
        [InlineData("10.200.3.4", "10.1.2.3", "255.0.0.0", true)]
        [InlineData("62.210.1.1", "212.83.1.1", "255.255.255.255", false)]
        [InlineData("212.83.1.1", "212.83.1.1", "255.255.255.255", true)]
        [InlineData("2001:db8::1", "192.168.1.1", "255.255.255.0", false)]
        public void IsInSubnet_ComparesTheNetworkPart(string InAddress, string InSubnetAddress, string InMask, bool InExpected)
        {
            Assert.Equal(InExpected, HardwareIds.IsInSubnet(IPAddress.Parse(InAddress), IPAddress.Parse(InSubnetAddress), IPAddress.Parse(InMask)));
        }

        [Fact]
        public void GetProbeAddresses_IgnoresIpv6()
        {
            Assert.Empty(HardwareIds.GetProbeAddresses(IPAddress.Parse("2001:db8::1"), IPAddress.Parse("255.255.255.0"), 20));
            Assert.Empty(HardwareIds.GetProbeAddresses(IPAddress.Parse("10.0.0.1"), IPAddress.IPv6Any, 20));
        }

        [Fact]
        public void GetProbeAddresses_NeverExceedsTheRequestedCount()
        {
            Assert.Equal(HardwareIds.NetworkProbeCount, HardwareIds.GetProbeAddresses(IPAddress.Parse("10.1.2.3"), IPAddress.Parse("255.0.0.0"), HardwareIds.NetworkProbeCount).Count());
            Assert.Empty(HardwareIds.GetProbeAddresses(IPAddress.Parse("10.1.2.3"), IPAddress.Parse("255.0.0.0"), 0));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(false, "fe80::1")]
        [InlineData(false, "fe80::1", "fe80::2")]
        [InlineData(true, "169.254.10.20")]
        [InlineData(true, "10.0.0.1")]
        [InlineData(true, "fe80::1", "2001:db8::1")]
        [InlineData(true, "fe80::1", "192.168.0.2")]
        public void HasUsableUnicastAddress_RequiresAnIpv4OrGlobalIpv6Address(bool InExpected, params string[] InAddresses)
        {
            Assert.Equal(InExpected, HardwareIds.HasUsableUnicastAddress(InAddresses.Select(IPAddress.Parse)));
        }

        [Theory]
        [InlineData(new byte[] { 10, 0, 0, 1 }, new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 }, IpHlpApi.NlnsReachable, true)]
        [InlineData(new byte[] { 10, 0, 0, 1 }, new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 }, IpHlpApi.NlnsStale, true)]
        [InlineData(new byte[] { 10, 0, 0, 1 }, new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 }, IpHlpApi.NlnsPermanent, true)]
        [InlineData(new byte[] { 10, 0, 0, 1 }, new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 }, IpHlpApi.NlnsIncomplete, false)]
        [InlineData(new byte[] { 10, 0, 0, 1 }, new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 }, IpHlpApi.NlnsUnreachable, false)]
        [InlineData(new byte[] { 224, 0, 0, 251 }, new byte[] { 0x01, 0x00, 0x5E, 0x00, 0x00, 0xFB }, IpHlpApi.NlnsPermanent, false)]
        [InlineData(new byte[] { 255, 255, 255, 255 }, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, IpHlpApi.NlnsPermanent, false)]
        [InlineData(new byte[] { 10, 0, 0, 1 }, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, IpHlpApi.NlnsReachable, false)]
        [InlineData(new byte[] { 10, 0, 0, 1 }, new byte[] { 0x00, 0x11 }, IpHlpApi.NlnsReachable, false)]
        [InlineData(new byte[] { 0, 0, 0, 0 }, new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 }, IpHlpApi.NlnsReachable, false)]
        public void IsReportableNeighbor_KeepsResolvedUnicastEntriesOnly(byte[] InAddress, byte[] InPhysicalAddress, uint InState, bool InExpected)
        {
            Assert.Equal(InExpected, IpHlpApi.IsReportableNeighbor(InAddress, InPhysicalAddress, InState));
        }

        [Fact]
        public void GetNeighbors_ReturnsWellFormedEntriesForKnownInterfaces()
        {
            var Neighbors = IpHlpApi.GetNeighbors();
            var InterfaceIndexes = NetworkInterface.GetAllNetworkInterfaces().Select(T =>
            {
                try { return T.GetIPProperties().GetIPv4Properties()?.Index ?? -1; }
                catch { return -1; }
            }).ToList();

            Assert.All(Neighbors, Neighbor =>
            {
                Assert.Equal(4, Neighbor.Address.GetAddressBytes().Length);
                Assert.Equal(6, Neighbor.PhysicalAddress.Length);
                Assert.Matches("^([0-9A-F]{2}:){5}[0-9A-F]{2}$", HardwareIds.FormatMacAddress(Neighbor.PhysicalAddress));
                Assert.Contains((int) Neighbor.InterfaceIndex, InterfaceIndexes);
            });
        }

        [Fact]
        public void ResolveMacAddress_ReturnsNullForAddressesThatCannotBeResolved()
        {
            Assert.Null(HardwareIds.ResolveMacAddress(IPAddress.Loopback));
            Assert.Null(HardwareIds.ResolveMacAddress(IPAddress.Broadcast));
            Assert.Null(HardwareIds.ResolveMacAddress(IPAddress.Any));
        }
    }
}
