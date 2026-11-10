using System.Text.Json;

namespace TrueMoon.Configuration;

/// <summary>An explicit source patch. A missing value deletes the key; JSON null writes null.</summary>
public sealed record ConfigurationEdit
{
    private readonly JsonElement? _value;
    /// <summary>Copies one edit.</summary>
    /// <param name="Path">Logical key.</param><param name="Value">Value, or null to delete.</param>
    public ConfigurationEdit(string Path, JsonElement? Value) { this.Path = Path; this.Value = Value; }
    /// <summary>Logical key, normalized by the writer.</summary>
    public string Path { get; init; }
    /// <summary>Owned value, or null to delete.</summary>
    public JsonElement? Value { get => _value; init => _value = value is { ValueKind: not JsonValueKind.Undefined } element ? element.Clone() : value; }
}
