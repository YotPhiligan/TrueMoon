namespace TrueMoon.Configuration;

/// <summary>Compatibility facade for the provider-neutral schema definition factory.</summary>
public static class ConfigurationSchemas
{
    /// <summary>Freezes a data schema and its defaults without App/DI or resource activation.</summary>
    /// <param name="section">Section path.</param><param name="create">Default data.</param><param name="copy">Deep copy.</param>
    /// <param name="validate">Pure validator.</param><param name="fields">Field metadata.</param><returns>A detached schema.</returns>
    public static IConfigurationSchema<T> Create<T>(string section, Func<T> create, Func<T, T> copy,
        Action<T> validate, Action<IConfigurationSchemaBuilder<T>> fields) where T : class
        => ConfigurationSchemaDefinitions.Create(section, create, copy, validate, fields);
}
