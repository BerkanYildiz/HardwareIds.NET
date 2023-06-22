namespace HardwareIds.NET
{
    using System;
    using System.Linq;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using WindowsMonitor.Windows.Users;

    public static partial class HardwareIds
    {
        private static void RetrieveUserAccounts(Hwid InHwid)
        {
            try
            {
                foreach (var User in UserAccount.Retrieve().Where(T => !T.Disabled))
                {
                    InHwid.Users.Add(new HwUser
                    {
                        Id = (int) InHwid.Users.Count,
                        Username = User.Name,
                        FullName = User.FullName,
                        SID = User.Sid,
                        Domain = User.Domain,
                        InstallDate = User.InstallDate,
                    });
                }
            }
            catch (Exception)
            {
                // ...
            }
        }
    }
}
