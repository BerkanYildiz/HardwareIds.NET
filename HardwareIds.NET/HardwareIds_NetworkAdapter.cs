namespace HardwareIds.NET
{
    using System;
    using System.Linq;

    using Driver.NET.DeviceIoControl;

    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using WindowsMonitor.Hardware.Network;

    public static partial class HardwareIds
    {
        private static void RetrieveNetworkAdapters(Hwid InHwid)
        {
            try
            {
                foreach (var NetworkAdapter in Win32NetworkAdapter.Retrieve().Where(T => T.Installed /* && T.NetEnabled */ && T.PhysicalAdapter).OrderBy(T => T.Index).ThenByDescending(T => T.PhysicalAdapter))
                {
                    var Entry = new HwNetworkAdapter
                    {
                        Id = (int) NetworkAdapter.Index,
                        InterfaceId = (int) NetworkAdapter.InterfaceIndex,
                        Name = NetworkAdapter.ProductName,
                        InterfaceGuid = NetworkAdapter.Guid,
                        ServiceName = NetworkAdapter.ServiceName,
                        IsPhysical = NetworkAdapter.PhysicalAdapter,
                        IsEnabled = NetworkAdapter.NetEnabled,
                        InstallDate = NetworkAdapter.InstallDate,
                        Address = { Current = NetworkAdapter.MacAddress, Permanent = NetworkAdapter.PermanentAddress },
                    };

                    if (NetworkAdapter.NetEnabled)
                    {
                        if (DeviceIoControl.Exists($@"\\.\{NetworkAdapter.Guid}"))
                        {
                            var DeviceIo = new DeviceIoControl($@"\\.\{NetworkAdapter.Guid}");

                            { DeviceIo.Connect();
                                {
                                    var InputValue = 0x01010101;
                                    var OutputValue = new byte[6];
                                    bool WasCallSuccessful;
                                    unsafe { fixed (byte* OutputBuffer = OutputValue) { WasCallSuccessful = DeviceIo.TryIoControl(0x170002, &InputValue, 4, OutputBuffer, 6); } }

                                    if (WasCallSuccessful)
                                        Entry.Address.Current = string.Join(":", OutputValue.Select(T => T.ToString("X2")));
                                }

                                {
                                    var InputValue = 0x01010102;
                                    var OutputValue = new byte[6];
                                    bool WasCallSuccessful;
                                    unsafe { fixed (byte* OutputBuffer = OutputValue) { WasCallSuccessful = DeviceIo.TryIoControl(0x170002, &InputValue, 4, OutputBuffer, 6); } }

                                    if (WasCallSuccessful)
                                        Entry.Address.Permanent = string.Join(":", OutputValue.Select(T => T.ToString("X2")));
                                }
                            } DeviceIo.Close();
                        }
                    }

                    InHwid.NetworkAdapters.Add(Entry);
                }
            }
            catch (Exception)
            {
                // ...
            }
        }
    }
}
