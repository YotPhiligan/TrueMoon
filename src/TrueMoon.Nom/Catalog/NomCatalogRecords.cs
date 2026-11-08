using System.Buffers.Binary;
using System.Text;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Catalog;

internal sealed record CatalogResourceControl(long Next, long Floor);
internal sealed record CatalogOperation(long Sequence, string Request, long Expected, long Total, int Count, string State, long? Result);
internal sealed record CatalogOperationItem(long Sequence, int Index, byte[] Asset, long Maximum, long? ExpectedLength, string? ExpectedDigest, string? Revision);
internal sealed record CatalogStoredResource(AssetResourceVersion Version, byte[] Asset);

internal static class NomCatalogKeys
{
    internal static readonly byte[] Header = NomKeyCodec.Encode(NomKeySpace.Control, "catalog"u8.ToArray());
    internal static readonly byte[] Control = NomKeyCodec.Encode(NomKeySpace.Control, "resources"u8.ToArray());
    internal static byte[] Prefix(NomKeySpace space) => [1, (byte)space];
    private static byte[] GuidBytes(Guid id) { var bytes = new byte[16]; NomBinary.WriteGuid(bytes, id); return bytes; }
    private static byte[] Number(long value) { var bytes = new byte[8]; NomBinary.WriteCounter(bytes, value); return bytes; }
    internal static byte[] Asset(Guid id) => NomKeyCodec.Encode(NomKeySpace.Asset, GuidBytes(id));
    internal static byte[] Binding(string binding) => NomKeyCodec.Encode(NomKeySpace.Binding, NomBinary.DigestBytes(binding));
    internal static byte[] Revision(string revision) => NomKeyCodec.Encode(NomKeySpace.Revision, NomBinary.DigestBytes(revision));
    internal static byte[] Head(Guid id) => NomKeyCodec.Encode(NomKeySpace.Head, GuidBytes(id));
    internal static byte[] Operation(long sequence) => NomKeyCodec.Encode(NomKeySpace.Operation, Number(sequence));
    internal static byte[] Item(long sequence, int index)
    { var number = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(number, index); return NomKeyCodec.Encode(NomKeySpace.OperationItem, Number(sequence), number); }
    internal static byte[] ItemPrefix(long sequence) => Item(sequence, 0)[..16];
}

