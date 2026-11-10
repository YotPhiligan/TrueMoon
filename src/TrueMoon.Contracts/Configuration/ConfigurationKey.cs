namespace TrueMoon.Configuration;

/// <summary>A fully qualified field definition.</summary>
/// <param name="Path">Canonical section and key path.</param>
/// <param name="ValueType">Declared value type.</param>
/// <param name="Field">Input metadata.</param>
public sealed record ConfigurationKey(string Path, Type ValueType, ConfigurationField Field)
{
    /// <summary>Optional explicit codec; null selects the standard invariant codec.</summary>
    public IConfigurationValueCodec? Codec { get; init; }
}
