namespace HardwareIds.NET.Native
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct PRINTER_INFO_2
    {
        public IntPtr pServerName;
        public IntPtr pPrinterName;
        public IntPtr pShareName;
        public IntPtr pPortName;
        public IntPtr pDriverName;
        public IntPtr pComment;
        public IntPtr pLocation;
        public IntPtr pDevMode;
        public IntPtr pSepFile;
        public IntPtr pPrintProcessor;
        public IntPtr pDatatype;
        public IntPtr pParameters;
        public IntPtr pSecurityDescriptor;
        public uint Attributes;
        public uint Priority;
        public uint DefaultPriority;
        public uint StartTime;
        public uint UntilTime;
        public uint Status;
        public uint cJobs;
        public uint AveragePPM;
    }

    internal sealed class PrinterInfo
    {
        public string? Name { get; set; }
        public string? PortName { get; set; }
        public string? Location { get; set; }
        public uint HorizontalResolution { get; set; }
        public uint VerticalResolution { get; set; }
    }

    internal static unsafe class WinSpool
    {
        public const uint PRINTER_ENUM_LOCAL = 0x00000002;
        public const uint PRINTER_ENUM_CONNECTIONS = 0x00000004;
        public const uint DM_PRINTQUALITY = 0x00000400;
        public const uint DM_YRESOLUTION = 0x00002000;
        private const int DEVMODE_FIELDS_OFFSET = 72;
        private const int DEVMODE_PRINTQUALITY_OFFSET = 90;
        private const int DEVMODE_YRESOLUTION_OFFSET = 96;

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool EnumPrintersW(uint Flags, string? Name, uint Level, byte* pPrinterEnum, uint cbBuf, out uint pcbNeeded, out uint pcReturned);

        /// <summary>
        /// Gets the local printers and the printer connections of the current user.
        /// </summary>
        public static List<PrinterInfo> GetPrinters()
        {
            const uint Flags = PRINTER_ENUM_LOCAL | PRINTER_ENUM_CONNECTIONS;
            var Result = new List<PrinterInfo>();

            EnumPrintersW(Flags, null, 2, null, 0, out var Needed, out _);

            if (Needed == 0)
                return Result;

            var Buffer = new byte[Needed];

            fixed (byte* BufferPtr = Buffer)
            {
                if (!EnumPrintersW(Flags, null, 2, BufferPtr, Needed, out Needed, out var Returned))
                    return Result;

                var Stride = Marshal.SizeOf(typeof(PRINTER_INFO_2));

                for (var I = 0; I < Returned; I++)
                {
                    var Info = Marshal.PtrToStructure<PRINTER_INFO_2>((IntPtr) (BufferPtr + I * Stride));

                    var Entry = new PrinterInfo
                    {
                        Name = Marshal.PtrToStringUni(Info.pPrinterName),
                        PortName = Marshal.PtrToStringUni(Info.pPortName),
                        Location = Marshal.PtrToStringUni(Info.pLocation),
                    };

                    if (Info.pDevMode != IntPtr.Zero)
                    {
                        var Fields = (uint) Marshal.ReadInt32(Info.pDevMode, DEVMODE_FIELDS_OFFSET);

                        if ((Fields & DM_PRINTQUALITY) != 0)
                        {
                            var Quality = Marshal.ReadInt16(Info.pDevMode, DEVMODE_PRINTQUALITY_OFFSET);

                            if (Quality > 0)
                                Entry.HorizontalResolution = (uint) Quality;
                        }

                        if ((Fields & DM_YRESOLUTION) != 0)
                        {
                            var Resolution = Marshal.ReadInt16(Info.pDevMode, DEVMODE_YRESOLUTION_OFFSET);

                            if (Resolution > 0)
                                Entry.VerticalResolution = (uint) Resolution;
                        }

                        if (Entry.VerticalResolution == 0)
                            Entry.VerticalResolution = Entry.HorizontalResolution;
                    }

                    Result.Add(Entry);
                }
            }

            return Result;
        }
    }
}