internal static class NomCatalogRecordCodec
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static byte[] Encode(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Utf8, leaveOpen: true);
        write(writer); NomBinary.Require(stream.Length <= 128 * 1024, "Nom catalog record budget."); return stream.ToArray();
    }
    private static T Decode<T>(byte[] bytes, string magic, Func<BinaryReader, T> read)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        NomBinary.Require(bytes.Length is >= 8 and <= 128 * 1024 && bytes.AsSpan(0, 8).SequenceEqual(Encoding.ASCII.GetBytes(magic)), "Nom catalog record format.");
        using var stream = new MemoryStream(bytes, writable: false); using var reader = new BinaryReader(stream, Utf8);
        stream.Position = 8;
        try { var value = read(reader); NomBinary.Require(stream.Position == stream.Length, "Trailing Nom catalog record data."); return value; }
        catch (EndOfStreamException error) { throw new InvalidDataException("Truncated Nom catalog record.", error); }
    }
    private static long Counter(BinaryReader reader) { var value = reader.ReadUInt64(); NomBinary.Require(value <= long.MaxValue, "Nom record counter overflow."); return (long)value; }
    private static void GuidValue(BinaryWriter writer, Guid value) { NomBinary.Require(value != Guid.Empty, "Empty Nom record identity."); var bytes = new byte[16]; NomBinary.WriteGuid(bytes, value); writer.Write(bytes); }
    private static Guid GuidValue(BinaryReader reader) { var bytes = Bytes(reader, 16); var id = NomBinary.ReadGuid(bytes); NomBinary.Require(id != Guid.Empty, "Empty Nom record identity."); return id; }
    private static byte[] Bytes(BinaryReader reader, int length) { var bytes = reader.ReadBytes(length); NomBinary.Require(bytes.Length == length, "Truncated Nom record field."); return bytes; }
    private static void Digest(BinaryWriter writer, string digest) => writer.Write(NomBinary.DigestBytes(digest));
    private static string Digest(BinaryReader reader) => Convert.ToHexStringLower(Bytes(reader, 32));
    private static void Magic(BinaryWriter writer, string magic) => writer.Write(Encoding.ASCII.GetBytes(magic));
    private static byte[] Asset(BinaryReader reader)
        => Asset(reader,out _);
    private static byte[] Asset(BinaryReader reader,out AssetRecord row)
    { var n = reader.ReadUInt32(); NomBinary.Require(n is >= 68 and <= 128 * 1024 && n <= reader.BaseStream.Length - reader.BaseStream.Position, "Nom embedded asset budget."); var bytes = Bytes(reader, (int)n); row = CatalogCodec.Decode(bytes); return bytes; }
    private static void Asset(BinaryWriter writer, byte[] asset) { _ = CatalogCodec.Decode(asset); writer.Write((uint)asset.Length); writer.Write(asset); }

    internal static byte[] EncodeHeader(CatalogHeader h) => Encode(w => { Magic(w, "NOMCAT01"); GuidValue(w, h.ProjectId); GuidValue(w, h.StoreId); NomBinary.Require(h.Generation >= 0, "Invalid catalog generation."); w.Write(h.Generation); });
    internal static CatalogHeader DecodeHeader(byte[] bytes) => Decode(bytes, "NOMCAT01", r => new CatalogHeader(GuidValue(r), GuidValue(r), Counter(r)));
    internal static byte[] EncodeControl(CatalogResourceControl c)
    { NomBinary.Require(c.Next > 0 && c.Floor >= 0 && c.Floor < c.Next, "Resource control counters."); return Encode(w => { Magic(w, "NOMCTL01"); w.Write(c.Next); w.Write(c.Floor); }); }
    internal static CatalogResourceControl DecodeControl(byte[] bytes) => Decode(bytes, "NOMCTL01", r =>
    { var c = new CatalogResourceControl(Counter(r), Counter(r)); _ = EncodeControl(c); return c; });
    internal static byte[] EncodeOperation(CatalogOperation op)
    {
        ValidateOperation(op);
        return Encode(w => { Magic(w, "NOMOPR01"); w.Write(op.Sequence); Digest(w, op.Request); w.Write(op.Expected); w.Write(op.Total); w.Write(op.Count);
            w.Write(op.State switch { "pending" => (byte)0, "committed" => (byte)1, _ => (byte)2 }); w.Write(new byte[3]); w.Write(op.Result ?? 0); });
    }
    internal static CatalogOperation DecodeOperation(byte[] bytes) => Decode(bytes, "NOMOPR01", r =>
    {
        var sequence = Counter(r); var request = Digest(r); var expected = Counter(r); var total = Counter(r); var count = r.ReadUInt32(); var state = r.ReadByte();
        NomBinary.Require(state <= 2 && count <= int.MaxValue, "Resource operation state/count."); NomBinary.RequireZero(Bytes(r, 3)); var result = Counter(r);
        NomBinary.Require(state == 1 || result == 0, "Unexpected operation result.");
        var op = new CatalogOperation(sequence, request, expected, total, (int)count, state == 0 ? "pending" : state == 1 ? "committed" : "aborted", state == 1 ? result : null);
        ValidateOperation(op); return op;
    });
    internal static void ValidateOperation(CatalogOperation op)
    {
        _ = NomBinary.DigestBytes(op.Request);
        NomBinary.Require(op.Sequence > 0 && op.Expected >= 0 && op.Total is > 0 and <= ResourceCodec.MaximumBatch && op.Count is > 0 and <= 256 &&
            op.State is "pending" or "committed" or "aborted" &&
            (op.State == "committed" ? op.Expected < long.MaxValue && op.Result == op.Expected + 1 : op.Result == null), "Resource operation schema.");
    }
    internal static byte[] EncodeItem(CatalogOperationItem item)
    {
        NomBinary.Require(item.Sequence > 0 && item.Index is >= 0 and < 256 && item.Maximum is > 0 and <= ResourceCodec.MaximumObject &&
            (item.ExpectedLength == null || item.ExpectedLength >= 0 && item.ExpectedLength <= item.Maximum), "Resource item bounds.");
        return Encode(w => { Magic(w, "NOMITM01"); w.Write(item.Sequence); w.Write(item.Index); w.Write(item.Maximum);
            w.Write(item.ExpectedLength.HasValue); w.Write(item.ExpectedLength ?? 0);
            w.Write(item.ExpectedDigest != null); Digest(w, item.ExpectedDigest ?? NomBinary.ZeroDigest);
            w.Write(item.Revision != null); Digest(w, item.Revision ?? NomBinary.ZeroDigest); Asset(w, item.Asset); });
    }
    private static bool Flag(BinaryReader r) { var value = r.ReadByte(); NomBinary.Require(value <= 1, "Noncanonical Nom flag."); return value == 1; }
    internal static CatalogOperationItem DecodeItem(byte[] bytes) => Decode(bytes, "NOMITM01", r =>
    {
        var sequence = Counter(r); var index = r.ReadUInt32(); NomBinary.Require(index < 256, "Resource item index."); var max = Counter(r);
        var hasLength = Flag(r); var length = Counter(r); var hasDigest = Flag(r); var digest = Digest(r); var hasRevision = Flag(r); var revision = Digest(r);
        NomBinary.Require((hasLength || length == 0) && (hasDigest || digest == NomBinary.ZeroDigest) && (hasRevision || revision == NomBinary.ZeroDigest), "Noncanonical absent resource field.");
        var item = new CatalogOperationItem(sequence, (int)index, Asset(r), max, hasLength ? length : null, hasDigest ? digest : null, hasRevision ? revision : null);
        _ = EncodeItem(item); return item;
    });
    internal static byte[] EncodeRevision(CatalogStoredResource resource)
    {
        var v = resource.Version; var row = CatalogCodec.Decode(resource.Asset);
        NomBinary.Require(row.Id == v.AssetId && row.Kind == v.KindId && v.Revision == NomResourceMetadataCodec.HashCanonicalMetadata(resource.Asset, v.Digest, v.Length), "Resource revision integrity.");
        return Encode(w => { Magic(w, "NOMREV01"); GuidValue(w, v.AssetId); GuidValue(w, v.KindId); Digest(w, v.Revision); Digest(w, v.Digest); w.Write(v.Length); w.Write((uint)resource.Asset.Length); w.Write(resource.Asset); });
    }
    internal static CatalogStoredResource DecodeRevision(byte[] bytes) => Decode(bytes, "NOMREV01", r =>
    {
        var version = new AssetResourceVersion(GuidValue(r), GuidValue(r), Digest(r), Digest(r), Counter(r)); var asset=Asset(r,out var row);
        NomBinary.Require(row.Id==version.AssetId && row.Kind==version.KindId && version.Revision==NomResourceMetadataCodec.HashCanonicalMetadata(asset,version.Digest,version.Length),"Resource revision integrity.");
        return new CatalogStoredResource(version,asset);
    });
    internal static byte[] EncodeHead(Guid id, string revision) => Encode(w => { Magic(w, "NOMHED01"); GuidValue(w, id); Digest(w, revision); });
    internal static (Guid Asset, string Revision) DecodeHead(byte[] bytes) => Decode(bytes, "NOMHED01", r => (GuidValue(r), Digest(r)));
    internal static byte[] EncodeBinding(Guid id) => Encode(w => { Magic(w, "NOMBND01"); GuidValue(w, id); });
    internal static Guid DecodeBinding(byte[] bytes) => Decode(bytes, "NOMBND01", GuidValue);
}
