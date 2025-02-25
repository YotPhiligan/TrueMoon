namespace TrueMoon.Configuration;

public class CommandLineArgsProvider : IConfigurationProvider
{
    private readonly Dictionary<string, object?> _dictionary = new ();
    
    public CommandLineArgsProvider()
    {
        var args = Environment.GetCommandLineArgs();

        foreach (var s in args)
        {
            if (s.Contains('='))
            {
                var parts = s.Split('=');
                var key = parts[0];
                var value = parts[1];
                
                SetCore(key,value);
            }
            else
            {
                SetCore(s, default);
            }
        }
    }
    
    private void SetCore(string key, object? value)
    {
        _dictionary[key] = value;
    }

    public string Name => ConfigurationSectionNames.CommandLineArguments;
    
    public IReadOnlyList<IConfigurationSection> GetSections()
    {
        return new List<IConfigurationSection>
        {
            new CommandLineArgsSection(_dictionary)
        };
    }
}