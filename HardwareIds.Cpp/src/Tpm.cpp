#include "Internal.hpp"

#include <bcrypt.h>
#include <ncrypt.h>
#include <tbs.h>
#include <wincrypt.h>

#include <algorithm>
#include <cstring>
#include <limits>
#include <mutex>
#include <thread>
#include <tuple>

namespace HardwareIds::Detail
{
    namespace
    {
        constexpr std::uint32_t CapAlgorithms = 0;
        constexpr std::uint32_t CapPcrs = 5;
        constexpr std::uint32_t CapProperties = 6;
        constexpr DWORD MaxPropertySize = 1024 * 1024;
        std::timed_mutex QueryLock;

        /// <summary>
        /// Retries transient read failures at most three times.
        /// </summary>
        template <typename TRead>
        std::uint32_t RetryRead(TRead&& InRead, std::stop_token InStopToken = {})
        {
            if (InStopToken.stop_requested()) return ERROR_CANCELLED;
            const auto IsTransient = [](std::uint32_t Status)
            {
                return Status == 0x908 || Status == 0x909 || Status == 0x90A || Status == 0x922 ||
                       Status == 0x80280908 || Status == 0x80280909 || Status == 0x8028090A || Status == 0x80280922 ||
                       Status == 0x80090030 || Status == 0x80284009 || Status == 0x8028400A;
            };
            auto Status = static_cast<std::uint32_t>(InRead());

            for (int Retry = 0; Retry < 3 && IsTransient(Status) && !InStopToken.stop_requested(); Retry++)
            {
                std::this_thread::sleep_for(std::chrono::milliseconds(25 * (Retry + 1)));
                if (InStopToken.stop_requested()) break;
                Status = static_cast<std::uint32_t>(InRead());
            }

            return Status;
        }

        struct CngObject
        {
            NCRYPT_HANDLE Handle = 0;
            CngObject() = default;
            CngObject(const CngObject&) = delete;
            CngObject& operator=(const CngObject&) = delete;
            ~CngObject() { if (this->Handle != 0) NCryptFreeObject(this->Handle); }
        };

        struct CngBuffer
        {
            void* Pointer = nullptr;
            CngBuffer() = default;
            CngBuffer(const CngBuffer&) = delete;
            CngBuffer& operator=(const CngBuffer&) = delete;
            ~CngBuffer() { if (this->Pointer != nullptr) NCryptFreeBuffer(this->Pointer); }
        };

        struct CertificateStore
        {
            HCERTSTORE Handle = nullptr;
            CertificateStore() = default;
            CertificateStore(const CertificateStore&) = delete;
            CertificateStore& operator=(const CertificateStore&) = delete;
            ~CertificateStore() { if (this->Handle != nullptr) CertCloseStore(this->Handle, 0); }
        };

        struct CertificateContext
        {
            PCCERT_CONTEXT Pointer = nullptr;
            CertificateContext() = default;
            CertificateContext(const CertificateContext&) = delete;
            CertificateContext& operator=(const CertificateContext&) = delete;
            ~CertificateContext() { if (this->Pointer != nullptr) CertFreeCertificateContext(this->Pointer); }
        };

        struct TbsContext
        {
            TBS_HCONTEXT Handle = nullptr;
            TbsContext() = default;
            TbsContext(const TbsContext&) = delete;
            TbsContext& operator=(const TbsContext&) = delete;
            ~TbsContext() { if (this->Handle != nullptr) Tbsip_Context_Close(this->Handle); }
        };

        /// <summary>
        /// Records an unavailable query without discarding other information.
        /// </summary>
        void AddError(HwTpm& InTpm, const std::wstring& InOperation, const wchar_t* InSource, std::uint32_t InCode)
        {
            InTpm.Errors.push_back({ InOperation, std::wstring(InSource), InCode });
        }

        /// <summary>
        /// Reads a previously bounds-checked TPM wire value.
        /// </summary>
        std::uint32_t ReadBigEndian(ByteSpan InBytes, std::size_t InOffset, std::size_t InSize = 4)
        {
            std::uint32_t Value = 0;
            for (std::size_t I = 0; I < InSize; I++) Value = (Value << 8) | InBytes[InOffset + I];
            return Value;
        }

        /// <summary>
        /// Writes a 32-bit TPM wire value.
        /// </summary>
        void WriteBigEndian(Bytes& InBytes, std::size_t InOffset, std::uint32_t InValue)
        {
            for (std::size_t I = 0; I < 4; I++) InBytes[InOffset + I] = static_cast<std::uint8_t>(InValue >> (24 - I * 8));
        }

        /// <summary>
        /// Decodes four TCG text bytes, removing trailing padding.
        /// </summary>
        std::wstring DecodeWord(std::uint32_t InWord)
        {
            Bytes Data(4);
            WriteBigEndian(Data, 0, InWord);
            auto Text = DecodeAscii(Data);
            while (!Text.empty() && (Text.back() == L'\0' || Text.back() == L' ')) Text.pop_back();
            return Text;
        }

