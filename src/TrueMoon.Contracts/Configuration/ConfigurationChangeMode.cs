namespace TrueMoon.Configuration;

/// <summary>Whether a setting can be reloaded.</summary>
public enum ConfigurationChangeMode
{
    /// <summary>StartupOnly.</summary>
    StartupOnly,
    /// <summary>Reloadable.</summary>
    Reloadable,
}
