using System.Buffers.Binary;
using System.Security.Cryptography;

namespace TrueMoon.Nom.Storage.Format;

internal static class NomJournalCodec
{
    internal const int HeaderBytes = 160;
    internal const int BatchHeaderBytes = 96;
    internal const int BatchFooterBytes = 48;
    internal const int EntryHeaderBytes = 12;

    internal static byte[] EncodeHeader(NomJournalHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        NomBinary.Require(header.ProjectId != Guid.Empty && header.StoreId != Guid.Empty && header.SegmentId > 0 &&
            header.BaseSequence >= 0 && header.BaseGeneration >= 0 && header.BaseGeneration <= header.BaseSequence, "Invalid Nom segment identity/counters.");
        var previous = NomBinary.DigestBytes(header.PreviousSegmentHash);
        NomBinary.Require(header.SegmentId == 1
            ? header.BaseSequence == 0 && header.BaseGeneration == 0 && header.PreviousSegmentHash == NomBinary.ZeroDigest
            : header.PreviousSegmentHash != NomBinary.ZeroDigest, "Invalid Nom segment chain anchor.");
        var bytes = new byte[HeaderBytes]; "NOMSEG01"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), HeaderBytes);
        NomBinary.WriteGuid(bytes.AsSpan(16), header.ProjectId); NomBinary.WriteGuid(bytes.AsSpan(32), header.StoreId);
        NomBinary.WriteCounter(bytes.AsSpan(48), header.SegmentId); NomBinary.WriteCounter(bytes.AsSpan(56), header.BaseSequence);
        NomBinary.WriteCounter(bytes.AsSpan(64), header.BaseGeneration); previous.CopyTo(bytes, 72);
        SHA256.HashData(bytes.AsSpan(0, 128), bytes.AsSpan(128)); return bytes;
    }
    internal static NomJournalHeader DecodeHeader(ReadOnlySpan<byte> bytes)
    {
        NomBinary.Require(bytes.Length == HeaderBytes && bytes[..8].SequenceEqual("NOMSEG01"u8) &&
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[8..]) == 1 && BinaryPrimitives.ReadUInt16LittleEndian(bytes[10..]) == HeaderBytes,
            "Unsupported Nom segment format.");
        NomBinary.VerifyHash(bytes[..128], bytes[128..]); NomBinary.RequireZero(bytes.Slice(12, 4)); NomBinary.RequireZero(bytes.Slice(104, 24));
        var header = new NomJournalHeader(NomBinary.ReadGuid(bytes[16..]), NomBinary.ReadGuid(bytes[32..]),
            NomBinary.ReadCounter(bytes[48..]), NomBinary.ReadCounter(bytes[56..]), NomBinary.ReadCounter(bytes[64..]),
            Convert.ToHexStringLower(bytes.Slice(72, 32)));
        NomBinary.Require(EncodeHeader(header).AsSpan().SequenceEqual(bytes), "Noncanonical Nom segment header."); return header;
    }
    internal static byte[] EncodeBatch(NomJournalHeader header, NomJournalBatch batch, NomFormatLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(batch); limits ??= new(); limits.Validate();
        var context = EncodeHeader(header); ValidateCounters(batch.Sequence, batch.ExpectedGeneration, batch.Generation, batch.OperationId);
        NomBinary.Require(batch.Sequence > header.BaseSequence && batch.ExpectedGeneration >= header.BaseGeneration, "Nom batch precedes segment anchor.");
        NomBinary.Require(batch.Mutations.Count > 0 && batch.Mutations.Count <= limits.MaximumMutations, "Nom mutation count budget.");
        var mutations = batch.Mutations.ToArray();
        foreach (var mutation in mutations) NomBinary.Require(mutation != null, "Null Nom mutation.");
        Array.Sort(mutations, (left, right) => left.Key.Span.SequenceCompareTo(right.Key.Span)); long payloadLength = 0;
        for (var i = 0; i < mutations.Length; i++)
        {
            var mutation = mutations[i];
            NomBinary.Require(mutation.Key.Length > 0 && mutation.Key.Length <= limits.MaximumKeyBytes && mutation.Value.Length <= limits.MaximumValueBytes, "Nom key/value budget.");
            NomBinary.Require(i == 0 || mutations[i - 1].Key.Span.SequenceCompareTo(mutation.Key.Span) < 0, "Duplicate Nom batch key.");
            payloadLength += EntryHeaderBytes + (long)mutation.Key.Length + mutation.Value.Length;
        }
        NomBinary.Require(payloadLength <= limits.MaximumPayloadBytes, "Nom payload budget.");
        NomBinary.Require(HeaderBytes + BatchHeaderBytes + payloadLength + BatchFooterBytes <= limits.MaximumSegmentBytes, "Nom frame cannot fit a segment.");
        var bytes = new byte[BatchHeaderBytes + (int)payloadLength + BatchFooterBytes]; "NOMBAT01"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), BatchHeaderBytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), bytes.Length); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), mutations.Length);
        NomBinary.WriteCounter(bytes.AsSpan(24), batch.Sequence); NomBinary.WriteCounter(bytes.AsSpan(32), batch.ExpectedGeneration);
        NomBinary.WriteCounter(bytes.AsSpan(40), batch.Generation); NomBinary.WriteGuid(bytes.AsSpan(48), batch.OperationId);
        SHA256.HashData(bytes.AsSpan(0, 64), bytes.AsSpan(64, 32)); var offset = BatchHeaderBytes;
        foreach (var mutation in mutations)
        {
            bytes[offset] = mutation.IsDelete ? (byte)2 : (byte)1;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 4), mutation.Key.Length);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 8), mutation.Value.Length); offset += EntryHeaderBytes;
            mutation.Key.Span.CopyTo(bytes.AsSpan(offset)); offset += mutation.Key.Length;
            mutation.Value.Span.CopyTo(bytes.AsSpan(offset)); offset += mutation.Value.Length;
        }
        BatchHash(context.AsSpan(128, 32), bytes.AsSpan(0, offset)).CopyTo(bytes, offset);
        "NOMEND01"u8.CopyTo(bytes.AsSpan(offset + 32)); NomBinary.WriteCounter(bytes.AsSpan(offset + 40), bytes.Length); return bytes;
    }
    internal static NomJournalBatch DecodeBatch(NomJournalHeader header, ReadOnlySpan<byte> bytes, NomFormatLimits? limits = null)
    {
        limits ??= new(); limits.Validate(); var context = EncodeHeader(header);
        NomBinary.Require(bytes.Length >= BatchHeaderBytes, "Truncated Nom batch header.");
        var length = ValidateBatchHeader(bytes[..BatchHeaderBytes], limits);
        NomBinary.Require(bytes.Length == length, "Nom batch framing length.");
        var footer = bytes.Length - BatchFooterBytes;
        NomBinary.Require(bytes.Slice(footer + 32, 8).SequenceEqual("NOMEND01"u8) && NomBinary.ReadCounter(bytes[(footer + 40)..]) == length, "Invalid Nom commit footer.");
        NomBinary.Require(CryptographicOperations.FixedTimeEquals(BatchHash(context.AsSpan(128, 32), bytes[..footer]), bytes.Slice(footer, 32)), "Nom batch checksum mismatch.");
        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes[20..]); var mutations = new NomJournalMutation[count]; var offset = BatchHeaderBytes;
        for (var i = 0; i < count; i++)
        {
            NomBinary.Require(footer - offset >= EntryHeaderBytes, "Truncated Nom mutation.");
            var kind = bytes[offset]; NomBinary.Require(kind is 1 or 2, "Unsupported Nom mutation kind.");
            NomBinary.RequireZero(bytes.Slice(offset + 1, 3));
            var keyLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 4)..]); var valueLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 8)..]);
            offset += EntryHeaderBytes;
            NomBinary.Require(keyLength > 0 && keyLength <= limits.MaximumKeyBytes && valueLength <= limits.MaximumValueBytes &&
                (kind == 1 || valueLength == 0) && (long)keyLength + valueLength <= footer - offset, "Nom mutation framing/budget.");
            var key = bytes.Slice(offset, (int)keyLength).ToArray(); offset += (int)keyLength;
            NomBinary.Require(i == 0 || mutations[i - 1].Key.Span.SequenceCompareTo(key) < 0, "Unordered/duplicate Nom batch key.");
            mutations[i] = new(key, kind == 2 ? null : bytes.Slice(offset, (int)valueLength).ToArray()); offset += (int)valueLength;
        }
        NomBinary.Require(offset == footer, "Trailing Nom batch payload.");
        var batch = new NomJournalBatch(NomBinary.ReadCounter(bytes[24..]), NomBinary.ReadCounter(bytes[32..]), NomBinary.ReadCounter(bytes[40..]),
            NomBinary.ReadGuid(bytes[48..]), mutations);
        NomBinary.Require(batch.Sequence > header.BaseSequence && batch.ExpectedGeneration >= header.BaseGeneration, "Nom batch precedes segment anchor.");
        return batch;
    }
    // Authenticate the length and counters before recovery allocates a variable-size frame.
    internal static int ValidateBatchHeader(ReadOnlySpan<byte> bytes, NomFormatLimits limits)
    {
        ValidatePartialBatchHeader(bytes, limits);
        NomBinary.Require(bytes.Length == BatchHeaderBytes, "Truncated Nom batch header.");
        NomBinary.VerifyHash(bytes[..64], bytes.Slice(64, 32));
        return BinaryPrimitives.ReadInt32LittleEndian(bytes[16..]);
    }
    internal static void ValidatePartialBatchHeader(ReadOnlySpan<byte> bytes, NomFormatLimits limits)
    {
        NomBinary.Require(bytes.Length <= BatchHeaderBytes, "Invalid Nom header prefix.");
        ReadOnlySpan<byte> fixedPrefix = [78, 79, 77, 66, 65, 84, 48, 49, 1, 0, 96, 0, 0, 0, 0, 0];
        var prefixLength = Math.Min(bytes.Length, fixedPrefix.Length);
        NomBinary.Require(bytes[..prefixLength].SequenceEqual(fixedPrefix[..prefixLength]), "Unsupported Nom batch header.");
        if (bytes.Length >= 20)
        {
            var length = BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]);
            NomBinary.Require(length >= BatchHeaderBytes + BatchFooterBytes + 13 &&
                length <= BatchHeaderBytes + BatchFooterBytes + (long)limits.MaximumPayloadBytes &&
                length <= limits.MaximumSegmentBytes - HeaderBytes, "Nom frame length budget.");
        }
        if (bytes.Length >= 24)
        {
            var count = BinaryPrimitives.ReadUInt32LittleEndian(bytes[20..]);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]);
            NomBinary.Require(count > 0 && count <= limits.MaximumMutations &&
                count * 13L <= length - BatchHeaderBytes - BatchFooterBytes, "Nom mutation count budget.");
        }
        if (bytes.Length >= 32) NomBinary.Require(NomBinary.ReadCounter(bytes[24..]) > 0, "Invalid Nom batch sequence.");
        if (bytes.Length >= 40) _ = NomBinary.ReadCounter(bytes[32..]);
        if (bytes.Length >= 48)
            ValidateGenerations(NomBinary.ReadCounter(bytes[24..]), NomBinary.ReadCounter(bytes[32..]), NomBinary.ReadCounter(bytes[40..]));
        if (bytes.Length >= 64)
            ValidateCounters(NomBinary.ReadCounter(bytes[24..]), NomBinary.ReadCounter(bytes[32..]), NomBinary.ReadCounter(bytes[40..]), NomBinary.ReadGuid(bytes[48..]));
    }
    private static void ValidateCounters(long sequence, long expected, long generation, Guid operation)
    {
        ValidateGenerations(sequence, expected, generation);
        NomBinary.Require(operation != Guid.Empty, "Empty Nom batch operation.");
    }
    private static void ValidateGenerations(long sequence, long expected, long generation)
    {
        NomBinary.Require(sequence > 0 && expected >= 0 && generation >= expected && generation <= sequence &&
            (generation == expected || expected < long.MaxValue && generation == expected + 1), "Invalid Nom batch counters.");
    }
    private static byte[] BatchHash(ReadOnlySpan<byte> context, ReadOnlySpan<byte> frame)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); hash.AppendData(context); hash.AppendData(frame); return hash.GetHashAndReset();
    }
}
