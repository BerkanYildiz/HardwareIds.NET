namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using Microsoft.Win32;

    public static partial class HardwareIds
    {
        internal static void RetrieveNetworkSignatures(Hwid InHwid)
        {
            try
            {
                using var UnmanagedSignatures = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\NetworkList\Signatures\Unmanaged\");

                if (UnmanagedSignatures is null)
                    return;

                foreach (var SubkeyName in UnmanagedSignatures.GetSubKeyNames())
                {
                    using var Signature = UnmanagedSignatures.OpenSubKey(SubkeyName);

                    if (Signature is null)
                        continue;

                    InHwid.NetworkSignatures.Add(new HwNetworkSignature
                    {
                        Id = InHwid.NetworkSignatures.Count,
                        ProfileGuid = Signature.GetValue("ProfileGuid") as string,
                        Name = Signature.GetValue("Description") as string,
                        DefaultGatewayMac = Signature.GetValue("DefaultGatewayMac") is byte[] GatewayMac ? FormatMacAddress(GatewayMac) : string.Empty,
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
