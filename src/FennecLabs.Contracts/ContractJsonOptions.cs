using System.Text.Json;
using System.Text.Json.Serialization;

namespace FennecLabs.Contracts;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for canonical dashboard artifacts, so producers
/// (CLI commands) and consumers (dashboard) serialize/deserialize with identical conventions.
/// </summary>
public static class ContractJsonOptions
{
    /// <summary>CamelCase, null-omitting, indented options for canonical artifact JSON.</summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
