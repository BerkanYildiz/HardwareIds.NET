namespace HardwareIds.NET.Tests.Unit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using global::HardwareIds.NET.Native;

    using Xunit;

    public class StorageParsingTests
    {
        /// <summary>
        /// Builds a STORAGE_DEVICE_ID_DESCRIPTOR holding the given STORAGE_IDENTIFIER entries, laid out like Windows does (16-byte headers, 4-byte aligned).
        /// </summary>
        private static byte[] BuildDeviceIdDescriptor(params (int CodeSet, int Type, int Association, byte[] Value)[] InIdentifiers)
        {
            var Bytes = new List<byte>();
            Bytes.AddRange(BitConverter.GetBytes(16));
            Bytes.AddRange(BitConverter.GetBytes(0));
            Bytes.AddRange(BitConverter.GetBytes(InIdentifiers.Length));

            for (var I = 0; I < InIdentifiers.Length; I++)
            {
                var (CodeSet, Type, Association, Value) = InIdentifiers[I];
                var Padded = (16 + Value.Length + 3) / 4 * 4;

                Bytes.AddRange(BitConverter.GetBytes(CodeSet));
                Bytes.AddRange(BitConverter.GetBytes(Type));
                Bytes.AddRange(BitConverter.GetBytes((ushort) Value.Length));
                Bytes.AddRange(BitConverter.GetBytes((ushort) (I == InIdentifiers.Length - 1 ? 0 : Padded)));
                Bytes.AddRange(BitConverter.GetBytes(Association));
                Bytes.AddRange(Value);
                Bytes.AddRange(new byte[Padded - 16 - Value.Length]);
            }

            var Result = Bytes.ToArray();
            BitConverter.GetBytes(Result.Length).CopyTo(Result, 4);
            return Result;
        }

        private static byte[] FromHex(string InHex)
        {
            return Enumerable.Range(0, InHex.Length / 2).Select(T => Convert.ToByte(InHex.Substring(T * 2, 2), 16)).ToArray();
        }

        private static byte[] AtaString(string InText, int InWords)
        {
            var Padded = InText.PadRight(InWords * 2);
            var Result = new byte[InWords * 2];

            for (var I = 0; I < InWords; I++)
            {
                Result[I * 2] = (byte) Padded[I * 2 + 1];
                Result[I * 2 + 1] = (byte) Padded[I * 2];
            }

            return Result;
        }

        [Fact]
        public void ParseDeviceIdentifiers_DecodesEveryDescriptorType()
        {
            var Eui64 = new byte[] { 0x00, 0x25, 0x38, 0xB7, 0x1C, 0x9B, 0x5B, 0x4E };
            var Naa = new byte[] { 0x50, 0x02, 0x53, 0x8B, 0x71, 0xC9, 0xB5, 0xB4 };
            var Descriptor = BuildDeviceIdDescriptor(
                (ScsiDeviceIdentifier.CodeSetBinary, ScsiDeviceIdentifier.TypeEui64, 0, Eui64),
                (ScsiDeviceIdentifier.CodeSetBinary, ScsiDeviceIdentifier.TypeNaa, 0, Naa),
                (ScsiDeviceIdentifier.CodeSetAscii, ScsiDeviceIdentifier.TypeT10VendorId, 0, Encoding.ASCII.GetBytes("NVMe    Samsung SSD 970 EVO     S4EWNX0N123456K ")),
                (ScsiDeviceIdentifier.CodeSetUtf8, ScsiDeviceIdentifier.TypeScsiNameString, 0, Encoding.UTF8.GetBytes("eui.002538B71C9B5B4E\0")),
                (ScsiDeviceIdentifier.CodeSetBinary, 4, 1, [0x00, 0x01]));

            var Identifiers = Storage.ParseDeviceIdentifiers(Descriptor);

            Assert.Equal(5, Identifiers.Count);
            Assert.Equal("002538B71C9B5B4E", Identifiers[0].Text);
            Assert.Equal(ScsiDeviceIdentifier.TypeEui64, Identifiers[0].Type);
            Assert.Equal("5002538B71C9B5B4", Identifiers[1].Text);
            Assert.Equal("NVMe    Samsung SSD 970 EVO     S4EWNX0N123456K", Identifiers[2].Text);
            Assert.Equal("eui.002538B71C9B5B4E", Identifiers[3].Text);
            Assert.Equal(1, Identifiers[4].Association);
            Assert.All(Identifiers.Take(4), T => Assert.Equal(ScsiDeviceIdentifier.AssociationLogicalUnit, T.Association));
        }

        [Fact]
        public void ParseDeviceIdentifiers_DecodesTheDescriptorOfASataSamsung850Pro()
        {
            //
            // A Samsung SSD 850 PRO behind storahci reports one NAA identifier translated from the ATA World Wide Name.
            //

            var Descriptor = FromHex("10000000280000000100000001000000030000000800" + "1C00" + "00000000500253D8EF17474500000000");
            var Identifier = Assert.Single(Storage.ParseDeviceIdentifiers(Descriptor));

            Assert.Equal(ScsiDeviceIdentifier.CodeSetBinary, Identifier.CodeSet);
            Assert.Equal(ScsiDeviceIdentifier.TypeNaa, Identifier.Type);
            Assert.Equal(ScsiDeviceIdentifier.AssociationLogicalUnit, Identifier.Association);
            Assert.Equal("500253D8EF174745", Identifier.Text);
        }

        [Fact]
        public void ParseDeviceIdentifiers_DecodesTheDescriptorOfAnNvmeSamsung980Pro()
        {
            //
            // A Samsung SSD 980 PRO behind stornvme reports one SCSI name string built from the namespace EUI-64.
            //

            var Descriptor = FromHex("100000003400000001000000030000000800000014002800000000006575692E3030323533384245344236444635364600000000");
            var Identifier = Assert.Single(Storage.ParseDeviceIdentifiers(Descriptor));

            Assert.Equal(ScsiDeviceIdentifier.CodeSetUtf8, Identifier.CodeSet);
            Assert.Equal(ScsiDeviceIdentifier.TypeScsiNameString, Identifier.Type);
            Assert.Equal("eui.002538BE4B6DF56F", Identifier.Text);
        }

        [Fact]
        public void ParseDeviceIdentifiers_ToleratesEmptyAndTruncatedDescriptors()
        {
            Assert.Empty(Storage.ParseDeviceIdentifiers([]));
            Assert.Empty(Storage.ParseDeviceIdentifiers(new byte[12]));

            var Empty = Storage.ParseDeviceIdentifiers(BuildDeviceIdDescriptor((ScsiDeviceIdentifier.CodeSetAscii, ScsiDeviceIdentifier.TypeVendorSpecific, 0, [])));
            Assert.Equal(string.Empty, Assert.Single(Empty).Text);

            var Truncated = BuildDeviceIdDescriptor((ScsiDeviceIdentifier.CodeSetBinary, ScsiDeviceIdentifier.TypeNaa, 0, new byte[8])).Take(32).ToArray();
            Assert.Equal(4, Assert.Single(Storage.ParseDeviceIdentifiers(Truncated)).Value.Length);

            var Lying = BuildDeviceIdDescriptor((ScsiDeviceIdentifier.CodeSetBinary, ScsiDeviceIdentifier.TypeNaa, 0, new byte[8]));
            BitConverter.GetBytes(50).CopyTo(Lying, 8);
            Assert.Single(Storage.ParseDeviceIdentifiers(Lying));
        }

        [Fact]
        public void ParseNvmeControllerIdentity_ReadsSerialModelFirmwareAndFruGuid()
        {
            var Data = new byte[4096];
            Encoding.ASCII.GetBytes("S4EWNX0N123456K     ").CopyTo(Data, 4);
            Encoding.ASCII.GetBytes("Samsung SSD 970 EVO Plus 1TB            ").CopyTo(Data, 24);
            Encoding.ASCII.GetBytes("2B2QEXM7").CopyTo(Data, 64);
            Data[112] = 0xAB;
            Data[127] = 0xCD;

            var Identity = Storage.ParseNvmeControllerIdentity(Data);

            Assert.NotNull(Identity);
            Assert.Equal("S4EWNX0N123456K", Identity.SerialNumber);
            Assert.Equal("Samsung SSD 970 EVO Plus 1TB", Identity.ModelNumber);
            Assert.Equal("2B2QEXM7", Identity.FirmwareRevision);
            Assert.Equal("AB0000000000000000000000000000CD", Identity.FruGuid);
        }

        [Fact]
        public void ParseNvmeControllerIdentity_ReturnsNullFieldsWhenBlank()
        {
            var Identity = Storage.ParseNvmeControllerIdentity(new byte[4096]);

            Assert.NotNull(Identity);
            Assert.Null(Identity.SerialNumber);
            Assert.Null(Identity.FruGuid);
            Assert.Null(Storage.ParseNvmeControllerIdentity(new byte[64]));
        }

        [Fact]
        public void ParseNvmeNamespaceIdentity_ReadsNguidAndEui64()
        {
            var Data = new byte[4096];

            for (var I = 0; I < 16; I++)
                Data[104 + I] = (byte) (0x10 + I);

            new byte[] { 0x00, 0x25, 0x38, 0xB7, 0x1C, 0x9B, 0x5B, 0x4E }.CopyTo(Data, 120);

            var Identity = Storage.ParseNvmeNamespaceIdentity(Data);

            Assert.NotNull(Identity);
            Assert.Equal("101112131415161718191A1B1C1D1E1F", Identity.Nguid);
            Assert.Equal("002538B71C9B5B4E", Identity.Eui64);
            Assert.Null(Storage.ParseNvmeNamespaceIdentity(new byte[4096])!.Eui64);
        }

        [Fact]
        public void ParseAtaIdentity_SwapsCharactersAndReadsTheWorldWideName()
        {
            var Data = new byte[512];
            AtaString("S39FNLQF220135G", 10).CopyTo(Data, 10 * 2);
            AtaString("EXM04B6Q", 4).CopyTo(Data, 23 * 2);
            AtaString("Samsung SSD 850 PRO 512GB", 20).CopyTo(Data, 27 * 2);
            BitConverter.GetBytes((ushort) 0x0100).CopyTo(Data, 84 * 2);
            BitConverter.GetBytes((ushort) 0x5002).CopyTo(Data, 108 * 2);
            BitConverter.GetBytes((ushort) 0x538D).CopyTo(Data, 109 * 2);
            BitConverter.GetBytes((ushort) 0xEF17).CopyTo(Data, 110 * 2);
            BitConverter.GetBytes((ushort) 0x4745).CopyTo(Data, 111 * 2);

            var Identity = Storage.ParseAtaIdentity(Data);

            Assert.NotNull(Identity);
            Assert.Equal("S39FNLQF220135G", Identity.SerialNumber);
            Assert.Equal("EXM04B6Q", Identity.FirmwareRevision);
            Assert.Equal("Samsung SSD 850 PRO 512GB", Identity.ModelNumber);
            Assert.Equal("5002538DEF174745", Identity.WorldWideName);
        }

        [Fact]
        public void ParseAtaIdentity_OmitsUnsupportedWorldWideName()
        {
            var Data = new byte[512];
            AtaString("WD-WCC4E1234567", 10).CopyTo(Data, 10 * 2);
            BitConverter.GetBytes((ushort) 0x5001).CopyTo(Data, 108 * 2);

            var Identity = Storage.ParseAtaIdentity(Data);

            Assert.NotNull(Identity);
            Assert.Equal("WD-WCC4E1234567", Identity.SerialNumber);
            Assert.Null(Identity.WorldWideName);
            Assert.Null(Storage.ParseAtaIdentity(new byte[511]));
        }

        [Fact]
        public void ParseDiskIdentifier_ReturnsGptGuidOrMbrSignature()
        {
            var Gpt = new byte[48];
            var DiskGuid = Guid.NewGuid();
            BitConverter.GetBytes(1).CopyTo(Gpt, 0);
            DiskGuid.ToByteArray().CopyTo(Gpt, 8);
            Assert.Equal(DiskGuid.ToString(), Storage.ParseDiskIdentifier(Gpt));

            var Mbr = new byte[48];
            BitConverter.GetBytes(0x1234ABCDu).CopyTo(Mbr, 8);
            Assert.Equal("0x1234ABCD", Storage.ParseDiskIdentifier(Mbr));

            Assert.Null(Storage.ParseDiskIdentifier(new byte[48]));
            Assert.Null(Storage.ParseDiskIdentifier(new byte[8]));

            var Raw = new byte[48];
            BitConverter.GetBytes(2).CopyTo(Raw, 0);
            Assert.Null(Storage.ParseDiskIdentifier(Raw));
        }

        [Fact]
        public void FormatHex_IsUpperCaseWithoutSeparators()
        {
            Assert.Equal("00FF10AB", Storage.FormatHex([0x00, 0xFF, 0x10, 0xAB]));
            Assert.Equal(string.Empty, Storage.FormatHex([]));
        }
    }
}
