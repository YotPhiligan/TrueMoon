namespace TrueMoon.Configuration;

public interface IConfigurationProvider
{
    string Name { get; }

    IReadOnlyList<IConfigurationSection> GetSections();
}