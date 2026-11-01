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

            //
            // TPM_PT_VAR counters and authorization state can change while other processes use the TPM.
            //

            if (Node["tpms"] is JsonArray Tpms)
            {
                foreach (var Tpm in Tpms)
                {
                    if (Tpm?["properties"] is JsonArray Properties)
                    {
                        for (var I = Properties.Count - 1; I >= 0; I--)
                            if (Properties[I]?["property"]?.GetValue<uint>() >= 0x200)
                                Properties.RemoveAt(I);
                    }

                    foreach (var Name in new[] { "permanent_flags", "startup_clear_flags", "is_owner_auth_set", "is_endorsement_auth_set", "is_lockout_auth_set", "is_in_lockout", "lockout_counter", "max_auth_fail", "lockout_interval", "lockout_recovery" })
                        Tpm?.AsObject().Remove(Name);
                }
            }

            return Node.ToJsonString();
        }
    }
}
