using System.Buffers.Binary;
using System.Collections;
using System.Numerics;
using System.Security.Cryptography;
using TrueMoon.Aether.Rendering;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering;

public sealed class RenderManifestTests
{
    [Fact]
    public void Manifest_SortsRfcGuidBytesRatherThanPlatformLittleEndianArrays()
    {
        var ids = new[] { "00000001-0000-0000-0000-000000000000", "00000100-0000-0000-0000-000000000000", "00010000-0000-0000-0000-000000000000" }
            .Select(Guid.Parse).ToArray();
        var material = RenderContentFixture.Material();
        var input = new[] { ids[2], ids[0], ids[1] }.Select(id => new RenderAssetEntry(material.CreateKey(RenderContentFixture.Project, id), material)).ToArray();
        var manifest = RenderContentFixture.Manifest(input);
        Assert.Equal(ids, manifest.Entries.Select(entry => entry.Key.AssetId));
        var payload = RenderContentCodec.EncodeManifest(manifest);
        Assert.Equal(Convert.FromHexString("00000001000000000000000000000000"), payload.AsSpan(76, 16).ToArray());
        Assert.Equal(Convert.FromHexString("00000100000000000000000000000000"), payload.AsSpan(160, 16).ToArray());
        Assert.Equal(Convert.FromHexString("00010000000000000000000000000000"), payload.AsSpan(244, 16).ToArray());
    }

