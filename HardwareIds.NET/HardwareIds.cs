namespace HardwareIds.NET
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;

    public static partial class HardwareIds
    {
        /// <summary>
        /// Gets the current hardware information of this (local) computer.
        /// </summary>
        /// <param name="InConfig">The configuration.</param>
        /// <param name="InCancellationToken">The cancellation token.</param>
    #if NET
        public static async ValueTask<Hwid> GetHwidAsync(HardwareIdsConfig? InConfig = null, CancellationToken InCancellationToken = default)
    #else
        public static async Task<Hwid> GetHwidAsync(HardwareIdsConfig? InConfig = null, CancellationToken InCancellationToken = default)
    #endif
        {
            var Hwid = new Hwid();
            var HwidTasks = new List<Task>();
            InConfig ??= new HardwareIdsConfig();

            // 
            // Read the SMBIOS table once; the baseboard, system, BIOS, processor and memory collectors are all built from it.
            // 

            SmbiosTable? Smbios = null;

            try
            {
                Smbios = SmbiosTable.Read();
            }
            catch
            {
                // ...
            }

            // 
            // Retrieve the WI-FI endpoints currently available around the computer.
            // 

            if (!InCancellationToken.IsCancellationRequested && InConfig.ScanNeighborEndpoints.GetValueOrDefault(false))
                HwidTasks.Add(ScanNetworkEndpointsAsync(Hwid, InConfig.DurationOfNetworkScan, InCancellationToken));

            // 
            // Retrieve information about the routers.
            // 

            if (!InCancellationToken.IsCancellationRequested && InConfig.ScanLocalNetworkDevices.GetValueOrDefault(false))
                HwidTasks.Add(Task.Run(() => ScanNetworkDevices(Hwid, InConfig.DurationOfLocalNetworkScan, InCancellationToken), InCancellationToken));

            // 
            // Retrieve the WI-FI endpoints this computer has connected to in the past.
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveNetworkSignatures(Hwid);

            // 
            // Retrieve the disk devices connected to this computer.
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveDiskDrives(Hwid);

            // 
            // Retrieve the volumes configured on each disk plugged into this computer.
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveDiskVolumes(Hwid);

            // 
            // Retrieve the network adapters installed (and enabled) on this computer.
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveNetworkAdapters(Hwid);

            // 
            // Retrieve the Bluetooth radios installed on this computer.
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveBluetoothRadios(Hwid);

            // 
            // Retrieve the baseboard(s) installed on this computer.
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveBaseBoards(Hwid, Smbios);

            // 
            // Retrieve the motherboard(s) installed on this computer.
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveMotherBoards(Hwid, Smbios);

            // 
            // Retrieve the chassis (enclosures) of this computer.
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveChassis(Hwid, Smbios);

            // 
            // Retrieve the BIOS firmwares installed on this computer's motherboard(s).
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveFirmwares(Hwid, Smbios);

            // 
            // Retrieve the SMBIOS Table(s) configured on this computer's motherboard's bios(es).
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveSmbiosTables(Hwid, Smbios);

            // 
            // Retrieve the processor(s) installed on this computer's motherboard(s).
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveProcessors(Hwid, Smbios);

            // 
            // Retrieve the memory sticks installed on this computer's motherboard(s).
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveMemorySticks(Hwid, Smbios);

            // 
            // Retrieve the batteries present in this computer.
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveBatteries(Hwid);

            // 
            // Retrieve the monitors plugged into this computer.
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveMonitors(Hwid);

            // 
            // Retrieve the video controllers plugged into this computer.
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveVideoControllers(Hwid);

            // 
            // Retrieve the printers ever connected to this computer.
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrievePrinters(Hwid);

            // 
            // Retrieve the users that have ever logged into this computer.
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveUserAccounts(Hwid);

            // 
            // Retrieve the operating systems' configuration.
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveOperatingSystems(Hwid);

            // 
            // Wait for every running tasks to terminate.
            // 

            if (HwidTasks.Count > 0)
            {
                try
                {
                    await Task.WhenAll(HwidTasks)
                              #if NET
                              .WaitAsync(cancellationToken: InCancellationToken)
                              #endif
                              .ConfigureAwait(false);
                }
                catch
                {
                    // ...
                }
            }

            return Hwid;
        }

        /// <summary>
        /// Gets the current hardware information of this (local) computer.
        /// </summary>
        /// <param name="InConfig">The configuration.</param>
        /// <param name="InCancellationToken">The cancellation token.</param>
        public static Hwid GetHwid(HardwareIdsConfig? InConfig = null, CancellationToken InCancellationToken = default)
        {
        #if NET
            return GetHwidAsync(InConfig, InCancellationToken).AsTask().GetAwaiter().GetResult();
        #else
            return GetHwidAsync(InConfig, InCancellationToken).GetAwaiter().GetResult();
        #endif
        }

        /// <summary>
        /// Formats the raw bytes of a MAC address as a colon-separated, upper-case hexadecimal string.
        /// </summary>
        /// <param name="InAddress">The raw bytes of the MAC address.</param>
        internal static string FormatMacAddress(IEnumerable<byte> InAddress)
        {
            return string.Join(":", InAddress.Select(T => T.ToString("X2")));
        }
    }
}
