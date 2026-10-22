using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Serialization;

namespace TrueMoon.Aether.Checkpoints;

/// <summary>One stable entity's game-owned interaction state; authoring geometry is unchanged.</summary>
/// <param name="EntityId">Loaded scene entity ID.</param><param name="Active">Whether the gameplay switch is active.</param>
public readonly record struct GameObjectState(string EntityId, bool Active);

/// <summary>Immutable canonical object states with value equality across independent captures and codec roundtrips.</summary>
public sealed class GameObjectStates : IEquatable<GameObjectStates>
{
    /// <summary>Maximum supported interaction objects in a bounded scene.</summary>
    public const int MaximumCount = 64;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    /// <summary>Gets the empty state used by games without per-object interactions.</summary>
    public static GameObjectStates Empty { get; } = new(Array.Empty<GameObjectState>());
    /// <summary>Gets states in ordinal entity-ID order; storage cannot be mutated by a caller.</summary>
    public ImmutableArray<GameObjectState> Entries { get; }
    // Diagnostic JSON must use the same validation/copy path, with an exact property-type binding.
    [JsonConstructor]
    private GameObjectStates(ImmutableArray<GameObjectState> entries)
        : this(entries.IsDefault ? throw new InvalidDataException("Missing game object entries.") : entries.AsEnumerable()) { }
    /// <summary>Copies, validates and sorts a complete state set, rejecting duplicate identities.</summary>
    /// <param name="entries">At most 64 distinct, nonempty IDs of at most 128 UTF16 units and valid UTF8.</param>
    public GameObjectStates(IEnumerable<GameObjectState> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var values = entries.Take(MaximumCount + 1).ToArray();
        if (values.Length > MaximumCount) throw new InvalidDataException("Too many game object states.");
        foreach (var entry in values) ValidateId(entry.EntityId);
        Array.Sort(values, (a, b) => StringComparer.Ordinal.Compare(a.EntityId, b.EntityId));
        for (var i = 1; i < values.Length; i++)
            if (values[i - 1].EntityId == values[i].EntityId) throw new InvalidDataException("Duplicate game object state ID.");
        Entries = [.. values];
    }
    internal static void ValidateId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128) throw new InvalidDataException("Invalid game object ID.");
        try { if (Utf8.GetByteCount(id) > 512) throw new InvalidDataException("Game object ID is too long."); }
        catch (EncoderFallbackException error) { throw new InvalidDataException("Invalid game object ID UTF8.", error); }
    }
    /// <summary>Compares canonical entity identities and all active flags.</summary>
    /// <param name="other">Other captured state, or null.</param>
    public bool Equals(GameObjectStates? other) => other != null && Entries.AsSpan().SequenceEqual(other.Entries.AsSpan());
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is GameObjectStates other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() { var hash = new HashCode(); foreach (var entry in Entries) hash.Add(entry); return hash.ToHashCode(); }
}
