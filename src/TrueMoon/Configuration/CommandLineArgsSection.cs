namespace TrueMoon.Configuration;

public class CommandLineArgsSection : ConfigurationSection
{
    public CommandLineArgsSection(Dictionary<string,object?> dictionary) : base("args", dictionary)
    {
        
    }
}