namespace TrueMoon.Configuration;

public class EnvironmentVariablesSection : ConfigurationSection
{
    public EnvironmentVariablesSection(Dictionary<string,object?> dictionary) : base("env", dictionary)
    {
        
    }
}