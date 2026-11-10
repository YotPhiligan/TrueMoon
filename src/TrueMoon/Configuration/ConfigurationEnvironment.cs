using System.Collections.Frozen;

namespace TrueMoon.Configuration;

/// <summary>Captures only explicitly named bootstrap environment variables.</summary>
public static class ConfigurationEnvironment
{
    /// <summary>Reads exact names without normalized merging or DI.</summary>
    /// <param name="names">Explicit environment names.</param><returns>Immutable capture, including missing null values.</returns>
    public static IReadOnlyDictionary<string, string?> Capture(params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (name.Contains('=') || name.Contains('\0') || !result.TryAdd(name, Environment.GetEnvironmentVariable(name)))
                throw new ArgumentException("Invalid or duplicate environment name.", nameof(names));
        }
        return result.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
