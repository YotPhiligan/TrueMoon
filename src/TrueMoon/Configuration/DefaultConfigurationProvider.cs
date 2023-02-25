using System.Reflection;

namespace TrueMoon.Configuration;

public class DefaultConfigurationProvider : IConfigurationProvider
{
    private readonly IConfigurationSection _section = new ConfigurationSection("default");
    private readonly List<IConfigurationSection> _list;
    public DefaultConfigurationProvider()
    {
        Name = "default";
        var entryAssembly = Assembly.GetEntryAssembly();
        var name = entryAssembly.GetName().Name;
        _section.Set("appName", name);
        _list = new() { _section };
    }

    public string Name { get; }

    public IReadOnlyList<IConfigurationSection> GetSections() => _list;
}