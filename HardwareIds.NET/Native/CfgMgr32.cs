namespace HardwareIds.NET.Native
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;

    [StructLayout(LayoutKind.Sequential)]
    internal struct DEVPROPKEY
    {
        public Guid fmtid;
        public uint pid;

        public DEVPROPKEY(string InFormatId, uint InPropertyId)
        {
            this.fmtid = new Guid(InFormatId);
            this.pid = InPropertyId;
        }
    }

    internal static unsafe class CfgMgr32
    {
        public const uint CR_SUCCESS = 0x00;
        public const uint CR_BUFFER_SMALL = 0x1A;
        public const uint CM_LOCATE_DEVNODE_NORMAL = 0x00000000;
        public const uint CM_GET_DEVICE_INTERFACE_LIST_PRESENT = 0x00000000;
        public const uint CM_GETIDLIST_FILTER_PRESENT = 0x00000100;
        public const uint CM_GETIDLIST_FILTER_CLASS = 0x00000200;
        public const uint DEVPROP_TYPE_FILETIME = 0x00000010;
        public const uint DEVPROP_TYPE_STRING = 0x00000012;

        public static readonly Guid GUID_DEVINTERFACE_DISK = new("53F56307-B6BF-11D0-94F2-00A0C91EFB8B");
        public static readonly Guid GUID_DEVINTERFACE_MONITOR = new("E6F07B5F-EE97-4A90-B076-33F57BF4EAA7");
        public static readonly Guid GUID_DEVCLASS_DISPLAY = new("4D36E968-E325-11CE-BFC1-08002BE10318");
        public static readonly Guid GUID_DEVCLASS_NET = new("4D36E972-E325-11CE-BFC1-08002BE10318");

        public static readonly DEVPROPKEY DEVPKEY_Device_DeviceDesc = new("A45C254E-DF1C-4EFD-8020-67D146A850E0", 2);
        public static readonly DEVPROPKEY DEVPKEY_Device_Service = new("A45C254E-DF1C-4EFD-8020-67D146A850E0", 6);
        public static readonly DEVPROPKEY DEVPKEY_Device_FriendlyName = new("A45C254E-DF1C-4EFD-8020-67D146A850E0", 14);
        public static readonly DEVPROPKEY DEVPKEY_Device_InstanceId = new("78C34FC8-104A-4ACA-9EA4-524D52996E57", 256);
        public static readonly DEVPROPKEY DEVPKEY_Device_DriverDate = new("A8B865DD-2E3D-4094-AD97-E593A70C75D6", 2);
        public static readonly DEVPROPKEY DEVPKEY_Device_DriverVersion = new("A8B865DD-2E3D-4094-AD97-E593A70C75D6", 3);
        public static readonly DEVPROPKEY DEVPKEY_Device_InstallDate = new("83DA6326-97A6-4088-9453-A1923F573B29", 100);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern uint CM_Get_Device_Interface_List_SizeW(out uint pulLen, ref Guid InterfaceClassGuid, string? pDeviceID, uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern uint CM_Get_Device_Interface_ListW(ref Guid InterfaceClassGuid, string? pDeviceID, char* Buffer, uint BufferLen, uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern uint CM_Get_Device_ID_List_SizeW(out uint pulLen, string? pszFilter, uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern uint CM_Get_Device_ID_ListW(string? pszFilter, char* Buffer, uint BufferLen, uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern uint CM_Get_Device_Interface_PropertyW(string pszDeviceInterface, ref DEVPROPKEY PropertyKey, out uint PropertyType, byte* PropertyBuffer, ref uint PropertyBufferSize, uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern uint CM_Locate_DevNodeW(out uint pdnDevInst, string pDeviceID, uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern uint CM_Get_DevNode_PropertyW(uint dnDevInst, ref DEVPROPKEY PropertyKey, out uint PropertyType, byte* PropertyBuffer, ref uint PropertyBufferSize, uint ulFlags);

        /// <summary>
        /// Gets the device interface paths of the present devices exposing the given interface class.
        /// </summary>
        /// <param name="InInterfaceClass">The interface class GUID.</param>
        public static string[] GetDeviceInterfaces(Guid InInterfaceClass)
        {
            for (var Attempt = 0; Attempt < 3; Attempt++)
            {
                if (CM_Get_Device_Interface_List_SizeW(out var Length, ref InInterfaceClass, null, CM_GET_DEVICE_INTERFACE_LIST_PRESENT) != CR_SUCCESS || Length == 0)
                    return [];

                var Buffer = new char[Length];
                uint Result;

                fixed (char* BufferPtr = Buffer)
                    Result = CM_Get_Device_Interface_ListW(ref InInterfaceClass, null, BufferPtr, Length, CM_GET_DEVICE_INTERFACE_LIST_PRESENT);

                if (Result == CR_SUCCESS)
                    return Kernel32.SplitMultiString(Buffer, Buffer.Length);

                if (Result != CR_BUFFER_SMALL)
                    return [];
            }

            return [];
        }

        /// <summary>
        /// Gets the device instance identifiers of the present devices belonging to the given setup class.
        /// </summary>
        /// <param name="InSetupClass">The setup class GUID.</param>
        public static string[] GetDeviceIds(Guid InSetupClass)
        {
            var Filter = InSetupClass.ToString("B").ToUpperInvariant();
            const uint Flags = CM_GETIDLIST_FILTER_CLASS | CM_GETIDLIST_FILTER_PRESENT;

            for (var Attempt = 0; Attempt < 3; Attempt++)
            {
                if (CM_Get_Device_ID_List_SizeW(out var Length, Filter, Flags) != CR_SUCCESS || Length == 0)
                    return [];

                var Buffer = new char[Length];
                uint Result;

                fixed (char* BufferPtr = Buffer)
                    Result = CM_Get_Device_ID_ListW(Filter, BufferPtr, Length, Flags);

                if (Result == CR_SUCCESS)
                    return Kernel32.SplitMultiString(Buffer, Buffer.Length);

                if (Result != CR_BUFFER_SMALL)
                    return [];
            }

            return [];
        }

        /// <summary>
        /// Locates the device node of the given device instance identifier.
        /// </summary>
        /// <param name="InInstanceId">The device instance identifier.</param>
        public static uint? LocateDevNode(string InInstanceId)
        {
            return CM_Locate_DevNodeW(out var DevInst, InInstanceId, CM_LOCATE_DEVNODE_NORMAL) == CR_SUCCESS ? DevInst : null;
        }

        /// <summary>
        /// Gets a string property of a device interface.
        /// </summary>
        /// <param name="InInterfacePath">The device interface path.</param>
        /// <param name="InKey">The property key.</param>
        public static string? GetInterfaceProperty(string InInterfacePath, DEVPROPKEY InKey)
        {
            var Value = ReadProperty((byte* InBuffer, ref uint InSize, out uint OutType) => CM_Get_Device_Interface_PropertyW(InInterfacePath, ref InKey, out OutType, InBuffer, ref InSize, 0), out var Type);
            return Value != null && Type == DEVPROP_TYPE_STRING ? DecodeString(Value) : null;
        }

        /// <summary>
        /// Gets a string property of a device node.
        /// </summary>
        /// <param name="InDevInst">The device node.</param>
        /// <param name="InKey">The property key.</param>
        public static string? GetDevNodeProperty(uint InDevInst, DEVPROPKEY InKey)
        {
            var Value = ReadProperty((byte* InBuffer, ref uint InSize, out uint OutType) => CM_Get_DevNode_PropertyW(InDevInst, ref InKey, out OutType, InBuffer, ref InSize, 0), out var Type);
            return Value != null && Type == DEVPROP_TYPE_STRING ? DecodeString(Value) : null;
        }

        /// <summary>
        /// Gets a FILETIME property of a device node.
        /// </summary>
        /// <param name="InDevInst">The device node.</param>
        /// <param name="InKey">The property key.</param>
        public static DateTime? GetDevNodeDateProperty(uint InDevInst, DEVPROPKEY InKey)
        {
            var Value = ReadProperty((byte* InBuffer, ref uint InSize, out uint OutType) => CM_Get_DevNode_PropertyW(InDevInst, ref InKey, out OutType, InBuffer, ref InSize, 0), out var Type);

            if (Value == null || Type != DEVPROP_TYPE_FILETIME || Value.Length < 8)
                return null;

            try
            {
                return DateTime.FromFileTime(BitConverter.ToInt64(Value, 0));
            }
            catch
            {
                return null;
            }
        }

        private delegate uint PropertyReader(byte* InBuffer, ref uint InSize, out uint OutType);

        private static byte[]? ReadProperty(PropertyReader InReader, out uint OutType)
        {
            uint Size = 0;
            var Result = InReader(null, ref Size, out OutType);

            if (Result != CR_BUFFER_SMALL || Size == 0)
                return null;

            var Buffer = new byte[Size];

            fixed (byte* BufferPtr = Buffer)
                Result = InReader(BufferPtr, ref Size, out OutType);

            return Result == CR_SUCCESS ? Buffer : null;
        }

        private static string DecodeString(byte[] InBuffer)
        {
            var Value = Encoding.Unicode.GetString(InBuffer);
            var End = Value.IndexOf('\0');
            return End >= 0 ? Value.Substring(0, End) : Value;
        }
    }
}
