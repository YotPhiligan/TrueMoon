namespace TrueMoon.Configuration;

/// <summary>
/// Configuration section, holds part of configuration data 
/// </summary>
public interface IConfigurationSection : IConfigurable
{
    /// <summary>
    /// Section name
    /// </summary>
    string Name { get; }
}