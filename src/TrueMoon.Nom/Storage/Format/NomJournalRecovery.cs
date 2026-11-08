using System.Security.Cryptography;

namespace TrueMoon.Nom.Storage.Format;

// Read-only single-segment replay. The future owner supplies the root/chain-validated expected header.
internal static class NomJournalRecovery
{
    internal static NomRecoveryResult Recover(Stream journal, NomJournalHeader expectedHeader, bool sealedSegment = false,
        long minimumSequence = 0, NomFormatLimits? limits = null, CancellationToken token = default, NomRecoveryResult? previous = null, NomCheckpointResult? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(journal); limits ??= new(); limits.Validate();
        NomBinary.Require(minimumSequence >= 0, "Negative required Nom sequence.");
        var expected = NomJournalCodec.EncodeHeader(expectedHeader); token.ThrowIfCancellationRequested();
        NomBinary.Require(journal.CanRead && journal.CanSeek && journal.Position == 0, "Nom recovery requires a seekable readable stream at zero.");
        var length = journal.Length;
        NomBinary.Require(length >= NomJournalCodec.HeaderBytes && length <= limits.MaximumSegmentBytes, "Nom segment size budget/header.");
        using var segmentHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var headerBytes = new byte[NomJournalCodec.HeaderBytes]; ReadExactly(journal, headerBytes, token, segmentHash);
        var header = NomJournalCodec.DecodeHeader(headerBytes);
        NomBinary.Require(headerBytes.AsSpan().SequenceEqual(expected), "Nom segment authority/identity mismatch.");
        NomBinary.Require(previous == null || checkpoint == null, "Ambiguous Nom recovery seed.");
        if (checkpoint != null)
            NomBinary.Require(checkpoint.Header.ProjectId == header.ProjectId && checkpoint.Header.StoreId == header.StoreId &&
                checkpoint.Header.PreviousSegmentId + 1 == header.SegmentId && checkpoint.Header.Sequence == header.BaseSequence &&
                checkpoint.Header.Generation == header.BaseGeneration && checkpoint.Header.PreviousSegmentHash == header.PreviousSegmentHash &&
                checkpoint.Header.EntryCount == checkpoint.KeyCount && checkpoint.KeyCount <= limits.MaximumIndexKeys && checkpoint.IndexBytes <= limits.MaximumIndexBytes, "Nom checkpoint seed mismatch.");
        else if (previous == null)
            NomBinary.Require(header.SegmentId == 1 && header.BaseSequence == 0 && header.BaseGeneration == 0, "Nom recovery requires its predecessor/checkpoint state.");
        else
            NomBinary.Require(previous.SealedSegmentHash != null && !previous.HasIncompleteTail &&
                previous.Header.ProjectId == header.ProjectId && previous.Header.StoreId == header.StoreId &&
                previous.Header.SegmentId < long.MaxValue && header.SegmentId == previous.Header.SegmentId + 1 &&
                previous.Sequence == header.BaseSequence && previous.Generation == header.BaseGeneration &&
                previous.SealedSegmentHash == header.PreviousSegmentHash && previous.KeyCount <= limits.MaximumIndexKeys &&
                previous.IndexBytes <= limits.MaximumIndexBytes, "Nom segment predecessor/seed mismatch.");
        var entries = previous?.CopyEntries(limits.MaximumKeyBytes) ?? checkpoint?.CopyEntries(limits.MaximumKeyBytes) ?? new Dictionary<byte[], NomValueLocation>(NomKeyComparer.Instance);
        var sequence = header.BaseSequence; var generation = header.BaseGeneration;
        long indexBytes = previous?.IndexBytes ?? checkpoint?.IndexBytes ?? 0, validLength = headerBytes.Length;
        var batchHeader = new byte[NomJournalCodec.BatchHeaderBytes]; var incomplete = false; var batches = 0;
        while (journal.Position < length)
        {
            token.ThrowIfCancellationRequested(); var frameStart = journal.Position;
            var availableHeader = (int)Math.Min(batchHeader.Length, length - frameStart);
            ReadExactly(journal, batchHeader.AsSpan(0, availableHeader), token, segmentHash);
            if (availableHeader < batchHeader.Length)
            {
                NomJournalCodec.ValidatePartialBatchHeader(batchHeader.AsSpan(0, availableHeader), limits);
                ValidateReplayPrefix(batchHeader.AsSpan(0, availableHeader), sequence, generation);
                incomplete = true; break;
            }
            var frameLength = NomJournalCodec.ValidateBatchHeader(batchHeader, limits);
            ValidateReplayPrefix(batchHeader, sequence, generation);
            if (frameLength > length - frameStart) { incomplete = true; break; }
            NomBinary.Require(batches < limits.MaximumReplayBatches, "Nom replay batch budget.");
            var frame = new byte[frameLength]; batchHeader.CopyTo(frame, 0);
            ReadExactly(journal, frame.AsSpan(batchHeader.Length), token, segmentHash);
            var batch = NomJournalCodec.DecodeBatch(header, frame, limits);
            NomBinary.Require(sequence < long.MaxValue && batch.Sequence == sequence + 1 && batch.ExpectedGeneration == generation,
                "Nom replay sequence/generation conflict.");
            var nextBytes = indexBytes; var nextCount = entries.Count;
            foreach (var mutation in batch.Mutations)
            {
                token.ThrowIfCancellationRequested(); var key = mutation.Key.ToArray(); var exists = entries.ContainsKey(key);
                if (mutation.IsDelete && exists) { nextBytes -= 96L + key.Length; nextCount--; }
                if (!mutation.IsDelete && !exists) { nextBytes += 96L + key.Length; nextCount++; }
            }
            NomBinary.Require(nextCount <= limits.MaximumIndexKeys && nextBytes <= limits.MaximumIndexBytes, "Nom replay index budget.");
            // Delete first so the temporary dictionary does not exceed the post-batch entry budget.
            foreach (var mutation in batch.Mutations) if (mutation.IsDelete) entries.Remove(mutation.Key.ToArray());
            var offset = NomJournalCodec.BatchHeaderBytes;
            foreach (var mutation in batch.Mutations)
            {
                token.ThrowIfCancellationRequested(); offset += NomJournalCodec.EntryHeaderBytes + mutation.Key.Length;
                if (!mutation.IsDelete) entries[mutation.Key.ToArray()] = new(frameStart + offset, mutation.Value.Length, batch.Sequence, header.SegmentId);
                offset += mutation.Value.Length;
            }
            sequence = batch.Sequence; generation = batch.Generation; indexBytes = nextBytes; batches++;
            validLength = frameStart + frameLength;
        }
        NomBinary.Require(!sealedSegment || !incomplete, "Incomplete sealed Nom segment.");
        NomBinary.Require(sequence >= minimumSequence, "Required Nom commits are missing.");
        NomBinary.Require(journal.Length == length, "Nom journal changed during recovery.");
        token.ThrowIfCancellationRequested();
        return new(header, sequence, generation, validLength, incomplete, indexBytes, entries,
            sealedSegment ? Convert.ToHexStringLower(segmentHash.GetHashAndReset()) : null);
    }
    private static void ValidateReplayPrefix(ReadOnlySpan<byte> bytes, long sequence, long generation)
    {
        if (bytes.Length >= 32) NomBinary.Require(sequence < long.MaxValue && NomBinary.ReadCounter(bytes[24..]) == sequence + 1, "Nom replay sequence conflict.");
        if (bytes.Length >= 40) NomBinary.Require(NomBinary.ReadCounter(bytes[32..]) == generation, "Nom replay generation conflict.");
    }
    private static void ReadExactly(Stream stream, Span<byte> bytes, CancellationToken token, IncrementalHash hash)
    {
        while (!bytes.IsEmpty)
        {
            token.ThrowIfCancellationRequested(); var read = stream.Read(bytes);
            if (read <= 0 || read > bytes.Length) throw new InvalidDataException("Nom journal changed/truncated during recovery.");
            hash.AppendData(bytes[..read]);
            bytes = bytes[read..];
        }
    }
}
