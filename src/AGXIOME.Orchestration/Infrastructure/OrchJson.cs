using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGXIOME.Orchestration.Infrastructure;

/// <summary>
/// Thin JSON serialisation wrapper — keeps System.Text.Json contained to this type.
/// Swap this class to change the serialiser without touching any tool code.
/// </summary>
internal static class OrchJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    internal static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, Options);

    internal static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options);
}
