using System.Text.Json;

namespace TrueMoon.Configuration;

/// <summary>An owned JSON input value; no source document is retained.</summary>
public sealed record ConfigurationEntry
{
    private readonly JsonElement _value;
    /// <summary>Copies an input value.</summary>
    /// <param name="Path">Logical path.</param><param name="Value">JSON value.</param>
    public ConfigurationEntry(string Path, JsonElement Value) { this.Path = Path; this.Value = Value; }
    /// <summary>Logical path.</summary>
    public string Path { get; init; }
    /// <summary>Owned JSON value. Undefined is rejected when loading a source.</summary>
    public JsonElement Value { get => _value; init => _value = value.ValueKind == JsonValueKind.Undefined ? value : value.Clone(); }
}
