namespace HardwareIds.NET
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    using global::HardwareIds.NET.Structures;

    public static partial class HardwareIds
    {
        /// <summary>
        /// Gets the current hardware information of this (local) computer.
        /// </summary>
        /// <param name="InConfig">The configuration.</param>
    #if NET6_0 || NET7_0
        public static async ValueTask<Hwid> GetHwidAsync(HardwareIdsConfig InConfig = null)
    #else
        public static async Task<Hwid> GetHwidAsync(HardwareIdsConfig InConfig = null)
    #endif
        {
            var Hwid = new Hwid();
            var HwidTasks = new List<Task>();
            InConfig = InConfig ?? new HardwareIdsConfig();

            // 
            // Retrieve the WIFI endpoints currently available around the computer.
            // 

            if (InConfig?.ScanNeighborEndpoints.GetValueOrDefault(true) ?? true)
                HwidTasks.Add(ScanNetworkEndpointsAsync(Hwid, InConfig?.DurationOfNetworkScan));

            // 
            // Retrieve information about the routers.
            // 

            if (InConfig?.ScanLocalNetworkDevices.GetValueOrDefault(false) ?? false)
                HwidTasks.Add(ScanNetworkDevicesAsync(Hwid));

            // 
            // Retrieve the WIFI endpoints this computer has connected to in the past.
            // 

            RetrieveNetworkSignatures(Hwid);

            // 
            // Retrieve the disk devices connected to this computer.
            // 

            RetrieveDiskDrives(Hwid);

            // 
            // Retrieve the volumes configured on each disks plugged into this computer.
            // 

            RetrieveDiskVolumes(Hwid);

            // 
            // Retrieve the network adapters installed (and enabled) on this computer.
            // 

            RetrieveNetworkAdapters(Hwid);

            // 
            // Retrieve the baseboard(s) installed on this computer.
            // 

            RetrieveBaseBoards(Hwid);

            // 
            // Retrieve the motherboard(s) installed on this computer.
            // 

            RetrieveMotherBoards(Hwid);

            // 
            // Retrieve the BIOS firmwares installed on this computer's motherboard(s).
            // 

            RetrieveFirmwares(Hwid);

            // 
            // Retrieve the SMBIOS Table(s) configured on this computer's motherboard's bios(es).
            // 

            RetrieveSmbiosTables(Hwid);

            // 
            // Retrieve the processor(s) installed on this computer's motherboard(s).
            // 

            RetrieveProcessors(Hwid);

            // 
            // Retrieve the memory sticks installed on this computer's motherboard(s).
            // 
            
            RetrieveMemorySticks(Hwid);

            // 
            // Retrieve the monitors plugged into this computer.
            // 
            
            RetrieveMonitors(Hwid);

            // 
            // Retrieve the video controllers plugged into this computer.
            // 

            RetrieveVideoControllers(Hwid);

            // 
            // Retrieve the printers ever connected to this computer.
            // 

            RetrievePrinters(Hwid);

            // 
            // Retrieve the users that have ever logged into this computer.
            // 

            RetrieveUserAccounts(Hwid);

            // 
            // Retrieve the operating systems configuration.
            // 

            RetrieveOperatingSystems(Hwid);

            // 
            // Wait for every running tasks to terminate.
            // 

            if (HwidTasks.Count > 0)
            {
                try
                {
                    await Task.WhenAll(HwidTasks).ConfigureAwait(false);
                }
                catch (Exception)
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
        public static Hwid GetHwid(HardwareIdsConfig InConfig = null)
        {
            return GetHwidAsync(InConfig).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(uint DestinationIp, uint SourceIp, byte[] MacAddress, ref int MacAddressLength);
    }
}