        /// <summary>
        /// Follows capability pagination with strict response and progress bounds.
        /// </summary>
        void ReadCapability(TBS_HCONTEXT InContext, std::uint32_t InCapability, std::uint32_t InStart, std::uint32_t InEnd, HwTpm& InTpm, std::stop_token InStopToken)
        {
            const auto Operation = L"TPM2_GetCapability/" + std::to_wstring(InCapability) + L"/" + std::to_wstring(InStart);

            for (int Page = 0; Page < 16 && !InStopToken.stop_requested(); Page++)
            {
                Bytes Command(22);
                Command[0] = 0x80;
                Command[1] = 0x01;
                WriteBigEndian(Command, 2, static_cast<std::uint32_t>(Command.size()));
                WriteBigEndian(Command, 6, 0x17A);   // TPM_CC_GetCapability.
                WriteBigEndian(Command, 10, InCapability);
                WriteBigEndian(Command, 14, InStart);
                WriteBigEndian(Command, 18, std::min(64u, InEnd - InStart));
                Bytes Response(65536);
                UINT32 Size = static_cast<UINT32>(Response.size());
                const wchar_t* Source = L"TBS";
                auto Status = RetryRead([&]
                {
                    Size = static_cast<UINT32>(Response.size());
                    Source = L"TBS";
                    auto Result = Tbsip_Submit_Command(InContext, TBS_COMMAND_LOCALITY_ZERO, TBS_COMMAND_PRIORITY_NORMAL, Command.data(), static_cast<UINT32>(Command.size()), Response.data(), &Size);
                    if (Result != 0) return static_cast<std::uint32_t>(Result);
                    Source = L"Win32";

                    if (Size < 10 || Size > Response.size() || ReadBigEndian(Response, 0, 2) != 0x8001 ||
                        ReadBigEndian(Response, 2) != Size || (ReadBigEndian(Response, 6) != 0 && Size != 10))
                        return static_cast<std::uint32_t>(ERROR_INVALID_DATA);

                    Source = L"TPM";
                    return ReadBigEndian(Response, 6);
                }, InStopToken);

                if (Status != 0)
                {
                    if (!InStopToken.stop_requested()) AddError(InTpm, Operation, Source, Status);
                    return;
                }

                if (InStopToken.stop_requested()) return;

                Response.resize(Size);
                bool More = false;
                std::uint32_t Next = InStart;

                if (!ParseTpmCapability(Response, InCapability, InStart, InTpm, More, Next))
                {
                    AddError(InTpm, Operation, L"Win32", ERROR_INVALID_DATA);
                    return;
                }

                if (!More || Next >= InEnd)
                    return;

                InStart = Next;
            }

            if (!InStopToken.stop_requested())
                AddError(InTpm, Operation, L"Win32", ERROR_MORE_DATA);
        }

        /// <summary>
        /// Reads fixed properties, variable state, algorithms and allocated PCR banks.
        /// </summary>
        void ReadCapabilities(HwTpm& InTpm, std::stop_token InStopToken)
        {
            TBS_CONTEXT_PARAMS2 Parameters{};
            Parameters.version = TBS_CONTEXT_VERSION_TWO;
            Parameters.includeTpm20 = 1;
            TbsContext Context;
            auto Status = RetryRead([&] { return Tbsi_Context_Create(reinterpret_cast<PCTBS_CONTEXT_PARAMS>(&Parameters), &Context.Handle); }, InStopToken);

            if (Status != 0)
            {
                if (!InStopToken.stop_requested()) AddError(InTpm, L"Tbsi_Context_Create", L"TBS", Status);
                return;
            }

            ReadCapability(Context.Handle, CapProperties, 0x100, 0x200, InTpm, InStopToken);
            ReadCapability(Context.Handle, CapProperties, 0x200, 0x300, InTpm, InStopToken);
            ReadCapability(Context.Handle, CapAlgorithms, 0, 0x10000, InTpm, InStopToken);
            ReadCapability(Context.Handle, CapPcrs, 0, 0x10000, InTpm, InStopToken);
            ApplyTpmProperties(InTpm);
        }

        /// <summary>
        /// Encodes public bytes as Base64 without line breaks.
        /// </summary>
        std::wstring Base64(ByteSpan InData)
        {
            DWORD Size = 0;
            const auto Flags = CRYPT_STRING_BASE64 | CRYPT_STRING_NOCRLF;

            if (!CryptBinaryToStringW(InData.data(), static_cast<DWORD>(InData.size()), Flags, nullptr, &Size) || Size == 0)
                return {};

            std::wstring Result(Size, L'\0');

            if (!CryptBinaryToStringW(InData.data(), static_cast<DWORD>(InData.size()), Flags, Result.data(), &Size))
                return {};

            Result.resize(Size);
            while (!Result.empty() && Result.back() == L'\0') Result.pop_back();
            return Result;
        }

        /// <summary>
        /// Reads a bounded binary CNG property and retains its native error code.
        /// </summary>
        std::optional<Bytes> GetProperty(NCRYPT_HANDLE InObject, const wchar_t* InName, HwTpm& InTpm, const std::wstring& InOperation, std::stop_token InStopToken)
        {
            DWORD Size = 0;
            auto Status = RetryRead([&] { return NCryptGetProperty(InObject, InName, nullptr, 0, &Size, NCRYPT_SILENT_FLAG); }, InStopToken);

            if (Status != 0 || Size == 0 || Size > MaxPropertySize)
            {
                if (!InStopToken.stop_requested()) AddError(InTpm, InOperation + L"/" + InName, Status == 0 ? L"Win32" : L"NCrypt", Status == 0 ? ERROR_INVALID_DATA : static_cast<DWORD>(Status));
                return std::nullopt;
            }

            Bytes Buffer(Size);
            DWORD Written = 0;
            Status = RetryRead([&] { return NCryptGetProperty(InObject, InName, Buffer.data(), Size, &Written, NCRYPT_SILENT_FLAG); }, InStopToken);

            if (Status != 0 || Written == 0 || Written > Size)
            {
                if (!InStopToken.stop_requested()) AddError(InTpm, InOperation + L"/" + InName, Status == 0 ? L"Win32" : L"NCrypt", Status == 0 ? ERROR_INVALID_DATA : static_cast<DWORD>(Status));
                return std::nullopt;
            }

            Buffer.resize(Written);
            return Buffer;
        }

