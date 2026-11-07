using System.Numerics;
using TrueMoon.Aether.Rendering;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering;

public sealed class RenderBindingSetTests
{
    [Fact]
    public void Bindings_CopyOrderedEntriesAndAllowTheSameMaterialForDistinctSlots()
    {
        var (mesh, key, material, slots) = Geometry(2);
        var input = slots.Select(slot => new MaterialSlotBinding(slot.SlotId, material)).ToArray();
        var set = new MaterialBindingSetDefinition(key, input);
        set.ValidateAgainst(key, mesh);
        Assert.Equal(key, set.Mesh);
        Assert.Equal(new[] { slots[0].SlotId, slots[1].SlotId }, set.Bindings.Select(binding => binding.SlotId));
        Assert.All(set.Bindings, binding => Assert.Equal(material, binding.Material));
        var revision = set.Revision;
        Array.Fill(input, default);
        Assert.Equal(new MaterialSlotBinding(slots[0].SlotId, material), set.Bindings[0]);
        Assert.Equal(new MaterialSlotBinding(slots[1].SlotId, material), set.Bindings[1]);
        Assert.Equal(revision, set.Revision);
        set.ValidateAgainst(key, mesh);
    }

    [Theory]
    [InlineData("empty", RenderContentError.InvalidBinding)]
    [InlineData("too-many", RenderContentError.LimitExceeded)]
    [InlineData("empty-slot", RenderContentError.InvalidBinding)]
    [InlineData("duplicate-slot", RenderContentError.InvalidBinding)]
    [InlineData("default-material", RenderContentError.InvalidIdentity)]
    [InlineData("wrong-material-kind", RenderContentError.AssetKindMismatch)]
    [InlineData("foreign-material", RenderContentError.InvalidIdentity)]
    [InlineData("material-version", RenderContentError.UnsupportedVersion)]
    public void Bindings_RejectInvalidCollectionOrMaterialReferencesBeforeCompatibility(string field, RenderContentError error)
    {
        var (_, key, material, slots) = Geometry(2);
        MaterialSlotBinding[] bindings = [new(slots[0].SlotId, material), new(slots[1].SlotId, material)];
        bindings = field switch
        {
            "empty" => [],
            "too-many" => Enumerable.Range(1, 9).Select(index => new MaterialSlotBinding(Slot(index), material)).ToArray(),
            "empty-slot" => [new(Guid.Empty, material)],
            "duplicate-slot" => [bindings[0], bindings[0]],
            "default-material" => [new(slots[0].SlotId, default)],
            "wrong-material-kind" => [new(slots[0].SlotId, key)],
            "foreign-material" => [new(slots[0].SlotId, material with { ProjectId = RenderContentFixture.Generation })],
            _ => [new(slots[0].SlotId, material with { SchemaVersion = 2 })]
        };
        Error(error, () => new MaterialBindingSetDefinition(key, bindings));
    }

    [Fact]
    public void Bindings_RejectNullDataDefaultMeshAndWrongMeshKind()
    {
        var (_, key, material, slots) = Geometry(1);
        Assert.Throws<ArgumentNullException>(() => new MaterialBindingSetDefinition(key, null!));
        Error(RenderContentError.InvalidIdentity, () => new MaterialBindingSetDefinition(default, [new(slots[0].SlotId, material)]));
        Error(RenderContentError.AssetKindMismatch, () => new MaterialBindingSetDefinition(material, [new(slots[0].SlotId, material)]));
    }

    [Theory]
    [InlineData(0)] [InlineData(2)]
    public void Bindings_RejectUnknownOwnSchemaVersion(int version)
    {
        var (_, key, material, slots) = Geometry(1);
        Error(RenderContentError.UnsupportedVersion, () => new MaterialBindingSetDefinition(key, [new(slots[0].SlotId, material)], version));
    }

