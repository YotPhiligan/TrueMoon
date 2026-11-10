namespace TrueMoon.Configuration;

public class CommandLineArgsSection : ConfigurationSection
{
    private readonly object _mutationGate = new();
    private readonly HashSet<string> _overridden = new(StringComparer.Ordinal);
    public CommandLineArgsSection(Dictionary<string,object?> dictionary) : base("args", dictionary)
    {
        
    }

    /// <summary>Creates a legacy section retaining an explicit detached token capture for common bootstrap parsing.</summary>
    /// <param name="dictionary">Legacy key/value projection.</param><param name="arguments">Original tokens excluding the executable.</param>
    public CommandLineArgsSection(Dictionary<string, object?> dictionary, IReadOnlyList<string> arguments) : base("args", dictionary)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        Arguments = Array.AsReadOnly(arguments.ToArray());
    }

    /// <summary>Original captured tokens, or null for dictionary-only compatibility sections.</summary>
    public IReadOnlyList<string>? Arguments { get; }

    /// <summary>Tests whether a legacy callback explicitly changed a projected argument after capture.</summary>
    /// <param name="key">Exact legacy key.</param><returns>Whether Set was called, including explicit null/same-value overrides.</returns>
    public bool IsArgumentOverridden(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_mutationGate) return Arguments is null || _overridden.Contains(key);
    }

    /// <inheritdoc />
    public override void Set<T>(string key, T? value) where T : default
    {
        lock (_mutationGate) { base.Set(key, value); _overridden.Add(key); }
    }
}
