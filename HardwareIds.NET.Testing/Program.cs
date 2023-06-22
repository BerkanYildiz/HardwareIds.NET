namespace HardwareIds.NET.Testing
{
    internal static class Program
    {
        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        /// <param name="InLaunchArgs">The launch arguments.</param>
        private static async Task Main(string[] InLaunchArgs)
        {
            var Hwid = await HardwareIds.GetHwidAsync(new HardwareIdsConfig { ScanLocalNetworkDevices = false, ScanNeighborEndpoints = true, DurationOfNetworkScan = TimeSpan.FromSeconds(5) });
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

            Console.ReadKey(true);
        }
    }
}