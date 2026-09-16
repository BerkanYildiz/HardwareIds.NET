namespace HardwareIds.NET.Tests.Integration
{
    using System.Linq;

    using global::HardwareIds.NET.Structures.Components;

    using Xunit;

    public class DisplayTests
    {
        [Fact]
        public void VideoControllers_AreEnumeratedWithValidFields()
        {
            var Controllers = HwidFixture.Hwid.VideoControllers;

            Assert.NotEmpty(Controllers);
            Assert.Equal(Enumerable.Range(0, Controllers.Count), Controllers.Select(T => T.Id));

            Assert.All(Controllers, Controller =>
            {
                Assert.False(string.IsNullOrWhiteSpace(Controller.Name));
                Assert.Matches(@"^\d+(\.\d+)+$", Controller.DriverVersion);
                Assert.True(Controller.DriverDate.Year >= 2000);
                Assert.Equal(Controller.Width == 0, Controller.Height == 0);
            });

            Assert.Contains(Controllers, T => T.Width > 0 && T.Height > 0 && T.RefreshRate > 0);
        }

        [Fact]
        public void VideoControllers_MatchWmi()
        {
            var Rows = Wmi.Query("Win32_VideoController");
            Assert.SkipWhen(Rows is null, "WMI is not available on this machine.");

            var Controllers = HwidFixture.Hwid.VideoControllers;
            Assert.Equal(Rows.Select(T => T.GetString("Name")).OrderBy(T => T), Controllers.Select(T => T.Name).OrderBy(T => T));

            foreach (var Row in Rows)
            {
                var Controller = Controllers.First(T => T.Name == Row.GetString("Name"));

                Assert.Equal(Row.GetString("DriverVersion"), Controller.DriverVersion);
                Assert.Equal(Row.GetDate("DriverDate")?.Date, Controller.DriverDate.Date);

                //
                // WMI runs in session 0 and can see displays this session cannot, so only compare active modes.
                //

                if (Controller.Width > 0)
                {
                    Assert.Equal(Row.GetNumber("CurrentHorizontalResolution"), Controller.Width);
                    Assert.Equal(Row.GetNumber("CurrentVerticalResolution"), Controller.Height);
                    Assert.Equal(Row.GetNumber("CurrentRefreshRate"), Controller.RefreshRate);
                }
            }
        }

        [Fact]
        public void Monitors_HaveValidEdidFields()
        {
            var Monitors = HwidFixture.Hwid.Monitors;
            Assert.SkipWhen(Monitors.Count == 0, "No monitor with an EDID block is attached to this machine.");

            Assert.Equal(Enumerable.Range(0, Monitors.Count), Monitors.Select(T => T.Id));

            Assert.All(Monitors, Monitor =>
            {
                Assert.Matches("^[A-Z]{3}$", Monitor.Manufacturer);
                Assert.Matches("^[0-9A-F]{4}$", Monitor.Product);
                Assert.NotNull(Monitor.Name);
                Assert.NotNull(Monitor.SerialNumber);
            });
        }

        [Fact]
        public void Monitors_MatchWmi()
        {
            var Rows = Wmi.Query("WmiMonitorID", @"root\wmi");
            Assert.SkipWhen(Rows is null, "The WMI monitor provider is not available on this machine.");

            var Monitors = HwidFixture.Hwid.Monitors;
            Assert.Equal(Rows.Count, Monitors.Count);

            var Expected = Rows.Select(T => (
                Manufacturer: HwMonitor.ArrayToString(T["ManufacturerName"] as ushort[]),
                Product: HwMonitor.ArrayToString(T["ProductCodeID"] as ushort[]),
                Serial: HwMonitor.ArrayToString(T["SerialNumberID"] as ushort[]),
                Name: HwMonitor.ArrayToString(T["UserFriendlyName"] as ushort[]))).OrderBy(T => T.ToString());

            var Actual = Monitors.Select(T => (Manufacturer: T.Manufacturer, Product: T.Product, Serial: T.SerialNumber, Name: T.Name)).OrderBy(T => T.ToString());

            Assert.Equal(Expected, Actual);
        }
    }
}
