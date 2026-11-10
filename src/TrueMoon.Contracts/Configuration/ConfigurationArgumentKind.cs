namespace TrueMoon.Configuration;

/// <summary>How a command line option consumes arguments.</summary>
public enum ConfigurationArgumentKind
{
    /// <summary>Value.</summary>
    Value,
    /// <summary>Flag.</summary>
    Flag,
    /// <summary>Repeated.</summary>
    Repeated,
}
