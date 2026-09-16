namespace HardwareIds.NET.Tests.Integration
{
    using System;
    using System.Linq;
    using System.Security.Principal;

    using Xunit;

    public class PrinterAndUserTests
    {
        [Fact]
        public void Printers_HaveValidFields()
        {
            var Printers = HwidFixture.Hwid.Printers;
            Assert.SkipWhen(Printers.Count == 0, "No printer is installed on this machine.");

            Assert.Equal(Enumerable.Range(0, Printers.Count), Printers.Select(T => T.Id));
            Assert.Equal(Printers.Count, Printers.Select(T => T.Name).Distinct().Count());

            Assert.All(Printers, Printer =>
            {
                Assert.False(string.IsNullOrWhiteSpace(Printer.Name));
                Assert.False(string.IsNullOrWhiteSpace(Printer.PortName));
                Assert.NotNull(Printer.Location);
                Assert.Equal(Printer.Width == 0, Printer.Height == 0);
            });
        }

        [Fact]
        public void Printers_MatchWmi()
        {
            var Rows = Wmi.Query("Win32_Printer");
            Assert.SkipWhen(Rows is null, "WMI is not available on this machine.");

            var Printers = HwidFixture.Hwid.Printers;
            Assert.Equal(Rows.Select(T => T.GetString("Name")).OrderBy(T => T), Printers.Select(T => T.Name).OrderBy(T => T));

            foreach (var Row in Rows)
            {
                var Printer = Assert.Single(Printers, T => T.Name == Row.GetString("Name"));

                Assert.Equal(Row.GetString("PortName"), Printer.PortName);
                Assert.Equal(Wmi.Normalize(Row.GetString("Location")), Wmi.Normalize(Printer.Location));

                if (Row["HorizontalResolution"] != null)
                {
                    Assert.Equal(Row.GetNumber("HorizontalResolution"), Printer.Width);
                    Assert.Equal(Row.GetNumber("VerticalResolution"), Printer.Height);
                }
            }
        }

        [Fact]
        public void Users_HaveValidFields()
        {
            var Users = HwidFixture.Hwid.Users;

            Assert.NotEmpty(Users);
            Assert.Equal(Enumerable.Range(0, Users.Count), Users.Select(T => T.Id));
            Assert.Equal(Users.Count, Users.Select(T => T.Username).Distinct(StringComparer.OrdinalIgnoreCase).Count());

            Assert.All(Users, User =>
            {
                Assert.False(string.IsNullOrWhiteSpace(User.Username));
                Assert.Matches(@"^S-1-5-21(-\d+){4}$", User.SID);
                Assert.Equal(Environment.MachineName, User.Domain);
                Assert.NotNull(User.FullName);
            });

            Assert.Single(Users.Select(T => T.SID!.Substring(0, T.SID!.LastIndexOf('-'))).Distinct());
        }

        [Fact]
        public void Users_IncludeTheCurrentLocalUser()
        {
            using var Identity = WindowsIdentity.GetCurrent();
            var CurrentSid = Identity.User;
            var Users = HwidFixture.Hwid.Users;

            Assert.SkipWhen(CurrentSid is null || Users.Count == 0, "No current user SID available.");

            var MachineDomain = Users[0].SID!.Substring(0, Users[0].SID!.LastIndexOf('-'));
            Assert.SkipUnless(CurrentSid.Value.StartsWith(MachineDomain + "-", StringComparison.Ordinal), "The current user is not a local account.");

            Assert.Contains(Users, T => T.SID == CurrentSid.Value);
        }

        [Fact]
        public void Users_MatchWmi()
        {
            var Rows = Wmi.Query("Win32_UserAccount", InCondition: "LocalAccount = TRUE AND Disabled = FALSE");
            Assert.SkipWhen(Rows is null, "WMI is not available on this machine.");

            var Users = HwidFixture.Hwid.Users;
            Assert.Equal(Rows.Select(T => T.GetString("Name")).OrderBy(T => T), Users.Select(T => T.Username).OrderBy(T => T));

            foreach (var Row in Rows)
            {
                var User = Assert.Single(Users, T => T.Username == Row.GetString("Name"));

                Assert.Equal(Row.GetString("SID"), User.SID);
                Assert.Equal(Row.GetString("Domain"), User.Domain);
                Assert.Equal(Wmi.Normalize(Row.GetString("FullName")), Wmi.Normalize(User.FullName));
            }
        }
    }
}
