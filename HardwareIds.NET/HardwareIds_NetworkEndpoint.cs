namespace HardwareIds.NET
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    public static partial class HardwareIds
    {
        internal static async Task ScanNetworkEndpointsAsync(Hwid InHwid, TimeSpan? InTimeout = null, CancellationToken InCancellationToken = default)
        {
            try
            {
                using var Session = WlanSession.Open();

                if (Session is null)
                    return;

                var Interfaces = Session.EnumerateInterfaces();

                if (Interfaces.Count == 0)
                    return;

                // 
                // Ask every wireless interface to scan for the WI-FI endpoints around this computer, and wait for them to finish.
                // 

                try
                {
                    await Session.ScanAsync(Interfaces, InTimeout.GetValueOrDefault(TimeSpan.FromSeconds(7)), InCancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                // 
                // Retrieve every WI-FI endpoint the interfaces have seen.
                // 

                foreach (var Interface in Interfaces)
                {
                    foreach (var Network in Session.GetNetworks(Interface))
                    {
                        InHwid.Wifis.Add(new HwWifi
                        {
                            Id = InHwid.Wifis.Count,
                            Ssid = WlanSession.DecodeSsid(Network.Ssid),
                            Bssid = FormatMacAddress(Network.Bssid),
                            Strength = Network.Rssi,
                            Channel = WlanApi.GetChannel(Network.FrequencyKHz),
                            Frequency = (int) Network.FrequencyKHz,
                            Band = WlanApi.GetBand(Network.FrequencyKHz),
                            Quality = (int) Network.LinkQuality,
                        });
                    }
                }
            }
            catch (Exception)
            {
                // ...
            }
        }
    }
}
