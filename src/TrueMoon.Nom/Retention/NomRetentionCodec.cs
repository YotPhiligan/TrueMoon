using System.Text;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Retention;

// Canonical bounded domain records; no CLR names or provider serialization.
internal static class NomRetentionCodec
{
    internal static string[] CaptureTags(IReadOnlyList<string>? tags)
    {
        if (tags == null) return [];
        NomBinary.Require(tags.Count <= 32, "Tag count budget."); var result = new List<string>();
        foreach (var tag in tags)
        {
            NomBinary.Require(result.Count < 32 && !string.IsNullOrWhiteSpace(tag) && !tag.Any(char.IsControl) && CatalogCodec.Utf8.GetByteCount(tag) <= 128, "Invalid tag/budget.");
            result.Add(tag);
        }
        NomBinary.Require(result.Count == tags.Count && result.Distinct(StringComparer.Ordinal).Count() == result.Count, "Duplicate/captured tag set.");
        return result.Order(StringComparer.Ordinal).ToArray();
    }
    internal static void ValidateVersion(AssetResourceVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        NomBinary.Require(version.AssetId != Guid.Empty && version.KindId != Guid.Empty && version.Length is >= 0 and <= ResourceCodec.MaximumObject, "Invalid exact resource identity/length.");
        ResourceCodec.Digest(version.Revision); ResourceCodec.Digest(version.Digest);
    }
    internal static AssetResourceVersion[] CaptureVersions(IReadOnlyList<AssetResourceVersion>? versions, int maximum)
    {
        if (versions == null) return [];
        NomBinary.Require(versions.Count <= maximum, "Exact version count budget."); var result = new List<AssetResourceVersion>();
        foreach (var version in versions) { NomBinary.Require(result.Count < maximum, "Captured version count budget."); ValidateVersion(version); result.Add(version); }
        NomBinary.Require(result.Count == versions.Count && result.Select(v => v.Revision).Distinct(StringComparer.Ordinal).Count() == result.Count, "Duplicate/captured exact revisions.");
        return result.OrderBy(v => v.Revision, StringComparer.Ordinal).ToArray();
    }
    internal static byte[] Encode(string magic, Action<BinaryWriter> body)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, CatalogCodec.Utf8, true);
        writer.Write(Encoding.ASCII.GetBytes(magic)); body(writer);
        NomBinary.Require(stream.Length <= 128 * 1024, "Retention record budget."); return stream.ToArray();
    }
    internal static T Decode<T>(byte[] bytes, string magic, Func<BinaryReader,T> body)
    {
        NomBinary.Require(bytes.Length is >= 8 and <= 128 * 1024 && bytes.AsSpan(0, 8).SequenceEqual(Encoding.ASCII.GetBytes(magic)), "Retention record format.");
        using var stream = new MemoryStream(bytes, false); using var reader = new BinaryReader(stream, CatalogCodec.Utf8); stream.Position = 8;
        try { var result = body(reader); NomBinary.Require(stream.Position == stream.Length, "Trailing retention data."); return result; }
        catch (EndOfStreamException error) { throw new InvalidDataException("Truncated retention data.", error); }
        catch (DecoderFallbackException error) { throw new InvalidDataException("Invalid retention UTF8.", error); }
    }
    internal static byte[] Bytes(BinaryReader reader, int size)
    { var bytes = reader.ReadBytes(size); NomBinary.Require(bytes.Length == size, "Truncated retention field."); return bytes; }
    internal static void Version(BinaryWriter writer, AssetResourceVersion version)
    {
        ValidateVersion(version); var bytes = new byte[16]; NomBinary.WriteGuid(bytes, version.AssetId); writer.Write(bytes);
        NomBinary.WriteGuid(bytes, version.KindId); writer.Write(bytes); writer.Write(NomBinary.DigestBytes(version.Revision)); writer.Write(NomBinary.DigestBytes(version.Digest)); writer.Write(version.Length);
    }
    internal static AssetResourceVersion Version(BinaryReader reader)
    {
        var result = new AssetResourceVersion(NomBinary.ReadGuid(Bytes(reader,16)), NomBinary.ReadGuid(Bytes(reader,16)),
            Convert.ToHexStringLower(Bytes(reader,32)), Convert.ToHexStringLower(Bytes(reader,32)), reader.ReadInt64()); ValidateVersion(result); return result;
    }
    internal static byte[] EncodeLinks(string[] tags, AssetResourceVersion[] dependencies) => Encode("NOMLNK01", w =>
    {
        w.Write((uint)tags.Length); w.Write((uint)dependencies.Length);
        foreach (var tag in tags) { var bytes = CatalogCodec.Utf8.GetBytes(tag); w.Write((uint)bytes.Length); w.Write(bytes); }
        foreach (var version in dependencies) Version(w, version);
    });
    internal static (string[] Tags, AssetResourceVersion[] Dependencies) DecodeLinks(byte[] bytes) => Decode(bytes,"NOMLNK01", r =>
    {
        var tagCount = r.ReadUInt32(); var depCount = r.ReadUInt32(); NomBinary.Require(tagCount <= 32 && depCount <= 32, "Link count budget.");
        var tags = new string[(int)tagCount]; var versions = new AssetResourceVersion[(int)depCount];
        for (var i = 0; i < tags.Length; i++) { var length = r.ReadUInt32(); NomBinary.Require(length is > 0 and <= 128, "Tag byte budget."); tags[i] = CatalogCodec.Utf8.GetString(Bytes(r,(int)length)); }
        for (var i = 0; i < versions.Length; i++) versions[i] = Version(r);
        NomBinary.Require(tags.SequenceEqual(CaptureTags(tags)) && versions.SequenceEqual(CaptureVersions(versions,32)), "Noncanonical link order."); return (tags,versions);
    });
    internal static string ManifestId(Guid project, Guid store, long generation, AssetResourceVersion[] members)
    {
        var fields = new List<string> { "TrueMoon.Nom/manifest/1",project.ToString("N"),store.ToString("N"),generation.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        foreach (var v in members) { using var s = new MemoryStream(); using var w = new BinaryWriter(s); Version(w,v); fields.Add(Convert.ToHexStringLower(s.ToArray())); }
        return CatalogCodec.Key(fields.ToArray());
    }
    internal static byte[] EncodeManifest(long generation, int count) => Encode("NOMMAN01", w =>
    { NomBinary.Require(generation >= 0 && count is > 0 and <= 4096,"Manifest bounds."); w.Write(generation); w.Write((uint)count); });
    internal static (long Generation,int Count) DecodeManifest(byte[] bytes) => Decode(bytes,"NOMMAN01",r =>
    { var generation = r.ReadInt64(); var count = r.ReadUInt32(); NomBinary.Require(count <= 4096,"Manifest count."); _ = EncodeManifest(generation,(int)count); return (generation,(int)count); });
    internal static byte[] EncodeMember(AssetResourceVersion version) => Encode("NOMMEM01",w => Version(w,version));
    internal static AssetResourceVersion DecodeMember(byte[] bytes) => Decode(bytes,"NOMMEM01",Version);
    internal static byte[] EncodeDigest(string magic,string digest) => Encode(magic,w => w.Write(NomBinary.DigestBytes(digest)));
    internal static string DecodeDigest(byte[] bytes,string magic) => Decode(bytes,magic,r => Convert.ToHexStringLower(Bytes(r,32)));
    internal static byte[] EncodeGc(string digest,long length) => Encode("NOMGCQ01",w =>
    { ResourceCodec.Digest(digest); NomBinary.Require(length is >= 0 and <= ResourceCodec.MaximumObject,"GC object length."); w.Write(NomBinary.DigestBytes(digest)); w.Write(length); });
    internal static (string Digest,long Length) DecodeGc(byte[] bytes) => Decode(bytes,"NOMGCQ01",r =>
    { var digest = Convert.ToHexStringLower(Bytes(r,32)); var length = r.ReadInt64(); _ = EncodeGc(digest,length); return (digest,length); });
}