    [Theory]
    [InlineData("missing")] [InlineData("extra")] [InlineData("reversed")] [InlineData("foreign")]
    public void ValidateAgainst_RequiresExactSlotSetAndItsOrder(string field)
    {
        var (mesh, key, material, slots) = Geometry(2);
        MaterialSlotBinding[] bindings = field switch
        {
            "missing" => [new(slots[0].SlotId, material)],
            "extra" => [new(slots[0].SlotId, material), new(slots[1].SlotId, material), new(Slot(3), material)],
            "reversed" => [new(slots[1].SlotId, material), new(slots[0].SlotId, material)],
            _ => [new(slots[0].SlotId, material), new(Slot(3), material)]
        };
        var set = new MaterialBindingSetDefinition(key, bindings);
        Error(RenderContentError.InvalidBinding, () => set.ValidateAgainst(key, mesh));
    }

    [Fact]
    public void ValidateAgainst_RejectsNewRevisionEvenWithIdenticalSlotIdsAndRequiresMatchingContent()
    {
        var (mesh, key, material, slots) = Geometry(2);
        var set = new MaterialBindingSetDefinition(key, slots.Select(slot => new MaterialSlotBinding(slot.SlotId, material)).ToArray());
        var newMesh = new MeshDefinition(mesh.Vertices, mesh.Indices, mesh.Slots, new(Vector3.Zero, new(2, 1, 0)));
        var next = newMesh.CreateKey(RenderContentFixture.Project, RenderContentFixture.MeshId);
        Assert.NotEqual(key.Revision, next.Revision);
        Error(RenderContentError.RevisionMismatch, () => set.ValidateAgainst(next, newMesh));
        Error(RenderContentError.RevisionMismatch, () => set.ValidateAgainst(key, newMesh));
        Error(RenderContentError.RevisionMismatch, () => set.ValidateAgainst(key with { AssetId = RenderContentFixture.MaterialId }, mesh));
        Error(RenderContentError.RevisionMismatch, () => set.ValidateAgainst(key with { ProjectId = RenderContentFixture.Generation }, mesh));
        set.ValidateAgainst(key, mesh);
        Assert.Equal(key, set.Mesh);
    }

    [Fact]
    public void ValidateAgainst_RejectsNullInvalidOrWrongKindTarget()
    {
        var (mesh, key, material, slots) = Geometry(1);
        var set = new MaterialBindingSetDefinition(key, [new(slots[0].SlotId, material)]);
        Assert.Throws<ArgumentNullException>(() => set.ValidateAgainst(key, null!));
        Error(RenderContentError.InvalidIdentity, () => set.ValidateAgainst(default, mesh));
        Error(RenderContentError.AssetKindMismatch, () => set.ValidateAgainst(material, mesh));
        Error(RenderContentError.UnsupportedVersion, () => set.ValidateAgainst(key with { SchemaVersion = 2 }, mesh));
    }

    [Fact]
    public void Bindings_AdmitAllEightExactRangesWithoutDroppingTheLastMaterial()
    {
        var (mesh, key, material, slots) = Geometry(8);
        var set = new MaterialBindingSetDefinition(key, slots.Select(slot => new MaterialSlotBinding(slot.SlotId, material)).ToArray());
        set.ValidateAgainst(key, mesh);
        Assert.Equal(8, set.Bindings.Length);
        Assert.Equal(new MaterialSlotBinding(Slot(8), material), set.Bindings[7]);
        Assert.Equal(900, RenderContentCodec.Encode(set).Length);
    }

    private static (MeshDefinition Mesh, RenderAssetKey Key, RenderAssetKey Material, MeshSlot[] Slots) Geometry(int count)
    {
        var slots = Enumerable.Range(1, count).Select(index => new MeshSlot(Slot(index), (uint)((index - 1) * 3), 3)).ToArray();
        var mesh = new MeshDefinition([Vector3.Zero, Vector3.UnitX, Vector3.UnitY], Enumerable.Range(0, count).SelectMany(_ => new uint[] { 0, 1, 2 }).ToArray(),
            slots, new(Vector3.Zero, new(1, 1, 0)));
        var material = RenderContentFixture.Material().CreateKey(RenderContentFixture.Project, RenderContentFixture.MaterialId);
        return (mesh, mesh.CreateKey(RenderContentFixture.Project, RenderContentFixture.MeshId), material, slots);
    }
    private static Guid Slot(int index) => new($"11223344-5566-7788-99aa-{index:000000000000}");
    private static void Error(RenderContentError expected, Action action)
        => Assert.Equal(expected, Assert.Throws<RenderContentException>(action).Error);
}
