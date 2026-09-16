namespace HardwareIds.NET.Native
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    [StructLayout(LayoutKind.Sequential)]
    internal struct WLAN_NOTIFICATION_DATA
    {
        public uint NotificationSource;
        public uint NotificationCode;
        public Guid InterfaceGuid;
        public uint dwDataSize;
        public IntPtr pData;
    }

    /// <summary>
    /// A wireless network (BSS) seen by a Wi-Fi interface.
    /// </summary>
    internal sealed class WlanBssEntry
    {
        public byte[] Ssid { get; set; } = [];
        public byte[] Bssid { get; set; } = [];
        public int Rssi { get; set; }
        public uint LinkQuality { get; set; }
        public uint FrequencyKHz { get; set; }
    }

    internal static class WlanApi
    {
        public const uint WLAN_API_VERSION_2_0 = 2;
        public const uint WLAN_NOTIFICATION_SOURCE_NONE = 0x00000000;
        public const uint WLAN_NOTIFICATION_SOURCE_ACM = 0x00000008;
        public const uint wlan_notification_acm_scan_complete = 7;
        public const uint wlan_notification_acm_scan_fail = 8;
        public const int dot11_BSS_type_any = 3;
        public const int WLAN_INTERFACE_INFO_SIZE = 532;
        public const int WLAN_BSS_ENTRY_SIZE = 360;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate void WLAN_NOTIFICATION_CALLBACK(ref WLAN_NOTIFICATION_DATA Data, IntPtr Context);

        [DllImport("wlanapi.dll")]
        public static extern uint WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

        [DllImport("wlanapi.dll")]
        public static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        public static extern uint WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

        [DllImport("wlanapi.dll")]
        public static extern void WlanFreeMemory(IntPtr pMemory);

        [DllImport("wlanapi.dll")]
        public static extern uint WlanScan(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pDot11Ssid, IntPtr pIeData, IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        public static extern uint WlanRegisterNotification(IntPtr hClientHandle, uint dwNotifSource, bool bIgnoreDuplicate, WLAN_NOTIFICATION_CALLBACK? funcCallback, IntPtr pCallbackContext, IntPtr pReserved, out uint pdwPrevNotifSource);

        [DllImport("wlanapi.dll")]
        public static extern uint WlanGetNetworkBssList(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pDot11Ssid, int dot11BssType, bool bSecurityEnabled, IntPtr pReserved, out IntPtr ppWlanBssList);

        /// <summary>
        /// Converts a channel center frequency (in kHz) into the Wi-Fi channel number, or 0 when unknown.
        /// </summary>
        /// <param name="InFrequencyKHz">The channel center frequency in kHz.</param>
        public static int GetChannel(uint InFrequencyKHz)
        {
            var MHz = InFrequencyKHz / 1000;

            if (MHz == 2484)
                return 14;

            if (MHz >= 2412 && MHz <= 2472)
                return (int) (MHz - 2407) / 5;

            if (MHz >= 5000 && MHz <= 5925)
                return (int) (MHz - 5000) / 5;

            if (MHz > 5925 && MHz <= 7125)
                return (int) (MHz - 5950) / 5;

            return 0;
        }

        /// <summary>
        /// Converts a channel center frequency (in kHz) into the Wi-Fi band in GHz (2.4, 5 or 6), or 0 when unknown.
        /// </summary>
        /// <param name="InFrequencyKHz">The channel center frequency in kHz.</param>
        public static float GetBand(uint InFrequencyKHz)
        {
            var MHz = InFrequencyKHz / 1000;

            if (MHz >= 2412 && MHz <= 2484)
                return 2.4f;

            if (MHz >= 5000 && MHz <= 5925)
                return 5f;

            if (MHz > 5925 && MHz <= 7125)
                return 6f;

            return 0f;
        }
    }

    /// <summary>
    /// A session with the WLAN service, used to scan for and list the wireless networks around this computer.
    /// </summary>
    internal sealed class WlanSession : IDisposable
    {
        private IntPtr Handle;

        private WlanSession(IntPtr InHandle)
        {
            this.Handle = InHandle;
        }

        /// <summary>
        /// Opens a session, or returns null when the WLAN service is not available.
        /// </summary>
        public static WlanSession? Open()
        {
            try
            {
                if (WlanApi.WlanOpenHandle(WlanApi.WLAN_API_VERSION_2_0, IntPtr.Zero, out _, out var Handle) != 0 || Handle == IntPtr.Zero)
                    return null;

                return new WlanSession(Handle);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the GUIDs of the wireless interfaces of this computer.
        /// </summary>
        public List<Guid> EnumerateInterfaces()
        {
            var Result = new List<Guid>();

            if (WlanApi.WlanEnumInterfaces(this.Handle, IntPtr.Zero, out var List) != 0 || List == IntPtr.Zero)
                return Result;

            try
            {
                var Count = Marshal.ReadInt32(List, 0);

                for (var I = 0; I < Count; I++)
                {
                    var Entry = new byte[16];
                    Marshal.Copy(List + 8 + I * WlanApi.WLAN_INTERFACE_INFO_SIZE, Entry, 0, 16);
                    Result.Add(new Guid(Entry));
                }
            }
            finally
            {
                WlanApi.WlanFreeMemory(List);
            }

            return Result;
        }

        /// <summary>
        /// Requests a scan on the given interfaces and waits until every scan completed (or failed), the timeout elapsed, or the token was cancelled.
        /// </summary>
        /// <param name="InInterfaces">The interfaces to scan with.</param>
        /// <param name="InTimeout">The maximum time to wait for the scans to complete.</param>
        /// <param name="InCancellationToken">The cancellation token.</param>
        /// <returns>The interfaces whose scan completed.</returns>
        public async Task<List<Guid>> ScanAsync(IEnumerable<Guid> InInterfaces, TimeSpan InTimeout, CancellationToken InCancellationToken = default)
        {
            var Pending = new ConcurrentDictionary<Guid, byte>();
            var Completed = new ConcurrentDictionary<Guid, byte>();
            var AllCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            //
            // The delegate must stay referenced for as long as it is registered, or the GC could collect it under the callback.
            //

            WlanApi.WLAN_NOTIFICATION_CALLBACK Callback = (ref WLAN_NOTIFICATION_DATA InData, IntPtr InContext) =>
            {
                if (InData.NotificationSource != WlanApi.WLAN_NOTIFICATION_SOURCE_ACM)
                    return;

                if (InData.NotificationCode != WlanApi.wlan_notification_acm_scan_complete && InData.NotificationCode != WlanApi.wlan_notification_acm_scan_fail)
                    return;

                if (!Pending.TryRemove(InData.InterfaceGuid, out _))
                    return;

                if (InData.NotificationCode == WlanApi.wlan_notification_acm_scan_complete)
                    Completed[InData.InterfaceGuid] = 0;

                if (Pending.IsEmpty)
                    AllCompleted.TrySetResult(true);
            };

            if (WlanApi.WlanRegisterNotification(this.Handle, WlanApi.WLAN_NOTIFICATION_SOURCE_ACM, true, Callback, IntPtr.Zero, IntPtr.Zero, out _) != 0)
                return new List<Guid>();

            try
            {
                foreach (var Interface in InInterfaces)
                {
                    var InterfaceGuid = Interface;
                    Pending[InterfaceGuid] = 0;

                    if (WlanApi.WlanScan(this.Handle, ref InterfaceGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) != 0)
                        Pending.TryRemove(InterfaceGuid, out _);
                }

                if (!Pending.IsEmpty)
                {
                    using var Timeout = CancellationTokenSource.CreateLinkedTokenSource(InCancellationToken);
                    Timeout.CancelAfter(InTimeout);

                    var Delay = Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, Timeout.Token);
                    var Finished = await Task.WhenAny(AllCompleted.Task, Delay).ConfigureAwait(false);

                    if (Finished == Delay)
                        InCancellationToken.ThrowIfCancellationRequested();

                    if (Finished == AllCompleted.Task)
                        Timeout.Cancel();

                    try { await Delay.ConfigureAwait(false); }
                    catch (OperationCanceledException) { }
                }
            }
            finally
            {
                WlanApi.WlanRegisterNotification(this.Handle, WlanApi.WLAN_NOTIFICATION_SOURCE_NONE, true, null, IntPtr.Zero, IntPtr.Zero, out _);
                GC.KeepAlive(Callback);
            }

            return new List<Guid>(Completed.Keys);
        }

        /// <summary>
        /// Gets the wireless networks currently known to the given interface (the result of its last scan).
        /// </summary>
        /// <param name="InInterface">The interface GUID.</param>
        public List<WlanBssEntry> GetNetworks(Guid InInterface)
        {
            var Result = new List<WlanBssEntry>();

            if (WlanApi.WlanGetNetworkBssList(this.Handle, ref InInterface, IntPtr.Zero, WlanApi.dot11_BSS_type_any, false, IntPtr.Zero, out var List) != 0 || List == IntPtr.Zero)
                return Result;

            try
            {
                var Count = Marshal.ReadInt32(List, 4);

                for (var I = 0; I < Count; I++)
                {
                    var Entry = List + 8 + I * WlanApi.WLAN_BSS_ENTRY_SIZE;
                    var SsidLength = Math.Min(Marshal.ReadInt32(Entry, 0), 32);
                    var Ssid = new byte[Math.Max(SsidLength, 0)];
                    var Bssid = new byte[6];

                    Marshal.Copy(Entry + 4, Ssid, 0, Ssid.Length);
                    Marshal.Copy(Entry + 40, Bssid, 0, 6);

                    Result.Add(new WlanBssEntry
                    {
                        Ssid = Ssid,
                        Bssid = Bssid,
                        Rssi = Marshal.ReadInt32(Entry, 56),
                        LinkQuality = (uint) Marshal.ReadInt32(Entry, 60),
                        FrequencyKHz = (uint) Marshal.ReadInt32(Entry, 92),
                    });
                }
            }
            finally
            {
                WlanApi.WlanFreeMemory(List);
            }

            return Result;
        }

        /// <summary>
        /// Decodes a raw SSID the same way Windows displays it.
        /// </summary>
        /// <param name="InSsid">The raw SSID bytes.</param>
        public static string DecodeSsid(byte[] InSsid)
        {
            return Encoding.UTF8.GetString(InSsid);
        }

        public void Dispose()
        {
            if (this.Handle == IntPtr.Zero)
                return;

            WlanApi.WlanCloseHandle(this.Handle, IntPtr.Zero);
            this.Handle = IntPtr.Zero;
        }
    }
}
