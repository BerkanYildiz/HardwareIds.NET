namespace HardwareIds.NET.Testing
{
    using System.Text.Json;

    internal static class Program
    {
        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        /// <param name="InLaunchArgs">The launch arguments.</param>
        private static async Task Main(string[] InLaunchArgs)
        {
            //
            // --json [--indented] [--lan]: print a scan as JSON, e.g. to compare it with the C++ library's snapshot.
            //

            if (InLaunchArgs.Contains("--json"))
            {
                var Config = new HardwareIdsConfig { ScanLocalNetworkDevices = InLaunchArgs.Contains("--lan") };
                var Options = new JsonSerializerOptions { WriteIndented = InLaunchArgs.Contains("--indented") };
                Console.Out.Write(JsonSerializer.Serialize(HardwareIds.GetHwid(Config), Options));
                return;
            }

            using var CancellationTokenSource = new CancellationTokenSource();
            CancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(10));
            var Hwid = await HardwareIds.GetHwidAsync(new HardwareIdsConfig { ScanLocalNetworkDevices = true, ScanNeighborEndpoints = true, DurationOfNetworkScan = TimeSpan.FromSeconds(5) }, CancellationTokenSource.Token);
            Console.WriteLine($"HWID->DISKS:");

            foreach (var Disk in Hwid.Disks)
            {
                Console.WriteLine($"  DISK->ID:       {Disk.Id}");
                Console.WriteLine($"  DISK->NAME:     {Disk.Model}");
                Console.WriteLine($"  DISK->SN:       {Disk.SerialNumber}");
                Console.WriteLine();
            }

            Console.WriteLine($"HWID->MEMORY:");

            foreach (var MemoryStick in Hwid.MemorySticks)
            {
                Console.WriteLine($"  MEMORY->ID:     {MemoryStick.Id}");
                Console.WriteLine($"  MEMORY->VENDOR: {MemoryStick.Manufacturer}");
                Console.WriteLine($"  MEMORY->SN:     {MemoryStick.SerialNumber}");
                Console.WriteLine();
            }

            Console.WriteLine($"HWID->NICS:");

            foreach (var NetworkAdapter in Hwid.NetworkAdapters)
            {
                Console.WriteLine($"  NIC->ID:        {NetworkAdapter.Id}");
                Console.WriteLine($"  NIC->NAME:      {NetworkAdapter.Name}");
                Console.WriteLine($"  NIC->GUID:      {NetworkAdapter.InterfaceGuid}");
                Console.WriteLine($"  NIC->MAC:       {NetworkAdapter.Address.Current}");
                Console.WriteLine($"                  {NetworkAdapter.Address.Permanent}");
                Console.WriteLine();
            }

            Console.WriteLine($"HWID->WIFIS:");

            foreach (var Wifi in Hwid.Wifis.Where(T => !string.IsNullOrEmpty(T.Ssid)).OrderByDescending(T => T.Strength))
            {
                Console.WriteLine($"  WIFI->ID:        {Wifi.Id}");
                Console.WriteLine($"  WIFI->SSID:      {Wifi.Ssid}");
                Console.WriteLine($"  WIFI->BSSID:     {Wifi.Bssid}");
                Console.WriteLine();
            }

            await File.WriteAllTextAsync("hwid.json", JsonSerializer.Serialize(Hwid, new JsonSerializerOptions { WriteIndented = true }));

            if (!Console.IsInputRedirected)
                Console.ReadKey(true);
        }
    }
}