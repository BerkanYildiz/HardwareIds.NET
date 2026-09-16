namespace HardwareIds.NET.Tests.Unit
{
    using System;
    using System.Text;

    using global::HardwareIds.NET.Native;

    using Xunit;

    public class EdidTests
    {
        private static readonly byte[] Header = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

        /// <summary>
        /// Builds a 128-byte EDID block with the given identification fields.
        /// </summary>
        private static byte[] BuildEdid(ushort InManufacturerId, ushort InProductCode, uint InSerial, string? InName, string? InSerialText)
        {
            var Edid = new byte[128];
            Array.Copy(Header, Edid, Header.Length);
            Edid[8] = (byte) (InManufacturerId >> 8);
            Edid[9] = (byte) (InManufacturerId & 0xFF);
            Edid[10] = (byte) (InProductCode & 0xFF);
            Edid[11] = (byte) (InProductCode >> 8);
            Array.Copy(BitConverter.GetBytes(InSerial), 0, Edid, 12, 4);

            var Block = 54;

            if (InName != null)
                WriteDescriptor(Edid, Block += 0, 0xFC, InName);

            if (InSerialText != null)
                WriteDescriptor(Edid, InName != null ? Block + 18 : Block, 0xFF, InSerialText);

            return Edid;
        }

        private static void WriteDescriptor(byte[] InEdid, int InOffset, byte InTag, string InText)
        {
            InEdid[InOffset + 3] = InTag;

            var Padded = (InText + "\n").PadRight(13);
            Array.Copy(Encoding.ASCII.GetBytes(Padded), 0, InEdid, InOffset + 5, 13);
        }

        [Fact]
        public void Parse_DecodesManufacturerProductAndDescriptors()
        {
            var Info = Edid.Parse(BuildEdid(0x10AC, 0x4070, 0x12345678, "DELL U2415", "ABC123XYZ"));

            Assert.NotNull(Info);
            Assert.Equal("DEL", Info.Manufacturer);
            Assert.Equal("4070", Info.ProductCode);
            Assert.Equal("DELL U2415", Info.Name);
            Assert.Equal("ABC123XYZ", Info.SerialNumber);
        }

        [Fact]
        public void Parse_FallsBackToNumericSerialWhenNoDescriptor()
        {
            var Info = Edid.Parse(BuildEdid(0x4C2D, 0x0BC3, 305419896, "S24E450", null));

            Assert.NotNull(Info);
            Assert.Equal("SAM", Info.Manufacturer);
            Assert.Equal("0BC3", Info.ProductCode);
            Assert.Equal("305419896", Info.SerialNumber);
        }

        [Fact]
        public void Parse_UsesEmptyStringsWhenNothingIsAvailable()
        {
            var Info = Edid.Parse(BuildEdid(0x1E6D, 0x0001, 0, null, null));

            Assert.NotNull(Info);
            Assert.Equal("GSM", Info.Manufacturer);
            Assert.Equal("0001", Info.ProductCode);
            Assert.Equal(string.Empty, Info.Name);
            Assert.Equal(string.Empty, Info.SerialNumber);
        }

        [Fact]
        public void Parse_TrimsDescriptorPadding()
        {
            var Edid = BuildEdid(0x10AC, 0x4070, 1, "X", "Y");
            var Info = global::HardwareIds.NET.Native.Edid.Parse(Edid);

            Assert.NotNull(Info);
            Assert.Equal("X", Info.Name);
            Assert.Equal("Y", Info.SerialNumber);
        }

        [Fact]
        public void Parse_RejectsInvalidInput()
        {
            Assert.Null(Edid.Parse(null));
            Assert.Null(Edid.Parse(new byte[127]));
            Assert.Null(Edid.Parse(new byte[128]));

            var Corrupted = BuildEdid(0x10AC, 0x4070, 1, "X", "Y");
            Corrupted[0] = 0x01;
            Assert.Null(Edid.Parse(Corrupted));
        }

        [Fact]
        public void Parse_AcceptsExtendedBlocks()
        {
            var Extended = new byte[256];
            Array.Copy(BuildEdid(0x10AC, 0x4070, 1, "Name", "Serial"), Extended, 128);

            var Info = Edid.Parse(Extended);

            Assert.NotNull(Info);
            Assert.Equal("Name", Info.Name);
        }
    }
}
