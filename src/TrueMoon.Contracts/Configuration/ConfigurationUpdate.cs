namespace TrueMoon.Configuration;

/// <summary>The outcome of reload, or a coalesced successful publication notification.</summary>
public sealed record ConfigurationUpdate
{
    private readonly IReadOnlyList<string> _changedKeys = Array.Empty<string>();
    /// <summary>Creates a detached update.</summary>
    /// <param name="Generation">Current generation.</param><param name="Published">Whether a candidate was published.</param>
    /// <param name="RestartRequired">Whether startup fields prevented publication.</param><param name="ChangedKeys">Canonical changed keys.</param>
    public ConfigurationUpdate(long Generation, bool Published, bool RestartRequired, IReadOnlyList<string> ChangedKeys)
    { this.Generation = Generation; this.Published = Published; this.RestartRequired = RestartRequired; this.ChangedKeys = ChangedKeys; }
    /// <summary>Current generation.</summary>
    public long Generation { get; init; }
    /// <summary>Whether a candidate was published.</summary>
    public bool Published { get; init; }
    /// <summary>Whether startup fields prevented publication.</summary>
    public bool RestartRequired { get; init; }
    /// <summary>Detached canonical keys in ordinal order for runtime updates.</summary>
    public IReadOnlyList<string> ChangedKeys { get => _changedKeys; init { ArgumentNullException.ThrowIfNull(value); _changedKeys = Array.AsReadOnly(value.ToArray()); } }
}
