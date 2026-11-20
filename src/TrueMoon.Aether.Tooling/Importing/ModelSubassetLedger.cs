using System.Text;

namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Independent persistent identity namespaces within one model source.</summary>
public enum ModelSubassetKind
{
    /// <summary>Geometry resource.</summary>
    Mesh = 1,
    /// <summary>Opaque material resource.</summary>
    Material = 2,
    /// <summary>Material slot within one mesh.</summary>
    Slot = 3,
    /// <summary>Material binding-set resource.</summary>
    BindingSet = 4,
    /// <summary>One scene node/primitive occurrence.</summary>
    Instance = 5,
    /// <summary>External base-color texture resource.</summary>
    Texture = 6
}

/// <summary>Ordinal semantic key, independent of array positions and content revisions.</summary>
/// <param name="Kind">Subasset category.</param><param name="Locator">Source-local, bounded semantic locator.</param>
public readonly record struct ModelSubassetKey(ModelSubassetKind Kind, string Locator);

/// <summary>Persistent identity, including inactive tombstones.</summary>
/// <param name="Key">Current semantic key.</param><param name="Id">Random persistent identity.</param><param name="Active">False for deleted resources.</param>
public sealed record ModelSubassetEntry(ModelSubassetKey Key, Guid Id, bool Active);

/// <summary>Explicit same-kind rename or tombstone restore; From == To explicitly restores the same locator.</summary>
/// <param name="From">Existing active or inactive key.</param><param name="To">Discovered target key.</param>
public readonly record struct ModelSubassetRename(ModelSubassetKey From, ModelSubassetKey To);

/// <summary>Immutable source identity ledger. Reconciliation never mutates a published generation.</summary>
public sealed class ModelSubassetLedger
{
    /// <summary>Maximum active identities, including slots, binding sets and instances.</summary>
    public const int MaximumActive = 1024;
    /// <summary>Maximum retained identities, including tombstones.</summary>
    public const int MaximumEntries = 4096;
    /// <summary>Validates/copies an existing ledger; an empty ledger initializes a new source without IO.</summary>
    /// <param name="sourceId">Persistent source identity.</param><param name="entries">Existing active entries and tombstones.</param>
    public ModelSubassetLedger(Guid sourceId, IEnumerable<ModelSubassetEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (sourceId == Guid.Empty) throw new ArgumentException("Empty model source identity.", nameof(sourceId));
        var values = entries.Take(MaximumEntries + 1).ToArray();
        if (values.Length > MaximumEntries || values.Count(value => value is { Active: true }) > MaximumActive)
            throw new InvalidDataException("Model ledger entry budget exceeded.");
        var keys = new HashSet<ModelSubassetKey>(); var ids = new HashSet<Guid> { sourceId };
        foreach (var entry in values)
        {
            ArgumentNullException.ThrowIfNull(entry); ModelSubassetValidation.Key(entry.Key);
            if (entry.Id == Guid.Empty || !keys.Add(entry.Key) || !ids.Add(entry.Id)) throw new InvalidDataException("Duplicate or invalid model identity.");
        }
        SourceId = sourceId;
        Entries = Array.AsReadOnly(values.OrderBy(value => value.Key.Kind).ThenBy(value => value.Key.Locator, StringComparer.Ordinal).ToArray());
    }
    /// <summary>Persistent model source identity.</summary>
    public Guid SourceId { get; }
    /// <summary>Copied entries in canonical key order.</summary>
    public IReadOnlyList<ModelSubassetEntry> Entries { get; }

    /// <summary>Creates a complete candidate ledger. Deleted keys become tombstones; restoring one requires an explicit mapping.</summary>
    /// <param name="discovered">Unique semantic keys from a validated CPU candidate.</param>
    /// <param name="renames">Explicit one-to-one same-kind mappings, including restores.</param><returns>A detached immutable ledger.</returns>
    public ModelSubassetLedger Reconcile(IEnumerable<ModelSubassetKey> discovered, IEnumerable<ModelSubassetRename>? renames = null)
    {
        ArgumentNullException.ThrowIfNull(discovered);
        var requested = discovered.Take(MaximumActive + 1).ToArray(); var keys = new HashSet<ModelSubassetKey>();
        if (requested.Length > MaximumActive) throw new InvalidDataException("Active model identity budget exceeded.");
        foreach (var key in requested) { ModelSubassetValidation.Key(key); if (!keys.Add(key)) throw new InvalidDataException("Ambiguous semantic locator; provide explicit labels."); }
        var mappings = (renames ?? []).Take(MaximumActive + 1).ToArray();
        if (mappings.Length > MaximumActive) throw new InvalidDataException("Model rename budget exceeded.");
        var old = Entries.ToDictionary(entry => entry.Key); var from = new HashSet<ModelSubassetKey>(); var to = new Dictionary<ModelSubassetKey,ModelSubassetKey>();
        foreach (var mapping in mappings)
        {
            ModelSubassetValidation.Key(mapping.From); ModelSubassetValidation.Key(mapping.To);
            if (mapping.From.Kind != mapping.To.Kind || !old.ContainsKey(mapping.From) || !keys.Contains(mapping.To) ||
                !from.Add(mapping.From) || !to.TryAdd(mapping.To, mapping.From) ||
                mapping.From != mapping.To && (old.ContainsKey(mapping.To) || keys.Contains(mapping.From)))
                throw new InvalidDataException("Invalid, colliding or still-present model rename.");
        }
        var result = new List<ModelSubassetEntry>(); var ids = new HashSet<Guid>(Entries.Select(entry => entry.Id)) { SourceId };
        foreach (var key in requested)
        {
            var origin = to.GetValueOrDefault(key, key);
            if (old.TryGetValue(origin, out var entry))
            {
                if (!entry.Active && !from.Contains(origin)) throw new InvalidDataException("Tombstone restore requires an explicit mapping.");
                result.Add(new(key, entry.Id, true));
            }
            else
            {
                Guid id; do { id = Guid.NewGuid(); } while (!ids.Add(id));
                result.Add(new(key, id, true));
            }
        }
        foreach (var entry in Entries) if (!keys.Contains(entry.Key) && !from.Contains(entry.Key)) result.Add(entry with { Active = false });
        return new(SourceId, result);
    }
}

internal static class ModelSubassetValidation
{
    internal static void Key(ModelSubassetKey key)
    {
        if (!Enum.IsDefined(key.Kind)) throw new InvalidDataException("Unknown model subasset kind."); Locator(key.Locator);
    }
    internal static void Locator(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.Any(char.IsControl) || Encoding.UTF8.GetByteCount(value) > 256 || !value.IsNormalized())
            throw new InvalidDataException("Semantic locator must be NFC, nonempty, control-free and <= 256 UTF8 bytes.");
    }
}
