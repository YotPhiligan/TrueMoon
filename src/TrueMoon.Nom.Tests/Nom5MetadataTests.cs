using Xunit;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.Tests;

public sealed class Nom5MetadataTests
{
    [Fact]
    public void AssetRecord_TagsAndExactDependenciesAreSortedCopiedAndIncludedInRevisionIdentity()
    {
        var asset = Nom5Context.Asset(1); var digest = new string('a', 64);
        var a = new AssetResourceVersion(Nom5Context.Id(2), asset.Kind, new string('1', 64), digest, 7);
        var b = new AssetResourceVersion(Nom5Context.Id(3), asset.Kind, new string('2', 64), digest, 8);
        var tags = new[] { "zeta", "Alpha", "alpha" }; var dependencies = new[] { b, a }; var metadata = new byte[] { 7, 8 };
        var value = new AssetRecord(asset.Id, asset.Source, asset.Kind, asset.Locator, metadata, tags, dependencies);
        tags[0] = "changed"; dependencies[0] = a; metadata[0] = 99;
        Assert.Equal(new[] { "Alpha", "alpha", "zeta" }, value.Tags.ToArray()); Assert.Equal(new[] { a, b }, value.Dependencies.ToArray());
        Assert.Equal(new byte[] { 7, 8 }, value.Metadata);
        var first = value.Tags; first[0] = "local"; Assert.Equal("Alpha", value.Tags.First());
        var copiedDependencies = value.Dependencies; copiedDependencies[0] = b; Assert.Equal(a, value.Dependencies[0]);
        var encoded = NomResourceMetadataCodec.Encode(value); Assert.Equal("NOMAST02", System.Text.Encoding.ASCII.GetString(encoded, 0, 8));
        var decoded = NomResourceMetadataCodec.Decode(encoded); Assert.Equal(value.Tags, decoded.Tags); Assert.Equal(value.Dependencies, decoded.Dependencies);
        var revision = NomResourceMetadataCodec.Revision(value, digest, 12);
        Assert.NotEqual(NomResourceMetadataCodec.Revision(new(asset.Id, asset.Source, asset.Kind, asset.Locator, metadata), digest, 12), revision);
        Assert.Equal(revision, NomResourceMetadataCodec.Revision(decoded, digest, 12));
    }

    [Fact]
    public void AssetRecord_EmptyExtensionPreservesLegacyMetadataWireAndRevision()
    {
        var asset = NomFormatFixtures.Asset;
        var empty = new AssetRecord(asset.Id, asset.Source, asset.Kind, asset.Locator, asset.Metadata, [], []);
        Assert.Equal(NomResourceMetadataCodec.Encode(asset), NomResourceMetadataCodec.Encode(empty));
        Assert.Equal("NOMAST01", System.Text.Encoding.ASCII.GetString(NomResourceMetadataCodec.Encode(empty), 0, 8));
        Assert.Equal(NomResourceMetadataCodec.Revision(asset, new string('a', 64), 9), NomResourceMetadataCodec.Revision(empty, new string('a', 64), 9));
        Assert.Empty(empty.Tags); Assert.Empty(empty.Dependencies);
    }

    [Theory]
    [InlineData("empty")][InlineData("blank")][InlineData("control")][InlineData("utf8-over")][InlineData("duplicate")][InlineData("count-over")]
    public void AssetRecord_InvalidTagsAreRejectedBeforeDescriptorPublication(string invalid)
    {
        string[] tags = invalid switch
        {
            "empty" => [""], "blank" => [" "], "control" => ["a\nb"], "utf8-over" => [new string('ж', 65)],
            "duplicate" => ["same", "same"], _ => Enumerable.Range(0, 33).Select(n => "tag-" + n).ToArray()
        };
        Assert.Throws<InvalidDataException>(() => Nom5Context.Asset(1, tags: tags));
    }

    [Fact]
    public void AssetRecord_TagCountAndUtf8LengthAcceptExactBoundaries()
    {
        var tags = Enumerable.Range(0, 31).Select(n => "tag-" + n).Append(new string('ж', 64)).ToArray();
        var asset = Nom5Context.Asset(1, tags: tags); var decoded = NomResourceMetadataCodec.Decode(NomResourceMetadataCodec.Encode(asset));
        Assert.Equal(32, decoded.Tags.Length); Assert.Contains(new string('ж', 64), decoded.Tags);
    }

    [Theory]
    [InlineData("empty-id")][InlineData("empty-kind")][InlineData("revision")][InlineData("digest")][InlineData("length")][InlineData("duplicate")][InlineData("count-over")]
    public void AssetRecord_InvalidExactDependenciesAreRejected(string invalid)
    {
        var valid = new AssetResourceVersion(Nom5Context.Id(1), Nom5Context.Id(2), new string('a', 64), new string('b', 64), 3);
        var value = invalid switch
        {
            "empty-id" => valid with { AssetId = Guid.Empty }, "empty-kind" => valid with { KindId = Guid.Empty },
            "revision" => valid with { Revision = "bad" }, "digest" => valid with { Digest = "bad" }, "length" => valid with { Length = -1 }, _ => valid
        };
        var deps = invalid == "duplicate" ? new[] { valid, valid } : invalid == "count-over"
            ? Enumerable.Range(0, 33).Select(n => valid with { Revision = n.ToString("x64") }).ToArray() : new[] { value };
        Assert.Throws<InvalidDataException>(() => Nom5Context.Asset(3, dependencies: deps));
    }

    [Fact]
    public void AssetRecord_ExactDependencyCountAndObjectLengthAcceptLastAllowedValues()
    {
        var dependencies = Enumerable.Range(0, 32).Select(n => new AssetResourceVersion(Nom5Context.Id(n + 10), Nom5Context.Id(2000),
            n.ToString("x64"), new string('a', 64), n == 31 ? ResourceCodec.MaximumObject : 0)).ToArray();
        var asset = Nom5Context.Asset(1, dependencies: dependencies); var decoded = NomResourceMetadataCodec.Decode(NomResourceMetadataCodec.Encode(asset));
        Assert.Equal(dependencies, decoded.Dependencies); Assert.Equal(ResourceCodec.MaximumObject, decoded.Dependencies[^1].Length);
        Assert.Throws<InvalidDataException>(() => Nom5Context.Asset(1, dependencies: [dependencies[^1] with { Length = ResourceCodec.MaximumObject + 1 }]));
    }
}
