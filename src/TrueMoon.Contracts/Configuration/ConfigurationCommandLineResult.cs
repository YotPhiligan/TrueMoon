namespace TrueMoon.Configuration;

/// <summary>Canonical CLI entries and positional arguments.</summary>
public sealed record ConfigurationCommandLineResult
{
    /// <summary>Copies parser results.</summary>
    /// <param name="Entries">Entries.</param><param name="Positionals">Positional arguments.</param>
    public ConfigurationCommandLineResult(IReadOnlyList<ConfigurationEntry> Entries, IReadOnlyList<string> Positionals)
    { this.Entries = Array.AsReadOnly(Entries.ToArray()); this.Positionals = Array.AsReadOnly(Positionals.ToArray()); }
    /// <summary>Canonical entries.</summary>
    public IReadOnlyList<ConfigurationEntry> Entries { get; }
    /// <summary>Positional arguments.</summary>
    public IReadOnlyList<string> Positionals { get; }
}
