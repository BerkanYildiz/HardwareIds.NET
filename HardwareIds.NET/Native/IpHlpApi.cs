namespace HardwareIds.NET.Native
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct MIB_IF_ROW2
    {
        public ulong InterfaceLuid;
        public uint InterfaceIndex;
        public Guid InterfaceGuid;
        public fixed char Alias[257];
        public fixed char Description[257];
        public uint PhysicalAddressLength;
        public fixed byte PhysicalAddress[32];
        public fixed byte PermanentPhysicalAddress[32];
        public uint Mtu;
        public uint Type;
        public uint TunnelType;
        public uint MediaType;
        public uint PhysicalMediumType;
        public uint AccessType;
        public uint DirectionType;
        public byte InterfaceAndOperStatusFlags;
        public uint OperStatus;
        public uint AdminStatus;
        public uint MediaConnectState;
        public Guid NetworkGuid;
        public uint ConnectionType;
        public ulong TransmitLinkSpeed;
        public ulong ReceiveLinkSpeed;
        public fixed ulong Counters[18];
    }

    internal sealed class NetworkInterfaceInfo
    {
        public Guid InterfaceGuid { get; set; }
        public uint InterfaceIndex { get; set; }
        public uint Type { get; set; }
        public bool IsHardware { get; set; }
        public byte Flags { get; set; }
        public uint MediaType { get; set; }
        public uint PhysicalMediumType { get; set; }
        public uint AccessType { get; set; }
        public uint ConnectionType { get; set; }
        public bool IsAdminUp { get; set; }
        public string? Description { get; set; }
        public string? Alias { get; set; }
        public byte[]? PhysicalAddress { get; set; }
        public byte[]? PermanentPhysicalAddress { get; set; }
    }

    /// <summary>
    /// An entry of the IPv4 neighbour (ARP) cache.
    /// </summary>
    internal sealed class NeighborInfo
    {
        public uint InterfaceIndex { get; set; }
        public IPAddress Address { get; set; } = IPAddress.None;
        public byte[] PhysicalAddress { get; set; } = [];
    }

    internal static unsafe class IpHlpApi
    {
        public const uint IF_TYPE_SOFTWARE_LOOPBACK = 24;
        public const uint NET_IF_ADMIN_STATUS_UP = 1;
        public const byte IF_FLAG_HARDWARE_INTERFACE = 0x01;
        public const int MIB_IF_ROW2_SIZE = 1352;
        public const int MIB_IPNET_ROW2_SIZE = 88;
        public const ushort AF_INET = 2;
        public const uint NlnsUnreachable = 0;
        public const uint NlnsIncomplete = 1;
        public const uint NlnsProbe = 2;
        public const uint NlnsDelay = 3;
        public const uint NlnsStale = 4;
        public const uint NlnsReachable = 5;
        public const uint NlnsPermanent = 6;

        [DllImport("iphlpapi.dll")]
        public static extern uint GetIfTable2(out IntPtr Table);

        [DllImport("iphlpapi.dll")]
        public static extern uint GetIpNetTable2(ushort Family, out IntPtr Table);

        [DllImport("iphlpapi.dll")]
        public static extern void FreeMibTable(IntPtr Memory);

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        public static extern int SendARP(uint DestIP, uint SrcIP, byte[] pMacAddr, ref int PhyAddrLen);

        /// <summary>
        /// Gets the IPv4 neighbours the operating system currently knows the physical address of, without sending any packet.
        /// </summary>
        public static List<NeighborInfo> GetNeighbors()
        {
            var Result = new List<NeighborInfo>();

            if (GetIpNetTable2(AF_INET, out var Table) != 0 || Table == IntPtr.Zero)
                return Result;

            try
            {
                var Count = Marshal.ReadInt32(Table, 0);

                for (var I = 0; I < Count; I++)
                {
                    var Row = Table + 8 + I * MIB_IPNET_ROW2_SIZE;

                    if ((ushort) Marshal.ReadInt16(Row, 0) != AF_INET)
                        continue;

                    var Address = new byte[4];
                    var PhysicalAddress = new byte[Math.Max(0, Math.Min(Marshal.ReadInt32(Row, 72), 32))];
                    var State = (uint) Marshal.ReadInt32(Row, 76);

                    Marshal.Copy(Row + 4, Address, 0, Address.Length);
                    Marshal.Copy(Row + 40, PhysicalAddress, 0, PhysicalAddress.Length);

                    if (!IsReportableNeighbor(Address, PhysicalAddress, State))
                        continue;

                    Result.Add(new NeighborInfo
                    {
                        InterfaceIndex = (uint) Marshal.ReadInt32(Row, 28),
                        Address = new IPAddress(Address),
                        PhysicalAddress = PhysicalAddress,
                    });
                }
            }
            finally
            {
                FreeMibTable(Table);
            }

            return Result;
        }

        /// <summary>
        /// Tells whether a neighbour cache entry describes an actual device: a unicast IPv4 address with a resolved, non-broadcast MAC address.
        /// </summary>
        /// <param name="InAddress">The IPv4 address bytes.</param>
        /// <param name="InPhysicalAddress">The physical address bytes.</param>
        /// <param name="InState">The neighbour state.</param>
        public static bool IsReportableNeighbor(byte[] InAddress, byte[] InPhysicalAddress, uint InState)
        {
            if (InAddress.Length != 4 || InPhysicalAddress.Length != 6)
                return false;

            if (InState == NlnsUnreachable || InState == NlnsIncomplete || InState > NlnsPermanent)
                return false;

            if (InAddress[0] == 0 || InAddress[0] >= 224)
                return false;

            var AllZero = true;
            var AllOnes = true;

            foreach (var Byte in InPhysicalAddress)
            {
                AllZero &= Byte == 0x00;
                AllOnes &= Byte == 0xFF;
            }

            return !AllZero && !AllOnes;
        }

        /// <summary>
        /// Gets every network interface known to the network stack, including disabled ones.
        /// </summary>
        public static List<NetworkInterfaceInfo> GetInterfaces()
        {
            if (sizeof(MIB_IF_ROW2) != MIB_IF_ROW2_SIZE)
                throw new InvalidOperationException("Unexpected MIB_IF_ROW2 layout.");

            var Result = new List<NetworkInterfaceInfo>();

            if (GetIfTable2(out var Table) != 0 || Table == IntPtr.Zero)
                return Result;

            try
            {
                var Count = *(uint*) Table;
                var Rows = (MIB_IF_ROW2*) ((byte*) Table + 8);

                for (var I = 0; I < Count; I++)
                {
                    var Row = &Rows[I];

                    Result.Add(new NetworkInterfaceInfo
                    {
                        InterfaceGuid = Row->InterfaceGuid,
                        InterfaceIndex = Row->InterfaceIndex,
                        Type = Row->Type,
                        IsHardware = (Row->InterfaceAndOperStatusFlags & IF_FLAG_HARDWARE_INTERFACE) != 0,
                        Flags = Row->InterfaceAndOperStatusFlags,
                        MediaType = Row->MediaType,
                        PhysicalMediumType = Row->PhysicalMediumType,
                        AccessType = Row->AccessType,
                        ConnectionType = Row->ConnectionType,
                        IsAdminUp = Row->AdminStatus == NET_IF_ADMIN_STATUS_UP,
                        Description = new string(Row->Description),
                        Alias = new string(Row->Alias),
                        PhysicalAddress = CopyAddress(Row->PhysicalAddress, Row->PhysicalAddressLength),
                        PermanentPhysicalAddress = CopyAddress(Row->PermanentPhysicalAddress, Row->PhysicalAddressLength),
                    });
                }
            }
            finally
            {
                FreeMibTable(Table);
            }

            return Result;
        }

        private static byte[]? CopyAddress(byte* InAddress, uint InLength)
        {
            if (InLength == 0 || InLength > 32)
                return null;

            var Result = new byte[InLength];

            for (var I = 0; I < InLength; I++)
                Result[I] = InAddress[I];

            return Result;
        }
    }
}
