using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Catalog;

// Typed resource catalog. A transaction is a bounded in-memory mutation set, committed by one KV batch.
internal sealed partial class NomCatalog(NomKvStore store,long domainIndexBytes=64L*1024*1024)
{
    private Dictionary<byte[], byte[]?>? changes;
    internal NomKvStore Store => store;
    internal CatalogHeader Header => NomCatalogRecordCodec.DecodeHeader(Required(NomCatalogKeys.Header));
    internal CatalogResourceControl Control => NomCatalogRecordCodec.DecodeControl(Required(NomCatalogKeys.Control));
    private byte[]? Get(byte[] key) => changes != null && changes.TryGetValue(key, out var value) ? value : store.Get(key);
    private byte[] Required(byte[] key) => Get(key) ?? throw new InvalidDataException("Mandatory Nom catalog record missing.");
    private void Set(byte[] key, byte[]? value)
    {
        if (changes == null) throw new InvalidOperationException("Catalog transaction not started.");
        changes[key] = value;
    }
    internal void Begin()
    { if (changes != null) throw new InvalidOperationException("Nested catalog transaction."); changes = new(NomKeyComparer.Instance); }
    internal void Rollback() => changes = null;
    internal void Commit()
    {
        var pending = changes ?? throw new InvalidOperationException("Catalog transaction not started.");
        try
        {
            var generation = Header.Generation;
            var preparedIndex = pending.Keys.Any(k => (NomKeySpace)k[1] is NomKeySpace.Asset or NomKeySpace.Revision or NomKeySpace.Tombstone)
                ? BuildIndex() : domainIndex;
            store.WriteBatch(new(checked(store.Sequence + 1), store.Generation, generation, Guid.NewGuid(), pending.Select(pair => new NomJournalMutation(pair.Key, pair.Value)).ToArray()));
            domainIndex = preparedIndex;
        }
        finally { changes = null; }
    }
    internal void Create(Guid project, Guid identity)
    {
        Begin(); try { SetHeader(new(project, identity, 0)); SetControl(new(1, 0)); Commit(); }
        catch { Rollback(); throw; }
    }
    internal void SetHeader(CatalogHeader header) => Set(NomCatalogKeys.Header, NomCatalogRecordCodec.EncodeHeader(header));
    internal void SetControl(CatalogResourceControl control) => Set(NomCatalogKeys.Control, NomCatalogRecordCodec.EncodeControl(control));
    internal AssetRecord? Asset(Guid id) => Get(NomCatalogKeys.Asset(id)) is { } bytes ? CatalogCodec.Decode(bytes) : null;
    internal void PutAsset(AssetRecord asset, bool insertOnly = false)
    {
        var old = Asset(asset.Id); if (insertOnly && old != null) throw new IOException("Duplicate asset identity.");
        NomBinary.Require(old == null || old.Source == asset.Source && old.Kind == asset.Kind, "Asset source/kind identity cannot be retargeted.");
        var binding = CatalogCodec.Binding(asset); var key = NomCatalogKeys.Binding(binding);
        if (Get(key) is { } existing && NomCatalogRecordCodec.DecodeBinding(existing) != asset.Id) throw new IOException("Duplicate asset binding.");
        if (old != null && CatalogCodec.Binding(old) != binding) Set(NomCatalogKeys.Binding(CatalogCodec.Binding(old)), null);
        Set(NomCatalogKeys.Asset(asset.Id), CatalogCodec.Encode(asset)); Set(key, NomCatalogRecordCodec.EncodeBinding(asset.Id));
        if (IsDeleted(asset.Id)) Set(TombstoneKey(asset.Id), null);
    }
    internal CatalogStoredResource? Revision(string revision) => Get(NomCatalogKeys.Revision(revision)) is { } bytes ? NomCatalogRecordCodec.DecodeRevision(bytes) : null;
    internal void PutRevision(CatalogStoredResource resource)
    {
        var key = NomCatalogKeys.Revision(resource.Version.Revision); var bytes = NomCatalogRecordCodec.EncodeRevision(resource);
        if (Get(key) is { } old) NomBinary.Require(old.AsSpan().SequenceEqual(bytes), "Immutable resource revision collision.");
        else Set(key, bytes);
    }
    internal string? Head(Guid id)
    {
        if (Get(NomCatalogKeys.Head(id)) is not { } bytes) return null;
        var head = NomCatalogRecordCodec.DecodeHead(bytes); NomBinary.Require(head.Asset == id, "Resource head identity mismatch."); return head.Revision;
    }
    internal void SetHead(Guid id, string revision) => Set(NomCatalogKeys.Head(id), NomCatalogRecordCodec.EncodeHead(id, revision));
    internal CatalogOperation? Operation(long sequence) => Get(NomCatalogKeys.Operation(sequence)) is { } bytes ? NomCatalogRecordCodec.DecodeOperation(bytes) : null;
    internal void SetOperation(CatalogOperation op) => Set(NomCatalogKeys.Operation(op.Sequence), NomCatalogRecordCodec.EncodeOperation(op));
    internal void DeleteOperation(long sequence) => Set(NomCatalogKeys.Operation(sequence), null);
    internal void SetItem(CatalogOperationItem item) => Set(NomCatalogKeys.Item(item.Sequence, item.Index), NomCatalogRecordCodec.EncodeItem(item));
    internal void DeleteItem(CatalogOperationItem item) => Set(NomCatalogKeys.Item(item.Sequence, item.Index), null);
    internal CatalogOperationItem[] Items(long sequence) => Rows(NomCatalogKeys.ItemPrefix(sequence), 256).Select(pair => NomCatalogRecordCodec.DecodeItem(pair.Value)).OrderBy(item => item.Index).ToArray();
    internal CatalogOperation[] Operations() => Rows(NomCatalogKeys.Prefix(NomKeySpace.Operation), 4096).Select(pair => NomCatalogRecordCodec.DecodeOperation(pair.Value)).OrderBy(op => op.Sequence).ToArray();
    internal AssetRecord[] Assets()
    {
        var rows = new List<AssetRecord>(); var bytes = 0;
        foreach (var pair in Rows(NomCatalogKeys.Prefix(NomKeySpace.Asset), 100_000))
        { var row = CatalogCodec.Decode(pair.Value); if (IsDeleted(row.Id)) continue; bytes = checked(bytes + pair.Value.Length); NomBinary.Require(rows.Count < 256 && bytes <= 1024 * 1024, "Detached catalog response budget."); rows.Add(row); }
        return rows.OrderBy(row => row.Id).ToArray();
    }
    private IEnumerable<NomKvPair> BaseRows(byte[] prefix)
    {
        byte[]? after = null; long? sequence = null;
        do
        {
            var page = store.ReadPage(prefix, after);
            NomBinary.Require(sequence == null || sequence == page.Sequence, "Catalog changed during paged read."); sequence = page.Sequence;
            foreach (var pair in page.Items) yield return pair;
            if (!page.HasMore) yield break;
            NomBinary.Require(page.Items.Count != 0, "Catalog page did not advance."); after = page.Items[^1].Key;
        } while (true);
    }
    private IEnumerable<NomKvPair> Rows(byte[] prefix, int maximum)
    {
        var seen = changes == null ? null : new HashSet<byte[]>(NomKeyComparer.Instance); var count = 0;
        foreach (var pair in BaseRows(prefix))
        {
            seen?.Add(pair.Key); var value = changes!=null && changes.TryGetValue(pair.Key,out var changed) ? changed : pair.Value; if (value == null) continue;
            NomBinary.Require(count++ < maximum, "Catalog query count budget."); yield return new(pair.Key, value);
        }
        if (changes == null) yield break;
        foreach (var pair in changes)
        {
            if (pair.Value == null || !pair.Key.AsSpan().StartsWith(prefix) || seen!.Contains(pair.Key)) continue;
            NomBinary.Require(count++ < maximum, "Catalog query count budget."); yield return new(pair.Key, pair.Value);
        }
    }
    internal void ValidateAll()
    {
        var header = Header; var control = Control;
        NomBinary.Require(header.ProjectId == store.ProjectId && header.StoreId == store.StoreId && header.Generation == store.Generation, "Catalog authority/generation mismatch.");
        var operations = 0;
        foreach (var pair in BaseRows([]))
        {
            var key = pair.Key; NomKeyCodec.Validate(key); byte[] expected;
            switch ((NomKeySpace)key[1])
            {
                case NomKeySpace.Control:
                    if (key.AsSpan().SequenceEqual(NomCatalogKeys.Header)) { _ = NomCatalogRecordCodec.DecodeHeader(pair.Value); expected = NomCatalogKeys.Header; }
                    else { _ = NomCatalogRecordCodec.DecodeControl(pair.Value); expected = NomCatalogKeys.Control; }
                    break;
                case NomKeySpace.Asset:
                    var asset = CatalogCodec.Decode(pair.Value); expected = NomCatalogKeys.Asset(asset.Id);
                    NomBinary.Require(Get(NomCatalogKeys.Binding(CatalogCodec.Binding(asset))) is { } binding && NomCatalogRecordCodec.DecodeBinding(binding) == asset.Id, "Missing/mismatched asset binding.");
                    break;
                case NomKeySpace.Binding:
                    var bound = Asset(NomCatalogRecordCodec.DecodeBinding(pair.Value)) ?? throw new InvalidDataException("Dangling asset binding."); expected = NomCatalogKeys.Binding(CatalogCodec.Binding(bound)); break;
                case NomKeySpace.Revision:
                    var resource = NomCatalogRecordCodec.DecodeRevision(pair.Value); expected = NomCatalogKeys.Revision(resource.Version.Revision); break;
                case NomKeySpace.Head:
                    var head = NomCatalogRecordCodec.DecodeHead(pair.Value); expected = NomCatalogKeys.Head(head.Asset);
                    var current = Asset(head.Asset) ?? throw new InvalidDataException("Dangling resource head.");
                    var revision = Revision(head.Revision) ?? throw new InvalidDataException("Missing resource head revision.");
                    NomBinary.Require(current.Id == revision.Version.AssetId && CatalogCodec.Encode(current).AsSpan().SequenceEqual(revision.Asset), "Resource head metadata mismatch."); break;
                case NomKeySpace.Operation:
                    var op = NomCatalogRecordCodec.DecodeOperation(pair.Value); expected = NomCatalogKeys.Operation(op.Sequence);
                    NomBinary.Require(++operations <= 4096 && op.Sequence > control.Floor && op.Sequence < control.Next, "Operation retention integrity."); break;
                case NomKeySpace.OperationItem:
                    var item = NomCatalogRecordCodec.DecodeItem(pair.Value); expected = NomCatalogKeys.Item(item.Sequence, item.Index);
                    var owner = Operation(item.Sequence) ?? throw new InvalidDataException("Dangling operation item.");
                    NomBinary.Require(item.Index < owner.Count && (owner.State == "committed" ? item.Revision != null : item.Revision == null), "Operation item state/closure."); break;
                default: expected = ValidateRetentionRecord(key, pair.Value); break;
            }
            NomBinary.Require(key.AsSpan().SequenceEqual(expected), "Noncanonical catalog key/value binding.");
        }
        domainIndex = BuildIndex(); ValidateRetention();
    }
}
