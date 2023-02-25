namespace TrueMoon.Configuration;

public class ConfigurationSection : ConfigurableBase, IConfigurationSection
{
    public ConfigurationSection(string name, Dictionary<string, object?>? dictionary = default) : base(dictionary)
    {
        Name = name;
    }

    public string Name { get; }
}