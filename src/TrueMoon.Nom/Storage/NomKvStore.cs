using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Storage;

internal sealed partial class NomKvStore : IDisposable
{
    private sealed class OwnedFile(FileStream stream)
    {
        internal readonly FileStream Stream = stream;
        internal readonly SafeFileHandle Handle = stream.SafeFileHandle;
    }
    private readonly object gate = new();
    private readonly NomKvOpenArguments arguments;
    private readonly NomFormatLimits limits;
    private readonly NomKvFileOperations files;
    private readonly Action<NomKvStore> closed;
    private readonly List<OwnedFile> owned = [];
    private readonly Dictionary<long, FileStream> segments = [];
    private Dictionary<byte[], NomValueLocation> index = new(NomKeyComparer.Instance);
    private byte[][]? orderedKeys;
    private OwnedFile? rootLock;
    private OwnedFile? authority;
    private FileStream? active;
    private NomJournalHeader? activeHeader;
    private NomRootDescriptor? descriptor;
    private long validLength, sequence, generation, indexBytes;
    private int stopping;
    private bool ready, faulted, isClosed;
    private bool pendingTail;
    internal string Root => arguments.Root;
    internal Guid ProjectId => arguments.ProjectId;
    internal Guid StoreId => arguments.StoreId;
    internal long Sequence => Interlocked.Read(ref sequence);
    internal long Generation => Interlocked.Read(ref generation);
    internal bool IsFaulted => Volatile.Read(ref faulted);
    internal bool IsClosed => Volatile.Read(ref isClosed);
    internal long RecoveredTailBytes { get; private set; }
    internal bool HasPendingTail { get { lock (gate) return pendingTail; } }
    internal int KeyCount { get { lock (gate) return index.Count; } }
    internal long IndexBytes => Interlocked.Read(ref indexBytes);

    internal NomKvStore(NomKvOpenArguments arguments, NomKvFileOperations files, Action<NomKvStore> closed)
    { this.arguments = arguments; limits = arguments.Limits!; this.files = files; this.closed = closed; }

