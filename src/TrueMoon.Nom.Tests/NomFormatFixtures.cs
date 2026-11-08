using System.Buffers.Binary;
using System.Security.Cryptography;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Tests;

internal static class NomFormatFixtures
{
    internal static readonly Guid ProjectId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
    internal static readonly Guid StoreId = Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f");
    internal static readonly Guid OperationId = Guid.Parse("11223344-5566-7788-99aa-bbccddeeff00");
    internal static readonly NomJournalHeader Header = new(ProjectId, StoreId);
    internal static readonly NomRootDescriptor Root = new(ProjectId, StoreId);
    // Independently assembled protocol bytes: explicit field hex + SHA-256, never produced by the codec under test.
    private const string HeaderHex = "4e4f4d53454730310100a0000000000000112233445566778899aabbccddeeff102132435465768798a9bacbdcedfe0f01000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000008196ab1c243711b876fe9e5c9d329844c6244b5a70961cd5858ffefa0719693d";
    private const string RootHex = "4e4f4d524f4f5431010000010000000000112233445566778899aabbccddeeff102132435465768798a9bacbdcedfe0f01000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000100000000000000010000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000008d2f5ed56b68ab4d57b80c3efa48c8fb2b7a2879df27ca295605fbf22b07b6a7";
    private const string BatchHex = "4e4f4d42415430310100600000000000b900000003000000010000000000000000000000000000000100000000000000112233445566778899aabbccddeeff008552789d72ec529a0c28f466ba01cdfbe66cbf4e4932eddab7c1589b6e2abcd50100000001000000020000000110200100000001000000000000007f02000000010000000000000080915c5477f8d643a2704ba7906cdf942dc9dec21148688b9e54ad676b5ac1f8224e4f4d454e443031b900000000000000";
    private const string MetadataHex = "4e4f4d4153543031010000002031425364758697a8b9cadbecfd0e1f00112233445566778899aabbccddeeff102132435465768798a9bacbdcedfe0f07000000030000006d6573682fcebb00ff01";
    internal const string ContentDigest = "039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81";
    internal const string ResourceRevision = "506f8ae07e0ac73b17b79059f11d0972468eb07cde2b258ebc08607df929af97";
    internal static byte[] HeaderGolden => Convert.FromHexString(HeaderHex);
    internal static byte[] RootGolden => Convert.FromHexString(RootHex);
    internal static byte[] BatchGolden => Convert.FromHexString(BatchHex);
    internal static byte[] MetadataGolden => Convert.FromHexString(MetadataHex);
    internal static AssetRecord Asset => new(Guid.Parse("20314253-6475-8697-a8b9-cadbecfd0e1f"), ProjectId, StoreId, "mesh/λ", [0, 255, 1]);
    internal static NomJournalBatch GoldenBatch => Batch(1, 0, 1, new NomJournalMutation([0x80], null), new NomJournalMutation([1], [0x10, 0x20]), new NomJournalMutation([0x7f], []));
    internal static NomJournalBatch Batch(long sequence, long expected, long generation, params NomJournalMutation[] mutations) =>
        new(sequence, expected, generation, OperationId, mutations);
    internal static byte[] Frame(long sequence, long expected, long generation, params NomJournalMutation[] mutations) =>
        NomJournalCodec.EncodeBatch(Header, Batch(sequence, expected, generation, mutations));
    internal static byte[] Journal(params byte[][] frames) => Join([HeaderGolden, .. frames]);
    internal static byte[] Join(params byte[][] parts)
    {
        var result = new byte[parts.Sum(part => part.Length)]; var offset = 0;
        foreach (var part in parts) { part.CopyTo(result, offset); offset += part.Length; }
        return result;
    }
    internal static void AuthenticateHeader(byte[] bytes) => SHA256.HashData(bytes.AsSpan(0, 128)).CopyTo(bytes, 128);
    internal static void AuthenticateRoot(byte[] bytes) => SHA256.HashData(bytes.AsSpan(0, 224)).CopyTo(bytes, 224);
    internal static void AuthenticateBatch(byte[] bytes, NomJournalHeader? context = null)
    {
        SHA256.HashData(bytes.AsSpan(0, 64)).CopyTo(bytes, 64);
        var segment = context is null ? HeaderGolden : NomJournalCodec.EncodeHeader(context);
        SHA256.HashData(Join(segment[128..], bytes[..^48])).CopyTo(bytes, bytes.Length - 48);
    }
    internal static byte[] Counter(byte[] bytes, int offset, long value)
    { BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(offset), value); return bytes; }
}

internal sealed class NomRecoveryProbeStream(byte[] bytes, int maximumRead = int.MaxValue) : MemoryStream(bytes, writable: false)
{
    internal bool WasDisposed;
    internal int Writes;
    internal int Truncations;
    internal int Reads;
    internal bool Seekable = true;
    internal bool Readable = true;
    internal int EofAfterBytes = int.MaxValue;
    internal bool InvalidReadCount;
    internal bool GrowReportedLength;
    internal IOException? ReadFailure;
    internal int FailOnRead = 1;
    internal Action<int>? AfterRead;
    public override bool CanSeek => Seekable;
    public override bool CanRead => Readable;
    public override long Length => base.Length + (GrowReportedLength && Reads > 1 ? 1 : 0);
    public override int Read(Span<byte> buffer)
    {
        Reads++;
        if (ReadFailure is not null && Reads == FailOnRead) throw ReadFailure;
        if (InvalidReadCount) return buffer.Length + 1;
        var allowed = (int)Math.Min(buffer.Length, Math.Min(maximumRead, Math.Max(0, EofAfterBytes - Position)));
        var count = base.Read(buffer[..allowed]); AfterRead?.Invoke(Reads); return count;
    }
    public override void Write(ReadOnlySpan<byte> buffer) { Writes++; throw new InvalidOperationException("Recovery wrote borrowed input."); }
    public override void Write(byte[] buffer, int offset, int count) { Writes++; throw new InvalidOperationException("Recovery wrote borrowed input."); }
    public override void SetLength(long value) { Truncations++; throw new InvalidOperationException("Recovery truncated borrowed input."); }
    protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
}
