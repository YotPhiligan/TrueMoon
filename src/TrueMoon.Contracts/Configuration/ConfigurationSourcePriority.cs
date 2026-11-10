namespace TrueMoon.Configuration;

/// <summary>The ordered configuration source levels.</summary>
public enum ConfigurationSourcePriority
{
    /// <summary>Defaults.</summary>
    Defaults = 0,
    /// <summary>Files.</summary>
    Files = 100,
    /// <summary>Environment.</summary>
    Environment = 200,
    /// <summary>Arguments.</summary>
    Arguments = 300,
    /// <summary>Overrides.</summary>
    Overrides = 400,
}
