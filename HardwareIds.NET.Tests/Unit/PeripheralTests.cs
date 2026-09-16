namespace HardwareIds.NET.Tests.Unit
{
    using System;
    using System.Runtime.InteropServices;

    using global::HardwareIds.NET.Native;

    using Xunit;

    public class PeripheralTests
    {
        [Fact]
        public void BluetoothAddressToBytes_IsMostSignificantByteFirst()
        {
            Assert.Equal(new byte[] { 0x00, 0x1A, 0x7D, 0xDA, 0x71, 0x13 }, Bluetooth.AddressToBytes(0x001A7DDA7113));
            Assert.Equal("00:1A:7D:DA:71:13", HardwareIds.FormatMacAddress(Bluetooth.AddressToBytes(0x001A7DDA7113)));
        }

        [Fact]
        public void BluetoothRadioInfo_HasTheNativeLayout()
        {
            Assert.Equal(Bluetooth.BLUETOOTH_RADIO_INFO_SIZE, Marshal.SizeOf(typeof(BLUETOOTH_RADIO_INFO)));
            Assert.Equal(4, Marshal.SizeOf(typeof(BLUETOOTH_FIND_RADIO_PARAMS)));
        }

        [Fact]
        public void BluetoothGetRadios_DoesNotThrowWithoutRadios()
        {
            var Radios = Bluetooth.GetRadios();

            Assert.All(Radios, Radio =>
            {
                Assert.Equal(6, Radio.Address.Length);
                Assert.False(string.IsNullOrEmpty(Radio.Name));
            });
        }

        [Theory]
        [InlineData(15, 6, 2023, "2023-06-15")]
        [InlineData(1, 1, 1980, "1980-01-01")]
        [InlineData(0, 0, 0, null)]
        [InlineData(31, 2, 2023, null)]
        [InlineData(1, 13, 2023, null)]
        [InlineData(1, 1, 1979, null)]
        public void BatteryManufactureDate_IsValidated(byte InDay, byte InMonth, ushort InYear, string? InExpected)
        {
            var Date = Battery.ParseManufactureDate(InDay, InMonth, InYear);

            Assert.Equal(InExpected, Date?.ToString("yyyy-MM-dd"));
        }

        [Fact]
        public void BatteryGetBatteries_DoesNotThrowWithoutBatteries()
        {
            var Batteries = Battery.GetBatteries();

            Assert.All(Batteries, Entry => Assert.True(Entry.DesignedCapacity > 0 || Entry.SerialNumber != null || Entry.DeviceName != null));
        }
    }
}
