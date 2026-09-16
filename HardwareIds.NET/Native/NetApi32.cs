namespace HardwareIds.NET.Native
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Security.Principal;

    [StructLayout(LayoutKind.Sequential)]
    internal struct USER_INFO_20
    {
        public IntPtr usri20_name;
        public IntPtr usri20_full_name;
        public IntPtr usri20_comment;
        public uint usri20_flags;
        public uint usri20_user_id;
    }

    internal sealed class LocalUserInfo
    {
        public string? Name { get; set; }
        public string? FullName { get; set; }
        public bool IsDisabled { get; set; }
        public uint RelativeId { get; set; }
    }

    internal static class NetApi32
    {
        public const uint FILTER_NORMAL_ACCOUNT = 0x0002;
        public const uint UF_ACCOUNTDISABLE = 0x0002;
        public const uint MAX_PREFERRED_LENGTH = 0xFFFFFFFF;
        public const uint NERR_Success = 0;
        public const uint ERROR_MORE_DATA = 234;

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        public static extern uint NetUserEnum(string? servername, uint level, uint filter, out IntPtr bufptr, uint prefmaxlen, out uint entriesread, out uint totalentries, ref uint resume_handle);

        [DllImport("netapi32.dll")]
        public static extern uint NetApiBufferFree(IntPtr Buffer);

        /// <summary>
        /// Gets the normal user accounts of the local machine.
        /// </summary>
        public static List<LocalUserInfo> GetLocalUsers()
        {
            var Result = new List<LocalUserInfo>();
            var Stride = Marshal.SizeOf(typeof(USER_INFO_20));
            uint Resume = 0;

            while (true)
            {
                var Status = NetUserEnum(null, 20, FILTER_NORMAL_ACCOUNT, out var Buffer, MAX_PREFERRED_LENGTH, out var Read, out _, ref Resume);

                if (Status != NERR_Success && Status != ERROR_MORE_DATA)
                    break;

                try
                {
                    for (var I = 0; I < Read; I++)
                    {
                        var Entry = Marshal.PtrToStructure<USER_INFO_20>(Buffer + I * Stride);

                        Result.Add(new LocalUserInfo
                        {
                            Name = Marshal.PtrToStringUni(Entry.usri20_name),
                            FullName = Marshal.PtrToStringUni(Entry.usri20_full_name),
                            IsDisabled = (Entry.usri20_flags & UF_ACCOUNTDISABLE) != 0,
                            RelativeId = Entry.usri20_user_id,
                        });
                    }
                }
                finally
                {
                    if (Buffer != IntPtr.Zero)
                        NetApiBufferFree(Buffer);
                }

                if (Status != ERROR_MORE_DATA)
                    break;
            }

            return Result;
        }
    }

    internal static unsafe class AdvApi32
    {
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool LookupAccountNameW(string? lpSystemName, string lpAccountName, byte* Sid, ref uint cbSid, char* ReferencedDomainName, ref uint cchReferencedDomainName, out uint peUse);

        /// <summary>
        /// Looks up the security identifier of the given account name.
        /// </summary>
        /// <param name="InAccountName">The account name.</param>
        public static SecurityIdentifier? LookupAccountSid(string InAccountName)
        {
            uint SidSize = 0;
            uint DomainSize = 0;

            LookupAccountNameW(null, InAccountName, null, ref SidSize, null, ref DomainSize, out _);

            if (SidSize == 0)
                return null;

            var Sid = new byte[SidSize];
            var Domain = new char[Math.Max(DomainSize, 1)];

            fixed (byte* SidPtr = Sid)
            fixed (char* DomainPtr = Domain)
            {
                if (!LookupAccountNameW(null, InAccountName, SidPtr, ref SidSize, DomainPtr, ref DomainSize, out _))
                    return null;
            }

            return new SecurityIdentifier(Sid, 0);
        }
    }
}
