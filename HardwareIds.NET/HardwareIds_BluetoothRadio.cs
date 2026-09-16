namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    public static partial class HardwareIds
    {
        internal static void RetrieveBluetoothRadios(Hwid InHwid)
        {
            try
            {
                foreach (var Radio in Bluetooth.GetRadios())
                {
                    InHwid.BluetoothRadios.Add(new HwBluetoothRadio
                    {
                        Id = InHwid.BluetoothRadios.Count,
                        Address = FormatMacAddress(Radio.Address),
                        Name = Radio.Name,
                        Manufacturer = Radio.Manufacturer,
                        ClassOfDevice = Radio.ClassOfDevice,
                        LmpSubversion = Radio.LmpSubversion,
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
