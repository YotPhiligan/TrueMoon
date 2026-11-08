using System.Buffers.Binary;
using System.Security.Cryptography;

namespace TrueMoon.Nom.Storage.Format;

internal sealed record NomCheckpointHeader(Guid ProjectId, Guid StoreId, long Epoch, long Sequence,
    long Generation, long PreviousSegmentId, string PreviousSegmentHash, int EntryCount);

internal sealed class NomCheckpointResult(NomCheckpointHeader header, long length, Dictionary<byte[], NomValueLocation> entries)
{
    internal NomCheckpointHeader Header { get; } = header;
    internal long Length { get; } = length;
    internal long IndexBytes { get; } = entries.Sum(pair => 96L + pair.Key.Length);
    internal int KeyCount => entries.Count;
    internal Dictionary<byte[], NomValueLocation> CopyEntries(int maximumKeyBytes)
    {
        foreach (var key in entries.Keys) NomBinary.Require(key.Length <= maximumKeyBytes, "Checkpoint seed key budget.");
        return entries.ToDictionary(pair => pair.Key.ToArray(), pair => pair.Value, NomKeyComparer.Instance);
    }
}

internal static class NomCheckpointCodec
{
    internal const int HeaderBytes = 128, FooterBytes = 32;
    private static byte[] Header(NomCheckpointHeader h, NomFormatLimits limits)
    {
        NomBinary.Require(h.ProjectId != Guid.Empty && h.StoreId != Guid.Empty && h.Epoch > 1 && h.Sequence > 0 &&
            h.Generation >= 0 && h.Generation <= h.Sequence && h.PreviousSegmentId > 0 && h.PreviousSegmentId < long.MaxValue &&
            h.PreviousSegmentHash != NomBinary.ZeroDigest && h.EntryCount >= 0 && h.EntryCount <= limits.MaximumIndexKeys, "Invalid checkpoint header.");
        var bytes = new byte[HeaderBytes]; "NOMCKP01"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), HeaderBytes);
        NomBinary.WriteGuid(bytes.AsSpan(16), h.ProjectId); NomBinary.WriteGuid(bytes.AsSpan(32), h.StoreId);
        NomBinary.WriteCounter(bytes.AsSpan(48), h.Epoch); NomBinary.WriteCounter(bytes.AsSpan(56), h.Sequence);
        NomBinary.WriteCounter(bytes.AsSpan(64), h.Generation); NomBinary.WriteCounter(bytes.AsSpan(72), h.PreviousSegmentId);
        NomBinary.DigestBytes(h.PreviousSegmentHash).CopyTo(bytes, 80); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(112), (uint)h.EntryCount);
        return bytes;
    }
    internal static string Write(Stream destination, NomCheckpointHeader header, IEnumerable<NomKvPair> entries,
        NomFormatLimits? limits = null, Action<ReadOnlyMemory<byte>>? write = null, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(destination); ArgumentNullException.ThrowIfNull(header); ArgumentNullException.ThrowIfNull(entries);
        limits ??= new(); limits.Validate(); var encoded = Header(header, limits);
        NomBinary.Require(destination.CanWrite && destination.CanSeek && destination.Position == 0 && destination.Length == 0, "Checkpoint output must be empty and seekable.");
        write ??= bytes => destination.Write(bytes.Span);
        using var content = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); using var whole = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long length = HeaderBytes + FooterBytes, indexBytes = 0; var count = 0; byte[]? previous = null;
        void Append(ReadOnlyMemory<byte> bytes) { token.ThrowIfCancellationRequested(); write(bytes); content.AppendData(bytes.Span); whole.AppendData(bytes.Span); }
        Append(encoded);
        foreach (var pair in entries)
        {
            token.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(pair.Key); ArgumentNullException.ThrowIfNull(pair.Value);
            NomBinary.Require(count < header.EntryCount && pair.Key.Length is > 0 && pair.Key.Length <= limits.MaximumKeyBytes && pair.Value.Length <= limits.MaximumValueBytes &&
                (previous == null || previous.AsSpan().SequenceCompareTo(pair.Key) < 0), "Checkpoint entry size/order/count.");
            length += 8L + pair.Key.Length + pair.Value.Length; indexBytes += 96L + pair.Key.Length;
            NomBinary.Require(length <= limits.MaximumCheckpointBytes && indexBytes <= limits.MaximumIndexBytes, "Checkpoint byte/index budget.");
            var sizes = new byte[8]; BinaryPrimitives.WriteInt32LittleEndian(sizes, pair.Key.Length); BinaryPrimitives.WriteInt32LittleEndian(sizes.AsSpan(4), pair.Value.Length);
            Append(sizes); Append(pair.Key);
            for (var offset = 0; offset < pair.Value.Length; offset += 64 * 1024) Append(pair.Value.AsMemory(offset, Math.Min(64 * 1024, pair.Value.Length - offset)));
            previous = pair.Key.ToArray(); count++;
        }
        NomBinary.Require(count == header.EntryCount, "Checkpoint entry count mismatch."); token.ThrowIfCancellationRequested();
        var footer = content.GetHashAndReset(); write(footer); whole.AppendData(footer);
        return Convert.ToHexStringLower(whole.GetHashAndReset());
    }
    internal static NomCheckpointResult Read(Stream source, NomRootDescriptor expected, NomFormatLimits? limits = null, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(expected); limits ??= new(); limits.Validate();
        NomBinary.Require(source.CanRead && source.CanSeek && source.Position == 0 && source.Length is >= HeaderBytes + FooterBytes &&
            source.Length <= limits.MaximumCheckpointBytes, "Checkpoint input budget/framing.");
        using var content = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); using var whole = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void ReadBytes(byte[] bytes)
        {
            var offset = 0;
            while (offset < bytes.Length)
            { token.ThrowIfCancellationRequested(); var n = source.Read(bytes.AsSpan(offset)); NomBinary.Require(n > 0 && n <= bytes.Length - offset, "Truncated checkpoint."); offset += n; }
            content.AppendData(bytes); whole.AppendData(bytes);
        }
        var header = new byte[HeaderBytes]; ReadBytes(header);
        NomBinary.Require(header.AsSpan(0, 8).SequenceEqual("NOMCKP01"u8) && BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(8)) == 1 &&
            BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(10)) == HeaderBytes, "Unsupported checkpoint format.");
        NomBinary.RequireZero(header.AsSpan(12, 4)); NomBinary.RequireZero(header.AsSpan(116, 12));
        var count = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(112)); NomBinary.Require(count <= limits.MaximumIndexKeys && count <= (source.Length - HeaderBytes - FooterBytes) / 9, "Checkpoint count budget.");
        var h = new NomCheckpointHeader(NomBinary.ReadGuid(header.AsSpan(16)), NomBinary.ReadGuid(header.AsSpan(32)), NomBinary.ReadCounter(header.AsSpan(48)),
            NomBinary.ReadCounter(header.AsSpan(56)), NomBinary.ReadCounter(header.AsSpan(64)), NomBinary.ReadCounter(header.AsSpan(72)), Convert.ToHexStringLower(header.AsSpan(80, 32)), (int)count);
        NomBinary.Require(Header(h, limits).AsSpan().SequenceEqual(header) && h.ProjectId == expected.ProjectId && h.StoreId == expected.StoreId &&
            h.Epoch == expected.Epoch && h.Sequence == expected.CheckpointSequence && h.Generation == expected.CheckpointGeneration &&
            h.PreviousSegmentId == expected.FirstSegmentId - 1 && h.PreviousSegmentHash == expected.PreviousSegmentHash, "Checkpoint authority mismatch.");
        var entries = new Dictionary<byte[], NomValueLocation>(NomKeyComparer.Instance); byte[]? previous = null; long indexBytes = 0;
        var buffer = new byte[64 * 1024];
        for (var i = 0; i < h.EntryCount; i++)
        {
            var sizes = new byte[8]; ReadBytes(sizes); var keySize = BinaryPrimitives.ReadUInt32LittleEndian(sizes); var valueSize = BinaryPrimitives.ReadUInt32LittleEndian(sizes.AsSpan(4));
            NomBinary.Require(keySize > 0 && keySize <= limits.MaximumKeyBytes && valueSize <= limits.MaximumValueBytes &&
                keySize + (long)valueSize <= source.Length - FooterBytes - source.Position, "Checkpoint entry bounds.");
            indexBytes += 96L + keySize; NomBinary.Require(indexBytes <= limits.MaximumIndexBytes, "Checkpoint index budget.");
            var key = new byte[(int)keySize]; ReadBytes(key); NomBinary.Require(previous == null || previous.AsSpan().SequenceCompareTo(key) < 0, "Checkpoint key order/duplicate.");
            entries.Add(key, new(source.Position, (int)valueSize, h.Sequence, 0)); previous = key;
            var remaining = (int)valueSize;
            while (remaining > 0)
            {
                token.ThrowIfCancellationRequested(); var ask = Math.Min(buffer.Length, remaining); var n = source.Read(buffer.AsSpan(0, ask)); NomBinary.Require(n > 0 && n <= ask, "Truncated checkpoint value.");
                content.AppendData(buffer.AsSpan(0, n)); whole.AppendData(buffer.AsSpan(0, n)); remaining -= n;
            }
        }
        NomBinary.Require(source.Position == source.Length - FooterBytes, "Checkpoint trailing records.");
        token.ThrowIfCancellationRequested(); var footer = new byte[FooterBytes]; source.ReadExactly(footer);
        NomBinary.Require(CryptographicOperations.FixedTimeEquals(content.GetHashAndReset(), footer), "Checkpoint checksum mismatch."); whole.AppendData(footer);
        NomBinary.Require(Convert.ToHexStringLower(whole.GetHashAndReset()) == expected.CheckpointDigest, "Checkpoint content address mismatch.");
        return new(h, source.Length, entries);
    }
}