        /// <summary>
        /// Reads a DWORD property without treating missing data as zero.
        /// </summary>
        std::optional<std::uint32_t> GetNumber(NCRYPT_HANDLE InObject, const wchar_t* InName, HwTpm& InTpm, const std::wstring& InOperation, std::stop_token InStopToken)
        {
            auto Data = GetProperty(InObject, InName, InTpm, InOperation, InStopToken);

            if (!Data) return std::nullopt;

            if (Data->size() != 4)
            {
                AddError(InTpm, InOperation + L"/" + InName, L"Win32", ERROR_INVALID_DATA);
                return std::nullopt;
            }

            std::uint32_t Value = 0;
            std::memcpy(&Value, Data->data(), 4);
            return Value;
        }

        /// <summary>
        /// Formats an X.500 name like X509Certificate2.Subject and Issuer.
        /// </summary>
        std::wstring CertificateName(CERT_NAME_BLOB InName)
        {
            const DWORD Flags = CERT_X500_NAME_STR | CERT_NAME_STR_REVERSE_FLAG;
            auto Size = CertNameToStrW(X509_ASN_ENCODING, &InName, Flags, nullptr, 0);
            if (Size == 0) return {};
            std::wstring Result(Size, L'\0');
            CertNameToStrW(X509_ASN_ENCODING, &InName, Flags, Result.data(), Size);
            while (!Result.empty() && Result.back() == L'\0') Result.pop_back();
            return Result;
        }

        /// <summary>
        /// Formats certificate validity as UTC ISO 8601.
        /// </summary>
        NullableString CertificateTime(const FILETIME& InTime)
        {
            SYSTEMTIME Time{};
            if (!FileTimeToSystemTime(&InTime, &Time) || Time.wYear > 9999) return std::nullopt;
            wchar_t Text[32]{};
            swprintf_s(Text, L"%04u-%02u-%02uT%02u:%02u:%02uZ", Time.wYear, Time.wMonth, Time.wDay, Time.wHour, Time.wMinute, Time.wSecond);
            return std::wstring(Text);
        }

        /// <summary>
        /// Decodes an X.509 certificate without building a chain or contacting its issuer.
        /// </summary>
        std::optional<HwTpmCertificate> ReadCertificate(PCCERT_CONTEXT InContext, const std::wstring& InSource)
        {
            if (InContext == nullptr || InContext->pCertInfo == nullptr || InContext->pbCertEncoded == nullptr || InContext->cbCertEncoded == 0 || InContext->cbCertEncoded > MaxPropertySize)
                return std::nullopt;

            const auto& Info = *InContext->pCertInfo;
            ByteSpan Der(InContext->pbCertEncoded, InContext->cbCertEncoded);
            if (!IsTpmCertificateDer(Der)) return std::nullopt;
            Bytes Serial(Info.SerialNumber.pbData, Info.SerialNumber.pbData + Info.SerialNumber.cbData);
            std::reverse(Serial.begin(), Serial.end());
            HwTpmCertificate Result;
            Result.Source = InSource;
            Result.Subject = CertificateName(Info.Subject);
            Result.Issuer = CertificateName(Info.Issuer);
            Result.SerialNumber = FormatHex(Serial);
            Result.NotBefore = CertificateTime(Info.NotBefore);
            Result.NotAfter = CertificateTime(Info.NotAfter);
            if (Info.SignatureAlgorithm.pszObjId) Result.SignatureAlgorithm = DecodeAscii(ByteSpan(reinterpret_cast<const std::uint8_t*>(Info.SignatureAlgorithm.pszObjId), std::strlen(Info.SignatureAlgorithm.pszObjId)));
            if (Info.SubjectPublicKeyInfo.Algorithm.pszObjId) Result.PublicKeyAlgorithm = DecodeAscii(ByteSpan(reinterpret_cast<const std::uint8_t*>(Info.SubjectPublicKeyInfo.Algorithm.pszObjId), std::strlen(Info.SubjectPublicKeyInfo.Algorithm.pszObjId)));
            Result.Sha256 = Sha256Hex(Der);
            Result.Der = Base64(Der);
            return Result;
        }

        /// <summary>
        /// Reads PCP certificate-store handles directly; these properties are not DER blobs.
        /// </summary>
        void ReadCertificateStore(NCRYPT_PROV_HANDLE InProvider, const wchar_t* InProperty, HwTpm& InTpm, std::stop_token InStopToken)
        {
            if (InStopToken.stop_requested()) return;
            CertificateStore Store;
            DWORD Size = 0;
            auto Status = NCryptGetProperty(InProvider, InProperty, reinterpret_cast<PBYTE>(&Store.Handle), sizeof(Store.Handle), &Size, NCRYPT_SILENT_FLAG);

            if (Status != 0)
            {
                AddError(InTpm, std::wstring(L"Provider/") + InProperty, L"NCrypt", static_cast<DWORD>(Status));
                return;
            }

            if (Store.Handle == nullptr || Size != sizeof(Store.Handle))
            {
                AddError(InTpm, std::wstring(L"Provider/") + InProperty, L"Win32", ERROR_INVALID_DATA);
                return;
            }

            CertificateContext Context;

            for (int I = 0; !InStopToken.stop_requested(); I++)
            {
                Context.Pointer = CertEnumCertificatesInStore(Store.Handle, Context.Pointer);

                if (Context.Pointer == nullptr)
                {
                    auto Error = GetLastError();
                    if (Error != static_cast<DWORD>(CRYPT_E_NOT_FOUND)) AddError(InTpm, std::wstring(InProperty) + L"/CertEnumCertificatesInStore", L"Win32", Error);
                    return;
                }

                if (I == 1024)
                {
                    AddError(InTpm, std::wstring(InProperty) + L"/CertEnumCertificatesInStore", L"Win32", ERROR_MORE_DATA);
                    return;
                }

                auto Certificate = ReadCertificate(Context.Pointer, InProperty);
                if (Certificate) InTpm.Certificates.push_back(std::move(*Certificate));
                else AddError(InTpm, std::wstring(InProperty) + L"/Certificate", L"Win32", ERROR_INVALID_DATA);
            }

        }

