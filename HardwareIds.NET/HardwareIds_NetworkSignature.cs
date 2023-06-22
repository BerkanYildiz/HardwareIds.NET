namespace HardwareIds.NET
{
    using System;
    using System.IO;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using Microsoft.Win32;

    public static partial class HardwareIds
    {
        private static void RetrieveNetworkSignatures(Hwid InHwid)
        {
            try
            {
                using (var UnmanagedSignatures = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\NetworkList\Signatures\Unmanaged\"))
                {
                    foreach (var Subkey in UnmanagedSignatures?.GetSubKeyNames())
                    {
                        InHwid.NetworkSignatures.Add(new HwNetworkSignature
                        {
                            Id = (int) InHwid.NetworkSignatures.Count,
                            ProfileGuid = (string) Registry.GetValue(Path.Combine(UnmanagedSignatures.Name, Subkey), "ProfileGuid", null),
                            Name = (string) Registry.GetValue(Path.Combine(UnmanagedSignatures.Name, Subkey), "Description", null),
                            DefaultGatewayMac = BitConverter.ToString((byte[]) Registry.GetValue(Path.Combine(UnmanagedSignatures.Name, Subkey), "DefaultGatewayMac", Array.Empty<byte>()))?.Replace('-', ':')
                        });
                    }
                }
            }
            catch (Exception)
            {
                // ...
            }
        }
    }
}
