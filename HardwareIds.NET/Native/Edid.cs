namespace HardwareIds.NET.Native
{
    using System;
    using System.Text;

    internal sealed class EdidInfo
    {
        public string? Manufacturer { get; set; }
        public string? ProductCode { get; set; }
        public string? SerialNumber { get; set; }
        public string? Name { get; set; }
    }

    internal static class Edid
    {
        private const byte DESCRIPTOR_SERIAL_NUMBER = 0xFF;
        private const byte DESCRIPTOR_NAME = 0xFC;

        /// <summary>
        /// Parses the identification fields of an EDID block.
        /// </summary>
        /// <param name="InData">The EDID data.</param>
        public static EdidInfo? Parse(byte[]? InData)
        {
            if (InData == null || InData.Length < 128)
                return null;

            if (InData[0] != 0x00 || InData[1] != 0xFF || InData[2] != 0xFF || InData[3] != 0xFF || InData[4] != 0xFF || InData[5] != 0xFF || InData[6] != 0xFF || InData[7] != 0x00)
                return null;

            var ManufacturerId = (InData[8] << 8) | InData[9];
            var Manufacturer = new string(
            [
                (char) ('A' - 1 + ((ManufacturerId >> 10) & 0x1F)),
                (char) ('A' - 1 + ((ManufacturerId >> 5) & 0x1F)),
                (char) ('A' - 1 + (ManufacturerId & 0x1F)),
            ]);

            var ProductCode = (InData[10] | (InData[11] << 8)).ToString("X4");
            var Serial = BitConverter.ToUInt32(InData, 12);
            string? Name = null;
            string? SerialText = null;

            for (var Block = 54; Block + 18 <= 126; Block += 18)
            {
                if (InData[Block] != 0 || InData[Block + 1] != 0 || InData[Block + 2] != 0)
                    continue;

                switch (InData[Block + 3])
                {
                    case DESCRIPTOR_NAME:
                        Name = ReadText(InData, Block + 5);
                        break;

                    case DESCRIPTOR_SERIAL_NUMBER:
                        SerialText = ReadText(InData, Block + 5);
                        break;
                }
            }

            return new EdidInfo
            {
                Manufacturer = Manufacturer,
                ProductCode = ProductCode,
                SerialNumber = SerialText ?? (Serial != 0 ? Serial.ToString() : string.Empty),
                Name = Name ?? string.Empty,
            };
        }

        private static string ReadText(byte[] InData, int InStart)
        {
            var Result = new StringBuilder(13);

            for (var I = 0; I < 13; I++)
            {
                var Character = InData[InStart + I];

                if (Character == 0x0A || Character == 0x00)
                    break;

                Result.Append((char) Character);
            }

            return Result.ToString().Trim();
        }
    }
}