        /// <summary>
        /// Exports only the public part of an existing CNG key.
        /// </summary>
        std::optional<Bytes> ExportPublicKey(NCRYPT_KEY_HANDLE InKey, HwTpm& InTpm, const std::wstring& InOperation, std::stop_token InStopToken)
        {
            DWORD Size = 0;
            auto Status = RetryRead([&] { return NCryptExportKey(InKey, 0, BCRYPT_PUBLIC_KEY_BLOB, nullptr, nullptr, 0, &Size, NCRYPT_SILENT_FLAG); }, InStopToken);

            if (Status != 0 || Size == 0 || Size > MaxPropertySize)
            {
                if (!InStopToken.stop_requested()) AddError(InTpm, InOperation + L"/NCryptExportKey", Status == 0 ? L"Win32" : L"NCrypt", Status == 0 ? ERROR_INVALID_DATA : static_cast<DWORD>(Status));
                return std::nullopt;
            }

            Bytes Blob(Size);
            DWORD Written = 0;
            Status = RetryRead([&] { return NCryptExportKey(InKey, 0, BCRYPT_PUBLIC_KEY_BLOB, nullptr, Blob.data(), Size, &Written, NCRYPT_SILENT_FLAG); }, InStopToken);

            if (Status != 0 || Written == 0 || Written > Size)
            {
                if (!InStopToken.stop_requested()) AddError(InTpm, InOperation + L"/NCryptExportKey", Status == 0 ? L"Win32" : L"NCrypt", Status == 0 ? ERROR_INVALID_DATA : static_cast<DWORD>(Status));
                return std::nullopt;
            }

            Blob.resize(Written);
            return Blob;
        }

        /// <summary>
        /// Enumerates existing user or machine keys, retaining metadata when opening is denied.
        /// </summary>
        void ReadPersistedKeys(NCRYPT_PROV_HANDLE InProvider, bool InMachine, HwTpm& InTpm, std::stop_token InStopToken)
        {
            const DWORD Flags = NCRYPT_SILENT_FLAG | (InMachine ? NCRYPT_MACHINE_KEY_FLAG : 0);
            const std::wstring Scope = InMachine ? L"LocalMachine" : L"CurrentUser";
            CngBuffer State;

            for (int I = 0; !InStopToken.stop_requested(); I++)
            {
                CngBuffer NameBuffer;
                auto Status = NCryptEnumKeys(InProvider, nullptr, reinterpret_cast<NCryptKeyName**>(&NameBuffer.Pointer), &State.Pointer, Flags);

                if (Status != 0)
                {
                    if (Status != NTE_NO_MORE_ITEMS) AddError(InTpm, Scope + L"/NCryptEnumKeys", L"NCrypt", static_cast<DWORD>(Status));
                    return;
                }

                if (NameBuffer.Pointer == nullptr)
                {
                    AddError(InTpm, Scope + L"/NCryptEnumKeys", L"Win32", ERROR_INVALID_DATA);
                    return;
                }

                if (I == 1024)
                {
                    AddError(InTpm, Scope + L"/NCryptEnumKeys", L"Win32", ERROR_MORE_DATA);
                    return;
                }

                const auto& Name = *static_cast<NCryptKeyName*>(NameBuffer.Pointer);
                auto& Key = InTpm.Keys.emplace_back();
                Key.Kind = L"Persisted";
                Key.Scope = Scope;
                if (Name.pszName) Key.Name = Name.pszName;
                if (Name.pszAlgid) Key.Algorithm = Name.pszAlgid;
                if (!Key.Name) continue;
                const auto Operation = Scope + L"/" + *Key.Name;
                CngObject Handle;
                Status = RetryRead([&] { return NCryptOpenKey(InProvider, &Handle.Handle, Name.pszName, Name.dwLegacyKeySpec, Flags); }, InStopToken);

                if (Status != 0)
                {
                    if (!InStopToken.stop_requested()) AddError(InTpm, Operation + L"/NCryptOpenKey", L"NCrypt", static_cast<DWORD>(Status));
                    continue;
                }

                Key.Length = GetNumber(Handle.Handle, NCRYPT_LENGTH_PROPERTY, InTpm, Operation, InStopToken);
                Key.Usage = GetNumber(Handle.Handle, NCRYPT_KEY_USAGE_PROPERTY, InTpm, Operation, InStopToken);
                Key.TpmUsage = GetNumber(Handle.Handle, NCRYPT_PCP_KEY_USAGE_POLICY_PROPERTY, InTpm, Operation, InStopToken);
                auto UniqueName = GetProperty(Handle.Handle, NCRYPT_UNIQUE_NAME_PROPERTY, InTpm, Operation, InStopToken);

                if (UniqueName && UniqueName->size() % 2 == 0)
                {
                    std::wstring Text(UniqueName->size() / 2, L'\0');
                    std::memcpy(Text.data(), UniqueName->data(), UniqueName->size());
                    while (!Text.empty() && Text.back() == L'\0') Text.pop_back();
                    Key.UniqueName = std::move(Text);
                }

                auto Blob = ExportPublicKey(Handle.Handle, InTpm, Operation, InStopToken);
                if (Blob && !SetTpmPublicBlob(Key, *Blob)) AddError(InTpm, Operation + L"/PublicKeyBlob", L"Win32", ERROR_INVALID_DATA);
                auto Der = GetProperty(Handle.Handle, NCRYPT_CERTIFICATE_PROPERTY, InTpm, Operation, InStopToken);

                if (Der)
                {
                    auto Certificate = ParseTpmCertificate(*Der, L"KeyProperty");
                    if (Certificate) Key.Certificates.push_back(std::move(*Certificate));
                    else AddError(InTpm, Operation + L"/Certificate", L"Win32", ERROR_INVALID_DATA);
                }
            }

        }

