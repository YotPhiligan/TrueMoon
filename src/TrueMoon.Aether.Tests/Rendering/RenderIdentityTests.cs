using System.Runtime.CompilerServices;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Rendering;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering;

public sealed class RenderIdentityTests
{
    private const string AbcHash = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    private static readonly Guid Project = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
    private static readonly Guid Asset = Guid.Parse("11223344-5566-7788-99aa-bbccddeeff00");

    [Fact]
    public void Hash_UsesStandardSha256BytesAndCanonicalLowercaseText()
    {
        var hash = RenderContentHash.Compute("abc"u8);
        Assert.Equal(AbcHash, hash.ToString());
        Assert.Equal(Convert.FromHexString(AbcHash), hash.ToArray());
        Assert.Equal(hash, RenderContentHash.Parse(AbcHash.ToUpperInvariant()));
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", RenderContentHash.Compute([]).ToString());
        Assert.False(hash.IsEmpty);
        Assert.True(default(RenderContentHash).IsEmpty);
        Assert.NotEqual(hash, RenderContentHash.Compute("abd"u8));
    }

    [Fact]
    public void Hash_CopiesInputAndOutputAndWritesOnlyThirtyTwoDestinationBytes()
    {
        var input = Convert.FromHexString(AbcHash);
        var hash = RenderContentHash.FromBytes(input);
        Array.Fill(input, (byte)0);
        var output = hash.ToArray();
        Array.Fill(output, (byte)255);
        var destination = Enumerable.Repeat((byte)0x5a, 35).ToArray();
        hash.WriteBytes(destination);
        Assert.Equal(Convert.FromHexString(AbcHash), destination[..32]);
        Assert.Equal(new byte[] { 0x5a, 0x5a, 0x5a }, destination[32..]);
        Assert.Equal(AbcHash, hash.ToString());
        Assert.Equal(Convert.FromHexString(AbcHash), hash.ToArray());
        Assert.True(RenderContentHash.FromBytes(new byte[32]).IsEmpty);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(31)] [InlineData(33)] [InlineData(64)]
    public void Hash_FromBytesRejectsEveryNonDigestLength(int length)
    {
        var error = Assert.Throws<ArgumentException>(() => RenderContentHash.FromBytes(new byte[length]));
        Assert.Equal("bytes", error.ParamName);
    }

    [Theory]
    [InlineData(0)] [InlineData(31)]
    public void Hash_WriteRejectsShortDestinationBeforeChangingIt(int length)
    {
        var destination = Enumerable.Repeat((byte)0x5a, length).ToArray();
        var hash = RenderContentHash.Parse(AbcHash);
        var error = Assert.Throws<ArgumentException>(() => hash.WriteBytes(destination));
        Assert.Equal("destination", error.ParamName);
        Assert.All(destination, value => Assert.Equal((byte)0x5a, value));
    }

    [Theory]
    [InlineData("")] [InlineData("short")]
    [InlineData("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015a")]
    [InlineData("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015add")]
    [InlineData("ga7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [InlineData(" ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    public void Hash_ParseRejectsNoncanonicalLengthOrNonhexText(string text)
        => Assert.Throws<FormatException>(() => RenderContentHash.Parse(text));

    [Fact]
    public void Hash_ParseRejectsNullWithoutTreatingItAsDefault()
        => Assert.Throws<ArgumentNullException>(() => RenderContentHash.Parse(null!));

    [Theory]
    [InlineData("project")] [InlineData("asset")] [InlineData("revision")] [InlineData("default")]
    public void Key_RejectsMissingIdentityDimensions(string field)
    {
        var key = Key();
        key = field switch
        {
            "project" => key with { ProjectId = Guid.Empty },
            "asset" => key with { AssetId = Guid.Empty },
            "revision" => key with { Revision = default },
            _ => default
        };
        Assert.Equal(RenderContentError.InvalidIdentity, Assert.Throws<RenderContentException>(() => key.Validate()).Error);
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(2)] [InlineData(int.MaxValue)]
    public void Key_RejectsUnknownExactSchemaVersion(int version)
    {
        var key = Key() with { SchemaVersion = version };
        Assert.Equal(RenderContentError.UnsupportedVersion, Assert.Throws<RenderContentException>(() => key.Validate()).Error);
    }

    [Fact]
    public void Key_RejectsUnknownAndMismatchedKindsWithoutCoercion()
    {
        var key = Key();
        key.Validate(RenderContentProfile.MaterialKindId);
        foreach (var kind in new[] { Guid.Empty, Asset })
        {
            var unknown = key with { KindId = kind };
            Assert.Equal(RenderContentError.AssetKindMismatch, Assert.Throws<RenderContentException>(() => unknown.Validate()).Error);
        }
        Assert.Equal(RenderContentError.AssetKindMismatch,
            Assert.Throws<RenderContentException>(() => key.Validate(RenderContentProfile.MeshKindId)).Error);
    }

    [Fact]
    public void Key_EqualitySeparatesEveryPinnedDimensionAndValuesEnterWorldWithoutManagedReferences()
    {
        var key = Key();
        Assert.Equal(key, new RenderAssetKey(Project, Asset, Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1202"), 1, RenderContentHash.Parse(AbcHash)));
        Assert.NotEqual(key, key with { ProjectId = Asset });
        Assert.NotEqual(key, key with { AssetId = Project });
        Assert.NotEqual(key, key with { KindId = RenderContentProfile.MeshKindId });
        Assert.NotEqual(key, key with { SchemaVersion = 2 });
        Assert.NotEqual(key, key with { Revision = RenderContentHash.Compute("different"u8) });
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<RenderAssetKey>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<RenderContentHash>());
        using var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, key);
        world.AddComponent(entity, key.Revision);
        Assert.Equal(Project, world.GetComponent<RenderAssetKey>(entity).ProjectId);
        Assert.Equal(Asset, world.GetComponent<RenderAssetKey>(entity).AssetId);
        Assert.Equal(AbcHash, world.GetComponent<RenderContentHash>(entity).ToString());
    }

    [Fact]
    public void Profile_ExposesExactReservedIdentitiesAndCpuCeilings()
    {
        Assert.Equal("aether.opaque-unlit/1", RenderContentProfile.ProfileId);
        Assert.Equal(1, RenderContentProfile.SchemaVersion);
        Assert.Equal(new[] { "1201", "1202", "1203", "1301", "1401", "1402", "1403", "1404" }
                .Select(suffix => Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f" + suffix)),
            new[] { RenderContentProfile.MeshKindId, RenderContentProfile.MaterialKindId, RenderContentProfile.BindingSetKindId,
                RenderContentProfile.UnlitFamilyId, RenderContentProfile.CubeAssetId, RenderContentProfile.WhiteMaterialAssetId,
                RenderContentProfile.CubeBindingSetAssetId, RenderContentProfile.DefaultSlotId });
        Assert.Equal((65536, 196608, 8, 2097152, 4096, 1024, 262144, 1000000f),
            (RenderContentProfile.MaximumVertices, RenderContentProfile.MaximumIndices, RenderContentProfile.MaximumSlots,
                RenderContentProfile.MaximumMeshBytes, RenderContentProfile.MaximumSmallAssetBytes,
                RenderContentProfile.MaximumManifestEntries, RenderContentProfile.MaximumManifestBytes, RenderContentProfile.MaximumCoordinate));
    }

    private static RenderAssetKey Key() => new(Project, Asset, RenderContentProfile.MaterialKindId, 1, RenderContentHash.Parse(AbcHash));
}
