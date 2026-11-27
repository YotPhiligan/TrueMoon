using System.Collections.Concurrent;

namespace TrueMoon.Alloy.Hosting.Generation;

/// <summary>Constructor delegates contributed for a closed view type, keyed by the contributing scope.</summary>
internal static class ViewActivations
{
    private readonly record struct Key(Type View, Type? Arguments);

    private static readonly ConcurrentDictionary<Key, ConcurrentDictionary<string, Delegate>> _entries = new();

    internal static void Add(Type view, Type? arguments, string contribution, Delegate create)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentException.ThrowIfNullOrWhiteSpace(contribution);
        ArgumentNullException.ThrowIfNull(create);
        _entries.GetOrAdd(new Key(view, arguments),
            static _ => new ConcurrentDictionary<string, Delegate>(StringComparer.Ordinal))[contribution] = create;
    }

    internal static bool TryGet(Type view, Type? arguments, string? contribution, out Delegate? create)
    {
        create = null;
        if (!_entries.TryGetValue(new Key(view, arguments), out var entries)) return false;
        if (contribution is not null) return entries.TryGetValue(contribution, out create);
        if (entries.Count != 1) return false;
        using var enumerator = entries.Values.GetEnumerator();
        return enumerator.MoveNext() && (create = enumerator.Current) is not null;
    }

    internal static string[] Contributions(Type view, Type? arguments) =>
        _entries.TryGetValue(new Key(view, arguments), out var entries)
            ? [.. entries.Keys.OrderBy(key => key, StringComparer.Ordinal)]
            : [];
}
