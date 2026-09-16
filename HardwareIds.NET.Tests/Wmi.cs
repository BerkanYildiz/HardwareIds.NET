namespace HardwareIds.NET.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Management;

    /// <summary>
    /// Queries WMI as an independent oracle for the integration tests.
    /// Returns null when WMI is unavailable so the callers can skip instead of failing.
    /// </summary>
    internal static class Wmi
    {
        public static List<Dictionary<string, object?>>? Query(string InClass, string InNamespace = @"root\cimv2", string? InCondition = null)
        {
            try
            {
                var Query = "SELECT * FROM " + InClass + (InCondition != null ? " WHERE " + InCondition : string.Empty);

                using var Searcher = new ManagementObjectSearcher(InNamespace, Query);
                using var Results = Searcher.Get();

                var Rows = new List<Dictionary<string, object?>>();

                foreach (var Item in Results)
                {
                    var Row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                    foreach (var Property in Item.Properties)
                        Row[Property.Name] = Property.Value;

                    Rows.Add(Row);
                    Item.Dispose();
                }

                return Rows;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string? GetString(this Dictionary<string, object?> InRow, string InName)
        {
            return InRow.TryGetValue(InName, out var Value) ? Value as string : null;
        }

        public static ulong GetNumber(this Dictionary<string, object?> InRow, string InName)
        {
            return InRow.TryGetValue(InName, out var Value) && Value != null ? Convert.ToUInt64(Value) : 0;
        }

        public static bool GetBool(this Dictionary<string, object?> InRow, string InName)
        {
            return InRow.TryGetValue(InName, out var Value) && Value is bool Flag && Flag;
        }

        public static DateTime? GetDate(this Dictionary<string, object?> InRow, string InName)
        {
            return InRow.TryGetValue(InName, out var Value) && Value is string Text ? ManagementDateTimeConverter.ToDateTime(Text) : null;
        }

        public static string[] GetStrings(this Dictionary<string, object?> InRow, string InName)
        {
            return InRow.TryGetValue(InName, out var Value) && Value is string[] Values ? Values : [];
        }

        /// <summary>
        /// Treats null and empty strings as equal, since WMI and the native APIs differ on how they report "no value".
        /// </summary>
        public static string Normalize(string? InValue)
        {
            return InValue ?? string.Empty;
        }
    }
}
