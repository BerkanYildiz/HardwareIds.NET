namespace HardwareIds.NET
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
            
            var NeighborEndpoints = (IEnumerable<Guid>) null;

            using (var WifiScanCancellationSource = new CancellationTokenSource(InTimeout.GetValueOrDefault(TimeSpan.FromSeconds(7))))
            {
                using (var LinkedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(InCancellationToken, WifiScanCancellationSource.Token))
                {
                    try { NeighborEndpoints = await NativeWifi.ScanNetworksAsync(InTimeout.GetValueOrDefault(TimeSpan.FromSeconds(7)), LinkedCancellationSource.Token); }
                    catch { }
                }
            }

            // 
            // Retrieve every available WI-FI endpoints that have been previously scanned.
            // 

            if (NeighborEndpoints != null)
            {
                var AvailableEndpoints = (IEnumerable<BssNetworkPack>) null;
                try { AvailableEndpoints = NativeWifi.EnumerateBssNetworks(); }
                catch { }

                // 
                // For each available WIFI endpoint...
                // 

                if (AvailableEndpoints != null)
                {
                    foreach (var Wifi in AvailableEndpoints)
                    {
                        InHwid.Wifis.Add(new HwWifi
                        {
                            Id = (int) InHwid.Wifis.Count,
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
        }
    }
}
