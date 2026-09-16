namespace HardwareIds.NET.Native
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct BLUETOOTH_FIND_RADIO_PARAMS
    {
        public uint dwSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct BLUETOOTH_RADIO_INFO
    {
        public uint dwSize;
        public ulong address;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)] public string szName;
        public uint ulClassofDevice;
        public ushort lmpSubversion;
        public ushort manufacturer;
    }

    internal sealed class BluetoothRadioInfo
    {
        public byte[] Address { get; set; } = [];
        public string? Name { get; set; }
        public uint ClassOfDevice { get; set; }
        public ushort LmpSubversion { get; set; }
        public ushort Manufacturer { get; set; }
    }

    internal static class Bluetooth
    {
        public const int BLUETOOTH_RADIO_INFO_SIZE = 520;

        [DllImport("bthprops.cpl", SetLastError = true)]
        private static extern IntPtr BluetoothFindFirstRadio(ref BLUETOOTH_FIND_RADIO_PARAMS pbtfrp, out IntPtr phRadio);

        [DllImport("bthprops.cpl", SetLastError = true)]
        private static extern bool BluetoothFindNextRadio(IntPtr hFind, out IntPtr phRadio);

        [DllImport("bthprops.cpl")]
        private static extern bool BluetoothFindRadioClose(IntPtr hFind);

        [DllImport("bthprops.cpl")]
        private static extern uint BluetoothGetRadioInfo(IntPtr hRadio, ref BLUETOOTH_RADIO_INFO pRadioInfo);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        /// <summary>
        /// Gets the Bluetooth radios of this computer; empty when the Bluetooth stack is not installed.
        /// </summary>
        public static List<BluetoothRadioInfo> GetRadios()
        {
            var Result = new List<BluetoothRadioInfo>();

            try
            {
                var Parameters = new BLUETOOTH_FIND_RADIO_PARAMS { dwSize = (uint) Marshal.SizeOf(typeof(BLUETOOTH_FIND_RADIO_PARAMS)) };
                var Find = BluetoothFindFirstRadio(ref Parameters, out var Radio);

                if (Find == IntPtr.Zero)
                    return Result;

                try
                {
                    do
                    {
                        try
                        {
                            var Info = new BLUETOOTH_RADIO_INFO { dwSize = (uint) Marshal.SizeOf(typeof(BLUETOOTH_RADIO_INFO)) };

                            if (BluetoothGetRadioInfo(Radio, ref Info) == 0)
                            {
                                Result.Add(new BluetoothRadioInfo
                                {
                                    Address = AddressToBytes(Info.address),
                                    Name = Info.szName,
                                    ClassOfDevice = Info.ulClassofDevice,
                                    LmpSubversion = Info.lmpSubversion,
                                    Manufacturer = Info.manufacturer,
                                });
                            }
                        }
                        finally
                        {
                            CloseHandle(Radio);
                        }
                    }
                    while (BluetoothFindNextRadio(Find, out Radio));
                }
                finally
                {
                    BluetoothFindRadioClose(Find);
                }
            }
            catch (DllNotFoundException)
            {
                // No Bluetooth stack on this computer.
            }
            catch (EntryPointNotFoundException)
            {
                // ...
            }

            return Result;
        }

        /// <summary>
        /// Converts a BLUETOOTH_ADDRESS (a 48-bit integer) into its bytes, most significant first, as it is usually displayed.
        /// </summary>
        /// <param name="InAddress">The address.</param>
        public static byte[] AddressToBytes(ulong InAddress)
        {
            return
            [
                (byte) (InAddress >> 40),
                (byte) (InAddress >> 32),
                (byte) (InAddress >> 24),
                (byte) (InAddress >> 16),
                (byte) (InAddress >> 8),
                (byte) InAddress,
            ];
        }
    }
}
