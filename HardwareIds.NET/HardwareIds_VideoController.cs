namespace HardwareIds.NET
{
    using System;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    public static partial class HardwareIds
    {
        internal static void RetrieveVideoControllers(Hwid InHwid)
        {
            try
            {
                var DisplayModes = User32.GetActiveDisplayModes();

                foreach (var InstanceId in CfgMgr32.GetDeviceIds(CfgMgr32.GUID_DEVCLASS_DISPLAY))
                {
                    var DevNode = CfgMgr32.LocateDevNode(InstanceId);

                    if (DevNode is null)
                        continue;

                    DisplayModes.TryGetValue(InstanceId, out var Mode);

                    InHwid.VideoControllers.Add(new HwVideo
                    {
                        Id = InHwid.VideoControllers.Count,
                        Name = CfgMgr32.GetDevNodeProperty(DevNode.Value, CfgMgr32.DEVPKEY_Device_DeviceDesc),
                        Width = Mode?.Width ?? 0,
                        Height = Mode?.Height ?? 0,
                        RefreshRate = Mode?.RefreshRate ?? 0,
                        DriverDate = CfgMgr32.GetDevNodeDateProperty(DevNode.Value, CfgMgr32.DEVPKEY_Device_DriverDate) ?? default,
                        DriverVersion = CfgMgr32.GetDevNodeProperty(DevNode.Value, CfgMgr32.DEVPKEY_Device_DriverVersion),
                        InstanceId = InstanceId,
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
