namespace TrueMoon.Configuration;

/// <summary>An immutable generation of normalized values.</summary>
public interface IConfigurationSnapshot
{
    /// <summary>Snapshot generation.</summary>
    long Generation { get; }
    /// <summary>Reads a declared typed value without IO.</summary><param name="path">Full logical path.</param><returns>Value and presence state.</returns>
    ConfigurationRead<T> Read<T>(string path);
    /// <summary>Copies stage-bound options. Standalone snapshots have no stage options.</summary>
    /// <param name="schema">Registered schema instance.</param><returns>Independent options copy.</returns>
    T GetOptions<T>(IConfigurationSchema<T> schema) where T : class;
}
