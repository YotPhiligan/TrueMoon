namespace TrueMoon.Nom.Storage.Format;

internal sealed record NomJournalHeader(Guid ProjectId, Guid StoreId, long SegmentId = 1,
    long BaseSequence = 0, long BaseGeneration = 0, string PreviousSegmentHash = NomBinary.ZeroDigest);

internal sealed record NomRootDescriptor(Guid ProjectId, Guid StoreId, long Epoch = 1,
    long CheckpointSequence = 0, long CheckpointGeneration = 0,
    string CheckpointDigest = NomBinary.ZeroDigest, long FirstSegmentId = 1,
    long ActiveSegmentId = 1, long MinimumSequence = 0,
    string PreviousSegmentHash = NomBinary.ZeroDigest);

internal sealed class NomJournalMutation
{
    private readonly byte[] key;
    private readonly byte[]? value;
    internal ReadOnlyMemory<byte> Key => key;
    internal ReadOnlyMemory<byte> Value => value is null ? ReadOnlyMemory<byte>.Empty : value;
    internal bool IsDelete => value == null;

    internal NomJournalMutation(byte[] key, byte[]? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        this.key = key.ToArray(); this.value = value?.ToArray();
    }
}

internal sealed class NomJournalBatch
{
    internal long Sequence { get; }
    internal long ExpectedGeneration { get; }
    internal long Generation { get; }
    internal Guid OperationId { get; }
    internal IReadOnlyList<NomJournalMutation> Mutations { get; }

    internal NomJournalBatch(long sequence, long expectedGeneration, long generation,
        Guid operationId, IReadOnlyList<NomJournalMutation> mutations)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        Sequence = sequence; ExpectedGeneration = expectedGeneration; Generation = generation;
        OperationId = operationId; Mutations = Array.AsReadOnly(mutations.ToArray());
    }
}

internal readonly record struct NomValueLocation(long Offset, int Length, long Sequence, long SegmentId = 1);

internal sealed class NomRecoveryResult(NomJournalHeader header, long sequence, long generation,
    long validLength, bool hasIncompleteTail, long indexBytes,
    Dictionary<byte[], NomValueLocation> entries, string? sealedSegmentHash = null)
{
    internal NomJournalHeader Header { get; } = header;
    internal long Sequence { get; } = sequence;
    internal long Generation { get; } = generation;
    internal long ValidLength { get; } = validLength;
    internal bool HasIncompleteTail { get; } = hasIncompleteTail;
    internal int KeyCount => entries.Count;
    // Logical index accounting: 96 bytes per live entry plus its key, not a CLR heap/RSS claim.
    internal long IndexBytes { get; } = indexBytes;
    internal string? SealedSegmentHash { get; } = sealedSegmentHash;
    internal Dictionary<byte[], NomValueLocation> CopyEntries(int maximumKeyBytes)
    {
        foreach (var key in entries.Keys) NomBinary.Require(key.Length <= maximumKeyBytes, "Nom seed key budget.");
        return entries.ToDictionary(pair => pair.Key.ToArray(), pair => pair.Value, NomKeyComparer.Instance);
    }
    internal bool TryGet(byte[] key, out NomValueLocation location)
    {
        ArgumentNullException.ThrowIfNull(key);
        return entries.TryGetValue(key, out location);
    }
}
