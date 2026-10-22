using System.Collections.Immutable;

namespace TrueMoon.Aether.Scenes;

internal static class AuthoringValidation
{
    internal static void Id(Guid value, string parameter)
    {
        if (value == Guid.Empty) throw new ArgumentException("An authoring identity must not be empty.", parameter);
    }

    internal static string Key(string value, string parameter)
    {
        ArgumentNullException.ThrowIfNull(value, parameter);
        if (value.Length is < 1 or > 128 || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '_' and not '-'))
            throw new ArgumentException("Property IDs require 1..128 ASCII letters, digits, dots, underscores or hyphens.", parameter);
        return value;
    }

    internal static ImmutableDictionary<string, PropertyValue> Values(IEnumerable<KeyValuePair<string, PropertyValue>>? values)
    {
        var result = ImmutableDictionary.CreateBuilder<string, PropertyValue>(StringComparer.Ordinal);
        foreach (var pair in values ?? [])
        {
            var key = Key(pair.Key, nameof(values));
            if (result.ContainsKey(key)) throw new ArgumentException("Duplicate property ID.", nameof(values));
            result.Add(key, pair.Value);
        }
        return result.ToImmutable();
    }

    internal static ImmutableHashSet<ComponentId> Components(IEnumerable<ComponentId>? values)
    {
        var result = ImmutableHashSet.CreateBuilder<ComponentId>();
        foreach (var id in values ?? [])
        {
            Id(id.Value, nameof(values));
            if (!result.Add(id)) throw new ArgumentException("Duplicate component identity.", nameof(values));
        }
        return result.ToImmutable();
    }
}