        /// <summary>
        /// Matches Personal-store certificates by provider and container without opening private keys.
        /// </summary>
        void ReadAssociatedCertificates(bool InMachine, HwTpm& InTpm, std::stop_token InStopToken)
        {
            if (InStopToken.stop_requested()) return;
            const std::wstring Source = InMachine ? L"LocalMachine/My" : L"CurrentUser/My";
            const DWORD Flags = CERT_STORE_READONLY_FLAG | CERT_STORE_OPEN_EXISTING_FLAG | (InMachine ? CERT_SYSTEM_STORE_LOCAL_MACHINE : CERT_SYSTEM_STORE_CURRENT_USER);
            CertificateStore Store;
            Store.Handle = CertOpenStore(CERT_STORE_PROV_SYSTEM_W, 0, 0, Flags, L"MY");

            if (Store.Handle == nullptr)
            {
                AddError(InTpm, Source, L"Win32", static_cast<DWORD>(HRESULT_FROM_WIN32(GetLastError())));
                return;
            }

            CertificateContext Context;

            while (!InStopToken.stop_requested())
            {
                Context.Pointer = CertEnumCertificatesInStore(Store.Handle, Context.Pointer);

                if (Context.Pointer == nullptr)
                {
                    auto Error = GetLastError();
                    if (Error != static_cast<DWORD>(CRYPT_E_NOT_FOUND)) AddError(InTpm, Source, L"Win32", static_cast<DWORD>(HRESULT_FROM_WIN32(Error)));
                    return;
                }

                DWORD Size = 0;

                if (!CertGetCertificateContextProperty(Context.Pointer, CERT_KEY_PROV_INFO_PROP_ID, nullptr, &Size) || Size < sizeof(CRYPT_KEY_PROV_INFO) || Size > MaxPropertySize)
                    continue;

                Bytes Buffer(Size);
                if (!CertGetCertificateContextProperty(Context.Pointer, CERT_KEY_PROV_INFO_PROP_ID, Buffer.data(), &Size) || Size < sizeof(CRYPT_KEY_PROV_INFO) || Size > Buffer.size()) continue;
                const auto& Info = *reinterpret_cast<const CRYPT_KEY_PROV_INFO*>(Buffer.data());

                if (Info.dwProvType != 0 || Info.pwszProvName == nullptr || Info.pwszContainerName == nullptr || !EqualsIgnoreCase(Info.pwszProvName, MS_PLATFORM_CRYPTO_PROVIDER))
                    continue;

                const std::wstring Scope = (Info.dwFlags & NCRYPT_MACHINE_KEY_FLAG) != 0 ? L"LocalMachine" : L"CurrentUser";
                auto Key = std::find_if(InTpm.Keys.begin(), InTpm.Keys.end(), [&](const HwTpmKey& Item)
                {
                    return Item.Kind == L"Persisted" && Item.Scope == Scope && Item.Name == Info.pwszContainerName;
                });

                if (Key != InTpm.Keys.end())
                {
                    auto Certificate = ReadCertificate(Context.Pointer, Source);
                    if (Certificate) Key->Certificates.push_back(std::move(*Certificate));
                    else AddError(InTpm, Source + L"/Certificate", L"Win32", ERROR_INVALID_DATA);
                }
            }
        }

