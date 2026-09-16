namespace HardwareIds.NET
{
    using System;
    using System.Linq;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    public static partial class HardwareIds
    {
        internal static void RetrieveUserAccounts(Hwid InHwid)
        {
            try
            {
                var MachineName = Environment.MachineName;
                var MachineSid = AdvApi32.LookupAccountSid(MachineName);

                foreach (var User in NetApi32.GetLocalUsers().Where(T => !T.IsDisabled))
                {
                    InHwid.Users.Add(new HwUser
                    {
                        Id = InHwid.Users.Count,
                        Username = User.Name,
                        FullName = User.FullName,
                        SID = MachineSid != null ? $"{MachineSid.Value}-{User.RelativeId}" : null,
                        Domain = MachineName,
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
