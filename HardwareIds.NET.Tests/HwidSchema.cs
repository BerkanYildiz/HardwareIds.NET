namespace HardwareIds.NET.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using global::HardwareIds.NET.Structures;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using NJsonSchema;
    using NJsonSchema.Validation;

    /// <summary>
    /// Validates serialized scans against hwid.schema.json, the schema published at the root of the repository.
    /// </summary>
    internal static class HwidSchema
    {
        private static readonly Lazy<NJsonSchema.JsonSchema> LazySchema = new(() => NJsonSchema.JsonSchema.FromJsonAsync(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "hwid.schema.json"))).GetAwaiter().GetResult());

        public static NJsonSchema.JsonSchema Schema => LazySchema.Value;

        /// <summary>
        /// Gets the schema violations of a scan result, serialized with the default options.
        /// </summary>
        public static List<string> Validate(Hwid InHwid)
        {
            return Validate(System.Text.Json.JsonSerializer.Serialize(InHwid));
        }

        /// <summary>
        /// Gets the schema violations of a JSON document, as "path: kind" entries.
        /// </summary>
        public static List<string> Validate(string InJson)
        {
            //
            // Dates must stay strings: Json.NET turns them into DateTime tokens by default, which changes what the patterns see.
            //

            using var Reader = new JsonTextReader(new StringReader(InJson)) { DateParseHandling = DateParseHandling.None };
            return Flatten(Schema.Validate(JToken.ReadFrom(Reader))).ToList();
        }

        private static IEnumerable<string> Flatten(IEnumerable<ValidationError> InErrors)
        {
            foreach (var Error in InErrors)
            {
                if (Error is ChildSchemaValidationError Child)
                {
                    foreach (var Nested in Child.Errors.SelectMany(T => Flatten(T.Value)))
                        yield return Nested;
                }
                else
                {
                    yield return $"{Error.Path}: {Error.Kind}";
                }
            }
        }
    }
}