        /// <summary>
        /// Reads public provider properties and existing keys using silent CNG operations.
        /// </summary>
        void ReadPublicMaterial(HwTpm& InTpm, std::stop_token InStopToken)
        {
            CngObject Provider;
            auto Status = RetryRead([&] { return NCryptOpenStorageProvider(&Provider.Handle, MS_PLATFORM_CRYPTO_PROVIDER, 0); }, InStopToken);

            if (Status != 0)
            {
                if (!InStopToken.stop_requested()) AddError(InTpm, L"NCryptOpenStorageProvider", L"NCrypt", static_cast<DWORD>(Status));
                return;
            }

            if (!InTpm.ManufacturerId)
            {
                auto Manufacturer = GetProperty(Provider.Handle, L"PCP_TPM_MANUFACTURER_ID", InTpm, L"Provider", InStopToken);
                if (Manufacturer && !ApplyTpmManufacturer(InTpm, *Manufacturer)) AddError(InTpm, L"Provider/PCP_TPM_MANUFACTURER_ID", L"Win32", ERROR_INVALID_DATA);
            }

            if (!InTpm.FirmwareVersion1 || !InTpm.FirmwareVersion2)
            {
                auto Firmware = GetProperty(Provider.Handle, L"PCP_TPM_FW_VERSION", InTpm, L"Provider", InStopToken);
                if (Firmware && !ApplyTpmFirmware(InTpm, *Firmware)) AddError(InTpm, L"Provider/PCP_TPM_FW_VERSION", L"Win32", ERROR_INVALID_DATA);
            }

            for (const auto* Property : { L"PCP_EKPUB", L"PCP_RSA_EKPUB", L"PCP_ECC_EKPUB", L"PCP_SRKPUB" })
            {
                if (InStopToken.stop_requested()) break;
                auto Blob = GetProperty(Provider.Handle, Property, InTpm, L"Provider", InStopToken);
                if (!Blob) continue;
                HwTpmKey Key;
                Key.Kind = std::wstring_view(Property) == L"PCP_SRKPUB" ? L"StorageRoot" : L"Endorsement";
                Key.Name = Property;
                Key.Scope = L"Platform";
                if (SetTpmPublicBlob(Key, *Blob)) InTpm.Keys.push_back(std::move(Key));
                else AddError(InTpm, std::wstring(L"Provider/") + Property, L"Win32", ERROR_INVALID_DATA);
            }

            for (const auto* Property : { L"PCP_EKCERT", L"PCP_EKNVCERT", L"PCP_RSA_EKCERT", L"PCP_RSA_EKNVCERT", L"PCP_ECC_EKCERT", L"PCP_ECC_EKNVCERT" })
            {
                if (InStopToken.stop_requested()) break;
                ReadCertificateStore(Provider.Handle, Property, InTpm, InStopToken);
            }

            ReadPersistedKeys(Provider.Handle, false, InTpm, InStopToken);
            ReadPersistedKeys(Provider.Handle, true, InTpm, InStopToken);
            ReadAssociatedCertificates(false, InTpm, InStopToken);
            ReadAssociatedCertificates(true, InTpm, InStopToken);
            std::sort(InTpm.Keys.begin(), InTpm.Keys.end(), [](const auto& A, const auto& B) { return std::tie(A.Scope, A.Name) < std::tie(B.Scope, B.Name); });
            const auto CompareCertificate = [](const auto& A, const auto& B) { return std::tie(A.Source, A.Sha256) < std::tie(B.Source, B.Sha256); };
            std::sort(InTpm.Certificates.begin(), InTpm.Certificates.end(), CompareCertificate);
            for (auto& Key : InTpm.Keys) std::sort(Key.Certificates.begin(), Key.Certificates.end(), CompareCertificate);
        }
    }

    bool ApplyTpmManufacturer(HwTpm& InTpm, ByteSpan InBytes)
    {
        if (InBytes.size() < 4 || InBytes.size() > 10 || InBytes.size() % 2 != 0 ||
            InBytes[InBytes.size() - 1] != 0 || InBytes[InBytes.size() - 2] != 0)
            return false;

        std::wstring Text(InBytes.size() / 2, L'\0');
        std::memcpy(Text.data(), InBytes.data(), InBytes.size());
        while (!Text.empty() && Text.back() == L'\0') Text.pop_back();

        if (Text.empty() || Text.size() > 4 || std::any_of(Text.begin(), Text.end(), [](wchar_t Character) { return Character < 0x20 || Character > 0x7E; }))
            return false;

        std::uint32_t Id = 0;
        for (std::size_t I = 0; I < 4; I++) Id = (Id << 8) | (I < Text.size() ? Text[I] : 0u);
        InTpm.ManufacturerId = Id;
        InTpm.Manufacturer = DecodeWord(Id);
        return true;
    }

    bool ApplyTpmFirmware(HwTpm& InTpm, ByteSpan InBytes)
    {
        if (InBytes.size() != 8) return false;
        std::uint16_t Words[4]{};
        std::memcpy(Words, InBytes.data(), sizeof(Words));
        if (!InTpm.FirmwareVersion1) InTpm.FirmwareVersion1 = (static_cast<std::uint32_t>(Words[0]) << 16) | Words[1];
        if (!InTpm.FirmwareVersion2) InTpm.FirmwareVersion2 = (static_cast<std::uint32_t>(Words[2]) << 16) | Words[3];
        return true;
    }