    [Fact]
    public void Manifest_SnapshotsAndSortsCompleteClosureAndMatchesIndependentFingerprint()
    {
        var entries = RenderContentFixture.Entries();
        var meshKey = entries[0].Key; var materialKey = entries[1].Key; var bindingKey = entries[2].Key;
        var input = entries.Reverse().ToArray();
        var manifest = RenderContentFixture.Manifest(input);
        var expected = LiteralManifestBytes(entries);
        Assert.Equal(312, expected.Length);
        Assert.Equal(new[] { meshKey, materialKey, bindingKey }, manifest.Entries.Select(entry => entry.Key));
        Assert.Equal(expected, RenderContentCodec.EncodeManifest(manifest));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(expected)).ToLowerInvariant(), manifest.Digest.ToString());
        Assert.Equal(RenderContentFixture.Project, manifest.ProjectId);
        Assert.Equal(RenderContentFixture.Build, manifest.BuildId);
        Assert.Equal(RenderContentFixture.Generation, manifest.Generation);
        Array.Fill(input, null!);
        Assert.Equal(meshKey, manifest.Resolve(RenderContentFixture.MeshId, RenderContentProfile.MeshKindId).Key);
        Assert.Equal(materialKey, manifest.Resolve(materialKey).Key);
        Assert.Equal(bindingKey, manifest.Resolve(bindingKey).Key);
        Assert.Equal(expected, RenderContentCodec.EncodeManifest(manifest));
    }

    [Fact]
    public void Manifest_FingerprintIgnoresInputOrderAndGenerationButPinsProjectBuildAndContent()
    {
        var entries = RenderContentFixture.Entries();
        var first = RenderContentFixture.Manifest(entries);
        var otherGeneration = Guid.Parse("aabbccdd-eeff-0011-2233-445566778800");
        var second = new RenderContentManifest(RenderContentFixture.Project, RenderContentFixture.Build, otherGeneration, entries.Reverse().ToArray());
        Assert.NotEqual(first.Generation, second.Generation);
        Assert.Equal(first.Digest, second.Digest);
        Assert.Equal(RenderContentCodec.EncodeManifest(first), RenderContentCodec.EncodeManifest(second));
        var changedBuild = new RenderContentManifest(RenderContentFixture.Project, RenderContentHash.Compute("next-build"u8), otherGeneration, entries);
        Assert.NotEqual(first.Digest, changedBuild.Digest);
        var changedMaterial = new OpaqueUnlitMaterialDefinition(new(.5f, .25f, 1));
        var changedMaterialKey = changedMaterial.CreateKey(RenderContentFixture.Project, RenderContentFixture.MaterialId);
        var changedBindings = new MaterialBindingSetDefinition(entries[0].Key, [new(RenderContentFixture.Slot, changedMaterialKey)]);
        var changed = RenderContentFixture.Manifest([entries[0], new(changedMaterialKey, changedMaterial),
            new(changedBindings.CreateKey(RenderContentFixture.Project, RenderContentFixture.BindingsId), changedBindings)]);
        Assert.NotEqual(first.Digest, changed.Digest);
        Assert.Equal(RenderContentFixture.MaterialHash, first.Resolve(RenderContentFixture.MaterialId, RenderContentProfile.MaterialKindId).Key.Revision.ToString());
        Assert.Equal(changedMaterial.Revision, changed.Resolve(changedMaterialKey).Key.Revision);
        var otherProject = Guid.Parse("00112233-4455-6677-8899-aabbccddee00");
        var projectA = new RenderContentManifest(RenderContentFixture.Project, RenderContentFixture.Build, otherGeneration, []);
        var projectB = new RenderContentManifest(otherProject, RenderContentFixture.Build, otherGeneration, []);
        Assert.NotEqual(projectA.Digest, projectB.Digest);
    }

    [Fact]
    public void Manifest_EmptyAndSingletonScopesHaveExactFingerprintsAndMissingResolution()
    {
        var empty = RenderContentFixture.Manifest([]);
        Assert.Empty(empty.Entries);
        Assert.Equal(LiteralManifestBytes([]), RenderContentCodec.EncodeManifest(empty));
        Assert.Equal(60, RenderContentCodec.EncodeManifest(empty).Length);
        Error(RenderContentError.MissingAsset, () => empty.Resolve(RenderContentFixture.MeshId, RenderContentProfile.MeshKindId));
        var material = RenderContentFixture.Entries()[1];
        var singleton = RenderContentFixture.Manifest([material]);
        Assert.Equal(material.Key, Assert.Single(singleton.Entries).Key);
        Assert.Equal(LiteralManifestBytes([material]), RenderContentCodec.EncodeManifest(singleton));
        Assert.NotEqual(empty.Digest, singleton.Digest);
        Assert.Same(material, singleton.Resolve(material.Key));
    }

    [Theory]
    [InlineData("project")] [InlineData("generation")] [InlineData("build")]
    public void Manifest_RejectsEmptyScopeIdentity(string field)
    {
        Error(RenderContentError.InvalidIdentity, () => new RenderContentManifest(
            field == "project" ? Guid.Empty : RenderContentFixture.Project,
            field == "build" ? default : RenderContentFixture.Build,
            field == "generation" ? Guid.Empty : RenderContentFixture.Generation, []));
    }

    [Fact]
    public void Manifest_RejectsNullEntriesAndNullDefinitions()
    {
        Assert.Throws<ArgumentNullException>(() => new RenderContentManifest(RenderContentFixture.Project, RenderContentFixture.Build, RenderContentFixture.Generation, null!));
        Assert.Throws<ArgumentNullException>(() => RenderContentFixture.Manifest([null!]));
        var valid = RenderContentFixture.Entries()[1];
        Assert.Throws<ArgumentNullException>(() => RenderContentFixture.Manifest([new(valid.Key, null!)]));
    }

    [Theory]
    [InlineData("same-entry")] [InlineData("other-revision")] [InlineData("other-kind")]
    public void Manifest_RejectsDuplicateAssetIdWithoutChoosingLatestOrFirstKind(string variant)
    {
        var first = RenderContentFixture.Entries()[1];
        RenderAssetEntry second = first;
        if (variant == "other-revision")
        {
            var material = new OpaqueUnlitMaterialDefinition(new(.5f, .5f, .5f));
            second = new(material.CreateKey(RenderContentFixture.Project, RenderContentFixture.MaterialId), material);
        }
        else if (variant == "other-kind")
        {
            var mesh = RenderContentFixture.Mesh();
            second = new(mesh.CreateKey(RenderContentFixture.Project, RenderContentFixture.MaterialId), mesh);
        }
        Error(RenderContentError.DuplicateAsset, () => RenderContentFixture.Manifest([first, second]));
    }

    [Theory]
    [InlineData("foreign-project", RenderContentError.InvalidIdentity)]
    [InlineData("kind", RenderContentError.AssetKindMismatch)]
    [InlineData("revision", RenderContentError.RevisionMismatch)]
    [InlineData("schema", RenderContentError.UnsupportedVersion)]
    public void Manifest_RejectsKeyAndDefinitionDisagreement(string field, RenderContentError error)
    {
        var entry = RenderContentFixture.Entries()[1];
        var key = field switch
        {
            "foreign-project" => entry.Key with { ProjectId = RenderContentFixture.Generation },
            "kind" => entry.Key with { KindId = RenderContentProfile.MeshKindId },
            "revision" => entry.Key with { Revision = RenderContentHash.Compute("missing-content"u8) },
            _ => entry.Key with { SchemaVersion = 2 }
        };
        Error(error, () => RenderContentFixture.Manifest([new(key, entry.Definition)]));
    }

    [Theory]
    [InlineData("missing-mesh", RenderContentError.MissingAsset)]
    [InlineData("missing-material", RenderContentError.MissingAsset)]
    [InlineData("mesh-revision", RenderContentError.RevisionMismatch)]
    [InlineData("material-revision", RenderContentError.RevisionMismatch)]
    [InlineData("mesh-kind", RenderContentError.AssetKindMismatch)]
    [InlineData("material-kind", RenderContentError.AssetKindMismatch)]
    [InlineData("foreign-slot", RenderContentError.InvalidBinding)]
    [InlineData("extra-slot", RenderContentError.InvalidBinding)]
    [InlineData("missing-slot", RenderContentError.InvalidBinding)]
    public void Manifest_RejectsIncompleteOrIncompatiblePinnedDependencyClosure(string field, RenderContentError error)
    {
        var entries = RenderContentFixture.Entries();
        if (field == "missing-mesh") { Error(error, () => RenderContentFixture.Manifest(entries[1..])); return; }
        if (field == "missing-material") { Error(error, () => RenderContentFixture.Manifest([entries[0], entries[2]])); return; }
        var mesh = Assert.IsType<MeshDefinition>(entries[0].Definition);
        var meshKey = entries[0].Key; var materialKey = entries[1].Key;
        var secondSlot = Guid.Parse("11223344-5566-7788-99aa-bbccddeeff11");
        if (field == "missing-slot")
        {
            mesh = new([Vector3.Zero, Vector3.UnitX, Vector3.UnitY], [0, 1, 2, 0, 2, 1],
                [new(RenderContentFixture.Slot, 0, 3), new(secondSlot, 3, 3)], new(Vector3.Zero, new(1, 1, 0)));
            meshKey = mesh.CreateKey(RenderContentFixture.Project, RenderContentFixture.MeshId);
            entries[0] = new(meshKey, mesh);
        }
        if (field == "mesh-revision") meshKey = meshKey with { Revision = RenderContentHash.Compute("unpublished-mesh"u8) };
        if (field == "material-revision") materialKey = materialKey with { Revision = RenderContentHash.Compute("unpublished-material"u8) };
        if (field == "mesh-kind") entries[0] = new(entries[1].Definition.CreateKey(RenderContentFixture.Project, RenderContentFixture.MeshId), entries[1].Definition);
        if (field == "material-kind") entries[1] = new(mesh.CreateKey(RenderContentFixture.Project, RenderContentFixture.MaterialId), mesh);
        MaterialSlotBinding[] bindings = [new(field == "foreign-slot" ? secondSlot : RenderContentFixture.Slot, materialKey)];
        if (field == "extra-slot") bindings = [.. bindings, new(secondSlot, materialKey)];
        var set = new MaterialBindingSetDefinition(meshKey, bindings);
        entries[2] = new(set.CreateKey(RenderContentFixture.Project, RenderContentFixture.BindingsId), set);
        Error(error, () => RenderContentFixture.Manifest(entries));
    }

    [Theory]
    [InlineData("foreign-project", RenderContentError.InvalidIdentity)]
    [InlineData("revision", RenderContentError.RevisionMismatch)]
    [InlineData("kind", RenderContentError.AssetKindMismatch)]
    [InlineData("missing", RenderContentError.MissingAsset)]
    public void Resolve_RequiresExactFrozenIdentityWithoutLatestFallback(string field, RenderContentError error)
    {
        var manifest = RenderContentFixture.Manifest();
        var key = manifest.Resolve(RenderContentFixture.MaterialId, RenderContentProfile.MaterialKindId).Key;
        var changed = field switch
        {
            "foreign-project" => key with { ProjectId = RenderContentFixture.Generation },
            "revision" => key with { Revision = RenderContentHash.Compute("next-revision"u8) },
            "kind" => key with { KindId = RenderContentProfile.MeshKindId },
            _ => key with { AssetId = RenderContentFixture.Generation }
        };
        Error(error, () => manifest.Resolve(changed));
        Assert.Equal(RenderContentFixture.MaterialHash, manifest.Resolve(key).Key.Revision.ToString());
    }

    [Fact]
    public void Resolve_RejectsInvalidIdsAndExpectedKindsAndValidationCancellationDoesNotPoisonScope()
    {
        var manifest = RenderContentFixture.Manifest();
        Error(RenderContentError.InvalidIdentity, () => manifest.Resolve(Guid.Empty, RenderContentProfile.MeshKindId));
        Error(RenderContentError.AssetKindMismatch, () => manifest.Resolve(RenderContentFixture.MeshId, Guid.Empty));
        Error(RenderContentError.AssetKindMismatch, () => manifest.Resolve(RenderContentFixture.MeshId, RenderContentFixture.Generation));
        Error(RenderContentError.AssetKindMismatch, () => manifest.Resolve(RenderContentFixture.MeshId, RenderContentProfile.MaterialKindId));
        Error(RenderContentError.InvalidIdentity, () => manifest.Resolve(default(RenderAssetKey)));
        Error(RenderContentError.InvalidIdentity, () => manifest.ValidateAsset(Guid.Empty));
        Error(RenderContentError.MissingAsset, () => manifest.ValidateAsset(RenderContentFixture.Generation));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var cancellation = Assert.Throws<OperationCanceledException>(() => manifest.ValidateAsset(RenderContentFixture.MeshId, cancelled.Token));
        Assert.Equal(cancelled.Token, cancellation.CancellationToken);
        manifest.ValidateAsset(RenderContentFixture.MeshId);
        Assert.Equal(RenderContentFixture.MeshHash, manifest.Resolve(RenderContentFixture.MeshId, RenderContentProfile.MeshKindId).Key.Revision.ToString());
    }

    [Fact]
    public void Manifest_AdmitsExactlyOneThousandTwentyFourEntriesAndRejectsNextBeforeReading()
    {
        var material = RenderContentFixture.Material();
        var entries = Enumerable.Range(1, 1024).Select(index => new RenderAssetEntry(material.CreateKey(RenderContentFixture.Project,
            new Guid($"00000000-0000-0000-0000-{index:000000000000}")), material)).ToArray();
        var manifest = RenderContentFixture.Manifest(entries);
        Assert.Equal(1024, manifest.Entries.Length);
        Assert.Equal(86076, RenderContentCodec.EncodeManifest(manifest).Length);
        Assert.Equal(entries[^1].Key, manifest.Resolve(entries[^1].Key).Key);
        var tooMany = new CountOnlyList<RenderAssetEntry>(1025);
        Error(RenderContentError.LimitExceeded, () => RenderContentFixture.Manifest(tooMany));
        Assert.Equal(0, tooMany.Reads);
        var negative = new CountOnlyList<RenderAssetEntry>(-1);
        Error(RenderContentError.LimitExceeded, () => RenderContentFixture.Manifest(negative));
        Assert.Equal(0, negative.Reads);
    }

    [Fact]
    public void BuiltinManifest_ContainsExactlyVersionedCubeWhiteAndClosedBindingSetForItsProject()
    {
        var manifest = BuiltinRenderContent.Create(RenderContentFixture.Project, RenderContentFixture.Build, RenderContentFixture.Generation);
        Assert.Equal(3, manifest.Entries.Length);
        var mesh = manifest.Resolve(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1401"), RenderContentProfile.MeshKindId);
        var material = manifest.Resolve(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1402"), RenderContentProfile.MaterialKindId);
        var bindings = manifest.Resolve(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1403"), RenderContentProfile.BindingSetKindId);
        Assert.Equal(Vector3.One, Assert.IsType<OpaqueUnlitMaterialDefinition>(material.Definition).BaseColorSrgb);
        var set = Assert.IsType<MaterialBindingSetDefinition>(bindings.Definition);
        Assert.Equal(mesh.Key, set.Mesh);
        Assert.Equal(new MaterialSlotBinding(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1404"), material.Key), Assert.Single(set.Bindings));
        set.ValidateAgainst(mesh.Key, Assert.IsType<MeshDefinition>(mesh.Definition));
        Assert.All(manifest.Entries, entry => Assert.Equal(RenderContentFixture.Project, entry.Key.ProjectId));
        var repeated = BuiltinRenderContent.Create(RenderContentFixture.Project, RenderContentFixture.Build, Guid.Parse("aabbccdd-eeff-0011-2233-445566778800"));
        Assert.Equal(manifest.Digest, repeated.Digest);
        Assert.NotSame(manifest, repeated);
        Assert.NotSame(mesh.Definition, repeated.Resolve(mesh.Key).Definition);
    }

    private static byte[] LiteralManifestBytes(IReadOnlyList<RenderAssetEntry> entries)
    {
        var hex = "544d524d01000000" + RenderContentFixture.Project.ToString("N") +
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
        var prefix = Convert.FromHexString(hex);
        var result = new byte[60 + 84 * entries.Count];
        prefix.CopyTo(result, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(56), (uint)entries.Count);
        var offset = 60;
        foreach (var entry in entries.OrderBy(entry => entry.Key.AssetId.ToString("N"), StringComparer.Ordinal))
        {
            var key = entry.Key;
            var bytes = Convert.FromHexString(key.ProjectId.ToString("N") + key.AssetId.ToString("N") + key.KindId.ToString("N") +
                "01000000" + key.Revision.ToString());
            bytes.CopyTo(result, offset); offset += 84;
        }
        return result;
    }

    private static void Error(RenderContentError expected, Action action)
        => Assert.Equal(expected, Assert.Throws<RenderContentException>(action).Error);

    private sealed class CountOnlyList<T>(int count) : IReadOnlyList<T>
    {
        public int Count => count;
        public int Reads { get; private set; }
        public T this[int index] { get { Reads++; throw new InvalidOperationException("Admission must precede reading entries."); } }
        public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("Admission must precede enumeration.");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
