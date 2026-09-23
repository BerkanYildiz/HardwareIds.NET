namespace HardwareIds.NET
{
    using System;
    using System.Linq;

    using global::HardwareIds.NET.Native;
    using global::HardwareIds.NET.Structures;
    using global::HardwareIds.NET.Structures.Components;

    using Microsoft.Win32;

    public static partial class HardwareIds
    {
        internal static void RetrieveProcessors(Hwid InHwid, SmbiosTable? InSmbios)
        {
            try
            {
                if (InSmbios is null)
                    return;

                // 
                // SMBIOS type 4: Processor Information, keeping only populated sockets (status bit 6).
                // 

                var Processors = InSmbios.OfType(4).Where(T => T.Length <= 0x18 || (T.GetByte(0x18) & 0x40) != 0).ToList();

                if (Processors.Count == 0)
                    return;

                // 
                // WMI takes the vendor, the name and the clock speed from the registry (the CPUID strings and the boot-time calibration), and the
                // identifier from the SMBIOS record (which hypervisors may leave zeroed). CPUID is only used when the record has no identifier.
                // 

                string? RegistryName;
                string? RegistryVendor;
                int? RegistrySpeed;

                using (var ProcessorKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    RegistryName = (ProcessorKey?.GetValue("ProcessorNameString") as string)?.Trim();
                    RegistryVendor = ProcessorKey?.GetValue("VendorIdentifier") as string;
                    RegistrySpeed = ProcessorKey?.GetValue("~MHz") as int?;
                }

                var CpuId = GetProcessorIdFromCpuId();
                var (Cores, LogicalProcessors, Packages) = GetProcessorTopology();
                var PackageCount = Math.Max(Packages, 1);

                foreach (var Processor in Processors)
                {
                    var Index = InHwid.Processors.Count;
                    var CurrentSpeed = Processor.GetWord(0x16);
                    var CoreCount = Processor.GetByte(0x23) == 0xFF ? Processor.GetWord(0x2A) : Processor.GetByte(0x23);
                    var ThreadCount = Processor.GetByte(0x25) == 0xFF ? Processor.GetWord(0x2E) : Processor.GetByte(0x25);

                    InHwid.Processors.Add(new HwProcessor
                    {
                        Id = Index,
                        Manufacturer = RegistryVendor ?? Processor.GetString(0x07),
                        Model = RegistryName ?? Processor.GetString(0x10)?.Trim(),
                        ModelNumber = FormatProcessorId(Processor.GetBytes(0x08, 8)) ?? CpuId,
                        Socket = Processor.GetString(0x04),
                        SerialNumber = Processor.GetString(0x20),
                        PartNumber = Processor.GetString(0x22),
                        ClockSpeed = $"{(RegistrySpeed > 0 ? RegistrySpeed.Value : CurrentSpeed)} MHz",
                        Voltage = $"{GetProcessorVoltage(Processor.GetByte(0x11)):0.0} V",
                        Channel = $"CPU{Index}",
                        NumberOfCores = Cores > 0 ? (uint) Math.Max(Cores / PackageCount, 1) : (CoreCount != 0 ? CoreCount : (uint) Environment.ProcessorCount),
                        NumberOfLogicalProcessors = LogicalProcessors > 0 ? (uint) Math.Max(LogicalProcessors / PackageCount, 1) : (ThreadCount != 0 ? ThreadCount : (uint) Environment.ProcessorCount),
                    });
                }
            }
            catch (Exception)
            {
                // ...
            }
        }

        /// <summary>
        /// Gets the processor identifier (EDX then EAX of CPUID leaf 1, as WMI formats it) from the CPUID instruction, when available.
        /// </summary>
        internal static string? GetProcessorIdFromCpuId()
        {
        #if NET
            if (System.Runtime.Intrinsics.X86.X86Base.IsSupported)
            {
                var (Eax, _, _, Edx) = System.Runtime.Intrinsics.X86.X86Base.CpuId(1, 0);
                return $"{Edx:X8}{Eax:X8}";
            }
        #endif

            return null;
        }

        /// <summary>
        /// Formats the 8-byte SMBIOS processor identifier (EAX then EDX) the way WMI does (EDX then EAX).
        /// </summary>
        internal static string? FormatProcessorId(byte[]? InProcessorId)
        {
            return InProcessorId != null ? $"{BitConverter.ToUInt32(InProcessorId, 4):X8}{BitConverter.ToUInt32(InProcessorId, 0):X8}" : null;
        }

        /// <summary>
        /// Decodes the SMBIOS processor voltage byte into volts.
        /// </summary>
        internal static double GetProcessorVoltage(byte InVoltage)
        {
            if ((InVoltage & 0x80) != 0)
                return (InVoltage & 0x7F) / 10.0;

            if ((InVoltage & 0x01) != 0)
                return 5.0;

            if ((InVoltage & 0x02) != 0)
                return 3.3;

            if ((InVoltage & 0x04) != 0)
                return 2.9;

            return 0.0;
        }

        /// <summary>
        /// Counts the physical cores, logical processors and packages known to the operating system.
        /// </summary>
        internal static unsafe (int Cores, int LogicalProcessors, int Packages) GetProcessorTopology()
        {
            const uint RelationAll = 0xFFFF;
            const int RelationProcessorCore = 0;
            const int RelationProcessorPackage = 3;

            uint Length = 0;
            Kernel32.GetLogicalProcessorInformationEx(RelationAll, null, ref Length);

            if (Length == 0)
                return (0, 0, 0);

            var Buffer = new byte[Length];

            fixed (byte* BufferPtr = Buffer)
            {
                if (!Kernel32.GetLogicalProcessorInformationEx(RelationAll, BufferPtr, ref Length))
                    return (0, 0, 0);
            }

            var Cores = 0;
            var LogicalProcessors = 0;
            var Packages = 0;
            var Offset = 0;

            while (Offset + 8 <= Length)
            {
                var Relationship = BitConverter.ToInt32(Buffer, Offset);
                var Size = BitConverter.ToInt32(Buffer, Offset + 4);

                if (Size <= 0)
                    break;

                if (Relationship == RelationProcessorCore)
                {
                    Cores++;

                    var GroupCount = BitConverter.ToUInt16(Buffer, Offset + 8 + 22);
                    var MaskOffset = Offset + 8 + 24;

                    for (var Group = 0; Group < GroupCount && MaskOffset + IntPtr.Size <= Length; Group++)
                    {
                        var Mask = IntPtr.Size == 8 ? BitConverter.ToUInt64(Buffer, MaskOffset) : BitConverter.ToUInt32(Buffer, MaskOffset);

                        for (; Mask != 0; Mask &= Mask - 1)
                            LogicalProcessors++;

                        MaskOffset += IntPtr.Size + 8;
                    }
                }
                else if (Relationship == RelationProcessorPackage)
                {
                    Packages++;
                }

                Offset += Size;
            }

            return (Cores, LogicalProcessors, Packages);
        }
    }
}