    bool SetTpmPublicBlob(HwTpmKey& InKey, ByteSpan InBlob)
    {
        if (InBlob.size() < 8 || InBlob.size() > MaxPropertySize) return false;
        std::uint32_t Magic = 0, Length = 0;
        std::memcpy(&Magic, InBlob.data(), 4);
        std::memcpy(&Length, InBlob.data() + 4, 4);
        std::wstring Algorithm;
        std::optional<std::uint32_t> Bits;

        if (Magic == BCRYPT_RSAPUBLIC_MAGIC)
        {
            if (InBlob.size() < sizeof(BCRYPT_RSAKEY_BLOB)) return false;
            BCRYPT_RSAKEY_BLOB Header{};
            std::memcpy(&Header, InBlob.data(), sizeof(Header));

            if (Length == 0 || Header.cbPublicExp == 0 || Header.cbModulus != (static_cast<std::uint64_t>(Length) + 7) / 8 ||
                24ull + Header.cbPublicExp + Header.cbModulus != InBlob.size() || Header.cbPrime1 != 0 || Header.cbPrime2 != 0)
                return false;

            Algorithm = L"RSA";
            Bits = Length;
        }
        else if ((Magic == BCRYPT_ECDH_PUBLIC_GENERIC_MAGIC || Magic == BCRYPT_ECDSA_PUBLIC_GENERIC_MAGIC) && Length == BCRYPT_ECC_FULLKEY_BLOB_V1)
        {
            // The full ECC format stores a version at offset 4, followed by explicit curve parameters.
            if (InBlob.size() < sizeof(BCRYPT_ECCFULLKEY_BLOB)) return false;
            BCRYPT_ECCFULLKEY_BLOB Header{};
            std::memcpy(&Header, InBlob.data(), sizeof(Header));

            if (Header.dwCurveType < BCRYPT_ECC_PRIME_SHORT_WEIERSTRASS_CURVE || Header.dwCurveType > BCRYPT_ECC_PRIME_MONTGOMERY_CURVE ||
                Header.cbFieldLength == 0 || Header.cbSubgroupOrder == 0 || Header.cbCofactor == 0 ||
                32ull + 7ull * Header.cbFieldLength + Header.cbSubgroupOrder + Header.cbCofactor + Header.cbSeed != InBlob.size())
                return false;

            std::uint32_t FieldBits = 0;

            for (std::uint32_t I = 0; I < Header.cbFieldLength; I++)
            {
                auto Value = InBlob[32 + I];
                if (Value == 0) continue;
                FieldBits = (Header.cbFieldLength - I - 1) * 8;
                for (; Value != 0; Value >>= 1) FieldBits++;
                break;
            }

            if (FieldBits == 0) return false;
            Algorithm = Magic == BCRYPT_ECDH_PUBLIC_GENERIC_MAGIC ? L"ECDH" : L"ECDSA";
            Bits = FieldBits;
        }
        else
        {
            switch (Magic)
            {
                case BCRYPT_ECDH_PUBLIC_P256_MAGIC: case BCRYPT_ECDSA_PUBLIC_P256_MAGIC: Bits = 256; break;
                case BCRYPT_ECDH_PUBLIC_P384_MAGIC: case BCRYPT_ECDSA_PUBLIC_P384_MAGIC: Bits = 384; break;
                case BCRYPT_ECDH_PUBLIC_P521_MAGIC: case BCRYPT_ECDSA_PUBLIC_P521_MAGIC: Bits = 521; break;
                case BCRYPT_ECDH_PUBLIC_GENERIC_MAGIC: case BCRYPT_ECDSA_PUBLIC_GENERIC_MAGIC: break;
                default: return false;
            }

            if (Length == 0 || 8ull + 2ull * Length != InBlob.size() || (Bits && Length != (*Bits + 7) / 8)) return false;
            Algorithm = (Magic & 0x00FFFFFF) == 0x004B4345 ? L"ECDH" : L"ECDSA";
        }

        if (!InKey.Algorithm) InKey.Algorithm = Algorithm;
        if (!InKey.Length) InKey.Length = Bits;
        InKey.PublicKeyBlob = Base64(InBlob);
        InKey.PublicKeySha256 = Sha256Hex(InBlob);
        return true;
    }

    bool IsTpmCertificateDer(ByteSpan InBytes)
    {
        if (InBytes.size() < 2 || InBytes.size() > MaxPropertySize || InBytes[0] != 0x30) return false;
        std::size_t Offset = 2;
        std::uint32_t Length = InBytes[1];

        if (Length >= 0x80)
        {
            const auto Count = Length & 0x7F;
            if (Count == 0 || Count > 4 || Count > InBytes.size() - Offset || InBytes[Offset] == 0) return false;
            Length = ReadBigEndian(InBytes, Offset, Count);
            Offset += Count;
            if (Length < 0x80) return false;
        }

        return Length == InBytes.size() - Offset;
    }

    std::optional<HwTpmCertificate> ParseTpmCertificate(ByteSpan InDer, const std::wstring& InSource)
    {
        if (!IsTpmCertificateDer(InDer)) return std::nullopt;
        CertificateContext Context;
        Context.Pointer = CertCreateCertificateContext(X509_ASN_ENCODING, InDer.data(), static_cast<DWORD>(InDer.size()));
        return ReadCertificate(Context.Pointer, InSource);
    }

