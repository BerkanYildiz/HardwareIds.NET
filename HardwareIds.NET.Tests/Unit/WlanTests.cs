namespace HardwareIds.NET.Tests.Unit
{
    using System;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    using global::HardwareIds.NET.Native;

    using Xunit;

    public class WlanTests
    {
        [Theory]
        [InlineData(2412000u, 1)]
        [InlineData(2437000u, 6)]
        [InlineData(2472000u, 13)]
        [InlineData(2484000u, 14)]
        [InlineData(5180000u, 36)]
        [InlineData(5500000u, 100)]
        [InlineData(5745000u, 149)]
        [InlineData(5955000u, 1)]
        [InlineData(6115000u, 33)]
        [InlineData(0u, 0)]
        [InlineData(900000u, 0)]
        public void GetChannel_MapsCenterFrequencyToChannelNumber(uint InFrequencyKHz, int InExpected)
        {
            Assert.Equal(InExpected, WlanApi.GetChannel(InFrequencyKHz));
        }

        [Theory]
        [InlineData(2412000u, 2.4f)]
        [InlineData(2484000u, 2.4f)]
        [InlineData(5180000u, 5f)]
        [InlineData(5825000u, 5f)]
        [InlineData(5955000u, 6f)]
        [InlineData(7115000u, 6f)]
        [InlineData(0u, 0f)]
        public void GetBand_MapsCenterFrequencyToBand(uint InFrequencyKHz, float InExpected)
        {
            Assert.Equal(InExpected, WlanApi.GetBand(InFrequencyKHz));
        }

        [Fact]
        public void NotificationData_HasTheNativeLayout()
        {
            Assert.Equal(IntPtr.Size == 8 ? 40 : 32, Marshal.SizeOf(typeof(WLAN_NOTIFICATION_DATA)));
        }

        [Fact]
        public void DecodeSsid_UsesUtf8()
        {
            Assert.Equal("Café WiFi", WlanSession.DecodeSsid([0x43, 0x61, 0x66, 0xC3, 0xA9, 0x20, 0x57, 0x69, 0x46, 0x69]));
            Assert.Equal(string.Empty, WlanSession.DecodeSsid([]));
        }

        [Fact]
        public void Session_OpensAndEnumeratesInterfacesWithoutThrowing()
        {
            using var Session = WlanSession.Open();
            Assert.SkipWhen(Session is null, "The WLAN service is not available on this machine.");

            var Interfaces = Session.EnumerateInterfaces();

            Assert.Equal(Interfaces.Count, Interfaces.Distinct().Count());
            Assert.All(Interfaces, T => Assert.NotEqual(Guid.Empty, T));
        }

        [Fact]
        public async Task Session_ScanAndListNetworks_ReturnWellFormedEntries()
        {
            using var Session = WlanSession.Open();
            Assert.SkipWhen(Session is null, "The WLAN service is not available on this machine.");

            var Interfaces = Session.EnumerateInterfaces();
            Assert.SkipWhen(Interfaces.Count == 0, "No Wi-Fi interface on this machine.");

            var Completed = await Session.ScanAsync(Interfaces, TimeSpan.FromSeconds(7), TestContext.Current.CancellationToken);
            Assert.All(Completed, T => Assert.Contains(T, Interfaces));

            foreach (var Interface in Interfaces)
            {
                Assert.All(Session.GetNetworks(Interface), Network =>
                {
                    Assert.Equal(6, Network.Bssid.Length);
                    Assert.InRange(Network.Ssid.Length, 0, 32);
                    Assert.InRange(Network.Rssi, -120, 0);
                    Assert.InRange(Network.LinkQuality, 0u, 100u);
                    Assert.NotEqual(0f, WlanApi.GetBand(Network.FrequencyKHz));
                });
            }
        }

        [Fact]
        public async Task Session_ScanWithNoInterfaces_CompletesImmediately()
        {
            using var Session = WlanSession.Open();
            Assert.SkipWhen(Session is null, "The WLAN service is not available on this machine.");

            var Completed = await Session.ScanAsync([], TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.Empty(Completed);
        }

        [Fact]
        public void Session_GetNetworksForUnknownInterface_ReturnsNothing()
        {
            using var Session = WlanSession.Open();
            Assert.SkipWhen(Session is null, "The WLAN service is not available on this machine.");

            Assert.Empty(Session.GetNetworks(Guid.NewGuid()));
        }

        [Fact]
        public void Session_CanBeDisposedTwice()
        {
            var Session = WlanSession.Open();
            Assert.SkipWhen(Session is null, "The WLAN service is not available on this machine.");

            Session.Dispose();
            Session.Dispose();
        }
    }
}
