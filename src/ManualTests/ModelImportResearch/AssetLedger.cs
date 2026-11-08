#if NETTERLOW
using System.Text.Json;

namespace ModelImportResearch;

internal sealed record AssetEntry(string Kind, string Locator, Guid Id, bool Active);
internal sealed record AssetLocator(string Kind, string Locator);

// Persist this ledger with the source, not with a content revision or traversal index.
internal sealed class AssetLedger
{
    internal Guid SourceId { get; }
    internal AssetEntry[] Entries => entries.ToArray();
    private readonly AssetEntry[] entries;
    internal AssetLedger(Guid sourceId, IEnumerable<AssetEntry> entries)
    {
        if (sourceId == Guid.Empty) throw new InvalidDataException("Empty source ID.");
        SourceId = sourceId; this.entries = entries.Take(4097).OrderBy(e => e.Kind, StringComparer.Ordinal).ThenBy(e => e.Locator, StringComparer.Ordinal).ToArray();
        if (this.entries.Length > 4096 || this.entries.Count(e => e.Active) > 1024) throw new InvalidDataException("Asset ledger quota.");
        var keys = new HashSet<AssetLocator>(); var ids = new HashSet<Guid> { sourceId };
        foreach (var e in this.entries)
            if (string.IsNullOrWhiteSpace(e.Kind) || e.Kind.Length > 64 || string.IsNullOrWhiteSpace(e.Locator) || e.Locator.Length > 256 || e.Id == Guid.Empty ||
                !keys.Add(new(e.Kind, e.Locator)) || !ids.Add(e.Id)) throw new InvalidDataException("Ambiguous asset identity.");
    }
    internal AssetLedger Reconcile(IEnumerable<AssetLocator> discovered, IReadOnlyDictionary<AssetLocator, AssetLocator>? renames = null)
    {
        renames ??= new Dictionary<AssetLocator, AssetLocator>();
        var requested = discovered.Take(1025).ToArray();
        if (requested.Length > 1024) throw new InvalidDataException("Asset count quota.");
        if (requested.Distinct().Count() != requested.Length) throw new InvalidDataException("Duplicate semantic locator requires explicit source disambiguation.");
        var old = entries.ToDictionary(e => new AssetLocator(e.Kind, e.Locator));
        var targets = new HashSet<AssetLocator>();
        foreach (var (from, to) in renames)
            if (!old.ContainsKey(from) || !requested.Contains(to) || from.Kind != to.Kind || !targets.Add(to) ||
                (from != to && old.ContainsKey(to))) throw new InvalidDataException("Invalid asset rename mapping.");
        var result = new List<AssetEntry>();
        foreach (var key in requested)
        {
            var source = renames.FirstOrDefault(p => p.Value == key).Key ?? key;
            if (renames.ContainsKey(key) && renames[key] != key) throw new InvalidDataException("Rename source is still present.");
            if (old.TryGetValue(source, out var entry))
            {
                if (!entry.Active && !renames.ContainsKey(source)) throw new InvalidDataException("Tombstone restore requires explicit mapping.");
                result.Add(new(key.Kind, key.Locator, entry.Id, true));
            }
            else result.Add(new(key.Kind, key.Locator, Guid.NewGuid(), true));
        }
        // Renamed keys are replaced; deleted keys retain their IDs as tombstones.
        foreach (var entry in entries)
        {
            var key = new AssetLocator(entry.Kind, entry.Locator);
            if (!requested.Contains(key) && !renames.ContainsKey(key)) result.Add(entry with { Active = false });
        }
        return new(SourceId, result);
    }
    internal string Save() => JsonSerializer.Serialize(new LedgerData(1, SourceId, entries));
    internal static AssetLedger Load(string json)
    {
        // 4096 entries * (64+256 UTF-16 chars escaped to at most six JSON chars + bounded metadata) < 16 Mi chars.
        if (json.Length > 16 * 1024 * 1024) throw new InvalidDataException("Ledger input quota.");
        var data = JsonSerializer.Deserialize<LedgerData>(json) ?? throw new InvalidDataException("Missing ledger.");
        if (data.Version != 1 || data.Entries == null) throw new InvalidDataException("Ledger version.");
        return new(data.SourceId, data.Entries);
    }
    private sealed record LedgerData(int Version, Guid SourceId, AssetEntry[] Entries);
}

internal sealed record PublishedImport(long Generation, string InputRevision, string Fingerprint, AssetLedger Ledger);
internal sealed class ImportPublication(PublishedImport initial)
{
    private readonly object gate = new();
    internal PublishedImport Current { get; private set; } = initial;
    internal void Commit(long expectedGeneration, string revision, string fingerprint, AssetLedger ledger, CancellationToken token)
    {
        lock (gate)
        {
            token.ThrowIfCancellationRequested();
            if (Current.Generation != expectedGeneration || ledger.SourceId != Current.Ledger.SourceId)
                throw new InvalidOperationException("Stale import or changed source identity.");
            Current = new(checked(expectedGeneration + 1), revision, fingerprint, ledger);
        }
    }
}
#endif
