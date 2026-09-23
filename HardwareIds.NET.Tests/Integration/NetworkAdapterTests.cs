namespace HardwareIds.NET.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.NetworkInformation;

    using global::HardwareIds.NET.Native;

    using Microsoft.Win32;

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
            var Expected = Rows.Select(T => (int) T.GetNumber("Index")).OrderBy(T => T).ToList();
            var Actual = Adapters.Select(T => T.Id).ToList();

            if (!Expected.SequenceEqual(Actual))
                Assert.Fail($"Physical adapters differ. WMI: [{string.Join(", ", Expected)}], native: [{string.Join(", ", Actual)}].\n{DescribeAdapters(Expected.Union(Actual))}");

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

        /// <summary>
        /// Describes adapters with everything WMI could base its "PhysicalAdapter" decision on, so a mismatch explains itself.
        /// </summary>
        private static string DescribeAdapters(IEnumerable<int> InIndexes)
        {
            var Rows = Wmi.Query("Win32_NetworkAdapter") ?? [];
            var Interfaces = IpHlpApi.GetInterfaces();
            var Lines = new List<string>();

            using var ClassKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4D36E972-E325-11CE-BFC1-08002BE10318}");

            foreach (var Index in InIndexes.OrderBy(T => T))
            {
                var Row = Rows.FirstOrDefault(T => (int) T.GetNumber("Index") == Index);
                using var AdapterKey = ClassKey?.OpenSubKey(Index.ToString("D4"));
                var Guid = AdapterKey?.GetValue("NetCfgInstanceId") as string;
                var Interface = Guid != null && System.Guid.TryParse(Guid, out var Parsed) ? Interfaces.FirstOrDefault(T => T.InterfaceGuid == Parsed) : null;

                Lines.Add(
                    $"#{Index} {Row?.GetString("Name") ?? AdapterKey?.GetValue("DriverDesc")}: " +
                    $"wmi physical={Row?.GetBool("PhysicalAdapter")} installed={Row?.GetBool("Installed")} enabled={Row?.GetBool("NetEnabled")} status={Row?.GetNumber("NetConnectionStatus")} type={Row?.GetNumber("AdapterTypeID")} pnp={Row?.GetString("PNPDeviceID")} service={Row?.GetString("ServiceName")}; " +
                    $"registry characteristics=0x{AdapterKey?.GetValue("Characteristics") as int? ?? -1:X} component={AdapterKey?.GetValue("ComponentId")} iftype={AdapterKey?.GetValue("*IfType")} media={AdapterKey?.GetValue("*MediaType")} physicalmedia={AdapterKey?.GetValue("*PhysicalMediaType")}; " +
                    $"ndis {(Interface is null ? "no interface" : $"flags=0x{Interface.Flags:X2} iftype={Interface.Type} media={Interface.MediaType} physicalmedia={Interface.PhysicalMediumType} access={Interface.AccessType} connection={Interface.ConnectionType} adminup={Interface.IsAdminUp}")}");
            }

            return string.Join("\n", Lines);
        }
    }
}
