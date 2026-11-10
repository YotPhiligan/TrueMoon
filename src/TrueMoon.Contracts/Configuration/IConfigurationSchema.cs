namespace TrueMoon.Configuration;

/// <summary>A frozen options schema with explicit copying and validation.</summary>
public interface IConfigurationSchema<T> where T : class
{
    /// <summary>Section path.</summary>
    string Section { get; }
    /// <summary>Immutable field definitions.</summary>
    IReadOnlyList<ConfigurationKey> Keys { get; }
    /// <summary>Returns copied default entries.</summary><returns>Default entries.</returns>
    IReadOnlyList<ConfigurationEntry> GetDefaults();
    /// <summary>Binds and validates an independent options copy.</summary>
    /// <param name="snapshot">Input snapshot.</param><param name="configure">Optional pure override callback.</param><returns>Validated independent options.</returns>
    T Bind(IConfigurationSnapshot snapshot, Action<T>? configure = null);
    /// <summary>Copies all mutable data.</summary><param name="value">Source.</param><returns>Independent copy.</returns>
    T Copy(T value);
}
