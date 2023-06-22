namespace HardwareIds.NET
{
    using System;

    public class HardwareIdsConfig
    {
        /// <summary>
        /// Gets or sets the duration of the network endpoints scan.
        /// </summary>
        public TimeSpan? DurationOfNetworkScan { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether it should execute and include the neighbor endpoints scan.
        /// </summary>
        public bool? ScanNeighborEndpoints { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether it should execute and include the local network devices scan.
        /// </summary>
        public bool? ScanLocalNetworkDevices { get; set; }
    }
}
