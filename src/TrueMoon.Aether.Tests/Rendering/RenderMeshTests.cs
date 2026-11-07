using System.Collections;
using System.Numerics;
using TrueMoon.Aether.Rendering;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering;

public sealed class RenderMeshTests
{
    private static readonly Guid FirstSlot = new("b30fe1ec-95de-43e9-b062-c30936e55c01");
    private static readonly Guid SecondSlot = new("b30fe1ec-95de-43e9-b062-c30936e55c02");

    [Fact]
    public void Constructor_CopiesAllInputsAndPreservesCompleteOrderedGeometry()
    {
        Vector3[] vertices = [new(-2, -1, -3), new(3, -1, -3), new(0, 4, -3), new(0, 0, 5)];
        uint[] indices = [0, 1, 2, 0, 2, 3];
        MeshSlot[] slots = [new(FirstSlot, 0, 3), new(SecondSlot, 3, 3)];
        var mesh = new MeshDefinition(vertices, indices, slots, new(new(-2, -1, -3), new(3, 4, 5)));
        vertices[3] = new(99); indices[5] = 999; slots[1] = default;

        Assert.Equal(new[] { new Vector3(-2, -1, -3), new Vector3(3, -1, -3), new Vector3(0, 4, -3), new Vector3(0, 0, 5) }, mesh.Vertices);
        Assert.Equal(new uint[] { 0, 1, 2, 0, 2, 3 }, mesh.Indices);
        Assert.Equal(new[] { new MeshSlot(FirstSlot, 0, 3), new MeshSlot(SecondSlot, 3, 3) }, mesh.Slots);
        Assert.Equal(new RenderBounds(new(-2, -1, -3), new(3, 4, 5)), mesh.Bounds);
        Assert.Equal(MeshTopology.TriangleList, mesh.Topology);
        Assert.Equal(MeshVertexLayout.PositionFloat3, mesh.VertexLayout);
        Assert.Equal(MeshIndexFormat.UInt32, mesh.IndexFormat);
        Assert.Equal(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1201"), mesh.KindId);
        Assert.Equal(1, mesh.SchemaVersion);
        Assert.NotEqual(default, mesh.Revision);
    }

    [Theory]
    [InlineData(1, 3, 1)] [InlineData(4, 12, 2)]
    [InlineData(65535, 196605, 7)] [InlineData(65536, 196608, 8)]
    public void Constructor_AcceptsSingletonInteriorAndInclusiveCountLimitsWithoutTruncation(int vertexCount, int indexCount, int slotCount)
    {
        var vertices = new Vector3[vertexCount]; vertices[^1] = Vector3.One;
        var indices = Enumerable.Repeat((uint)(vertexCount - 1), indexCount).ToArray();
        var slots = CreateSlots(indexCount, slotCount);

        var mesh = new MeshDefinition(vertices, indices, slots, new(Vector3.Zero, Vector3.One));

        Assert.Equal(vertexCount, mesh.Vertices.Length);
        Assert.Equal(Vector3.One, mesh.Vertices[^1]);
        Assert.Equal(indexCount, mesh.Indices.Length);
        Assert.Equal((uint)(vertexCount - 1), mesh.Indices[^1]);
        Assert.Equal(slotCount, mesh.Slots.Length);
        Assert.Equal((uint)indexCount, mesh.Slots[^1].FirstIndex + mesh.Slots[^1].IndexCount);
        Assert.Equal(slots.Select(slot => slot.SlotId), mesh.Slots.Select(slot => slot.SlotId));
    }

    [Theory]
    [InlineData(65537, 3, 1)] [InlineData(1, 196609, 1)]
    [InlineData(1, 196611, 1)] [InlineData(1, 27, 9)]
    public void Constructor_RejectsOverLimitCountsBeforeAccessingAnyInputItems(int vertices, int indices, int slots)
    {
        Assert.Equal(RenderContentError.LimitExceeded, Assert.Throws<RenderContentException>(() =>
            new MeshDefinition(new CountOnlyList<Vector3>(vertices), new CountOnlyList<uint>(indices),
                new CountOnlyList<MeshSlot>(slots), new(Vector3.Zero, Vector3.One))).Error);
    }

    [Theory]
    [InlineData("vertices")] [InlineData("indices")] [InlineData("slots")]
    public void Constructor_RejectsNullCollections(string kind)
    {
        Assert.Throws<ArgumentNullException>(() => new MeshDefinition(kind == "vertices" ? null! : [Vector3.Zero],
            kind == "indices" ? null! : [0u, 0u, 0u], kind == "slots" ? null! : [new(FirstSlot, 0, 3)], default));
    }

    [Theory]
    [InlineData("vertices")] [InlineData("indices")] [InlineData("slots")]
    [InlineData("one-index")] [InlineData("two-indices")] [InlineData("four-indices")]
    public void Constructor_RejectsEmptyAndIncompleteTriangleCollections(string kind)
    {
        Vector3[] vertices = kind == "vertices" ? [] : [Vector3.Zero];
        uint[] indices = kind switch { "indices" => [], "one-index" => [0], "two-indices" => [0, 0], "four-indices" => [0, 0, 0, 0], _ => [0, 0, 0] };
        MeshSlot[] slots = kind == "slots" ? [] : [new(FirstSlot, 0, 3)];
        Assert.Equal(RenderContentError.InvalidGeometry,
            Assert.Throws<RenderContentException>(() => new MeshDefinition(vertices, indices, slots, default)).Error);
    }

    [Theory]
    [InlineData("topology", 0)] [InlineData("topology", 2)] [InlineData("topology", -1)]
    [InlineData("vertex", 0)] [InlineData("vertex", 2)] [InlineData("vertex", -1)]
    [InlineData("index", 0)] [InlineData("index", 2)] [InlineData("index", -1)]
    [InlineData("schema", 0)] [InlineData("schema", 2)] [InlineData("schema", -1)]
    public void Constructor_RejectsEveryUnsupportedVersionAndLayout(string kind, int value)
    {
        var error = Assert.Throws<RenderContentException>(() => new MeshDefinition([Vector3.Zero], [0u, 0u, 0u], [new(FirstSlot, 0, 3)], default,
            schemaVersion: kind == "schema" ? value : 1,
            topology: kind == "topology" ? (MeshTopology)value : MeshTopology.TriangleList,
            vertexLayout: kind == "vertex" ? (MeshVertexLayout)value : MeshVertexLayout.PositionFloat3,
            indexFormat: kind == "index" ? (MeshIndexFormat)value : MeshIndexFormat.UInt32));
        Assert.Equal(kind == "schema" ? RenderContentError.UnsupportedVersion : RenderContentError.UnsupportedLayout, error.Error);
    }

    [Fact]
    public void Constructor_AcceptsCoordinateEndpointsConservativeAndFlatBounds()
    {
        Vector3[] positions = [new(-1000000), new(1000000)];
        var endpoints = new MeshDefinition(positions, [0u, 1u, 1u], [new(FirstSlot, 0, 3)], new(new(-1000000), new(1000000)));
        Assert.Equal(new Vector3(-1000000), endpoints.Vertices[0]);
        Assert.Equal(new Vector3(1000000), endpoints.Vertices[1]);
        var flat = new MeshDefinition([Vector3.Zero], [0u, 0u, 0u], [new(FirstSlot, 0, 3)], default);
        Assert.Equal(new RenderBounds(Vector3.Zero, Vector3.Zero), flat.Bounds);
        var conservative = new MeshDefinition([Vector3.Zero], [0u, 0u, 0u], [new(FirstSlot, 0, 3)], new(new(-1), Vector3.One));
        Assert.Equal(new RenderBounds(new(-1), Vector3.One), conservative.Bounds);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Constructor_RejectsAdjacentOutsideAndNonfiniteCoordinatesOnEachAxis(int axis)
    {
        foreach (var invalid in new[] { MathF.BitIncrement(1000000), MathF.BitDecrement(-1000000), float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var position = Vector3.Zero; position[axis] = invalid;
            Assert.Equal(RenderContentError.InvalidGeometry, Assert.Throws<RenderContentException>(() =>
                new MeshDefinition([Vector3.Zero, position], [0u, 0u, 1u], [new(FirstSlot, 0, 3)], new(new(-float.MaxValue), new(float.MaxValue)))).Error);
        }
    }

    [Theory]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)]
    public void Constructor_RejectsBoundsMissingAnyActualVertexExtremum(int axis, bool minimum)
    {
        var low = Vector3.Zero; var high = Vector3.One;
        if (minimum) low[axis] = .001f; else high[axis] = .999f;
        Assert.Equal(RenderContentError.InvalidBounds, Assert.Throws<RenderContentException>(() =>
            new MeshDefinition([Vector3.Zero, Vector3.One], [0u, 1u, 1u], [new(FirstSlot, 0, 3)], new(low, high))).Error);
    }

    [Theory]
    [InlineData("infinity")] [InlineData("nan")] [InlineData("unordered")]
    public void Constructor_ValidatesBoundsEvenWhenFiniteVerticesWouldOtherwiseFit(string kind)
    {
        var bounds = kind switch
        {
            "infinity" => new RenderBounds(new(float.NegativeInfinity), new(float.PositiveInfinity)),
            "nan" => new RenderBounds(new(float.NaN, -1, -1), Vector3.One),
            _ => new RenderBounds(Vector3.One, Vector3.Zero)
        };
        Assert.Equal(RenderContentError.InvalidBounds, Assert.Throws<RenderContentException>(() =>
            new MeshDefinition([Vector3.Zero], [0u, 0u, 0u], [new(FirstSlot, 0, 3)], bounds)).Error);
    }

    [Theory]
    [InlineData(0, 3u)] [InlineData(1, 3u)] [InlineData(2, 3u)] [InlineData(2, uint.MaxValue)]
    public void Constructor_RejectsAnOutOfRangeIndexAtEveryBufferPosition(int position, uint invalidIndex)
    {
        uint[] indices = [0, 1, 2]; indices[position] = invalidIndex;
        Assert.Equal(RenderContentError.InvalidGeometry, Assert.Throws<RenderContentException>(() =>
            new MeshDefinition([Vector3.Zero, Vector3.UnitX, Vector3.UnitY], indices, [new(FirstSlot, 0, 3)], new(Vector3.Zero, Vector3.One))).Error);
    }

    [Theory]
    [InlineData("empty-id")] [InlineData("duplicate-id")] [InlineData("first-gap")]
    [InlineData("gap")] [InlineData("overlap")] [InlineData("unordered")]
    [InlineData("empty-range")] [InlineData("incomplete-triangle")]
    [InlineData("unaligned-start")] [InlineData("range-beyond-buffer")]
    [InlineData("uncovered-tail")] [InlineData("huge-count")]
    public void Constructor_RejectsInvalidOrIncompleteOrderedSlotPartitions(string kind)
    {
        MeshSlot[] slots = [new(FirstSlot, 0, 3), new(SecondSlot, 3, 3)];
        switch (kind)
        {
            case "empty-id": slots[1] = slots[1] with { SlotId = Guid.Empty }; break;
            case "duplicate-id": slots[1] = slots[1] with { SlotId = FirstSlot }; break;
            case "first-gap": slots[0] = slots[0] with { FirstIndex = 3 }; break;
            case "gap": slots[1] = slots[1] with { FirstIndex = 6 }; break;
            case "overlap": slots[1] = slots[1] with { FirstIndex = 0 }; break;
            case "unordered": Array.Reverse(slots); break;
            case "empty-range": slots[1] = slots[1] with { IndexCount = 0 }; break;
            case "incomplete-triangle": slots[1] = slots[1] with { IndexCount = 2 }; break;
            case "unaligned-start": slots[1] = slots[1] with { FirstIndex = 4 }; break;
            case "range-beyond-buffer": slots[1] = slots[1] with { IndexCount = 6 }; break;
            case "uncovered-tail": slots = [new(FirstSlot, 0, 3)]; break;
            default: slots[1] = slots[1] with { IndexCount = uint.MaxValue }; break;
        }
        Assert.Equal(RenderContentError.InvalidGeometry, Assert.Throws<RenderContentException>(() =>
            new MeshDefinition([Vector3.Zero], [0u, 0u, 0u, 0u, 0u, 0u], slots, default)).Error);
    }

    [Fact]
    public void BuiltinCube_HasEightLiteralCornersAndTwelveOutwardCounterClockwiseTriangles()
    {
        var manifest = BuiltinRenderContent.Create(Guid.Parse("b30fe1ec-95de-43e9-b062-c30936e55c10"),
            RenderContentHash.Compute(new byte[] { 1, 2, 3 }), Guid.Parse("b30fe1ec-95de-43e9-b062-c30936e55c11"));
        var entry = manifest.Resolve(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1401"), RenderContentProfile.MeshKindId);
        var mesh = Assert.IsType<MeshDefinition>(entry.Definition);

        Vector3[] expectedCorners = [new(-.5f, -.5f, -.5f), new(.5f, -.5f, -.5f), new(.5f, .5f, -.5f), new(-.5f, .5f, -.5f),
            new(-.5f, -.5f, .5f), new(.5f, -.5f, .5f), new(.5f, .5f, .5f), new(-.5f, .5f, .5f)];
        Assert.Equal(8, mesh.Vertices.Length);
        Assert.True(expectedCorners.ToHashSet().SetEquals(mesh.Vertices));
        Assert.Equal(36, mesh.Indices.Length);
        Assert.Equal(new RenderBounds(new(-.5f), new(.5f)), mesh.Bounds);
        Assert.Equal(new MeshSlot(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1404"), 0, 36), Assert.Single(mesh.Slots));
        Vector3[] faceNormals = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
        var trianglesPerFace = faceNormals.ToDictionary(normal => normal, _ => 0);
        for (var triangle = 0; triangle < 12; triangle++)
        {
            var first = mesh.Indices[triangle * 3]; var second = mesh.Indices[triangle * 3 + 1]; var third = mesh.Indices[triangle * 3 + 2];
            Assert.InRange(first, 0u, 7u); Assert.InRange(second, 0u, 7u); Assert.InRange(third, 0u, 7u);
            var a = mesh.Vertices[(int)first]; var b = mesh.Vertices[(int)second]; var c = mesh.Vertices[(int)third];
            var normal = Vector3.Cross(b - a, c - a);
            Assert.Contains(normal, faceNormals);
            Assert.True(Vector3.Dot(normal, (a + b + c) / 3) > 0, "Each triangle must face out from the origin.");
            trianglesPerFace[normal]++;
        }
        Assert.All(trianglesPerFace.Values, count => Assert.Equal(2, count));
    }

    private static MeshSlot[] CreateSlots(int indexCount, int slotCount)
    {
        var result = new MeshSlot[slotCount]; uint cursor = 0;
        var triangles = indexCount / 3;
        for (var index = 0; index < slotCount; index++)
        {
            var count = (uint)((triangles / slotCount + (index < triangles % slotCount ? 1 : 0)) * 3);
            result[index] = new(new Guid($"b30fe1ec-95de-43e9-b062-{index + 1:000000000000}"), cursor, count);
            cursor += count;
        }
        return result;
    }

    private sealed class CountOnlyList<T>(int count) : IReadOnlyList<T>
    {
        public int Count => count;
        public T this[int index] => throw new InvalidOperationException("Over-limit input must not be read.");
        public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("Over-limit input must not be enumerated.");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
