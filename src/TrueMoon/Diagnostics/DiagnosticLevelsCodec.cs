using System.Text.Json;
using System.Text.Json.Serialization;
using TrueMoon.Configuration;

namespace TrueMoon.Diagnostics;

internal sealed class DiagnosticLevelsCodec : IConfigurationValueCodec<IReadOnlyDictionary<string, DiagnosticLevel>>
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter<DiagnosticLevel>() } };
    public IReadOnlyDictionary<string, DiagnosticLevel> Read(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return null!;
        if (value.ValueKind != JsonValueKind.Object) throw new FormatException();
        var result = new Dictionary<string, DiagnosticLevel>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            result.Add(property.Name, JsonSerializer.Deserialize<DiagnosticLevel>(property.Value, Options));
        return result;
    }
    public JsonElement Write(IReadOnlyDictionary<string, DiagnosticLevel> value) => JsonSerializer.SerializeToElement(value, Options);
}
