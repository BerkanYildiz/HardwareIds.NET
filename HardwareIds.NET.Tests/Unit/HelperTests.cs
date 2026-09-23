namespace HardwareIds.NET.Tests.Unit
{
    using System;
    using System.Linq;
    using System.Net;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using Xunit;

    public class HelperTests
    {
        [Theory]
        [InlineData(@"\\?\PCI#VEN_1AF4&DEV_1050&SUBSYS_11001AF4&REV_01#3&11583659&0&08#{5b45201d-f2f2-4f3b-85bb-30ff1f953599}", @"PCI\VEN_1AF4&DEV_1050&SUBSYS_11001AF4&REV_01\3&11583659&0&08")]
        [InlineData(@"\\?\SWD#REMOTEDISPLAYENUM#RDPIDD_INDIRECTDISPLAY&SESSIONID_0002#{5b45201d-f2f2-4f3b-85bb-30ff1f953599}", @"SWD\REMOTEDISPLAYENUM\RDPIDD_INDIRECTDISPLAY&SESSIONID_0002")]
        [InlineData(@"PCI#VEN_10DE&DEV_2206#4&2B0E5B6F&0&0008", @"PCI\VEN_10DE&DEV_2206\4&2B0E5B6F&0&0008")]
        public void InterfaceNameToInstanceId_StripsPrefixAndClassGuid(string InInterfaceName, string InExpected)
        {
            Assert.Equal(InExpected, User32.InterfaceNameToInstanceId(InInterfaceName));
        }

        [Fact]
        public void InterfaceNameToInstanceId_ReturnsNullForEmptyInput()
        {
            Assert.Null(User32.InterfaceNameToInstanceId(null));
            Assert.Null(User32.InterfaceNameToInstanceId(string.Empty));
        }

        [Fact]
        public void SplitMultiString_SplitsOnNullCharacters()
        {
            var Buffer = "first\0second\0\0".ToCharArray();

            Assert.Equal(["first", "second"], Kernel32.SplitMultiString(Buffer, Buffer.Length));
            Assert.Equal(["first"], Kernel32.SplitMultiString(Buffer, 6));
            Assert.Empty(Kernel32.SplitMultiString("\0\0".ToCharArray(), 2));
            Assert.Empty(Kernel32.SplitMultiString([], 0));
        }

        [Fact]
        public void FormatMacAddress_UsesUpperCaseColonSeparatedHex()
        {
            Assert.Equal("00:1A:2B:3C:4D:5E", HardwareIds.FormatMacAddress([0x00, 0x1A, 0x2B, 0x3C, 0x4D, 0x5E]));
            Assert.Equal(string.Empty, HardwareIds.FormatMacAddress([]));
        }

        [Theory]
        [InlineData(@"USBSTOR\DISK&VEN_SANDISK&PROD_ULTRA\4C530001&0", 7u, "USB")]
        [InlineData(@"SCSI\DISK&VEN_NVME&PROD_SAMSUNG\5&1&0&0", 17u, "SCSI")]
        [InlineData(@"IDE\DISKWDC_WD10\5&1&0&0.0.0", 3u, "IDE")]
        [InlineData(@"SD\DISK&GENERIC&SD\1&2&3", 12u, "SD")]
        [InlineData(null, 17u, "SCSI")]
        [InlineData(null, 11u, "SCSI")]
        [InlineData(null, 3u, "IDE")]
        [InlineData(null, 7u, "USB")]
        [InlineData(null, 4u, "1394")]
        [InlineData(null, null, "Unknown")]
        public void GetDiskInterfaceType_MapsEnumeratorThenBusType(string? InInstanceId, uint? InBusType, string InExpected)
        {
            Assert.Equal(InExpected, HardwareIds.GetDiskInterfaceType(InInstanceId, InBusType));
        }

        /// <summary>
        /// Each case is an adapter met on a real machine, with the answer WMI gave for it.
        /// </summary>
        [Theory]
        [InlineData("VirtIO Ethernet (QEMU)", 0x84, true, (byte) 0x05, true)]
        [InlineData("Mellanox ConnectX-5 VF (Azure)", 0x84, true, (byte) 0x05, true)]
        [InlineData("Hyper-V synthetic adapter (Azure)", 0x04, true, (byte) 0x05, true)]
        [InlineData("Ghost Hyper-V adapter left by the VM image (Azure)", 0x04, false, (byte) 0x01, false)]
        [InlineData("Azure Network Adapter (MANA) whose driver did not start", 0x04, true, null, false)]
        [InlineData("Kernel debugger adapter", 0x09, true, (byte) 0x00, false)]
        [InlineData("WAN miniport", 0x29, true, (byte) 0x00, false)]
        [InlineData("Hyper-V virtual switch (vEthernet)", 0x01, true, (byte) 0x01, false)]
        [InlineData("Adapter without a Characteristics value", null, true, (byte) 0x05, false)]
        public void IsPhysicalAdapter_MatchesWmiOnKnownAdapters(string InAdapter, int? InCharacteristics, bool InIsPresent, byte? InInterfaceFlags, bool InExpected)
        {
            var Interface = InInterfaceFlags is byte Flags ? new NetworkInterfaceInfo { Flags = Flags } : null;

            Assert.True(InExpected == HardwareIds.IsPhysicalAdapter(InCharacteristics, InIsPresent, Interface), InAdapter);
        }

        [Fact]
        public void FormatProcessorId_PutsEdxBeforeEax()
        {
            Assert.Equal("BFEBFBFF000906EA", HardwareIds.FormatProcessorId([0xEA, 0x06, 0x09, 0x00, 0xFF, 0xFB, 0xEB, 0xBF]));
            Assert.Null(HardwareIds.FormatProcessorId(null));
        }

        [Fact]
        public void GetProcessorIdFromCpuId_MatchesRuntimeCapabilities()
        {
            var ProcessorId = HardwareIds.GetProcessorIdFromCpuId();

        #if NET
            if (System.Runtime.Intrinsics.X86.X86Base.IsSupported)
                Assert.Matches("^[0-9A-F]{16}$", ProcessorId);
            else
                Assert.Null(ProcessorId);
        #else
            Assert.Null(ProcessorId);
        #endif
        }

        [Theory]
        [InlineData(0x8B, 1.1)]
        [InlineData(0x92, 1.8)]
        [InlineData(0x01, 5.0)]
        [InlineData(0x02, 3.3)]
        [InlineData(0x04, 2.9)]
        [InlineData(0x00, 0.0)]
        public void GetProcessorVoltage_DecodesSmbiosVoltageByte(byte InVoltage, double InExpected)
        {
            Assert.Equal(InExpected, HardwareIds.GetProcessorVoltage(InVoltage), 3);
        }

        [Fact]
        public void GetProcessorTopology_AgreesWithTheRuntime()
        {
            var (Cores, LogicalProcessors, Packages) = HardwareIds.GetProcessorTopology();

            Assert.True(Cores >= 1);
            Assert.True(Packages >= 1);
            Assert.True(LogicalProcessors >= Cores);
            Assert.Equal(Environment.ProcessorCount, LogicalProcessors);
        }

        [Fact]
        public void GetOperatingSystemArchitecture_UsesWmiWording()
        {
            var Architecture = HardwareIds.GetOperatingSystemArchitecture();

            Assert.Contains(Architecture, new[] { "64-bit", "32-bit", "ARM 64-bit", "ARM 32-bit" });
            Assert.Equal(Environment.Is64BitOperatingSystem, Architecture.EndsWith("64-bit", StringComparison.Ordinal));
        }

        [Fact]
        public void ArrayToString_DecodesMonitorIdArrays()
        {
            Assert.Equal("DEL", HwMonitor.ArrayToString([0x44, 0x45, 0x4C, 0x00, 0x00]));
            Assert.Equal(string.Empty, HwMonitor.ArrayToString(new ushort[13]));
            Assert.Null(HwMonitor.ArrayToString(null));
        }

        [Fact]
        public void NetworkDevice_IpProperty_RoundTripsAndAcceptsNull()
        {
            var Device = new HwNetworkDevice();
            Assert.Null(Device.Ip);

            Device.Ip = "192.168.1.1";
            Assert.Equal(IPAddress.Parse("192.168.1.1"), Device.Address);
            Assert.Equal("192.168.1.1", Device.Ip);

            Device.Ip = null;
            Assert.Null(Device.Address);
        }

        [Fact]
        public void Hwid_ConvenienceGetters_ReturnTheFirstEntryOrNull()
        {
            var Empty = new Hwid();
            Assert.Null(Empty.Baseboard);
            Assert.Null(Empty.Motherboard);
            Assert.Null(Empty.BiosFirmware);
            Assert.Null(Empty.SmbiosTable);
            Assert.Null(Empty.Processor);
            Assert.Null(Empty.Monitor);
            Assert.Null(Empty.VideoController);
            Assert.Null(Empty.Printer);
            Assert.Null(Empty.User);
            Assert.Null(Empty.OperatingSystem);
            Assert.Null(Empty.Wifi);
            Assert.Null(Empty.Router);
            Assert.Null(Empty.MainChassis);
            Assert.Null(Empty.BluetoothRadio);
            Assert.Null(Empty.Battery);
            Assert.Null(Empty.Disk);
            Assert.Null(Empty.Volume);
            Assert.Null(Empty.NetworkAdapter);
            Assert.Null(Empty.MemoryStick);
            Assert.Null(Empty.NetworkSignature);

            //
            // Every list has a getter for its first entry.
            //

            var Lists = typeof(Hwid).GetProperties().Where(T => T.PropertyType.IsGenericType).Select(T => T.PropertyType.GetGenericArguments()[0]).ToList();
            var Getters = typeof(Hwid).GetProperties().Where(T => !T.PropertyType.IsGenericType).Select(T => T.PropertyType).ToList();
            Assert.Equal(Lists.OrderBy(T => T.Name), Getters.OrderBy(T => T.Name));

            var Populated = new Hwid();
            Populated.Processors.Add(new HwProcessor { Id = 0 });
            Populated.Processors.Add(new HwProcessor { Id = 1 });
            Assert.Same(Populated.Processors[0], Populated.Processor);
        }

        [Fact]
        public void Hwid_ListsAreInitializedAndIndependent()
        {
            var First = new Hwid();
            var Second = new Hwid();

            First.Disks.Add(new HwDisk());

            Assert.NotSame(First.Disks, Second.Disks);
            Assert.Empty(Second.Disks);
            Assert.All(typeof(Hwid).GetProperties().Where(T => T.PropertyType.IsGenericType), T => Assert.NotNull(T.GetValue(Second)));
        }
    }
}
