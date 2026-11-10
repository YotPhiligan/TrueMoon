using System.Text.Json;

namespace TrueMoon.Configuration;

/// <summary>A type-erased codec carried by field metadata.</summary>
public interface IConfigurationValueCodec
{
    /// <summary>Declared value type.</summary>
    Type ValueType { get; }
    /// <summary>Converts an input without IO.</summary><param name="value">Input.</param><returns>Independent data.</returns>
    object? Read(JsonElement value);
    /// <summary>Copies a value to JSON.</summary><param name="value">Data.</param><returns>Owned JSON.</returns>
    JsonElement Write(object? value);
}

/// <summary>Converts one typed configuration value without IO.</summary>
/// <typeparam name="T">Value type.</typeparam>
public interface IConfigurationValueCodec<T>
{
    /// <summary>Reads an input.</summary><param name="value">Input.</param><returns>Independent typed data.</returns>
    T Read(JsonElement value);
    /// <summary>Copies data into JSON.</summary><param name="value">Data.</param><returns>Owned JSON.</returns>
    JsonElement Write(T value);
}
