namespace TrueMoon.Configuration;

/// <summary>Provides a new environment snapshot each time configuration is refreshed.</summary>
public class EnvironmentVariablesProvider : IConfigurationProvider
{
    private readonly Func<IEnumerable<KeyValuePair<string, string?>>> _variables;
    /// <summary>Uses the common legacy environment-provider boundary.</summary>
    public EnvironmentVariablesProvider() : this(() => Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
        .Select(entry => new KeyValuePair<string, string?>((string)entry.Key, entry.Value as string))) { }
    /// <summary>Uses an explicit environment capture, without modifying the process environment.</summary>
    /// <param name="variables">Selected or complete captured variables.</param>
    public EnvironmentVariablesProvider(Func<IEnumerable<KeyValuePair<string, string?>>> variables)
    { ArgumentNullException.ThrowIfNull(variables); _variables = variables; }
    public string Name => ConfigurationSectionNames.EnvironmentVariables;

    public IReadOnlyList<IConfigurationSection> GetSections()
    {
        var dictionary = new Dictionary<string, object?>();
        foreach (var entry in _variables()) dictionary[entry.Key] = entry.Value;
        return [new EnvironmentVariablesSection(dictionary)];
    }
}
