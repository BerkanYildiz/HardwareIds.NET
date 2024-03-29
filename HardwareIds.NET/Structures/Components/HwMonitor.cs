﻿namespace HardwareIds.NET.Structures.Components
{
    using System;
    using System.Text;
    using System.Text.Json.Serialization;

    public class HwMonitor
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the manufacturer.
        /// </summary>
        [JsonPropertyName("manufacturer")]
        public string Manufacturer { get; set; }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the product.
        /// </summary>
        [JsonPropertyName("product")]
        public string Product { get; set; }

        /// <summary>
        /// Gets or sets the serial number.
        /// </summary>
        [JsonPropertyName("serial_number")]
        public string SerialNumber { get; set; }

        /// <summary>
        /// Turns a 'WmiMonitorID' class ushort encoded data to a UTF8 string.
        /// </summary>
        /// <param name="Array">The array.</param>
        public static string ArrayToString(ushort[] Array)
        {
            var Result = new StringBuilder(Array.Length * sizeof(ushort));

            for (var I = 0; I < Array.Length; I++)
            {
                var Buffer = BitConverter.GetBytes(Array[I]);

                if (Buffer[0] != 0x00)
                {
                    Result.Append(Encoding.UTF8.GetString(Buffer, 0, 1));

                    if (Buffer[1] != 0x00)
                    {
                        Result.Append(Encoding.UTF8.GetString(Buffer, 1, 1));
                    }
                }
            }

            return Result.ToString();
        }
    }
}
