namespace HardwareIds.NET.Structures
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Source-generated serialization metadata of <see cref="Hwid"/>, for trimmed and Native AOT applications, where the
    /// reflection-based <c>JsonSerializer.Serialize(Hwid)</c> is unavailable: <c>JsonSerializer.Serialize(Hwid, HwidJsonContext.Default.Hwid)</c>
    /// (or <see cref="Hwid.ToJson"/>) writes the very same JSON.
    /// </summary>
    [JsonSerializable(typeof(Hwid))]
    public partial class HwidJsonContext : JsonSerializerContext
    {
        /// <summary>
        /// Gets the context that indents the JSON, like <c>new JsonSerializerOptions { WriteIndented = true }</c>.
        /// </summary>
        public static HwidJsonContext Indented { get; } = new(new JsonSerializerOptions { WriteIndented = true });
    }
}
