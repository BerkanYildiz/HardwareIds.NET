namespace HardwareIds.NET
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using ManagedNativeWifi;

    public static partial class HardwareIds
    {
        private static async Task ScanNetworkEndpointsAsync(Hwid InHwid, TimeSpan? InTimeout = null, CancellationToken InCancellationToken = default)
        {
            // 
            // Scan for the available WI-FI endpoints around this computer.
            // 

            var ScanTimeout = InTimeout.GetValueOrDefault(TimeSpan.FromSeconds(7));
            var NeighborEndpoints = (IEnumerable<Guid>?) null;

            using (var WifiScanCancellationSource = new CancellationTokenSource(ScanTimeout))
            using (var LinkedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(InCancellationToken, WifiScanCancellationSource.Token))
            {
                try { NeighborEndpoints = await NativeWifi.ScanNetworksAsync(ScanTimeout, LinkedCancellationSource.Token).ConfigureAwait(false); }
                catch { }
            }

            // 
            // Retrieve every available WI-FI endpoints that have been previously scanned.
            // 

            if (NeighborEndpoints is null)
                return;

            var AvailableEndpoints = (IEnumerable<BssNetworkPack>?) null;
            try { AvailableEndpoints = NativeWifi.EnumerateBssNetworks(); }
            catch { }

            if (AvailableEndpoints is null)
                return;

            // 
            // For each available WIFI endpoint...
            // 

            foreach (var Wifi in AvailableEndpoints)
            {
                InHwid.Wifis.Add(new HwWifi
                {
                    Id = InHwid.Wifis.Count,
                    Ssid = Wifi.Ssid.ToString(),
                    Bssid = FormatMacAddress(Wifi.Bssid.ToBytes()),
                    Strength = Wifi.Rssi,
                    Channel = Wifi.Channel,
                    Frequency = Wifi.Frequency,
                    Band = Wifi.Band,
                    Quality = Wifi.LinkQuality,
                });
            }
        }
    }
}
