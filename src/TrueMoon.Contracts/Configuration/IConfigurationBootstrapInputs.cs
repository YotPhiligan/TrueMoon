namespace TrueMoon.Configuration;

/// <summary>Optional access to original bootstrap-provider sections, before ordinary file/provider overlays.</summary>
public interface IConfigurationBootstrapInputs
{
    /// <summary>Gets a captured args/env/default input section, or null if that provider was removed.</summary>
    /// <param name="name">Exact bootstrap section name.</param><returns>The original captured input section.</returns>
    IConfigurationSection? GetBootstrapSection(string name);
}
