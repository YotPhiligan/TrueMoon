using System.Globalization;
using System.Security.Cryptography;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Retention;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;


namespace TrueMoon.Nom.Catalog;

internal sealed partial class CatalogOwner
{
    private sealed record PreparedSpec(byte[] Asset, long Maximum, long? Length, string? Digest);
    private sealed class ResourceWrite(PreparedSpec[] specs, long total)
    {
        internal readonly PreparedSpec[] Specs = specs;
        internal readonly long Total = total;
        internal AssetWriteTicket Ticket = null!;
        internal bool Reserving = true;
        internal bool Running;
        internal bool Committed;
        internal bool OutcomeUncertain;
    }
    private readonly CancellationTokenSource resourceShutdown = new();
    private readonly HashSet<ResourceReadStream> resourceReaders = [];
    private ResourceWrite? activeResourceWrite;
    private int resourceUsers;
    private bool resourceRecoveryRequired;
    private Task? stopReserved;
    internal int ActiveResourceUsers { get { lock(admission) return resourceUsers; } }
    private void RequireResourceReady() => Require(!resourceRecoveryRequired && kvStore?.IsFaulted != true,"Resource commit outcome unknown; close/reopen required.");
    private void CompleteAfterResourceDrain() { if(stopping && resourceUsers == 0 && !queue.IsAddingCompleted) queue.CompleteAdding(); }
    private void EndWrite(ResourceWrite write)
    {
        lock(admission)
        {
            if(!ReferenceEquals(activeResourceWrite,write)) return;
            activeResourceWrite=null; resourceUsers--; CompleteAfterResourceDrain();
        }
    }
    internal void ReleaseResourceReader(ResourceReadStream stream)
    { lock(admission) { snapshotReaders.Remove(stream); if(resourceReaders.Remove(stream)) resourceUsers--; CompleteAfterResourceDrain(); } }
    private void StopReservedResource()
    {
        ResourceWrite? write;
        lock(admission)
        {
            write=activeResourceWrite;
            if(write == null || write.Reserving || write.Running) return;
            write.Running=true;
        }
        try
        {
            var control=Enqueue(owner =>
            {
                try { owner.AbortWrite(write); owner.EndWrite(write); return true; }
                catch(Exception error) { lock(owner.admission) { write.Running=false; owner.ReportDrainFailure(error); } throw; }
            },control:true);
            stopReserved=ObserveStopCleanup(control);
        }
        catch { lock(admission) write.Running=false; throw; }
    }
    private async Task ObserveStopCleanup(Task control)
    {
        try { await control.ConfigureAwait(false); }
        catch { /* The control body already reported the error to its drain attempt synchronously. */ }
    }
    private static string Sequence(long sequence)
    { Require(sequence > 0,"Invalid operation sequence."); return sequence.ToString("x16",CultureInfo.InvariantCulture); }
    private CatalogResourceControl Control() => Catalog.Control;
    private CatalogOperation Operation(AssetWriteTicket ticket)
    {
        Require(ticket.StoreId == Header.StoreId, "Foreign operation ticket."); ResourceCodec.Digest(ticket.RequestDigest);
        var control = Control();
        if (ticket.Sequence <= control.Floor) throw new ExpiredOperationException();
        var op = Catalog.Operation(ticket.Sequence);
        if (op == null) { Require(ticket.Sequence > 0 && ticket.Sequence < control.Next, "Unknown operation ticket."); throw new AbortedOperationException(); }
        ValidateOperation(op); Require(op.Request == ticket.RequestDigest, "Operation request mismatch."); return op;
    }
    private static void ValidateOperation(CatalogOperation op) => NomCatalogRecordCodec.ValidateOperation(op);
    private CatalogOperationItem[] Items(CatalogOperation op)
    {
        var items = Catalog.Items(op.Sequence); Require(items.Length == op.Count, "Operation item closure.");
        for (var i = 0; i < items.Length; i++)
            Require(items[i].Sequence == op.Sequence && items[i].Index == i && (op.State == "committed" ? items[i].Revision != null : items[i].Revision == null), "Operation item order/state.");
        long known = 0;
        foreach (var item in items) if (item.ExpectedLength is { } length) { known = checked(known + length); Require(known <= op.Total, "Journal declared total budget."); }
        var specs = items.Select(item => new PreparedSpec(item.Asset, item.Maximum, item.ExpectedLength, item.ExpectedDigest)).ToArray();
        var ids = specs.Select(p => CatalogCodec.Decode(p.Asset).Id).ToArray();
        Require(ids.Distinct().Count() == ids.Length && ids.SequenceEqual(ids.Order()), "Operation canonical item order.");
        Require(RequestHash(op.Expected, op.Total, specs) == op.Request, "Journal request integrity."); return items;
    }
    private string RequestHash(long generation, long total, IEnumerable<PreparedSpec> specs)
    {
        var fields = new List<string> { "TrueMoon.Nom/request/1", Header.StoreId.ToString("N"), generation.ToString(CultureInfo.InvariantCulture), total.ToString(CultureInfo.InvariantCulture) };
        foreach (var p in specs) fields.AddRange([ResourceCodec.Hash(p.Asset), p.Maximum.ToString(CultureInfo.InvariantCulture), p.Length?.ToString(CultureInfo.InvariantCulture) ?? "unknown", p.Digest ?? "unknown"]);
        return CatalogCodec.Key(fields.ToArray());
    }
    internal Task<AssetWriteTicket> ReserveResourcesAsync(long expectedGeneration, IReadOnlyList<AssetResourceWrite> specs, long maximumTotalBytes = ResourceCodec.MaximumBatch, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(specs);
        Require(expectedGeneration >= 0 && maximumTotalBytes is > 0 and <= ResourceCodec.MaximumBatch && specs.Count > 0 && specs.Count <= limits.BatchRows, "Resource batch budget.");
        var prepared = new List<PreparedSpec>(); var ids = new HashSet<Guid>(); var bytes = 0; long known = 0;
        long physical = 183; // Header/outcome update plus worst-case asset/binding/revision/head/item mutations.
        var maximumPayload = new NomFormatLimits().MaximumPayloadBytes;
        foreach (var spec in specs)
        {
            Require(prepared.Count < limits.BatchRows, "Resource descriptor count budget."); ArgumentNullException.ThrowIfNull(spec); ArgumentNullException.ThrowIfNull(spec.Asset);
            Require(spec.MaximumBytes is > 0 and <= ResourceCodec.MaximumObject && spec.ExpectedLength is null or >= 0, "Resource length budget.");
            if (spec.ExpectedLength is { } n) { Require(n <= spec.MaximumBytes, "Declared object budget."); known = checked(known + n); }
            if (spec.ExpectedDigest != null) ResourceCodec.Digest(spec.ExpectedDigest);
            Require(ids.Add(spec.Asset.Id), "Duplicate resource ID in batch."); var encoded = CatalogCodec.Encode(spec.Asset, limits.DocumentBytes);
            bytes = checked(bytes + encoded.Length); Require(bytes <= limits.BatchBytes, "Resource metadata budget.");
            physical += 3L * encoded.Length + 567;
            Require(physical <= maximumPayload, "Whole resource publication KV payload budget.");
            prepared.Add(new(encoded, spec.MaximumBytes, spec.ExpectedLength, spec.ExpectedDigest));
        }
        Require(prepared.Count == specs.Count && known <= maximumTotalBytes, "Captured resource descriptor/total budget.");
        var captured = prepared.OrderBy(p => CatalogCodec.Decode(p.Asset).Id).ToArray(); var request = RequestHash(expectedGeneration, maximumTotalBytes, captured);
        return Enqueue(owner => owner.ReserveWrite(expectedGeneration, captured, maximumTotalBytes, request, token), bytes, token);
    }
    private AssetWriteTicket ReserveWrite(long generation, PreparedSpec[] specs, long total, string request, CancellationToken token)
    {
        RequireResourceReady(); RequireNoGarbage(); Require(Header.Generation == generation, "Stale resource generation."); _ = checked(generation + 1);
        // Reserve high-water + pending + worst-case publication (or the smaller abort) before any allocation.
        // The worker forbids other catalog writes while this reservation is active.
        var payload = 344L; var publication = 183L; var maximumValue = 0;
        var tombstones = 0;
        foreach (var spec in specs)
        { payload += 4L * spec.Asset.Length + 710; publication += 3L * spec.Asset.Length + 567; maximumValue = Math.Max(maximumValue, spec.Asset.Length + 116); }
        foreach (var spec in specs) if (Catalog.IsDeleted(CatalogCodec.Decode(spec.Asset).Id)) tombstones++;
        payload += (NomJournalCodec.EntryHeaderBytes+24L) * tombstones; publication += (NomJournalCodec.EntryHeaderBytes+24L) * tombstones;
        kvStore!.RequireAppendCapacity(new(3, payload, publication, 6 * specs.Length + 2 + tombstones, 40, maximumValue, 5 * specs.Length + 1, 112L + 632L * specs.Length));
        var write = new ResourceWrite(specs, total);
        lock (admission) { ObjectDisposedException.ThrowIf(stopping, this); if (activeResourceWrite != null) throw new InvalidOperationException("A resource reservation is active."); activeResourceWrite = write; resourceUsers++; }
        try
        {
            var control = Control(); var next = control.Next; _ = checked(next + 1); Require(Catalog.Operations().Length < 4096, "Operation history full.");
            Begin();
            try { Catalog.SetControl(control with { Next = next + 1 }); Commit(); }
            catch (Exception error) { if (error is NomKvUnknownOutcomeException) { resourceRecoveryRequired = true; throw new IOException("Reservation allocation uncertain; no ticket issued; reopen required.", error); } Rollback(error); throw; }
            write.Ticket = new(Header.StoreId, next, request); Begin();
            try
            {
                Catalog.SetOperation(new(next, request, generation, total, specs.Length, "pending", null));
                for (var i = 0; i < specs.Length; i++)
                {
                    var p = specs[i]; var item = new CatalogOperationItem(next, i, p.Asset, p.Maximum, p.Length, p.Digest, null);
                    Require(NomCatalogRecordCodec.EncodeItem(item).Length <= limits.DocumentBytes, "Journal item budget."); Catalog.SetItem(item);
                }
                token.ThrowIfCancellationRequested(); Commit();
            }
            catch (Exception error) { if (error is NomKvUnknownOutcomeException) { resourceRecoveryRequired = true; throw new UnknownOutcomeException(write.Ticket, error); } Rollback(error); throw; }
            bool stop;
            lock (admission) { write.Reserving = false; stop = stopping; }
            if (stop) StopReservedResource();
            return write.Ticket;
        }
        catch { EndWrite(write); throw; }
    }
    internal Task<AssetWriteOutcome> LookupResourceOperationAsync(AssetWriteTicket ticket) => Enqueue(owner =>
    {
        owner.RequireResourceReady(); CatalogOperation op;
        try { op = owner.Operation(ticket); } catch (AbortedOperationException) { return new AssetWriteOutcome("aborted", null, []); }
        var items = owner.Items(op); return new AssetWriteOutcome(op.State, op.Result, op.State == "committed" ? owner.Outputs(op, items) : []);
    });
    internal Task AbortResourceOperationAsync(AssetWriteTicket ticket) => Enqueue(owner =>
    {
        owner.RequireResourceReady(); var op = owner.Operation(ticket); _ = owner.Items(op); if (op.State != "pending") return true;
        ResourceWrite? write;
        lock (owner.admission) { write = owner.activeResourceWrite; Require(write != null && write.Ticket == ticket && !write.Reserving && !write.Running, "Write is already running."); write!.Running = true; }
        try { owner.AbortWrite(write); owner.EndWrite(write); return true; } catch { lock (owner.admission) write.Running = false; throw; }
    });
    internal Task PruneResourceOperationsAsync(long throughSequence) => Enqueue(owner =>
    {
        owner.RequireResourceReady(); lock (owner.admission) Require(owner.activeResourceWrite == null, "Write operation active."); var control = owner.Control();
        Require(throughSequence >= control.Floor && throughSequence < control.Next, "Invalid operation retention floor."); if (throughSequence == control.Floor) return true;
        var ops = owner.Catalog.Operations().Where(op => op.Sequence <= throughSequence).ToArray();
        foreach (var op in ops) { ValidateOperation(op); Require(op.State != "pending", "Pending outcome cannot expire."); }
        owner.Begin();
        try { foreach (var op in ops) { foreach (var item in owner.Items(op)) owner.Catalog.DeleteItem(item); owner.Catalog.DeleteOperation(op.Sequence); } owner.Catalog.SetControl(control with { Floor = throughSequence }); owner.Commit(); }
        catch (Exception error) { owner.Rollback(error); throw; } return true;
    });
    private AssetResourceVersion StoredVersion(string revision)
    { ResourceCodec.Digest(revision); return (Catalog.Revision(revision) ?? throw new InvalidDataException("Missing resource revision.")).Version; }
    private AssetResourceVersion[] Outputs(CatalogOperation op, CatalogOperationItem[] items)
    {
        long total = 0; var versions = new AssetResourceVersion[items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i]; var record = Catalog.Revision(item.Revision!) ?? throw new InvalidDataException("Missing operation revision."); var version = record.Version;
            Require(record.Asset.AsSpan().SequenceEqual(item.Asset), "Journal output metadata mismatch.");
            Require(version.Length <= item.Maximum && (item.ExpectedLength == null || version.Length == item.ExpectedLength) && (item.ExpectedDigest == null || version.Digest == item.ExpectedDigest), "Journal output length/digest mismatch.");
            total = checked(total + version.Length); Require(total <= op.Total, "Journal output total budget."); versions[i] = version;
        }
        return versions;
    }
    internal Task<AssetResourceVersion?> FindResourceAsync(Guid assetId) => Enqueue(owner =>
    { owner.RequireResourceReady(); var revision = owner.Catalog.Head(assetId); if (revision == null) return null; var version = owner.StoredVersion(revision); Require(version.AssetId == assetId, "Resource head identity mismatch."); return version; });
    internal async Task<Stream> OpenResourceAsync(AssetResourceVersion exact, CancellationToken token = default, ResourceGate? readGate = null, AssetSnapshot? snapshot = null)
    {
        token.ThrowIfCancellationRequested();
        var stream = await Enqueue(owner =>
        {
            owner.RequireResourceReady();
            lock (owner.admission)
            {
                if (snapshot != null) ObjectDisposedException.ThrowIf(snapshot.Closed || !owner.snapshots.Contains(snapshot), snapshot);
                Require(owner.StoredVersion(exact.Revision) == exact, "Exact resource key mismatch.");
                ObjectDisposedException.ThrowIf(owner.stopping, owner); if (owner.resourceReaders.Count >= 16) throw new InvalidOperationException("Resource read lease budget.");
                if (snapshot != null)
                {
                    ObjectDisposedException.ThrowIf(snapshot.Closed || !owner.snapshots.Contains(snapshot), snapshot);
                    Require(snapshot.Manifest.Members.Contains(exact), "Resource is not an exact snapshot member.");
                }
                var result = new ResourceReadStream(owner, exact, owner.resourceShutdown.Token, readGate); owner.resourceReaders.Add(result);
                if (snapshot != null) owner.snapshotReaders.Add(result,snapshot.Manifest.Id);
                owner.resourceUsers++; return result;
            }
        }, token: token).ConfigureAwait(false);
        try { await stream.InitializeAsync(ObjectPath(exact.Digest, false), token).ConfigureAwait(false); return stream; }
        catch { await stream.DisposeAsync().ConfigureAwait(false); throw; }
    }
    private string ObjectPath(string digest, bool create)
    {
        ResourceCodec.Digest(digest); var directory = Path.Combine(Root, "objects", "sha256", digest[..2]); CheckPath(directory);
        if (create) { Directory.CreateDirectory(directory); CheckPath(directory); }
        var path = Path.Combine(directory, digest + ".blob"); if (File.Exists(path)) Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0, "Reparse object unsupported."); return path;
    }
    private string StageDirectory(AssetWriteTicket ticket, bool create)
    {
        Require(ticket.StoreId == Header.StoreId, "Foreign stage ticket."); var path = Path.Combine(Root, "staging", Sequence(ticket.Sequence)); CheckPath(path);
        if (create) { Directory.CreateDirectory(path); CheckPath(path); } return path;
    }
    private void CleanupStage(AssetWriteTicket ticket, int count)
    {
        var directory = StageDirectory(ticket, false); if (!Directory.Exists(directory)) return;
        for (var i = 0; i < count; i++)
        { var file = Path.Combine(directory, i.ToString("D4", CultureInfo.InvariantCulture) + ".tmp"); if (File.Exists(file)) { Require((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0, "Reparse stage unsupported."); files.DeleteStagingFile(file); } }
        files.DeleteStagingDirectory(directory);
    }
    private void AbortWrite(ResourceWrite write)
    {
        // A poisoned KV owner cannot append an abort. Release only owned staging now;
        // validated reopen will abort the durable pending operation before exposing it.
        if (write.Committed || write.OutcomeUncertain || kvStore?.IsFaulted == true || resourceRecoveryRequired)
        { CleanupStage(write.Ticket, write.Specs.Length); return; }
        Exception? metadataError = null;
        try
        {
            RequireResourceReady(); var op = Operation(write.Ticket);
            if (op.State == "pending")
            {
                Begin();
                try { Catalog.SetOperation(op with { State = "aborted" }); Commit(); }
                catch (Exception error)
                { Rollback(error); if (error is NomKvUnknownOutcomeException) { write.OutcomeUncertain = true; resourceRecoveryRequired = true; throw new UnknownOutcomeException(write.Ticket, error); } throw; }
            }
        }
        catch (Exception error) { metadataError = error; }
        try { CleanupStage(write.Ticket, write.Specs.Length); }
        catch (Exception cleanup) { if (metadataError != null) throw new AggregateException("Abort metadata and staging cleanup failed.", metadataError, cleanup); throw; }
        if (metadataError != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(metadataError).Throw();
    }
    private void RecoverResources()
    {
        var operations = Catalog.Operations(); var control = Control();
        foreach (var op in operations)
        {
            ValidateOperation(op); Require(op.Sequence > control.Floor && op.Sequence < control.Next && op.Expected <= Header.Generation, "Journal sequence/generation integrity."); var items = Items(op);
            if (op.State == "committed") { Require(op.Result <= Header.Generation, "Journal generation integrity."); _ = Outputs(op, items); }
        }
        // Domain schemas/cross-references have all passed before the first tail/staging mutation.
        kvStore!.CompleteRecovery();
        foreach (var op in operations)
        {
            Exception? metadataError = null;
            if (op.State == "pending")
            {
                try { Begin(); try { Catalog.SetOperation(op with { State = "aborted" }); Commit(); } catch (Exception error) { Rollback(error); throw; } }
                catch (Exception error) { metadataError = error; }
            }
            try { CleanupStage(new(Header.StoreId, op.Sequence, op.Request), op.Count); }
            catch (Exception cleanup) { if (metadataError != null) throw new AggregateException("Recovery metadata and cleanup failed.", metadataError, cleanup); throw; }
            if (metadataError != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(metadataError).Throw();
        }
    }
    internal Task<NomCompactionReceipt> CompactAsync(CancellationToken token = default) => Enqueue(owner =>
    {
        owner.RequireResourceReady();
        lock (owner.admission) Require(owner.activeResourceWrite == null && owner.resourceReaders.Count == 0, "Finish reservations and close read leases before compaction.");
        return owner.kvStore!.Compact(token);
    }, token: token);
}
