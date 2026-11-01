namespace HardwareIds.NET.Native
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;

    using global::HardwareIds.NET.Structures.Components;

    [StructLayout(LayoutKind.Sequential)]
    internal struct NCryptKeyName
    {
        public IntPtr pszName;
        public IntPtr pszAlgid;
        public uint dwLegacyKeySpec;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CERT_CONTEXT
    {
        public uint dwCertEncodingType;
        public IntPtr pbCertEncoded;
        public uint cbCertEncoded;
        public IntPtr pCertInfo;
        public IntPtr hCertStore;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CRYPT_KEY_PROV_INFO
    {
        public IntPtr pwszContainerName;
        public IntPtr pwszProvName;
        public uint dwProvType;
        public uint dwFlags;
        public uint cProvParam;
        public IntPtr rgProvParam;
        public uint dwKeySpec;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CRYPT_DATA_BLOB
    {
        public uint cbData;
        public IntPtr pbData;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CRYPT_ALGORITHM_IDENTIFIER
    {
        public IntPtr pszObjId;
        public CRYPT_DATA_BLOB Parameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CERT_INFO_PREFIX
    {
        public uint dwVersion;
        public CRYPT_DATA_BLOB SerialNumber;
        public CRYPT_ALGORITHM_IDENTIFIER SignatureAlgorithm;
        public CRYPT_DATA_BLOB Issuer;
        public System.Runtime.InteropServices.ComTypes.FILETIME NotBefore;
        public System.Runtime.InteropServices.ComTypes.FILETIME NotAfter;
    }

    internal static partial class Tpm
    {
        private const string PlatformProvider = "Microsoft Platform Crypto Provider";
        private const uint NCRYPT_MACHINE_KEY_FLAG = 0x20;
        private const uint NCRYPT_SILENT_FLAG = 0x40;
        private const uint NTE_NO_MORE_ITEMS = 0x8009002A;
        private const uint MaxPropertySize = 1024 * 1024;

        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        private static extern uint NCryptOpenStorageProvider(out IntPtr OutProvider, string InName, uint InFlags);

        [DllImport("ncrypt.dll")]
        private static extern uint NCryptFreeObject(IntPtr InObject);

        [DllImport("ncrypt.dll")]
        private static extern uint NCryptFreeBuffer(IntPtr InBuffer);

        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        private static extern uint NCryptGetProperty(IntPtr InObject, string InName, [Out] byte[]? OutBuffer, uint InSize, out uint OutSize, uint InFlags);

        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode, EntryPoint = "NCryptGetProperty")]
        private static extern uint NCryptGetStoreProperty(IntPtr InObject, string InName, out IntPtr OutStore, uint InSize, out uint OutSize, uint InFlags);

        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        private static extern uint NCryptEnumKeys(IntPtr InProvider, string? InScope, out IntPtr OutName, ref IntPtr InOutState, uint InFlags);

        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        private static extern uint NCryptOpenKey(IntPtr InProvider, out IntPtr OutKey, string InName, uint InLegacyKeySpec, uint InFlags);

        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        private static extern uint NCryptExportKey(IntPtr InKey, IntPtr InExportKey, string InBlobType, IntPtr InParameters, [Out] byte[]? OutBuffer, uint InSize, out uint OutSize, uint InFlags);

        [DllImport("crypt32.dll", SetLastError = true)]
        private static extern IntPtr CertCreateCertificateContext(uint InEncoding, byte[] InEncoded, uint InSize);

        [DllImport("crypt32.dll", SetLastError = true)]
        private static extern IntPtr CertEnumCertificatesInStore(IntPtr InStore, IntPtr InPrevious);

        [DllImport("crypt32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CertCloseStore(IntPtr InStore, uint InFlags);

        [DllImport("crypt32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CertFreeCertificateContext(IntPtr InContext);

        [DllImport("crypt32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CertGetCertificateContextProperty(IntPtr InContext, uint InProperty, IntPtr OutBuffer, ref uint InOutSize);

        /// <summary>
        /// Reads public provider properties and existing keys using silent CNG operations.
        /// </summary>
        internal static void ReadPublicMaterial(HwTpm InTpm, CancellationToken InCancellationToken)
        {
            var Provider = IntPtr.Zero;
            var Status = RetryRead(() => NCryptOpenStorageProvider(out Provider, PlatformProvider, 0), InCancellationToken);

            if (Status != 0)
            {
                if (!InCancellationToken.IsCancellationRequested)
                    AddError(InTpm, "NCryptOpenStorageProvider", "NCrypt", Status);
                return;
            }

            try
            {
                if (InTpm.ManufacturerId == null)
                {
                    var Manufacturer = GetProperty(Provider, "PCP_TPM_MANUFACTURER_ID", InTpm, "Provider", InCancellationToken);

                    if (Manufacturer != null && !ApplyManufacturer(InTpm, Manufacturer))
                        AddError(InTpm, "Provider/PCP_TPM_MANUFACTURER_ID", "Win32", ERROR_INVALID_DATA);
                }

                if (InTpm.FirmwareVersion1 == null || InTpm.FirmwareVersion2 == null)
                {
                    var Firmware = GetProperty(Provider, "PCP_TPM_FW_VERSION", InTpm, "Provider", InCancellationToken);

                    if (Firmware != null && !ApplyFirmware(InTpm, Firmware))
                        AddError(InTpm, "Provider/PCP_TPM_FW_VERSION", "Win32", ERROR_INVALID_DATA);
                }

                foreach (var Property in new[] { "PCP_EKPUB", "PCP_RSA_EKPUB", "PCP_ECC_EKPUB", "PCP_SRKPUB" })
                {
                    if (InCancellationToken.IsCancellationRequested)
                        return;

                    var Blob = GetProperty(Provider, Property, InTpm, "Provider", InCancellationToken);

                    if (Blob == null)
                        continue;

                    var Key = new HwTpmKey { Kind = Property == "PCP_SRKPUB" ? "StorageRoot" : "Endorsement", Name = Property, Scope = "Platform" };
                    if (SetPublicBlob(Key, Blob))
                        InTpm.Keys.Add(Key);
                    else
                        AddError(InTpm, "Provider/" + Property, "Win32", ERROR_INVALID_DATA);
                }

                foreach (var Property in new[] { "PCP_EKCERT", "PCP_EKNVCERT", "PCP_RSA_EKCERT", "PCP_RSA_EKNVCERT", "PCP_ECC_EKCERT", "PCP_ECC_EKNVCERT" })
                {
                    if (InCancellationToken.IsCancellationRequested)
                        return;

                    ReadCertificateStore(Provider, Property, InTpm, InCancellationToken);
                }

                ReadPersistedKeys(Provider, false, InTpm, InCancellationToken);
                ReadPersistedKeys(Provider, true, InTpm, InCancellationToken);
                ReadAssociatedCertificates(StoreLocation.CurrentUser, InTpm, InCancellationToken);
                ReadAssociatedCertificates(StoreLocation.LocalMachine, InTpm, InCancellationToken);
            }
            finally
            {
                NCryptFreeObject(Provider);
                InTpm.Keys = InTpm.Keys.OrderBy(T => T.Scope, StringComparer.Ordinal).ThenBy(T => T.Name, StringComparer.Ordinal).ToList();
                InTpm.Certificates = InTpm.Certificates.OrderBy(T => T.Source, StringComparer.Ordinal).ThenBy(T => T.Sha256, StringComparer.Ordinal).ToList();

                foreach (var Key in InTpm.Keys)
                    Key.Certificates = Key.Certificates.OrderBy(T => T.Source, StringComparer.Ordinal).ThenBy(T => T.Sha256, StringComparer.Ordinal).ToList();
            }
        }

        /// <summary>
        /// Reads a bounded binary property and retains its native error code.
        /// </summary>
        private static byte[]? GetProperty(IntPtr InObject, string InName, HwTpm InTpm, string InOperation, CancellationToken InCancellationToken)
        {
            uint Size = 0;
            var Status = RetryRead(() => NCryptGetProperty(InObject, InName, null, 0, out Size, NCRYPT_SILENT_FLAG), InCancellationToken);

            if (Status != 0 || Size == 0 || Size > MaxPropertySize)
            {
                if (!InCancellationToken.IsCancellationRequested)
                    AddError(InTpm, InOperation + "/" + InName, Status == 0 ? "Win32" : "NCrypt", Status == 0 ? ERROR_INVALID_DATA : Status);
                return null;
            }

            var Buffer = new byte[Size];
            uint Written = 0;
            Status = RetryRead(() => NCryptGetProperty(InObject, InName, Buffer, Size, out Written, NCRYPT_SILENT_FLAG), InCancellationToken);

            if (Status != 0 || Written == 0 || Written > Size)
            {
                if (!InCancellationToken.IsCancellationRequested)
                    AddError(InTpm, InOperation + "/" + InName, Status == 0 ? "Win32" : "NCrypt", Status == 0 ? ERROR_INVALID_DATA : Status);
                return null;
            }

            Array.Resize(ref Buffer, (int) Written);
            return Buffer;
        }

        /// <summary>
        /// Reads a DWORD property without treating missing data as zero.
        /// </summary>
        private static uint? GetNumber(IntPtr InObject, string InName, HwTpm InTpm, string InOperation, CancellationToken InCancellationToken)
        {
            var Bytes = GetProperty(InObject, InName, InTpm, InOperation, InCancellationToken);

            if (Bytes != null && Bytes.Length != 4)
                AddError(InTpm, InOperation + "/" + InName, "Win32", ERROR_INVALID_DATA);

            return Bytes?.Length == 4 ? BitConverter.ToUInt32(Bytes, 0) : null;
        }

        /// <summary>
        /// Computes a lower-case SHA-256 fingerprint of the exact supplied encoding.
        /// </summary>
        internal static string Sha256(byte[] InBytes)
        {
            using var Hash = SHA256.Create();
            return BitConverter.ToString(Hash.ComputeHash(InBytes)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Validates a complete RSA/ECC public blob before exposing its bytes and metadata.
        /// </summary>
        internal static bool SetPublicBlob(HwTpmKey InKey, byte[] InBlob)
        {
            if (InBlob.Length < 8 || InBlob.Length > MaxPropertySize)
                return false;

            var Magic = BitConverter.ToUInt32(InBlob, 0);
            var Length = BitConverter.ToUInt32(InBlob, 4);
            string Algorithm;
            uint? Bits;

            if (Magic == 0x31415352)
            {
                if (InBlob.Length < 24)
                    return false;

                var ExponentSize = BitConverter.ToUInt32(InBlob, 8);
                var ModulusSize = BitConverter.ToUInt32(InBlob, 12);

                if (Length == 0 || ExponentSize == 0 || ModulusSize != ((ulong) Length + 7) / 8 ||
                    24ul + ExponentSize + ModulusSize != (ulong) InBlob.Length ||
                    BitConverter.ToUInt32(InBlob, 16) != 0 || BitConverter.ToUInt32(InBlob, 20) != 0)
                    return false;

                Algorithm = "RSA";
                Bits = Length;
            }
            else if ((Magic == 0x504B4345 || Magic == 0x50444345) && Length == 1)
            {
                // The full ECC format stores a version at offset 4, followed by explicit curve parameters.
                if (InBlob.Length < 32)
                    return false;

                var CurveType = BitConverter.ToUInt32(InBlob, 8);
                var FieldSize = BitConverter.ToUInt32(InBlob, 16);
                var OrderSize = BitConverter.ToUInt32(InBlob, 20);
                var CofactorSize = BitConverter.ToUInt32(InBlob, 24);
                var SeedSize = BitConverter.ToUInt32(InBlob, 28);

                if (CurveType < 1 || CurveType > 3 || FieldSize == 0 || OrderSize == 0 || CofactorSize == 0 ||
                    32ul + 7ul * FieldSize + OrderSize + CofactorSize + SeedSize != (ulong) InBlob.Length)
                    return false;

                uint FieldBits = 0;

                for (var I = 0; I < FieldSize; I++)
                {
                    var Value = InBlob[32 + I];

                    if (Value == 0)
                        continue;

                    FieldBits = (FieldSize - (uint) I - 1) * 8;
                    for (; Value != 0; Value >>= 1) FieldBits++;
                    break;
                }

                if (FieldBits == 0)
                    return false;

                Algorithm = Magic == 0x504B4345 ? "ECDH" : "ECDSA";
                Bits = FieldBits;
            }
            else
            {
                switch (Magic)
                {
                    case 0x314B4345: case 0x31534345: Bits = 256; break;
                    case 0x334B4345: case 0x33534345: Bits = 384; break;
                    case 0x354B4345: case 0x35534345: Bits = 521; break;
                    case 0x504B4345: case 0x50444345: Bits = null; break;
                    default: return false;
                }

                if (Length == 0 || 8ul + 2ul * Length != (ulong) InBlob.Length ||
                    (Bits != null && Length != (Bits.Value + 7) / 8))
                    return false;

                Algorithm = (Magic & 0x00FFFFFF) == 0x004B4345 ? "ECDH" : "ECDSA";
            }

            InKey.Algorithm ??= Algorithm;
            InKey.Length ??= Bits;
            InKey.PublicKeyBlob = Convert.ToBase64String(InBlob);
            InKey.PublicKeySha256 = Sha256(InBlob);
            return true;
        }

        /// <summary>
        /// Decodes the PCP manufacturer string into its four-byte TCG identifier.
        /// </summary>
        internal static bool ApplyManufacturer(HwTpm InTpm, byte[] InBytes)
        {
            if (InBytes.Length < 4 || InBytes.Length > 10 || InBytes.Length % 2 != 0 ||
                InBytes[InBytes.Length - 1] != 0 || InBytes[InBytes.Length - 2] != 0)
                return false;

            var Text = Encoding.Unicode.GetString(InBytes).TrimEnd('\0');

            if (Text.Length == 0 || Text.Length > 4 || Text.Any(T => T < 0x20 || T > 0x7E))
                return false;

            uint Id = 0;

            for (var I = 0; I < 4; I++)
                Id = (Id << 8) | (I < Text.Length ? Text[I] : 0u);

            InTpm.ManufacturerId = Id;
            InTpm.Manufacturer = DecodeManufacturer(Id);
            return true;
        }

        /// <summary>
        /// Decodes the four UINT16 words returned by PCP_TPM_FW_VERSION.
        /// </summary>
        internal static bool ApplyFirmware(HwTpm InTpm, byte[] InBytes)
        {
            if (InBytes.Length != 8)
                return false;

            InTpm.FirmwareVersion1 ??= ((uint) BitConverter.ToUInt16(InBytes, 0) << 16) | BitConverter.ToUInt16(InBytes, 2);
            InTpm.FirmwareVersion2 ??= ((uint) BitConverter.ToUInt16(InBytes, 4) << 16) | BitConverter.ToUInt16(InBytes, 6);
            return true;
        }

        /// <summary>
        /// Decodes an X.509 certificate without building a chain or contacting its issuer.
        /// </summary>
        internal static HwTpmCertificate? ParseCertificate(byte[] InDer, string InSource)
        {
            if (!IsDerSequence(InDer))
                return null;

            var Handle = CertCreateCertificateContext(1, InDer, (uint) InDer.Length);

            if (Handle == IntPtr.Zero)
                return null;

            try
            {
                using var Certificate = new X509Certificate2(Handle);
                var Context = Marshal.PtrToStructure<CERT_CONTEXT>(Handle);
                var Info = Marshal.PtrToStructure<CERT_INFO_PREFIX>(Context.pCertInfo);

                return new HwTpmCertificate
                {
                    Source = InSource,
                    Subject = Certificate.Subject,
                    Issuer = Certificate.Issuer,
                    SerialNumber = Certificate.SerialNumber,
                    NotBefore = CertificateTime(Info.NotBefore),
                    NotAfter = CertificateTime(Info.NotAfter),
                    SignatureAlgorithm = Certificate.SignatureAlgorithm.Value,
                    PublicKeyAlgorithm = Certificate.PublicKey.Oid.Value,
                    Sha256 = Sha256(InDer),
                    Der = Convert.ToBase64String(InDer),
                };
            }
            catch (CryptographicException)
            {
                return null;
            }
            finally
            {
                CertFreeCertificateContext(Handle);
            }
        }

        /// <summary>
        /// Checks that one bounded DER sequence consumes the complete certificate buffer.
        /// </summary>
        internal static bool IsDerSequence(byte[] InBytes)
        {
            if (InBytes.Length < 2 || InBytes.Length > MaxPropertySize || InBytes[0] != 0x30)
                return false;

            var Offset = 2;
            uint Length = InBytes[1];

            if (Length >= 0x80)
            {
                var Count = (int) (Length & 0x7F);

                if (Count == 0 || Count > 4 || Count > InBytes.Length - Offset || InBytes[Offset] == 0)
                    return false;

                Length = ReadBigEndian(InBytes, Offset, Count);
                Offset += Count;

                if (Length < 0x80)
                    return false;
            }

            return Length == InBytes.Length - Offset;
        }

        /// <summary>
        /// Reads UTC validity directly, avoiding local-time overflow at year 9999.
        /// </summary>
        private static string? CertificateTime(System.Runtime.InteropServices.ComTypes.FILETIME InTime)
        {
            var Ticks = ((long) InTime.dwHighDateTime << 32) | (uint) InTime.dwLowDateTime;

            if (Ticks < 0 || Ticks > 2650467743999999999L)
                return null;

            return DateTime.FromFileTimeUtc(Ticks).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Copies a certificate before its native enumeration context is released.
        /// </summary>
        private static HwTpmCertificate? ReadCertificate(IntPtr InContext, string InSource)
        {
            var Context = Marshal.PtrToStructure<CERT_CONTEXT>(InContext);

            if (Context.cbCertEncoded == 0 || Context.cbCertEncoded > MaxPropertySize || Context.pbCertEncoded == IntPtr.Zero)
                return null;

            var Bytes = new byte[Context.cbCertEncoded];
            Marshal.Copy(Context.pbCertEncoded, Bytes, 0, Bytes.Length);
            return ParseCertificate(Bytes, InSource);
        }

        /// <summary>
        /// Reads PCP certificate-store handles directly; these properties are not DER blobs.
        /// </summary>
        private static void ReadCertificateStore(IntPtr InProvider, string InProperty, HwTpm InTpm, CancellationToken InCancellationToken)
        {
            if (InCancellationToken.IsCancellationRequested)
                return;

            var Status = NCryptGetStoreProperty(InProvider, InProperty, out var Store, (uint) IntPtr.Size, out var Size, NCRYPT_SILENT_FLAG);

            if (Status != 0)
            {
                AddError(InTpm, "Provider/" + InProperty, "NCrypt", Status);
                return;
            }

            if (Store == IntPtr.Zero || Size != IntPtr.Size)
            {
                if (Store != IntPtr.Zero)
                    CertCloseStore(Store, 0);

                AddError(InTpm, "Provider/" + InProperty, "Win32", ERROR_INVALID_DATA);
                return;
            }

            var Context = IntPtr.Zero;

            try
            {
                for (var I = 0; !InCancellationToken.IsCancellationRequested; I++)
                {
                    Context = CertEnumCertificatesInStore(Store, Context);

                    if (Context == IntPtr.Zero)
                    {
                        var Error = unchecked((uint) Marshal.GetLastWin32Error());

                        if (Error != 0x80092004)
                            AddError(InTpm, InProperty + "/CertEnumCertificatesInStore", "Win32", Error);

                        return;
                    }

                    if (I == 1024)
                    {
                        AddError(InTpm, InProperty + "/CertEnumCertificatesInStore", "Win32", ERROR_MORE_DATA);
                        return;
                    }

                    var Certificate = ReadCertificate(Context, InProperty);

                    if (Certificate != null)
                        InTpm.Certificates.Add(Certificate);
                    else
                        AddError(InTpm, InProperty + "/Certificate", "Win32", ERROR_INVALID_DATA);
                }

            }
            finally
            {
                if (Context != IntPtr.Zero)
                    CertFreeCertificateContext(Context);

                CertCloseStore(Store, 0);
            }
        }

        /// <summary>
        /// Exports only the public part of an existing CNG key.
        /// </summary>
        private static byte[]? ExportPublicKey(IntPtr InKey, HwTpm InTpm, string InOperation, CancellationToken InCancellationToken)
        {
            uint Size = 0;
            var Status = RetryRead(() => NCryptExportKey(InKey, IntPtr.Zero, "PUBLICBLOB", IntPtr.Zero, null, 0, out Size, NCRYPT_SILENT_FLAG), InCancellationToken);

            if (Status != 0 || Size == 0 || Size > MaxPropertySize)
            {
                if (!InCancellationToken.IsCancellationRequested)
                    AddError(InTpm, InOperation + "/NCryptExportKey", Status == 0 ? "Win32" : "NCrypt", Status == 0 ? ERROR_INVALID_DATA : Status);
                return null;
            }

            var Blob = new byte[Size];
            uint Written = 0;
            Status = RetryRead(() => NCryptExportKey(InKey, IntPtr.Zero, "PUBLICBLOB", IntPtr.Zero, Blob, Size, out Written, NCRYPT_SILENT_FLAG), InCancellationToken);

            if (Status != 0 || Written == 0 || Written > Size)
            {
                if (!InCancellationToken.IsCancellationRequested)
                    AddError(InTpm, InOperation + "/NCryptExportKey", Status == 0 ? "Win32" : "NCrypt", Status == 0 ? ERROR_INVALID_DATA : Status);
                return null;
            }

            Array.Resize(ref Blob, (int) Written);
            return Blob;
        }

        /// <summary>
        /// Enumerates existing user or machine keys, retaining metadata when opening is denied.
        /// </summary>
        private static void ReadPersistedKeys(IntPtr InProvider, bool InMachine, HwTpm InTpm, CancellationToken InCancellationToken)
        {
            var Flags = NCRYPT_SILENT_FLAG | (InMachine ? NCRYPT_MACHINE_KEY_FLAG : 0);
            var Scope = InMachine ? "LocalMachine" : "CurrentUser";
            var State = IntPtr.Zero;

            try
            {
                for (var I = 0; !InCancellationToken.IsCancellationRequested; I++)
                {
                    var Status = NCryptEnumKeys(InProvider, null, out var NamePointer, ref State, Flags);

                    try
                    {
                        if (Status != 0)
                        {
                            if (Status != NTE_NO_MORE_ITEMS)
                                AddError(InTpm, Scope + "/NCryptEnumKeys", "NCrypt", Status);

                            return;
                        }

                        if (NamePointer == IntPtr.Zero)
                        {
                            AddError(InTpm, Scope + "/NCryptEnumKeys", "Win32", ERROR_INVALID_DATA);
                            return;
                        }

                        if (I == 1024)
                        {
                            AddError(InTpm, Scope + "/NCryptEnumKeys", "Win32", ERROR_MORE_DATA);
                            return;
                        }

                        var Name = Marshal.PtrToStructure<NCryptKeyName>(NamePointer);
                        var Key = new HwTpmKey { Kind = "Persisted", Name = Marshal.PtrToStringUni(Name.pszName), Algorithm = Marshal.PtrToStringUni(Name.pszAlgid), Scope = Scope };
                        InTpm.Keys.Add(Key);

                        if (Key.Name == null)
                            continue;

                        var Operation = Scope + "/" + Key.Name;
                        var Handle = IntPtr.Zero;
                        Status = RetryRead(() => NCryptOpenKey(InProvider, out Handle, Key.Name, Name.dwLegacyKeySpec, Flags), InCancellationToken);

                        if (Status != 0)
                        {
                            if (!InCancellationToken.IsCancellationRequested)
                                AddError(InTpm, Operation + "/NCryptOpenKey", "NCrypt", Status);
                            continue;
                        }

                        try
                        {
                            Key.Length = GetNumber(Handle, "Length", InTpm, Operation, InCancellationToken);
                            Key.Usage = GetNumber(Handle, "Key Usage", InTpm, Operation, InCancellationToken);
                            Key.TpmUsage = GetNumber(Handle, "PCP_KEY_USAGE_POLICY", InTpm, Operation, InCancellationToken);
                            var UniqueName = GetProperty(Handle, "Unique Name", InTpm, Operation, InCancellationToken);
                            Key.UniqueName = UniqueName != null && UniqueName.Length % 2 == 0 ? Encoding.Unicode.GetString(UniqueName).TrimEnd('\0') : null;
                            var Blob = ExportPublicKey(Handle, InTpm, Operation, InCancellationToken);

                            if (Blob != null && !SetPublicBlob(Key, Blob))
                                AddError(InTpm, Operation + "/PublicKeyBlob", "Win32", ERROR_INVALID_DATA);

                            var Der = GetProperty(Handle, "SmartCardKeyCertificate", InTpm, Operation, InCancellationToken);

                            if (Der != null)
                            {
                                var Certificate = ParseCertificate(Der, "KeyProperty");

                                if (Certificate != null)
                                    Key.Certificates.Add(Certificate);
                                else
                                    AddError(InTpm, Operation + "/Certificate", "Win32", ERROR_INVALID_DATA);
                            }
                        }
                        finally
                        {
                            NCryptFreeObject(Handle);
                        }
                    }
                    finally
                    {
                        if (NamePointer != IntPtr.Zero)
                            NCryptFreeBuffer(NamePointer);
                    }
                }

            }
            finally
            {
                if (State != IntPtr.Zero)
                    NCryptFreeBuffer(State);
            }
        }

        /// <summary>
        /// Matches Personal-store certificates by provider and container without opening private keys.
        /// </summary>
        private static void ReadAssociatedCertificates(StoreLocation InLocation, HwTpm InTpm, CancellationToken InCancellationToken)
        {
            if (InCancellationToken.IsCancellationRequested)
                return;

            var Scope = InLocation == StoreLocation.LocalMachine ? "LocalMachine" : "CurrentUser";

            try
            {
                using var Store = new X509Store(StoreName.My, InLocation);
                Store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                var Certificates = Store.Certificates;

                try
                {
                    foreach (var Certificate in Certificates)
                    {
                        if (InCancellationToken.IsCancellationRequested)
                            return;

                        uint Size = 0;

                        if (!CertGetCertificateContextProperty(Certificate.Handle, 2, IntPtr.Zero, ref Size) || Size < Marshal.SizeOf<CRYPT_KEY_PROV_INFO>() || Size > MaxPropertySize)
                            continue;

                        var Buffer = Marshal.AllocHGlobal((int) Size);

                        try
                        {
                            var Capacity = Size;

                            if (!CertGetCertificateContextProperty(Certificate.Handle, 2, Buffer, ref Size) || Size < Marshal.SizeOf<CRYPT_KEY_PROV_INFO>() || Size > Capacity)
                                continue;

                            var Info = Marshal.PtrToStructure<CRYPT_KEY_PROV_INFO>(Buffer);

                            if (Info.dwProvType != 0 || !string.Equals(Marshal.PtrToStringUni(Info.pwszProvName), PlatformProvider, StringComparison.OrdinalIgnoreCase))
                                continue;

                            var Name = Marshal.PtrToStringUni(Info.pwszContainerName);

                            if (Name == null)
                                continue;

                            var KeyScope = (Info.dwFlags & NCRYPT_MACHINE_KEY_FLAG) != 0 ? "LocalMachine" : "CurrentUser";
                            var Key = InTpm.Keys.FirstOrDefault(T => T.Kind == "Persisted" && T.Scope == KeyScope && T.Name == Name);

                            if (Key == null)
                                continue;

                            var Parsed = ParseCertificate(Certificate.RawData, Scope + "/My");

                            if (Parsed != null)
                                Key.Certificates.Add(Parsed);
                            else
                                AddError(InTpm, Scope + "/My/Certificate", "Win32", ERROR_INVALID_DATA);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(Buffer);
                        }
                    }
                }
                finally
                {
                    foreach (var Certificate in Certificates)
                        Certificate.Dispose();
                }
            }
            catch (CryptographicException Error)
            {
                AddError(InTpm, Scope + "/My", "Win32", unchecked((uint) Error.HResult));
            }
        }
    }
}
