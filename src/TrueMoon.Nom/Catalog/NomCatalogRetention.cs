using TrueMoon.Nom.Retention;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Catalog;

internal sealed partial class NomCatalog
{
    private NomDomainIndex? domainIndex;
    internal NomDomainIndex Index => domainIndex ??= BuildIndex();
    private NomDomainIndex BuildIndex()
    {
        var result = new NomDomainIndex(domainIndexBytes);
        foreach (var pair in Rows(NomCatalogKeys.Prefix(NomKeySpace.Asset),100_000))
        { var asset = CatalogCodec.Decode(pair.Value); result.AddAsset(asset,IsDeleted(asset.Id)); }
        foreach (var pair in Rows(NomCatalogKeys.Prefix(NomKeySpace.Revision),200_000)) result.AddRevision(NomCatalogRecordCodec.DecodeRevision(pair.Value));
        result.Validate(); return result;
    }
    private static byte[] GuidBytes(Guid id) { var bytes = new byte[16]; NomBinary.WriteGuid(bytes,id); return bytes; }
    internal static byte[] TombstoneKey(Guid id) => NomKeyCodec.Encode(NomKeySpace.Tombstone,GuidBytes(id));
    internal static byte[] ManifestKey(string id) => NomKeyCodec.Encode(NomKeySpace.Manifest,NomBinary.DigestBytes(id));
    internal static byte[] MemberKey(string id,string revision) => NomKeyCodec.Encode(NomKeySpace.Member,NomBinary.DigestBytes(id),NomBinary.DigestBytes(revision));
    private static byte[] MemberPrefix(string id) => MemberKey(id,NomBinary.ZeroDigest)[..40];
    internal static byte[] PinKey(Guid pin) => NomKeyCodec.Encode(NomKeySpace.Pin,GuidBytes(pin));
    internal static byte[] GarbageKey(string digest) => NomKeyCodec.Encode(NomKeySpace.Garbage,NomBinary.DigestBytes(digest));
    internal bool IsDeleted(Guid id) => Get(TombstoneKey(id)) != null;
    internal bool DeleteAsset(Guid id)
    {
        NomBinary.Require(Asset(id) != null,"Unknown logical asset."); if (IsDeleted(id)) return false;
        Set(TombstoneKey(id),NomCatalogRecordCodec.EncodeBinding(id)); Set(NomCatalogKeys.Head(id),null); return true;
    }
    internal void DeleteRevision(string revision) => Set(NomCatalogKeys.Revision(revision),null);
    internal AssetSnapshotManifest? Manifest(string id)
    {
        if (Get(ManifestKey(id)) is not { } bytes) return null;
        var header = NomRetentionCodec.DecodeManifest(bytes);
        var members = Rows(MemberPrefix(id),4096).Select(p => NomRetentionCodec.DecodeMember(p.Value)).OrderBy(v => v.Revision,StringComparer.Ordinal).ToArray();
        NomBinary.Require(members.Length == header.Count && header.Generation <= Header.Generation &&
            NomRetentionCodec.ManifestId(Header.ProjectId,Header.StoreId,header.Generation,members) == id,"Manifest integrity/closure.");
        return new(id,header.Generation,members);
    }
    internal AssetSnapshotManifest[] Manifests() => Rows(NomCatalogKeys.Prefix(NomKeySpace.Manifest),256)
        .Select(p => Manifest(Convert.ToHexStringLower(p.Key.AsSpan(8,32)))!).ToArray();
    internal void PutManifest(AssetSnapshotManifest manifest)
    {
        if (Manifest(manifest.Id) != null) return;
        NomBinary.Require(Rows(NomCatalogKeys.Prefix(NomKeySpace.Manifest),256).Count() < 256,"Manifest retention budget.");
        Set(ManifestKey(manifest.Id),NomRetentionCodec.EncodeManifest(manifest.Generation,manifest.Members.Length));
        foreach (var member in manifest.Members) Set(MemberKey(manifest.Id,member.Revision),NomRetentionCodec.EncodeMember(member));
    }
    internal void RemoveManifest(AssetSnapshotManifest manifest)
    { foreach (var member in manifest.Members) Set(MemberKey(manifest.Id,member.Revision),null); Set(ManifestKey(manifest.Id),null); }
    internal (Guid Pin,string Manifest)[] Pins() => Rows(NomCatalogKeys.Prefix(NomKeySpace.Pin),1024)
        .Select(p => (NomBinary.ReadGuid(p.Key.AsSpan(8,16)),NomRetentionCodec.DecodeDigest(p.Value,"NOMPIN01"))).ToArray();
    internal void PutPin(Guid pin,string manifest)
    {
        var key = PinKey(pin);
        if (Get(key) is { } bytes) { NomBinary.Require(NomRetentionCodec.DecodeDigest(bytes,"NOMPIN01") == manifest,"Pin identity already bound."); return; }
        NomBinary.Require(Pins().Length < 1024,"Durable pin budget."); Set(key,NomRetentionCodec.EncodeDigest("NOMPIN01",manifest));
    }
    internal void DeletePin(Guid pin) => Set(PinKey(pin),null);
    internal (string Digest,long Length)[] Garbage() => Rows(NomCatalogKeys.Prefix(NomKeySpace.Garbage),2048).Select(p => NomRetentionCodec.DecodeGc(p.Value)).ToArray();
    internal void SetGarbage(string digest,long length) => Set(GarbageKey(digest),NomRetentionCodec.EncodeGc(digest,length));
    internal void DeleteGarbage(string digest) => Set(GarbageKey(digest),null);
    private byte[] ValidateRetentionRecord(byte[] key,byte[] value)
    {
        switch ((NomKeySpace)key[1])
        {
            case NomKeySpace.Tombstone:
                var id = NomCatalogRecordCodec.DecodeBinding(value); NomBinary.Require(Asset(id) != null && Head(id) == null,"Invalid identity tombstone."); return TombstoneKey(id);
            case NomKeySpace.Manifest:
                _ = NomRetentionCodec.DecodeManifest(value); NomBinary.Require(key.Length == 40,"Manifest key shape."); return ManifestKey(Convert.ToHexStringLower(key.AsSpan(8,32)));
            case NomKeySpace.Member:
                var version = NomRetentionCodec.DecodeMember(value); NomBinary.Require(key.Length == 76,"Member key shape.");
                var manifestId = Convert.ToHexStringLower(key.AsSpan(8,32)); NomBinary.Require(Get(ManifestKey(manifestId)) != null,"Dangling manifest member."); return MemberKey(manifestId,version.Revision);
            case NomKeySpace.Pin:
                var target = NomRetentionCodec.DecodeDigest(value,"NOMPIN01"); NomBinary.Require(key.Length == 24 && Get(ManifestKey(target)) != null,"Dangling pin/key shape.");
                var pin = NomBinary.ReadGuid(key.AsSpan(8,16)); NomBinary.Require(pin != Guid.Empty,"Empty pin identity."); return PinKey(pin);
            case NomKeySpace.Garbage:
                var garbage = NomRetentionCodec.DecodeGc(value); return GarbageKey(garbage.Digest);
            default: throw new InvalidDataException("Unsupported catalog key space.");
        }
    }
    private void ValidateRetention()
    {
        foreach (var manifest in Manifests())
        {
            var members = manifest.Members;
            NomBinary.Require(Index.Closure(members,4096).SequenceEqual(members),"Manifest missing exact dependency closure.");
        }
        _ = Pins();
        var referenced = Index.Versions.Values.Select(v => v.Digest).ToHashSet(StringComparer.Ordinal);
        foreach (var garbage in Garbage()) NomBinary.Require(!referenced.Contains(garbage.Digest),"GC deletion intent references a retained resource.");
    }
}
