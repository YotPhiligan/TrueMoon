namespace TrueMoon.Configuration;

public class EnvironmentVariablesProvider : IConfigurationProvider
{
    private readonly Dictionary<string, object?> _dictionary = new ();
    
    public EnvironmentVariablesProvider()
    {
        var variables = Environment.GetEnvironmentVariables();
        
        foreach (var key in variables.Keys)
        {
            var value = variables[key];
            
            SetCore($"{key}",value);
        }
    }
    
    private void SetCore(string key, object? value)
    {
        _dictionary[key] = value;
    }

    public string Name => ConfigurationSectionNames.EnvironmentVariables;
    
    public IReadOnlyList<IConfigurationSection> GetSections()
    {
        return new List<IConfigurationSection>
        {
            new EnvironmentVariablesSection(_dictionary)
        };
    }
}