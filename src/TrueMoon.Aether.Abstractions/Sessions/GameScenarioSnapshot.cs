using System.Collections.Immutable;

namespace TrueMoon.Aether.Abstractions.Sessions;

/// <summary>Detached numeric observations sampled at a completed game tick.</summary>
public sealed class GameScenarioSnapshot
{
    /// <summary>Maximum number of named observations in one snapshot.</summary>
    public const int MaximumValues = 64;

    /// <summary>Completed simulation tick.</summary>
    public ulong Tick { get; }

    /// <summary>Positive version of the keys, units and semantics supplied by this game.</summary>
    public int Version { get; }

    /// <summary>Values under unique, case-sensitive keys.</summary>
    public ImmutableDictionary<string, GameScenarioNumber> Values { get; }

    /// <summary>Validates and copies a bounded set of finite, unit-labelled values.</summary>
    /// <param name="tick">Completed simulation tick.</param>
    /// <param name="version">Positive version of the game's observation schema.</param>
    /// <param name="values">Values with distinct nonblank keys up to 64 characters and units up to 32 characters.</param>
    public GameScenarioSnapshot(ulong tick, int version, IEnumerable<KeyValuePair<string, GameScenarioNumber>> values)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        ArgumentNullException.ThrowIfNull(values);
        var copy = ImmutableDictionary.CreateBuilder<string, GameScenarioNumber>(StringComparer.Ordinal);
        foreach (var (key, number) in values)
        {
            if (copy.Count == MaximumValues || string.IsNullOrWhiteSpace(key) || key.Length > 64 ||
                !double.IsFinite(number.Value) || string.IsNullOrWhiteSpace(number.Unit) || number.Unit.Length > 32 ||
                !copy.TryAdd(key, number))
                throw new ArgumentException("Invalid, duplicate, or excessive scenario observation.", nameof(values));
        }
        Tick = tick;
        Version = version;
        Values = copy.ToImmutable();
    }
}
