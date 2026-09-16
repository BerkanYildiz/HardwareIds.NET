namespace HardwareIds.NET.Tests
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;

    using global::HardwareIds.NET.Structures;

    /// <summary>
    /// Runs a single hardware scan (without network probes) shared by every integration test.
    /// </summary>
    internal static class HwidFixture
    {
        private static readonly Lazy<Hwid> LazyHwid = new(() => HardwareIds.GetHwid(new HardwareIdsConfig()), LazyThreadSafetyMode.ExecutionAndPublication);

        public static Hwid Hwid => LazyHwid.Value;

        /// <summary>
        /// Serializes a scan result without the fields that legitimately change between two runs.
        /// </summary>
        public static string ToComparableJson(Hwid InHwid)
        {
            var Node = JsonNode.Parse(JsonSerializer.Serialize(InHwid))!.AsObject();

            if (Node["operating_systems"] is JsonArray OperatingSystems)
            {
                foreach (var OperatingSystem in OperatingSystems)
                    OperatingSystem?.AsObject().Remove("last_boot_up_time");
            }

            return Node.ToJsonString();
        }
    }
}
