using System.Buffers.Binary;
using Xunit;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Retention;

namespace TrueMoon.Nom.Tests;

public sealed class Nom5RetentionCodecTests
{
    private static AssetResourceVersion Version => new(NomFormatFixtures.ProjectId, NomFormatFixtures.StoreId,
        "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f", new string('a', 64), 0x0102030405);
    private const string VersionHex = "00112233445566778899aabbccddeeff102132435465768798a9bacbdcedfe0f" +
        "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f" +
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa0504030201000000";

    [Fact]
    public void RetentionCodecs_IndependentGoldenBytesPinManifestMemberPinGcAndLinkFieldOrder()
    {
        var version = Version with { Length = 0x01020304 };
        var versionHex = VersionHex[..^16] + "0403020100000000";
        var member = Convert.FromHexString("4e4f4d4d454d3031" + versionHex);
        Assert.Equal(member, NomRetentionCodec.EncodeMember(version)); Assert.Equal(version, NomRetentionCodec.DecodeMember(member));
        var manifest = Convert.FromHexString("4e4f4d4d414e3031030000000000000002000000");
        Assert.Equal(manifest, NomRetentionCodec.EncodeManifest(3, 2)); Assert.Equal((3L, 2), NomRetentionCodec.DecodeManifest(manifest));
        var pin = Convert.FromHexString("4e4f4d50494e3031" + new string('a', 64));
        Assert.Equal(pin, NomRetentionCodec.EncodeDigest("NOMPIN01", new string('a', 64))); Assert.Equal(new string('a', 64), NomRetentionCodec.DecodeDigest(pin, "NOMPIN01"));
        var gc = Convert.FromHexString("4e4f4d4743513031" + new string('a', 64) + "0300000000000000");
        Assert.Equal(gc, NomRetentionCodec.EncodeGc(new string('a', 64), 3)); Assert.Equal((new string('a', 64), 3L), NomRetentionCodec.DecodeGc(gc));
        var links = Convert.FromHexString("4e4f4d4c4e4b30310200000001000000010000004102000000d0b6" + versionHex);
        Assert.Equal(links, NomRetentionCodec.EncodeLinks(["A", "ж"], [version]));
        var decoded = NomRetentionCodec.DecodeLinks(links); Assert.Equal(new[] { "A", "ж" }, decoded.Tags); Assert.Equal(version, Assert.Single(decoded.Dependencies));
    }

    [Theory]
    [InlineData("links")][InlineData("manifest")][InlineData("member")][InlineData("gc")][InlineData("pin")]
    public void RetentionCodecs_EveryTruncatedPrefixAndTrailingByteRejects(string kind)
    {
        var version = Version with { Length = 3 }; var bytes = kind switch
        {
            "links" => NomRetentionCodec.EncodeLinks(["A"], [version]), "manifest" => NomRetentionCodec.EncodeManifest(2, 1),
            "member" => NomRetentionCodec.EncodeMember(version), "gc" => NomRetentionCodec.EncodeGc(version.Digest, 3), _ => NomRetentionCodec.EncodeDigest("NOMPIN01", version.Revision)
        };
        void Decode(byte[] value)
        {
            switch (kind)
            {
                case "links": NomRetentionCodec.DecodeLinks(value); break; case "manifest": NomRetentionCodec.DecodeManifest(value); break;
                case "member": NomRetentionCodec.DecodeMember(value); break; case "gc": NomRetentionCodec.DecodeGc(value); break; default: NomRetentionCodec.DecodeDigest(value, "NOMPIN01"); break;
            }
        }
        for (var n = 0; n < bytes.Length; n++) Assert.Throws<InvalidDataException>(() => Decode(bytes[..n]));
        Assert.Throws<InvalidDataException>(() => Decode([.. bytes, 0]));
    }

    [Theory]
    [InlineData("tag-order")][InlineData("tag-duplicate")][InlineData("dep-order")][InlineData("dep-duplicate")][InlineData("tag-utf8")][InlineData("tag-count")][InlineData("dep-count")]
    public void Metadata02_NoncanonicalOrOversizedExtensionRejectsWithoutNormalization(string invalid)
    {
        var version = Version with { Length = 3 }; var second = version with { Revision = new string('f', 64) };
        var links = invalid switch
        {
            "tag-order" => NomRetentionCodec.EncodeLinks(["z", "A"], []), "tag-duplicate" => NomRetentionCodec.EncodeLinks(["A", "A"], []),
            "dep-order" => NomRetentionCodec.EncodeLinks([], [second, version]), "dep-duplicate" => NomRetentionCodec.EncodeLinks([], [version, version]),
            _ => NomRetentionCodec.EncodeLinks(["A"], [])
        };
        if (invalid == "tag-utf8") links[20] = 0xff;
        if (invalid == "tag-count") BinaryPrimitives.WriteUInt32LittleEndian(links.AsSpan(8), 33);
        if (invalid == "dep-count") BinaryPrimitives.WriteUInt32LittleEndian(links.AsSpan(12), 33);
        var legacy = NomResourceMetadataCodec.Encode(Nom5Context.Asset(1)); "NOMAST02"u8.CopyTo(legacy);
        Assert.Throws<InvalidDataException>(() => NomResourceMetadataCodec.Decode([.. legacy, .. links]));
    }
}
