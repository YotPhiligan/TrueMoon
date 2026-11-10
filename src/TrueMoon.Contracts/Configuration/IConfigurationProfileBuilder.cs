namespace TrueMoon.Configuration;

/// <summary>Declares an opt-in unified profile.</summary>
public interface IConfigurationProfileBuilder
{
    /// <summary>Adds a schema.</summary><param name="schema">Schema instance.</param>
    void AddSchema<T>(IConfigurationSchema<T> schema) where T : class;
    /// <summary>Adds a uniquely named source.</summary><param name="id">Safe source identifier.</param><param name="priority">Source level.</param><param name="source">Data source.</param>
    void AddSource(string id, ConfigurationSourcePriority priority, IConfigurationSource source);
    /// <summary>Adds a copied explicit override.</summary><param name="path">Full path.</param><param name="value">Value.</param><param name="codec">Optional codec.</param>
    void Override<T>(string path, T value, IConfigurationValueCodec<T>? codec = null);
}
