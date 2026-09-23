namespace HardwareIds.NET.Tests.Integration
{
    using System;
    using System.Linq;
    using System.Net.NetworkInformation;

    using Xunit;

    public class NetworkAdapterTests
    {
        private const string MacPattern = "^([0-9A-F]{2}:){5}[0-9A-F]{2}$";

        [Fact]
        public void Adapters_AreEnumeratedWithValidFields()
        {
            var Adapters = HwidFixture.Hwid.NetworkAdapters;
            Assert.SkipWhen(Adapters.Count == 0, "No physical network adapter on this machine.");

            Assert.Equal(Adapters.Select(T => T.Id).OrderBy(T => T), Adapters.Select(T => T.Id));
            Assert.Equal(Adapters.Count, Adapters.Select(T => T.Id).Distinct().Count());
            Assert.Equal(Adapters.Count, Adapters.Select(T => T.InterfaceGuid).Distinct().Count());

            Assert.All(Adapters, Adapter =>
            {
                Assert.True(Adapter.Id >= 0);
                Assert.True(Adapter.InterfaceId > 0 || !Adapter.IsEnabled, "An enabled adapter has a network interface.");
                Assert.False(string.IsNullOrWhiteSpace(Adapter.Name));
                Assert.Matches("^\\{[0-9A-F-]{36}\\}$", Adapter.InterfaceGuid);
                Assert.True(Adapter.IsPhysical);

                if (Adapter.Address.Current != null)
                    Assert.Matches(MacPattern, Adapter.Address.Current);

                if (Adapter.Address.Permanent != null)
                    Assert.Matches(MacPattern, Adapter.Address.Permanent);
            });
        }

        [Fact]
        public void Adapters_MatchTheNetworkInterfaceApi()
        {
            var Adapters = HwidFixture.Hwid.NetworkAdapters;
            Assert.SkipWhen(Adapters.Count == 0, "No physical network adapter on this machine.");

            //
            // .NET Framework only lists some of the interfaces (disconnected adapters are missing), so compare the ones both sides know.
            //

            var Interfaces = NetworkInterface.GetAllNetworkInterfaces();
            var Known = Adapters.Where(Adapter => Interfaces.Any(T => string.Equals(T.Id, Adapter.InterfaceGuid, StringComparison.OrdinalIgnoreCase))).ToList();

            Assert.SkipWhen(Known.Count == 0, "The network interface API lists none of the physical adapters.");

            foreach (var Adapter in Known)
            {
                var Interface = Assert.Single(Interfaces, T => string.Equals(T.Id, Adapter.InterfaceGuid, StringComparison.OrdinalIgnoreCase));
                var Address = Interface.GetPhysicalAddress().GetAddressBytes();

                if (Address.Length == 6 && Adapter.Address.Current != null)
                    Assert.Equal(HardwareIds.FormatMacAddress(Address), Adapter.Address.Current);
            }
        }

        [Fact]
        public void Adapters_MatchWmi()
        {
            var Rows = Wmi.Query("Win32_NetworkAdapter", InCondition: "PhysicalAdapter = TRUE");
            Assert.SkipWhen(Rows is null, "WMI is not available on this machine.");

            var Adapters = HwidFixture.Hwid.NetworkAdapters;
            Assert.Equal(Rows.Select(T => (int) T.GetNumber("Index")).OrderBy(T => T), Adapters.Select(T => T.Id));

            foreach (var Row in Rows)
            {
                var Adapter = Assert.Single(Adapters, T => T.Id == (int) Row.GetNumber("Index"));

                Assert.Equal(Row.GetString("GUID"), Adapter.InterfaceGuid);
                Assert.Equal(Row.GetString("ProductName"), Adapter.Name);
                Assert.Equal(Wmi.Normalize(Row.GetString("ServiceName")), Wmi.Normalize(Adapter.ServiceName));
                Assert.Equal(Row.GetBool("NetEnabled"), Adapter.IsEnabled);

                if (Adapter.IsEnabled)
                    Assert.Equal((int) Row.GetNumber("InterfaceIndex"), Adapter.InterfaceId);

                if (Row.GetString("MACAddress") is string MacAddress)
                    Assert.Equal(MacAddress, Adapter.Address.Current);
            }
        }
    }
}
