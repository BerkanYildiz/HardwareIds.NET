namespace HardwareIds.NET.Native
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;

    using global::HardwareIds.NET.Structures.Components;

    [StructLayout(LayoutKind.Sequential)]
    internal struct TPM_DEVICE_INFO
    {
        public uint structVersion;
        public uint tpmVersion;
        public uint tpmInterfaceType;
        public uint tpmImpRevision;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TBS_CONTEXT_PARAMS2
    {
        public uint version;
        public uint flags;
    }

    /// <summary>
    /// Read-only TBS commands and bounded TPM 2.0 capability decoding.
    /// </summary>
    internal static partial class Tpm
    {
        internal static readonly SemaphoreSlim QueryLock = new(1, 1);
        internal const uint TBS_E_TPM_NOT_FOUND = 0x8028400F;
        private const uint TPM_CAP_ALGS = 0;
        private const uint TPM_CAP_PCRS = 5;
        private const uint TPM_CAP_TPM_PROPERTIES = 6;
        private const uint TPM_CC_GetCapability = 0x17A;
        private const uint ERROR_INVALID_DATA = 13;
        private const uint ERROR_MORE_DATA = 234;
        private const uint ERROR_CANCELLED = 1223;

        /// <summary>
        /// Identifies transient TPM and provider responses that a read can safely retry.
        /// </summary>
        internal static bool IsTransient(uint InStatus)
        {
            return InStatus == 0x908 || InStatus == 0x909 || InStatus == 0x90A || InStatus == 0x922 ||
                   InStatus == 0x80280908 || InStatus == 0x80280909 || InStatus == 0x8028090A || InStatus == 0x80280922 ||
                   InStatus == 0x80090030 || InStatus == 0x80284009 || InStatus == 0x8028400A;
        }

        /// <summary>
        /// Retries a transient read at most three times, leaving permanent failures intact.
        /// </summary>
        internal static uint RetryRead(Func<uint> InRead, CancellationToken InCancellationToken = default)
        {
            if (InCancellationToken.IsCancellationRequested)
                return ERROR_CANCELLED;

            var Status = InRead();

            for (var Retry = 0; Retry < 3 && IsTransient(Status) && !InCancellationToken.IsCancellationRequested; Retry++)
            {
                if (InCancellationToken.WaitHandle.WaitOne(25 * (Retry + 1)))
                    break;

                Status = InRead();
            }

            return Status;
        }

        [DllImport("tbs.dll")]
        internal static extern uint Tbsi_GetDeviceInfo(uint InSize, out TPM_DEVICE_INFO OutInfo);

        [DllImport("tbs.dll")]
        private static extern uint Tbsi_Context_Create(ref TBS_CONTEXT_PARAMS2 InParams, out IntPtr OutContext);

        [DllImport("tbs.dll")]
        private static extern uint Tbsip_Context_Close(IntPtr InContext);

        [DllImport("tbs.dll")]
        private static extern uint Tbsip_Submit_Command(IntPtr InContext, uint InLocality, uint InPriority, byte[] InCommand, uint InCommandSize, [Out] byte[] OutResponse, ref uint InOutResponseSize);

        /// <summary>
        /// Records an unavailable query without discarding other information.
        /// </summary>
        internal static void AddError(HwTpm InTpm, string InOperation, string InSource, uint InCode)
        {
            InTpm.Errors.Add(new HwTpmError { Operation = InOperation, Source = InSource, Code = InCode });
        }

        /// <summary>
        /// Builds an unauthenticated TPM2_GetCapability request.
        /// </summary>
        internal static byte[] BuildCapabilityCommand(uint InCapability, uint InProperty, uint InCount)
        {
            var Command = new byte[22];
            Command[0] = 0x80;
            Command[1] = 0x01;
            WriteBigEndian(Command, 2, (uint) Command.Length);
            WriteBigEndian(Command, 6, TPM_CC_GetCapability);
            WriteBigEndian(Command, 10, InCapability);
            WriteBigEndian(Command, 14, InProperty);
            WriteBigEndian(Command, 18, InCount);
            return Command;
        }

        /// <summary>
        /// Writes a 32-bit TPM wire value.
        /// </summary>
        private static void WriteBigEndian(byte[] InBuffer, int InOffset, uint InValue)
        {
            for (var I = 0; I < 4; I++)
                InBuffer[InOffset + I] = (byte) (InValue >> (24 - I * 8));
        }

        /// <summary>
        /// Reads a previously bounds-checked TPM wire value.
        /// </summary>
        private static uint ReadBigEndian(byte[] InBuffer, int InOffset, int InSize = 4)
        {
            uint Value = 0;

            for (var I = 0; I < InSize; I++)
                Value = (Value << 8) | InBuffer[InOffset + I];

            return Value;
        }

        /// <summary>
        /// Validates a complete capability page before publishing any entries.
        /// </summary>
        internal static bool ParseCapability(byte[] InResponse, uint InCapability, uint InStart, HwTpm InTpm, out bool OutMore, out uint OutNext)
        {
            OutMore = false;
            OutNext = InStart;

            if (InResponse.Length < 19 || ReadBigEndian(InResponse, 0, 2) != 0x8001 ||
                ReadBigEndian(InResponse, 2) != InResponse.Length || ReadBigEndian(InResponse, 6) != 0 ||
                InResponse[10] > 1 || ReadBigEndian(InResponse, 11) != InCapability)
                return false;

            var Count = ReadBigEndian(InResponse, 15);
            var Offset = 19;
            uint Previous = 0;
            var Properties = new List<HwTpmProperty>();
            var Algorithms = new List<HwTpmAlgorithm>();
            var Banks = new List<HwTpmPcrBank>();

            if (Count > 64 || (Count == 0 && InResponse[10] != 0) ||
                (InCapability == TPM_CAP_PCRS && (InStart != 0 || InResponse[10] != 0)) ||
                (InCapability != TPM_CAP_ALGS && InCapability != TPM_CAP_PCRS && InCapability != TPM_CAP_TPM_PROPERTIES))
                return false;

            for (uint I = 0; I < Count; I++)
            {
                var Size = InCapability == TPM_CAP_TPM_PROPERTIES ? 8 : InCapability == TPM_CAP_ALGS ? 6 : 3;

                if (InResponse.Length - Offset < Size)
                    return false;

                var Id = ReadBigEndian(InResponse, Offset, InCapability == TPM_CAP_TPM_PROPERTIES ? 4 : 2);

                if (Id == uint.MaxValue || (InCapability != TPM_CAP_PCRS && (Id < InStart || (I != 0 && Id <= Previous))) ||
                    (InCapability == TPM_CAP_TPM_PROPERTIES && (Id >> 8) != (InStart >> 8)))
                    return false;

                if (InCapability == TPM_CAP_TPM_PROPERTIES)
                {
                    Properties.Add(new HwTpmProperty { Property = Id, Value = ReadBigEndian(InResponse, Offset + 4) });
                }
                else if (InCapability == TPM_CAP_ALGS)
                {
                    Algorithms.Add(new HwTpmAlgorithm { Algorithm = Id, Attributes = ReadBigEndian(InResponse, Offset + 2) });
                }
                else
                {
                    var SelectionSize = InResponse[Offset + 2];
                    Size += SelectionSize;

                    if (SelectionSize == 0 || InResponse.Length - Offset < Size || Banks.Any(T => T.Algorithm == Id))
                        return false;

                    Banks.Add(new HwTpmPcrBank { Algorithm = Id, Selection = BitConverter.ToString(InResponse, Offset + 3, SelectionSize).Replace("-", "") });
                }

                Previous = Id;
                Offset += Size;
            }

            if (Offset != InResponse.Length)
                return false;

            InTpm.Properties.AddRange(Properties);
            InTpm.Algorithms.AddRange(Algorithms);
            InTpm.PcrBanks.AddRange(Banks);
            OutMore = InResponse[10] != 0;
            OutNext = Count == 0 ? InStart : Previous + 1;
            return true;
        }

        /// <summary>
        /// Reads fixed properties, variable state, algorithms and allocated PCR banks.
        /// </summary>
        internal static void ReadCapabilities(HwTpm InTpm, CancellationToken InCancellationToken)
        {
            var Parameters = new TBS_CONTEXT_PARAMS2 { version = 2, flags = 4 };
            var Context = IntPtr.Zero;
            var Status = RetryRead(() => Tbsi_Context_Create(ref Parameters, out Context), InCancellationToken);

            if (Status != 0)
            {
                if (!InCancellationToken.IsCancellationRequested)
                    AddError(InTpm, "Tbsi_Context_Create", "TBS", Status);
                return;
            }

            try
            {
                ReadCapability(Context, TPM_CAP_TPM_PROPERTIES, 0x100, 0x200, InTpm, InCancellationToken);
                ReadCapability(Context, TPM_CAP_TPM_PROPERTIES, 0x200, 0x300, InTpm, InCancellationToken);
                ReadCapability(Context, TPM_CAP_ALGS, 0, 0x10000, InTpm, InCancellationToken);
                ReadCapability(Context, TPM_CAP_PCRS, 0, 0x10000, InTpm, InCancellationToken);
                ApplyProperties(InTpm);
            }
            finally
            {
                Tbsip_Context_Close(Context);
            }
        }

        /// <summary>
        /// Follows capability pagination with strict response and progress bounds.
        /// </summary>
        private static void ReadCapability(IntPtr InContext, uint InCapability, uint InStart, uint InEnd, HwTpm InTpm, CancellationToken InCancellationToken)
        {
            var Operation = "TPM2_GetCapability/" + InCapability + "/" + InStart;

            for (var Page = 0; Page < 16 && !InCancellationToken.IsCancellationRequested; Page++)
            {
                var Command = BuildCapabilityCommand(InCapability, InStart, Math.Min(64u, InEnd - InStart));
                var Response = new byte[65536];
                var Size = (uint) Response.Length;
                var Source = "TBS";
                var Status = RetryRead(() =>
                {
                    Size = (uint) Response.Length;
                    Source = "TBS";
                    var Result = Tbsip_Submit_Command(InContext, 0, 200, Command, (uint) Command.Length, Response, ref Size);

                    if (Result != 0)
                        return Result;

                    Source = "Win32";

                    if (Size < 10 || Size > Response.Length || ReadBigEndian(Response, 0, 2) != 0x8001 ||
                        ReadBigEndian(Response, 2) != Size || (ReadBigEndian(Response, 6) != 0 && Size != 10))
                        return ERROR_INVALID_DATA;

                    Source = "TPM";
                    return ReadBigEndian(Response, 6);
                }, InCancellationToken);

                if (Status != 0)
                {
                    if (!InCancellationToken.IsCancellationRequested)
                        AddError(InTpm, Operation, Source, Status);
                    return;
                }

                if (InCancellationToken.IsCancellationRequested)
                    return;

                Array.Resize(ref Response, (int) Size);

                if (!ParseCapability(Response, InCapability, InStart, InTpm, out var More, out var Next))
                {
                    AddError(InTpm, Operation, "Win32", ERROR_INVALID_DATA);
                    return;
                }

                if (!More || Next >= InEnd)
                    return;

                InStart = Next;
            }

            if (!InCancellationToken.IsCancellationRequested)
                AddError(InTpm, Operation, "Win32", ERROR_MORE_DATA);
        }

        /// <summary>
        /// Converts a TCG four-character value to text.
        /// </summary>
        internal static string DecodeManufacturer(uint InValue)
        {
            var Bytes = new byte[4];
            WriteBigEndian(Bytes, 0, InValue);
            return Encoding.ASCII.GetString(Bytes).TrimEnd('\0', ' ');
        }

        /// <summary>
        /// Projects raw TPM properties into their named fields without inventing defaults.
        /// </summary>
        internal static void ApplyProperties(HwTpm InTpm)
        {
            uint? Get(uint InProperty) => InTpm.Properties.FirstOrDefault(T => T.Property == InProperty)?.Value;
            InTpm.Family = Get(0x100) is uint Family ? DecodeManufacturer(Family) : null;
            InTpm.SpecificationLevel = Get(0x101);
            InTpm.SpecificationRevision = Get(0x102);
            InTpm.SpecificationDayOfYear = Get(0x103);
            InTpm.SpecificationYear = Get(0x104);
            InTpm.ManufacturerId = Get(0x105);
            InTpm.Manufacturer = InTpm.ManufacturerId is uint Manufacturer ? DecodeManufacturer(Manufacturer) : null;

            var Vendor = new List<byte>();

            for (uint I = 0x106; I <= 0x109; I++)
            {
                if (Get(I) is not uint Word)
                    break;

                var Bytes = new byte[4];
                WriteBigEndian(Bytes, 0, Word);
                Vendor.AddRange(Bytes);
            }

            InTpm.VendorString = Vendor.Count == 0 ? null : Encoding.ASCII.GetString(Vendor.ToArray()).TrimEnd('\0', ' ');
            InTpm.FirmwareVersion1 = Get(0x10B);
            InTpm.FirmwareVersion2 = Get(0x10C);
            InTpm.PermanentFlags = Get(0x200);
            InTpm.StartupClearFlags = Get(0x201);
            InTpm.IsOwnerAuthSet = InTpm.PermanentFlags is uint Owner ? (Owner & 1) != 0 : null;
            InTpm.IsEndorsementAuthSet = InTpm.PermanentFlags is uint Endorsement ? (Endorsement & 2) != 0 : null;
            InTpm.IsLockoutAuthSet = InTpm.PermanentFlags is uint Lockout ? (Lockout & 4) != 0 : null;
            InTpm.IsInLockout = InTpm.PermanentFlags is uint State ? (State & 0x200) != 0 : null;
            InTpm.LockoutCounter = Get(0x20E);
            InTpm.MaxAuthFail = Get(0x20F);
            InTpm.LockoutInterval = Get(0x210);
            InTpm.LockoutRecovery = Get(0x211);
        }
    }
}
