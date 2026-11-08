using System.Globalization;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Retention;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Catalog;

internal sealed partial class CatalogOwner
{
    private void RequireNoGarbage() => Require(Catalog.Garbage().Length == 0,"Resume pending garbage collection before publishing new resources.");
    private void RequireGcOffline()
    {
        RequireResourceReady();
        lock(admission) Require(activeResourceWrite == null && resourceReaders.Count == 0,"Finish reservations and close resource streams before garbage collection.");
    }
    internal Task<NomGarbageCollectionPlan> PlanGarbageCollectionAsync(CancellationToken token=default) => Enqueue(owner => owner.PlanGarbage(token),token:token);
    private NomGarbageCollectionPlan PlanGarbage(CancellationToken token)
    {
        RequireGcOffline(); RequireNoGarbage(); var index = Catalog.Index;
        var live = new HashSet<string>(StringComparer.Ordinal); var queue = new Queue<string>(); var rootWork = 0;
        void Mark(AssetResourceVersion version)
        {
            token.ThrowIfCancellationRequested(); Require(++rootWork <= 1_300_000,"GC root work budget.");
            Require(index.Versions.TryGetValue(version.Revision,out var stored) && stored == version,"GC root exact identity.");
            if (live.Add(version.Revision)) queue.Enqueue(version.Revision);
        }
        foreach (var id in index.Assets.Keys) if (Catalog.Head(id) is { } head) Mark(StoredVersion(head));
        foreach (var manifest in Catalog.Manifests()) foreach (var member in manifest.Members) Mark(member);
        foreach (var operation in Catalog.Operations())
            if (operation.State == "committed") foreach (var version in Outputs(operation,Items(operation))) Mark(version);
        var edgeWork = 0;
        while (queue.TryDequeue(out var revision))
            foreach (var dependency in index.Forward[revision]) { Require(++edgeWork <= 400_000,"GC edge work budget."); Mark(dependency); }
        var revisions = index.PruneCandidates(live,2048); var selected = revisions.ToHashSet(StringComparer.Ordinal);
        // Keep bytes for every revision left by this slice, including unreachable dependants awaiting a later slice.
        var digests = index.Versions.Where(p => !selected.Contains(p.Key)).Select(p => p.Value.Digest).ToHashSet(StringComparer.Ordinal);
        var inventory = ObjectInventory(token); var candidates = inventory.Where(p => !digests.Contains(p.Digest)).ToArray();
        var objects = candidates.Take(2048).ToArray();
        var hasMore = index.Versions.Count-live.Count > revisions.Length || candidates.Length > objects.Length;
        // Check the live namespace before the first metadata or object mutation.
        var inventoryByDigest = inventory.ToDictionary(o => o.Digest,o => o.Length,StringComparer.Ordinal);
        foreach (var version in live.Select(r => index.Versions[r]))
            Require(inventoryByDigest.TryGetValue(version.Digest,out var length) && length == version.Length,"Missing/mismatched live CAS object.");
        var fields = new List<string> { "TrueMoon.Nom/gc/1",Header.ProjectId.ToString("N"),Header.StoreId.ToString("N"),kvStore!.Sequence.ToString(CultureInfo.InvariantCulture) };
        fields.AddRange(revisions.Select(r => "revision:"+r));
        fields.AddRange(inventory.Select(o => "object:"+o.Digest+":"+o.Length.ToString(CultureInfo.InvariantCulture)));
        return new(Header.StoreId,kvStore.Sequence,CatalogCodec.Key(fields.ToArray()),revisions,objects,hasMore);
    }
    private (string Digest,long Length)[] ObjectInventory(CancellationToken token)
    {
        var root = Path.Combine(Root,"objects","sha256"); CheckPath(root);
        var result = new List<(string Digest,long Length)>(); var entries = 0;
        if (!Directory.Exists(root)) return [];
        foreach (var bucket in Directory.EnumerateFileSystemEntries(root))
        {
            token.ThrowIfCancellationRequested(); Require(++entries <= 100_256,"CAS inventory entry budget.");
            var attributes = File.GetAttributes(bucket); Require((attributes & FileAttributes.ReparsePoint) == 0,"Reparse CAS inventory unsupported.");
            var name = Path.GetFileName(bucket);
            if (name.Length != 2 || name.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')) || (attributes & FileAttributes.Directory) == 0) continue;
            foreach (var file in Directory.EnumerateFileSystemEntries(bucket))
            {
                token.ThrowIfCancellationRequested(); Require(++entries <= 100_256,"CAS inventory entry budget.");
                var fileAttributes = File.GetAttributes(file); Require((fileAttributes & FileAttributes.ReparsePoint) == 0,"Reparse CAS object unsupported.");
                var filename = Path.GetFileName(file);
                if ((fileAttributes & FileAttributes.Directory) != 0 || filename.Length != 69 || !filename.EndsWith(".blob",StringComparison.Ordinal)) continue;
                var digest = filename[..64];
                if (!digest.StartsWith(name,StringComparison.Ordinal) || digest.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f'))) continue;
                var length = new FileInfo(file).Length; Require(length <= ResourceCodec.MaximumObject,"GC object length budget."); result.Add((digest,length));
            }
        }
        return result.OrderBy(p => p.Digest,StringComparer.Ordinal).ToArray();
    }
    internal Task<NomGarbageCollectionReceipt> CollectGarbageAsync(NomGarbageCollectionPlan? plan=null,CancellationToken token=default) => Enqueue(owner =>
    {
        owner.RequireGcOffline(); var pruned = 0;
        if (plan != null)
        {
            Require(plan.StoreId == owner.Header.StoreId && plan.Sequence == owner.kvStore!.Sequence,"Stale/foreign garbage collection plan.");
            var current = owner.PlanGarbage(token); Require(current.Id == plan.Id,"Garbage collection inventory changed.");
            if (current.RevisionsToPrune != 0 || current.ObjectsToDelete != 0)
            {
                var entryBytes = NomJournalCodec.EntryHeaderBytes+40L;
                var prunePayload = entryBytes * current.RevisionsToPrune + (entryBytes+48) * current.ObjectsToDelete;
                var acknowledgementPayload = entryBytes * current.ObjectsToDelete;
                var added = Math.Max(0,current.ObjectsToDelete-current.RevisionsToPrune);
                owner.kvStore!.RequireAppendCapacity(new(current.ObjectsToDelete==0 ? 1 : 2,prunePayload+acknowledgementPayload,
                    prunePayload,current.RevisionsToPrune+current.ObjectsToDelete,40,current.ObjectsToDelete==0 ? 1 : 48,added,136L*added));
                owner.Begin();
                try
                {
                    foreach (var revision in current.Revisions) owner.Catalog.DeleteRevision(revision);
                    foreach (var item in current.Objects) owner.Catalog.SetGarbage(item.Digest,item.Length);
                    token.ThrowIfCancellationRequested(); owner.Commit(); pruned = current.RevisionsToPrune;
                }
                catch(Exception error) { owner.Rollback(error); throw; }
            }
        }
        else token.ThrowIfCancellationRequested();
        return owner.ResumeGarbage(pruned,token);
    },token:token);
    private NomGarbageCollectionReceipt ResumeGarbage(int pruned,CancellationToken token)
    {
        var pending = Catalog.Garbage(); var removed = new List<(string Digest,long Length)>();
        if (pending.Length != 0 && !token.IsCancellationRequested)
            kvStore!.RequireAppendCapacity(new(1,(NomJournalCodec.EntryHeaderBytes+40L)*pending.Length,(NomJournalCodec.EntryHeaderBytes+40L)*pending.Length,pending.Length,40,1,0,0));
        foreach (var item in pending)
        {
            if (token.IsCancellationRequested) break;
            try { var path = ObjectPath(item.Digest,false); files.DeleteFile(path); removed.Add(item); }
            catch(IOException) { /* Retain the durable intent for an explicit retry. */ }
            catch(UnauthorizedAccessException) { /* No live reference was pruned partially. */ }
        }
        if (removed.Count != 0)
        {
            Begin(); try { foreach (var item in removed) Catalog.DeleteGarbage(item.Digest); Commit(); }
            catch(Exception error) { Rollback(error); throw; }
        }
        return new(pruned,removed.Count,removed.Sum(p => p.Length),Catalog.Garbage().Length != 0);
    }
}
