namespace HardwareIds.NET
{
    using System;

    public class HardwareIdsConfig
    {
        /// <summary>
        /// Gets the default configuration for the hardware ids scanning.
        /// </summary>
        public static readonly HardwareIdsConfig Default = new HardwareIdsConfig();

        /// <summary>
        /// Gets or sets the duration of the network endpoints scan.
        /// </summary>
        public TimeSpan DurationOfNetworkScan
        {
            get;
            set;
        } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets a value indicating whether it should execute and include the neighbor endpoints scan.
        /// </summary>
        public bool ScanNeighborEndpoints
        {
            get;
            set;
        } = true;

        /// <summary>
        /// Gets or sets a value indicating whether it should execute and include the local network devices scan.
        /// </summary>
        public bool ScanLocalNetworkDevices
        {
            get;
            set;
        } = true;
    }
}