    bool ParseTpmCapability(ByteSpan InResponse, std::uint32_t InCapability, std::uint32_t InStart, HwTpm& InTpm, bool& OutMore, std::uint32_t& OutNext)
    {
        OutMore = false;
        OutNext = InStart;

        if (InResponse.size() < 19 || ReadBigEndian(InResponse, 0, 2) != 0x8001 || ReadBigEndian(InResponse, 2) != InResponse.size() ||
            ReadBigEndian(InResponse, 6) != 0 || InResponse[10] > 1 || ReadBigEndian(InResponse, 11) != InCapability)
            return false;

        const auto Count = ReadBigEndian(InResponse, 15);
        std::size_t Offset = 19;
        std::uint32_t Previous = 0;
        std::vector<HwTpmProperty> Properties;
        std::vector<HwTpmAlgorithm> Algorithms;
        std::vector<HwTpmPcrBank> Banks;

        if (Count > 64 || (Count == 0 && InResponse[10] != 0) ||
            (InCapability == CapPcrs && (InStart != 0 || InResponse[10] != 0)) ||
            (InCapability != CapAlgorithms && InCapability != CapPcrs && InCapability != CapProperties))
            return false;

        for (std::uint32_t I = 0; I < Count; I++)
        {
            std::size_t Size = InCapability == CapProperties ? 8 : InCapability == CapAlgorithms ? 6 : 3;
            if (InResponse.size() - Offset < Size) return false;
            const auto Id = ReadBigEndian(InResponse, Offset, InCapability == CapProperties ? 4 : 2);
            if (Id == UINT32_MAX || (InCapability != CapPcrs && (Id < InStart || (I != 0 && Id <= Previous))) ||
                (InCapability == CapProperties && (Id >> 8) != (InStart >> 8))) return false;

            if (InCapability == CapProperties)
            {
                Properties.push_back({ Id, ReadBigEndian(InResponse, Offset + 4) });
            }
            else if (InCapability == CapAlgorithms)
            {
                Algorithms.push_back({ Id, ReadBigEndian(InResponse, Offset + 2) });
            }
            else
            {
                const auto SelectionSize = InResponse[Offset + 2];
                Size += SelectionSize;
                if (SelectionSize == 0 || InResponse.size() - Offset < Size ||
                    std::any_of(Banks.begin(), Banks.end(), [&](const auto& Bank) { return Bank.Algorithm == Id; })) return false;
                Banks.push_back({ Id, FormatHex(InResponse.subspan(Offset + 3, SelectionSize)) });
            }

            Previous = Id;
            Offset += Size;
        }

        if (Offset != InResponse.size()) return false;
        InTpm.Properties.insert(InTpm.Properties.end(), Properties.begin(), Properties.end());
        InTpm.Algorithms.insert(InTpm.Algorithms.end(), Algorithms.begin(), Algorithms.end());
        InTpm.PcrBanks.insert(InTpm.PcrBanks.end(), Banks.begin(), Banks.end());
        OutMore = InResponse[10] != 0;
        OutNext = Count == 0 ? InStart : Previous + 1;
        return true;
    }

    void ApplyTpmProperties(HwTpm& InTpm)
    {
        const auto Get = [&](std::uint32_t InProperty) -> std::optional<std::uint32_t>
        {
            for (const auto& Item : InTpm.Properties) if (Item.Property == InProperty) return Item.Value;
            return std::nullopt;
        };

        if (auto Family = Get(0x100)) InTpm.Family = DecodeWord(*Family);
        InTpm.SpecificationLevel = Get(0x101);
        InTpm.SpecificationRevision = Get(0x102);
        InTpm.SpecificationDayOfYear = Get(0x103);
        InTpm.SpecificationYear = Get(0x104);
        InTpm.ManufacturerId = Get(0x105);
        if (InTpm.ManufacturerId) InTpm.Manufacturer = DecodeWord(*InTpm.ManufacturerId);
        Bytes Vendor;

        for (std::uint32_t I = 0x106; I <= 0x109; I++)
        {
            auto Word = Get(I);
            if (!Word) break;
            const auto Offset = Vendor.size();
            Vendor.resize(Offset + 4);
            WriteBigEndian(Vendor, Offset, *Word);
        }

        if (!Vendor.empty())
        {
            auto Text = DecodeAscii(Vendor);
            while (!Text.empty() && (Text.back() == L'\0' || Text.back() == L' ')) Text.pop_back();
            InTpm.VendorString = std::move(Text);
        }

        InTpm.FirmwareVersion1 = Get(0x10B);
        InTpm.FirmwareVersion2 = Get(0x10C);
        InTpm.PermanentFlags = Get(0x200);
        InTpm.StartupClearFlags = Get(0x201);

        if (InTpm.PermanentFlags)
        {
            InTpm.IsOwnerAuthSet = (*InTpm.PermanentFlags & 1) != 0;
            InTpm.IsEndorsementAuthSet = (*InTpm.PermanentFlags & 2) != 0;
            InTpm.IsLockoutAuthSet = (*InTpm.PermanentFlags & 4) != 0;
            InTpm.IsInLockout = (*InTpm.PermanentFlags & 0x200) != 0;
        }

        InTpm.LockoutCounter = Get(0x20E);
        InTpm.MaxAuthFail = Get(0x20F);
        InTpm.LockoutInterval = Get(0x210);
        InTpm.LockoutRecovery = Get(0x211);
    }

    void RetrieveTpms(Hwid& InHwid, std::stop_token InStopToken)
    {
        if (InStopToken.stop_requested()) return;
        std::unique_lock Lock(QueryLock, std::defer_lock);

        while (!Lock.try_lock_for(std::chrono::milliseconds(25)))
            if (InStopToken.stop_requested()) return;

        if (InStopToken.stop_requested()) return;
        TPM_DEVICE_INFO Info{};
        auto Status = Tbsi_GetDeviceInfo(sizeof(Info), &Info);
        if (Status == TBS_E_TPM_NOT_FOUND) return;
        auto& Device = InHwid.Tpms.emplace_back();
        Device.Id = static_cast<int>(InHwid.Tpms.size() - 1);

        if (Status == 0)
        {
            if (Info.tpmVersion == TPM_VERSION_20) Device.Version = L"2.0";
            else if (Info.tpmVersion == TPM_VERSION_12) Device.Version = L"1.2";
            Device.InterfaceType = Info.tpmInterfaceType;
            Device.ImplementationRevision = Info.tpmImpRevision;
        }
        else
        {
            AddError(Device, L"Tbsi_GetDeviceInfo", L"TBS", Status);
        }

        if (Device.Version == L"2.0" && !InStopToken.stop_requested()) ReadCapabilities(Device, InStopToken);
        if (!InStopToken.stop_requested()) ReadPublicMaterial(Device, InStopToken);
    }
}
