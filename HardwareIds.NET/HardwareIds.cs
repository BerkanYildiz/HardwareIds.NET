namespace HardwareIds.NET
{
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    using global::HardwareIds.NET.Structures;

    public static partial class HardwareIds
    {
        /// <summary>
        /// Gets the current hardware information of this (local) computer.
        /// </summary>
        /// <param name="InConfig">The configuration.</param>
        /// <param name="InCancellationToken">The cancellation token.</param>
    #if NET
        public static async ValueTask<Hwid> GetHwidAsync(HardwareIdsConfig InConfig = null, CancellationToken InCancellationToken = default)
    #else
        public static async Task<Hwid> GetHwidAsync(HardwareIdsConfig InConfig = null, CancellationToken InCancellationToken = default)
    #endif
        {
            var Hwid = new Hwid();
            var HwidTasks = new List<Task>();
            InConfig = InConfig ?? new HardwareIdsConfig();

            // 
            // Retrieve the WI-FI endpoints currently available around the computer.
            // 

            if (!InCancellationToken.IsCancellationRequested && InConfig.ScanNeighborEndpoints.GetValueOrDefault(false))
                HwidTasks.Add(ScanNetworkEndpointsAsync(Hwid, InConfig.DurationOfNetworkScan, InCancellationToken));

            // 
            // Retrieve information about the routers.
            // 

            if (!InCancellationToken.IsCancellationRequested && InConfig.ScanLocalNetworkDevices.GetValueOrDefault(false))
                HwidTasks.Add(Task.Run(() => ScanNetworkDevices(Hwid, InCancellationToken), InCancellationToken));

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
            // Retrieve the baseboard(s) installed on this computer.
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveBaseBoards(Hwid);

            // 
            // Retrieve the motherboard(s) installed on this computer.
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveMotherBoards(Hwid);

            // 
            // Retrieve the BIOS firmwares installed on this computer's motherboard(s).
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveFirmwares(Hwid);

            // 
            // Retrieve the SMBIOS Table(s) configured on this computer's motherboard's bios(es).
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveSmbiosTables(Hwid);

            // 
            // Retrieve the processor(s) installed on this computer's motherboard(s).
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveProcessors(Hwid);

            // 
            // Retrieve the memory sticks installed on this computer's motherboard(s).
            // 

            if (!InCancellationToken.IsCancellationRequested)
                RetrieveMemorySticks(Hwid);

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
        public static Hwid GetHwid(HardwareIdsConfig InConfig = null, CancellationToken InCancellationToken = default)
        {
            return GetHwidAsync(InConfig, InCancellationToken).GetAwaiter().GetResult();
        }

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(uint DestinationIp, uint SourceIp, byte[] MacAddress, ref int MacAddressLength);
    }
}