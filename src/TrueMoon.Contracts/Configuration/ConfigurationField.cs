namespace TrueMoon.Configuration;

/// <summary>Immutable input and validation metadata for a field.</summary>
public sealed record ConfigurationField
{
    private readonly IReadOnlyList<string>? _aliases;
    /// <summary>Creates field metadata, copying aliases.</summary>
    /// <param name="Aliases">Complete command line aliases.</param>
    /// <param name="Required">Whether the final value must be nonempty.</param>
    /// <param name="Sensitive">Whether the field contains sensitive data.</param>
    /// <param name="Change">Change policy.</param>
    /// <param name="Argument">Command line value shape.</param>
    public ConfigurationField(IReadOnlyList<string>? Aliases = null, bool Required = false,
        bool Sensitive = false, ConfigurationChangeMode Change = ConfigurationChangeMode.StartupOnly,
        ConfigurationArgumentKind Argument = ConfigurationArgumentKind.Value)
    { this.Aliases = Aliases; this.Required = Required; this.Sensitive = Sensitive; this.Change = Change; this.Argument = Argument; }
    /// <summary>Copied command line aliases.</summary>
    public IReadOnlyList<string>? Aliases { get => _aliases; init => _aliases = value is null ? null : Array.AsReadOnly(value.ToArray()); }
    /// <summary>Whether the final value must be nonempty.</summary>
    public bool Required { get; init; }
    /// <summary>Whether diagnostic values must be masked.</summary>
    public bool Sensitive { get; init; }
    /// <summary>Change policy.</summary>
    public ConfigurationChangeMode Change { get; init; }
    /// <summary>Command line value shape.</summary>
    public ConfigurationArgumentKind Argument { get; init; }
}