    internal void Initialize(bool create)
    {
        lock (gate)
        {
            EnsureAdmission(); arguments.Token.ThrowIfCancellationRequested(); RejectReparseAncestors(Root);
            if (create) CreateFiles(); else OpenFiles();
            arguments.Token.ThrowIfCancellationRequested(); EnsureAdmission(); ready = true;
        }
    }
    private OwnedFile OpenOwned(string path, FileMode mode, FileAccess access, FileShare share)
    {
        RejectReparse(path); var file = new OwnedFile(files.OpenFile(path, mode, access, share)); owned.Add(file); return file;
    }
    private string SegmentPath(long id) => Path.Combine(Root, "journal", id.ToString("D20", System.Globalization.CultureInfo.InvariantCulture) + ".nom");
    private void CreateFiles()
    {
        if (Directory.Exists(Root) && Directory.EnumerateFileSystemEntries(Root).Any()) throw new InvalidDataException("Nom Create requires a new or empty root.");
        Directory.CreateDirectory(Root); RejectReparseAncestors(Root);
        rootLock = OpenOwned(Path.Combine(Root, "owner.lock"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        var journal = Path.Combine(Root, "journal"); RejectReparse(journal);
        if (Directory.Exists(journal)) throw new InvalidDataException("Nom journal directory already exists.");
        Directory.CreateDirectory(journal);
        activeHeader = new(ProjectId, StoreId); descriptor = new(ProjectId, StoreId);
        active = OpenOwned(SegmentPath(1), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read).Stream; segments.Add(1, active);
        var header = NomJournalCodec.EncodeHeader(activeHeader); files.Write(active, header); files.Flush(active);
        validLength = header.Length;
        var pendingPath = Path.Combine(Root, "nom.root.pending");
        var pending = OpenOwned(pendingPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        files.Write(pending.Stream, NomRootCodec.Encode(descriptor)); files.Flush(pending.Stream); CloseFile(pending);
        arguments.Token.ThrowIfCancellationRequested(); EnsureAdmission();
        // Authority is published last. An interrupted Create remains explicit uninitialized data, never reset.
        files.PublishRoot(pendingPath, Path.Combine(Root, "nom.root"));
        authority = OpenOwned(Path.Combine(Root, "nom.root"), FileMode.Open, FileAccess.Read, FileShare.Read);
        _ = NomRootCodec.Decode(ReadRoot(authority.Stream), ProjectId, StoreId);
    }
    private void OpenFiles()
    {
        if (!Directory.Exists(Root)) throw new DirectoryNotFoundException("Nom root does not exist.");
        if (!arguments.AllowBackupRoot && File.Exists(Path.Combine(Root,"nom.backup"))) throw new InvalidDataException("Immutable Nom backup: use the explicit restore factory.");
        if (File.Exists(Path.Combine(Root, "catalog.litedb"))) throw new InvalidDataException("Legacy LiteDB root is unsupported by the Nom KV format.");
        authority = OpenOwned(Path.Combine(Root, "nom.root"), FileMode.Open, FileAccess.Read, FileShare.Read);
        descriptor = NomRootCodec.Decode(ReadRoot(authority.Stream), ProjectId, StoreId);
        rootLock = OpenOwned(Path.Combine(Root, "owner.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var directory = Path.Combine(Root, "journal"); RejectReparse(directory);
        NomRecoveryResult? previous = null; NomCheckpointResult? checkpoint = null;
        if (descriptor.CheckpointSequence != 0)
        {
            RejectReparse(Path.Combine(Root, "checkpoints"));
            var file = OpenOwned(CheckpointPath(descriptor.CheckpointDigest), FileMode.Open, FileAccess.Read, FileShare.Read).Stream;
            checkpoint = NomCheckpointCodec.Read(file, descriptor, limits, arguments.Token); segments.Add(0, file);
        }
        for (var id = descriptor.FirstSegmentId; ; id++)
        {
            arguments.Token.ThrowIfCancellationRequested(); EnsureAdmission();
            var isActive = id == descriptor.ActiveSegmentId;
            var file = OpenOwned(SegmentPath(id), FileMode.Open, isActive ? FileAccess.ReadWrite : FileAccess.Read, FileShare.Read).Stream;
            segments.Add(id, file);
            var header = previous == null ? checkpoint == null ? new NomJournalHeader(ProjectId, StoreId) :
                new NomJournalHeader(ProjectId, StoreId, id, descriptor.CheckpointSequence, descriptor.CheckpointGeneration, descriptor.PreviousSegmentHash) :
                new(ProjectId, StoreId, id, previous.Sequence, previous.Generation, previous.SealedSegmentHash!);
            var recovered = NomJournalRecovery.Recover(file, header, sealedSegment: !isActive,
                minimumSequence: isActive ? descriptor.MinimumSequence : 0, limits: limits, token: arguments.Token, previous: previous, checkpoint: previous == null ? checkpoint : null);
            if (!isActive) { previous = recovered; continue; }
            index = recovered.CopyEntries(limits.MaximumKeyBytes); indexBytes = recovered.IndexBytes;
            sequence = recovered.Sequence; generation = recovered.Generation; active = file; activeHeader = header; validLength = recovered.ValidLength;
            RecoveredTailBytes = file.Length - validLength;
            pendingTail = recovered.HasIncompleteTail;
            if (pendingTail && arguments.RepairIncompleteTail)
            {
                arguments.Token.ThrowIfCancellationRequested(); EnsureAdmission();
                files.Truncate(file, validLength); files.Flush(file);
                pendingTail = false;
            }
            file.Position = validLength; break;
        }
    }
    private static byte[] ReadRoot(FileStream file)
    {
        NomBinary.Require(file.Length == NomRootCodec.RootBytes, "Nom root descriptor length.");
        var bytes = new byte[NomRootCodec.RootBytes]; file.Position = 0; file.ReadExactly(bytes); return bytes;
    }
    internal byte[]? Get(byte[] key) => GetMany([key]).Values[0];
    internal void CompleteRecovery()
    {
        lock (gate)
        {
            EnsureReady(); if (!pendingTail) return;
            try { files.Truncate(active!, validLength); files.Flush(active!); pendingTail = false; active!.Position = validLength; }
            catch { Volatile.Write(ref faulted, true); throw; }
        }
    }
    internal NomKvPage ReadPage(byte[] prefix, byte[]? afterExclusive = null, int maximumItems = 256, int maximumBytes = 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        NomBinary.Require(prefix.Length <= limits.MaximumKeyBytes && (afterExclusive == null || afterExclusive.Length <= limits.MaximumKeyBytes) &&
            maximumItems is >= 1 and <= 256 && maximumBytes is >= 1 and <= 1024 * 1024, "Nom page budget.");
        var captured = prefix.ToArray(); var after = afterExclusive?.ToArray();
        lock (gate)
        {
            EnsureReady(); var keys=orderedKeys??=index.Keys.Order(NomKeyComparer.Instance).ToArray();
            var found=Array.BinarySearch(keys,captured,NomKeyComparer.Instance); var position=found>=0 ? found : ~found;
            if(after!=null) { found=Array.BinarySearch(keys,after,NomKeyComparer.Instance); position=Math.Max(position,found>=0 ? found+1 : ~found); }
            var rows = new List<NomKvPair>(); var bytes = 0;
            if(position<keys.Length && keys[position].AsSpan().StartsWith(captured))
                NomBinary.Require(keys[position].Length+index[keys[position]].Length<=maximumBytes,"Nom single page row exceeds response budget.");
            try
            {
                while(position<keys.Length && rows.Count<maximumItems && keys[position].AsSpan().StartsWith(captured))
                {
                    var key=keys[position];
                    var location = index[key]; var cost = key.Length + location.Length;
                    if (cost > maximumBytes - bytes) break;
                    var file = segments[location.SegmentId]; file.Position = location.Offset;
                    var value = new byte[location.Length]; file.ReadExactly(value); rows.Add(new(key.ToArray(), value)); bytes += cost; position++;
                }
            }
            catch (IOException) { Volatile.Write(ref faulted, true); throw; }
            return new(sequence, generation, rows.AsReadOnly(),position<keys.Length && keys[position].AsSpan().StartsWith(captured));
        }
    }
    internal NomKvReadResult GetMany(IReadOnlyList<byte[]> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        NomBinary.Require(keys.Count is > 0 and <= 256, "Nom read key count budget.");
        var captured = new byte[keys.Count][];
        for (var i = 0; i < captured.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(keys[i]);
            NomBinary.Require(keys[i].Length > 0 && keys[i].Length <= limits.MaximumKeyBytes, "Nom read key budget."); captured[i] = keys[i].ToArray();
        }
        lock (gate)
        {
            EnsureReady(); long bytes = 0;
            foreach (var key in captured) if (index.TryGetValue(key, out var location)) bytes += location.Length;
            NomBinary.Require(bytes <= 1024 * 1024, "Nom detached read byte budget.");
            var values = new byte[]?[captured.Length];
            try
            {
                for (var i = 0; i < captured.Length; i++)
                {
                    if (!index.TryGetValue(captured[i], out var location)) continue;
                    var file = segments[location.SegmentId]; file.Position = location.Offset;
                    var value = new byte[location.Length]; file.ReadExactly(value); values[i] = value;
                }
            }
            catch { Volatile.Write(ref faulted, true); throw; }
            return new(sequence, generation, Array.AsReadOnly(values));
        }
    }
    internal void RequireAppendCapacity(NomKvAppendBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        lock (gate)
        {
            EnsureReady();
            NomBinary.Require(!pendingTail && budget.Batches > 0 && budget.PayloadBytes >= 0 && budget.AdditionalKeys >= 0 && budget.AdditionalIndexBytes >= 0 &&
                budget.MaximumBatchPayload > 0 && budget.MaximumBatchMutations > 0 && budget.MaximumKeyBytes > 0 && budget.MaximumValueBytes > 0, "Invalid Nom capacity request.");
            NomBinary.Require(budget.Batches <= long.MaxValue - sequence && budget.Batches <= limits.MaximumReplayBatches - (sequence - activeHeader!.BaseSequence) &&
                budget.PayloadBytes <= limits.MaximumSegmentBytes - validLength - (long)budget.Batches * (NomJournalCodec.BatchHeaderBytes + NomJournalCodec.BatchFooterBytes) &&
                budget.MaximumBatchPayload <= limits.MaximumPayloadBytes && budget.MaximumBatchMutations <= limits.MaximumMutations &&
                budget.MaximumKeyBytes <= limits.MaximumKeyBytes && budget.MaximumValueBytes <= limits.MaximumValueBytes &&
                budget.AdditionalKeys <= limits.MaximumIndexKeys - index.Count && budget.AdditionalIndexBytes <= limits.MaximumIndexBytes - indexBytes,
                "Nom reservation capacity reached; maintenance is required.");
        }
    }
    internal NomKvWriteReceipt WriteBatch(NomJournalBatch batch, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(batch); token.ThrowIfCancellationRequested();
        lock (gate)
        {
            token.ThrowIfCancellationRequested(); EnsureReady();
            if (pendingTail) throw new InvalidOperationException("Nom tail must be validated and repaired before append.");
            NomBinary.Require(sequence < long.MaxValue && batch.Sequence == sequence + 1 && batch.ExpectedGeneration == generation, "Nom write sequence/generation conflict.");
            var frame = NomJournalCodec.EncodeBatch(activeHeader!, batch, limits);
            var canonical = NomJournalCodec.DecodeBatch(activeHeader!, frame, limits);
            NomBinary.Require(sequence - activeHeader!.BaseSequence < limits.MaximumReplayBatches && validLength + frame.Length <= limits.MaximumSegmentBytes,
                "Nom segment capacity reached; maintenance is required.");
            var changes = Prepare(canonical, validLength, out var nextBytes, out var nextCount);
            index.EnsureCapacity(Math.Max(index.Count, nextCount));
            var ticket = new NomKvWriteTicket(ProjectId, StoreId, canonical.Sequence, canonical.OperationId, Convert.ToHexStringLower(SHA256.HashData(frame)));
            var receipt = new NomKvWriteReceipt(ticket, canonical.Generation);
            token.ThrowIfCancellationRequested(); EnsureAdmission();
            active!.Position = validLength;
            try
            {
                // From the first write onward cancellation cannot turn a possibly committed batch into Aborted.
                files.Write(active, frame); files.Flush(active);
                foreach (var change in changes) if (change.Delete) index.Remove(change.Key);
                foreach (var change in changes) if (!change.Delete) index[change.Key] = change.Location;
                orderedKeys=null;
                indexBytes = nextBytes; validLength += frame.Length; sequence = canonical.Sequence; generation = canonical.Generation;
                return receipt;
            }
            catch (Exception error)
            {
                Volatile.Write(ref faulted, true); throw new NomKvUnknownOutcomeException(ticket, error);
            }
        }
    }
    private sealed record IndexChange(byte[] Key, bool Delete, NomValueLocation Location);
    private IndexChange[] Prepare(NomJournalBatch batch, long start, out long nextBytes, out int nextCount)
    {
        nextBytes = indexBytes; nextCount = index.Count; var changes = new IndexChange[batch.Mutations.Count]; var offset = NomJournalCodec.BatchHeaderBytes;
        for (var i = 0; i < changes.Length; i++)
        {
            var mutation = batch.Mutations[i]; var key = mutation.Key.ToArray(); var exists = index.ContainsKey(key);
            if (mutation.IsDelete && exists) { nextCount--; nextBytes -= 96L + key.Length; }
            if (!mutation.IsDelete && !exists) { nextCount++; nextBytes += 96L + key.Length; }
            offset += NomJournalCodec.EntryHeaderBytes + key.Length;
            changes[i] = new(key, mutation.IsDelete, new(start + offset, mutation.Value.Length, batch.Sequence, activeHeader!.SegmentId)); offset += mutation.Value.Length;
        }
        NomBinary.Require(nextCount <= limits.MaximumIndexKeys && nextBytes <= limits.MaximumIndexBytes, "Nom write index budget."); return changes;
    }
    internal NomKvWriteOutcome LookupOutcome(NomKvWriteTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        NomBinary.Require(ticket.ProjectId == ProjectId && ticket.StoreId == StoreId && ticket.Sequence > 0 && ticket.OperationId != Guid.Empty, "Nom ticket identity mismatch.");
        _ = NomBinary.DigestBytes(ticket.FrameDigest);
        lock (gate)
        {
            EnsureReady(); if (ticket.Sequence > sequence) return NomKvWriteOutcome.NotCommitted;
            if (ticket.Sequence <= descriptor!.CheckpointSequence) return NomKvWriteOutcome.Expired;
            try
            {
                foreach (var pair in segments.Where(pair => pair.Key != 0).OrderBy(pair => pair.Key))
                {
                    var file = pair.Value; file.Position = 0; var headerBytes = new byte[NomJournalCodec.HeaderBytes]; file.ReadExactly(headerBytes);
                    var header = NomJournalCodec.DecodeHeader(headerBytes); var count = 0;
                    var end = pair.Key == activeHeader!.SegmentId ? validLength : file.Length;
                    while (file.Position < end)
                    {
                        NomBinary.Require(count++ < limits.MaximumReplayBatches, "Nom outcome scan budget.");
                        var frameHeader = new byte[NomJournalCodec.BatchHeaderBytes]; var start = file.Position; file.ReadExactly(frameHeader);
                        var length = NomJournalCodec.ValidateBatchHeader(frameHeader, limits);
                        NomBinary.Require(start + length <= end, "Nom outcome frame boundary.");
                        var candidate = NomBinary.ReadCounter(frameHeader.AsSpan(24));
                        if (candidate == ticket.Sequence)
                        {
                            var frame = new byte[length]; frameHeader.CopyTo(frame, 0); file.ReadExactly(frame.AsSpan(frameHeader.Length));
                            var batch = NomJournalCodec.DecodeBatch(header, frame, limits);
                            return batch.OperationId == ticket.OperationId && Convert.ToHexStringLower(SHA256.HashData(frame)) == ticket.FrameDigest
                                ? NomKvWriteOutcome.Committed : NomKvWriteOutcome.NotCommitted;
                        }
                        file.Position = start + length;
                    }
                }
                throw new InvalidDataException("Nom ticket sequence missing from verified journal.");
            }
            catch { Volatile.Write(ref faulted, true); throw; }
        }
    }
    private void EnsureAdmission() => ObjectDisposedException.ThrowIf(Volatile.Read(ref stopping) != 0 || isClosed, this);
    private void EnsureReady()
    {
        EnsureAdmission();
        if (!ready || faulted) throw new InvalidOperationException("Nom KV owner requires close/reopen before use.");
    }
    internal void RequestStop() => Interlocked.Exchange(ref stopping, 1);
    public void Dispose()
    {
        RequestStop();
        lock (gate)
        {
            if (isClosed) return;
            List<Exception>? errors = null;
            foreach (var file in owned)
            {
                if (ReferenceEquals(file, rootLock)) continue;
                try { CloseFile(file); } catch (Exception error) { (errors ??= []).Add(error); }
            }
            if (errors != null) throw new AggregateException("Nom KV files retained until close retry.", errors);
            try { if (rootLock != null) CloseFile(rootLock); }
            catch
            {
                if (rootLock!.Handle.IsClosed) CompleteClose();
                throw;
            }
            CompleteClose();
        }
    }
    private void CloseFile(OwnedFile file)
    {
        if (file.Handle.IsClosed) return;
        files.Close(file.Stream);
        if (!file.Handle.IsClosed) throw new IOException("Nom file close did not release its handle.");
    }
    private void CompleteClose()
    {
        ready = false; index.Clear(); orderedKeys=null; segments.Clear(); Volatile.Write(ref isClosed, true); closed(this);
    }
    private static void RejectReparse(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Nom KV reparse paths are unsupported.");
    }
    private static void RejectReparseAncestors(string path)
    {
        for (var directory = new DirectoryInfo(path); directory != null; directory = directory.Parent) RejectReparse(directory.FullName);
    }
}
