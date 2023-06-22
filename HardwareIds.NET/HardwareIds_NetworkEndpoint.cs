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
        private static async Task ScanNetworkEndpointsAsync(Hwid InHwid, TimeSpan? InTimeout = null)
        {
            // 
            // Scan for the available WIFI endpoints around this computer.
            // 

            var NeighborEndpoints = (IEnumerable<Guid>) null;

            try
            {
                using (var CancellationSource = new CancellationTokenSource(InTimeout.GetValueOrDefault(TimeSpan.FromSeconds(7))))
                {
                    NeighborEndpoints = await NativeWifi.ScanNetworksAsync(InTimeout.GetValueOrDefault(TimeSpan.FromSeconds(7)), CancellationSource.Token);
                }
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
