using System.Globalization;
using System.Security.Cryptography;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Storage;

internal sealed partial class NomKvStore
{
    private string CheckpointPath(string digest) => Path.Combine(Root, "checkpoints", digest + ".nom");
    internal NomCompactionReceipt Compact(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        lock (gate)
        {
            EnsureReady(); NomBinary.Require(!pendingTail, "Validate/repair before maintenance.");
            var checkpointDirectory = Path.Combine(Root, "checkpoints"); RejectReparse(checkpointDirectory);
            var estimated = NomCheckpointCodec.HeaderBytes + (long)NomCheckpointCodec.FooterBytes;
            foreach (var pair in index) estimated += 8L + pair.Key.Length + pair.Value.Length;
            NomBinary.Require(estimated <= limits.MaximumCheckpointBytes && estimated + NomJournalCodec.HeaderBytes + NomRootCodec.RootBytes <= limits.MaximumMaintenanceBytes,
                "Nom checkpoint/maintenance byte budget.");
            if (CleanupObsolete()) return Receipt(changed: false, pending: true);
            if (sequence == descriptor!.CheckpointSequence) return Receipt(changed: false, pending: false);
            var existingFiles = 0;
            foreach (var directory in new[] { Path.Combine(Root, "journal"), checkpointDirectory })
                if (Directory.Exists(directory)) foreach (var _ in Directory.EnumerateFiles(directory))
                    NomBinary.Require(++existingFiles <= 1022, "Reserve checkpoint/segment file slots before maintenance.");
            var epoch = checked(descriptor.Epoch + 1); var nextId = checked(activeHeader!.SegmentId + 1);
            var ordered = index.Keys.Order(NomKeyComparer.Instance).ToArray(); token.ThrowIfCancellationRequested();
            var switchAttempted = false;
            try
            {
                active!.Position = 0; var previousHash = Convert.ToHexStringLower(SHA256.HashData(active));
                var checkpointHeader = new NomCheckpointHeader(ProjectId, StoreId, epoch, sequence, generation, activeHeader.SegmentId, previousHash, index.Count);
                Directory.CreateDirectory(checkpointDirectory); RejectReparse(checkpointDirectory);
                var pendingCheckpoint = Path.Combine(checkpointDirectory, epoch.ToString("D20", CultureInfo.InvariantCulture) + ".pending");
                var output = OpenOwned(pendingCheckpoint, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                IEnumerable<NomKvPair> Rows()
                {
                    foreach (var key in ordered)
                    {
                        token.ThrowIfCancellationRequested(); var location = index[key]; var file = segments[location.SegmentId]; file.Position = location.Offset;
                        var value = new byte[location.Length]; file.ReadExactly(value); yield return new(key, value);
                    }
                }
                var digest = NomCheckpointCodec.Write(output.Stream, checkpointHeader, Rows(), limits, bytes => files.Write(output.Stream, bytes), token);
                files.Flush(output.Stream); CloseFile(output);
                var nextRoot = new NomRootDescriptor(ProjectId, StoreId, epoch, sequence, generation, digest, nextId, nextId, sequence, previousHash);
                var verify = OpenOwned(pendingCheckpoint, FileMode.Open, FileAccess.Read, FileShare.Read);
                var checkpoint = NomCheckpointCodec.Read(verify.Stream, nextRoot, limits, token); CloseFile(verify);
                var checkpointPath = CheckpointPath(digest); files.PublishRoot(pendingCheckpoint, checkpointPath);
                var checkpointFile = OpenOwned(checkpointPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                var nextHeader = new NomJournalHeader(ProjectId, StoreId, nextId, sequence, generation, previousHash);
                var pendingSegment = Path.Combine(Root, "journal", nextId.ToString("D20", CultureInfo.InvariantCulture) + ".pending");
                var segment = OpenOwned(pendingSegment, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                var header = NomJournalCodec.EncodeHeader(nextHeader); files.Write(segment.Stream, header); files.Flush(segment.Stream); CloseFile(segment);
                var verifiedSegment = OpenOwned(pendingSegment, FileMode.Open, FileAccess.Read, FileShare.Read);
                NomBinary.Require(verifiedSegment.Stream.Length == header.Length && ReadSegmentHeader(verifiedSegment.Stream).AsSpan().SequenceEqual(header), "Prepared segment verification."); CloseFile(verifiedSegment);
                files.PublishRoot(pendingSegment, SegmentPath(nextId));
                var nextActive = OpenOwned(SegmentPath(nextId), FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                var switchPath = Path.Combine(Root, "nom.root.switch");
                var root = OpenOwned(switchPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                var encodedRoot = NomRootCodec.Encode(nextRoot); files.Write(root.Stream, encodedRoot); files.Flush(root.Stream); CloseFile(root);
                var verifiedRoot = OpenOwned(switchPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                NomBinary.Require(ReadRoot(verifiedRoot.Stream).AsSpan().SequenceEqual(encodedRoot), "Prepared root verification."); CloseFile(verifiedRoot);
                // The exclusive owner lock spans root-handle close, atomic replacement and reopening.
                token.ThrowIfCancellationRequested(); EnsureAdmission(); CloseFile(authority!); token.ThrowIfCancellationRequested();
                switchAttempted = true; files.ReplaceRoot(switchPath, Path.Combine(Root, "nom.root"));
                authority = OpenOwned(Path.Combine(Root, "nom.root"), FileMode.Open, FileAccess.Read, FileShare.Read);
                NomBinary.Require(ReadRoot(authority.Stream).AsSpan().SequenceEqual(encodedRoot), "Selected root verification.");
                descriptor = nextRoot; activeHeader = nextHeader; active = nextActive.Stream;
                segments.Clear(); segments.Add(0, checkpointFile.Stream); segments.Add(nextId, active);
                index = checkpoint.CopyEntries(limits.MaximumKeyBytes); indexBytes = checkpoint.IndexBytes; validLength = header.Length;
                orderedKeys=null;
                active.Position = validLength; RecoveredTailBytes = 0;
                // No cancellation relabels a selected root. Obsolete-file failure is retryable cleanup.
                return Receipt(changed: true, pending: CleanupObsolete());
            }
            catch (Exception error)
            {
                Volatile.Write(ref faulted, true);
                if (!switchAttempted && error is OperationCanceledException) throw;
                throw new NomCompactionException(switchAttempted, error);
            }
        }
    }
    private static byte[] ReadSegmentHeader(FileStream file)
    { file.Position = 0; var header = new byte[NomJournalCodec.HeaderBytes]; file.ReadExactly(header); return header; }
    private NomCompactionReceipt Receipt(bool changed, bool pending)
    {
        var size = descriptor!.CheckpointSequence == 0 ? 0 : segments[0].Length;
        return new(descriptor.Epoch, sequence, generation, index.Count, size, pending, changed);
    }
    private bool CleanupObsolete()
    {
        var keep = new HashSet<FileStream>(segments.Values) { authority!.Stream, rootLock!.Stream }; var pending = false;
        foreach (var file in owned)
        {
            if (keep.Contains(file.Stream)) continue;
            try { CloseFile(file); } catch { pending = true; }
        }
        owned.RemoveAll(file => !keep.Contains(file.Stream) && file.Handle.IsClosed);
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.Combine(Root, "nom.root") };
        foreach (var file in segments.Values) selected.Add(file.Name);
        var candidates = new List<string>(); var seen = 0;
        foreach (var directory in new[] { Path.Combine(Root, "journal"), Path.Combine(Root, "checkpoints") })
        {
            RejectReparse(directory); if (!Directory.Exists(directory)) continue;
            foreach (var path in Directory.EnumerateFiles(directory))
            {
                NomBinary.Require(seen++ < 1024, "Nom maintenance file-count budget.");
                var name = Path.GetFileName(path); var stem = Path.GetFileNameWithoutExtension(path);
                var numeric = stem.Length == 20 && stem.All(c => c is >= '0' and <= '9');
                var digest = stem.Length == 64 && stem.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
                var journal = string.Equals(directory, Path.Combine(Root, "journal"), StringComparison.OrdinalIgnoreCase);
                if (journal ? numeric && (name.EndsWith(".nom", StringComparison.Ordinal) || name.EndsWith(".pending", StringComparison.Ordinal)) :
                    numeric && name.EndsWith(".pending", StringComparison.Ordinal) || digest && name.EndsWith(".nom", StringComparison.Ordinal)) candidates.Add(path);
            }
        }
        var switchPath = Path.Combine(Root, "nom.root.switch"); if (File.Exists(switchPath)) candidates.Add(switchPath);
        foreach (var path in candidates)
        {
            if (selected.Contains(path)) continue;
            RejectReparse(path);
            if (owned.Any(file => !file.Handle.IsClosed && string.Equals(file.Stream.Name, path, StringComparison.OrdinalIgnoreCase))) { pending = true; continue; }
            try { files.DeleteFile(path); } catch { pending = true; }
        }
        return pending;
    }
}
