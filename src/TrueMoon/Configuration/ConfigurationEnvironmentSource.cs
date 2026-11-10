using System.Text.Json;

namespace TrueMoon.Configuration;

internal sealed class ConfigurationEnvironmentSource : IConfigurationSource
{
    private readonly string _prefix;
    private readonly HashSet<string>? _allowlist;
    private readonly Func<IEnumerable<KeyValuePair<string, string?>>> _variables;

    internal ConfigurationEnvironmentSource(string prefix, IReadOnlyList<string>? allowlist = null,
        Func<IEnumerable<KeyValuePair<string, string?>>>? variables = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        _prefix = prefix;
        _allowlist = allowlist is null ? null : new(ConfigurationMetadata.Names);
        foreach (var path in allowlist ?? [])
            if (!_allowlist!.Add(ConfigurationMetadata.Path(path))) throw new ArgumentException("Duplicate allowlist key.", nameof(allowlist));
        _variables = variables ?? ReadVariables;
    }

    public ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var map = ConfigurationMetadata.Keys(keys);
        var result = new List<ConfigurationEntry>();
        var seen = new HashSet<string>(ConfigurationMetadata.Names);
        foreach (var pair in _variables())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!pair.Key.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var path = pair.Key[_prefix.Length..].Replace("__", ":", StringComparison.Ordinal);
            if (_allowlist is not null && !_allowlist.Contains(path)) continue;
            if (!map.TryGetValue(path, out var key)) throw ConfigurationMetadata.Error("UnknownEnvironmentKey");
            if (!seen.Add(key.Path)) throw ConfigurationMetadata.Error("DuplicateEnvironmentKey", key.Path);
            result.Add(new(key.Path, JsonSerializer.SerializeToElement(pair.Value)));
        }
        return ValueTask.FromResult<IReadOnlyList<ConfigurationEntry>>(result.AsReadOnly());
    }

    private static IEnumerable<KeyValuePair<string, string?>> ReadVariables()
    {
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            yield return new((string)entry.Key, entry.Value as string);
    }
}
