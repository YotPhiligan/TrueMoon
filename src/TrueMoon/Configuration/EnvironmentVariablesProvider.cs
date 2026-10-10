namespace TrueMoon.Configuration;

/// <summary>Provides a new environment snapshot each time configuration is refreshed.</summary>
public class EnvironmentVariablesProvider : IConfigurationProvider
{
    public string Name => ConfigurationSectionNames.EnvironmentVariables;

    public IReadOnlyList<IConfigurationSection> GetSections()
    {
        var dictionary = new Dictionary<string, object?>();
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            dictionary[(string)entry.Key] = entry.Value;
        return [new EnvironmentVariablesSection(dictionary)];
    }
}
