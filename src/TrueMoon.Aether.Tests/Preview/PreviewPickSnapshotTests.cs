using System.Numerics;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;
using Xunit;
using BuiltInScene = TrueMoon.Aether.Tests.Scenes.BuiltInSceneTests;

namespace TrueMoon.Aether.Tests.Preview;

public sealed class PreviewPickSnapshotTests
{
    private static readonly Guid SceneGuid = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid First = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid Second = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Ray3 Ray = new(new(0, 0, 6), -Vector3.UnitZ, 100);

    [Fact]
    public void Capture_FreezesExactProjectedGeometryIdentityAndOrderWithoutInteractionFilter()
    {
        var group = new SceneEntity { Id = Guid.NewGuid().ToString("D") };
        var cube = Cube(First, Matrix4x4.CreateScale(2, 1, 3)); cube.Interactive = false;
        var entities = new List<SceneEntity> { group, cube };
        var source = new SceneSnapshot(SceneGuid.ToString("D"), 7, entities);
        var snapshot = Capture(source);
        cube.Id = Second.ToString("D"); cube.Primitive!.Transform[0] = 500; entities.Clear();
        var copy = source.CopyEntities(); copy[1].Primitive!.Transform[10] = 1000; copy.Clear();
        var primitive = Assert.Single(snapshot.Primitives);
        Assert.Equal(new SceneId(SceneGuid), snapshot.Scene); Assert.Equal(7, snapshot.Revision);
        Assert.Equal(1, primitive.DrawOrder); Assert.Equal(new EntityId(First), primitive.Entity);
        Assert.Equal(Matrix4x4.CreateScale(2, 1, 3), primitive.World);
        var localMatrix = primitive.World; localMatrix.M33 = 999;
        var result = snapshot.Pick(Ray);
        Assert.Equal(PreviewPickKind.Hit, result.Kind); Assert.Equal(new EntityId(First), result.Entity); Assert.Equal(4.5, result.Distance!.Value, 6);
        Assert.Same(snapshot, result.Snapshot); Assert.Equal(PreviewPickUnavailableReason.None, result.UnavailableReason);
        Assert.Equal(3f, snapshot.Primitives[0].World.M33);
    }

    [Fact]
    public void Pick_NearestSurfaceWinsEvenWhenDrawnLater()
    {
        var snapshot = Capture(Scene(Cube(First, Matrix4x4.CreateTranslation(0, 0, -2)), Cube(Second, Matrix4x4.Identity)));
        var result = snapshot.Pick(Ray);
        Assert.Equal(PreviewPickKind.Hit, result.Kind); Assert.Equal(new EntityId(Second), result.Entity); Assert.Equal(5.5, result.Distance);
    }

    [Fact]
    public void Pick_ExactTiesFollowFrozenDrawOrderRatherThanGuidSort()
    {
        var snapshot = Capture(Scene(Cube(First, Matrix4x4.Identity), Cube(Second, Matrix4x4.Identity)));
        Assert.Equal(new EntityId(First), snapshot.Pick(Ray).Entity);
        var reversed = Capture(Scene(Cube(Second, Matrix4x4.Identity), Cube(First, Matrix4x4.Identity)));
        Assert.Equal(new EntityId(Second), reversed.Pick(Ray).Entity);
        Assert.Equal(5.5, reversed.Pick(Ray).Distance);
    }

    [Fact]
    public void Pick_EmptyOrBackgroundIsMissButInvalidRayIsUnavailable()
    {
        foreach (var source in new[] { Scene(), Scene(Cube(First, Matrix4x4.CreateTranslation(20, 0, 0))) })
        {
            var snapshot = Capture(source); var miss = snapshot.Pick(Ray);
            Assert.Equal(PreviewPickKind.Miss, miss.Kind); Assert.Null(miss.Entity); Assert.Null(miss.Distance);
            Assert.Same(snapshot, miss.Snapshot); Assert.Equal(PreviewPickUnavailableReason.None, miss.UnavailableReason);
            var invalid = snapshot.Pick(default(Ray3));
            Assert.Equal(PreviewPickKind.Unavailable, invalid.Kind); Assert.Equal(PreviewPickUnavailableReason.InvalidMath, invalid.UnavailableReason);
        }
        Assert.Equal(PreviewPickKind.Unavailable, default(PreviewPickResult).Kind);
        Assert.Equal(PreviewPickUnavailableReason.InvalidMath, default(PreviewPickResult).UnavailableReason);
    }

    [Theory]
    [InlineData("mesh/1")] [InlineData("")] [InlineData("EDITOR.OPAQUE-CUBES/1")]
    public void Pick_UnknownVisibleProfileNeverSelectsCubeBehindUnsupportedOccluder(string profile)
    {
        var snapshot = PreviewPickSnapshot.Capture(Scene(Cube(First, Matrix4x4.Identity)), profile);
        var result = snapshot.Pick(Ray);
        Assert.Equal(PreviewPickKind.Unavailable, result.Kind); Assert.Equal(PreviewPickUnavailableReason.UnsupportedGeometry, result.UnavailableReason);
        Assert.Null(result.Entity); Assert.Null(result.Distance); Assert.Same(snapshot, result.Snapshot);
        Assert.Equal(profile, snapshot.GeometryProfile);
    }

    [Fact]
    public void Pick_InvalidInverseDisablesWholeFrameRatherThanSelectingAnotherCube()
    {
        var source = Scene(Cube(First, Matrix4x4.CreateScale(float.Epsilon, 1, 1)), Cube(Second, Matrix4x4.Identity));
        var result = Capture(source).Pick(Ray);
        Assert.Equal(PreviewPickKind.Unavailable, result.Kind); Assert.Equal(PreviewPickUnavailableReason.InvalidMath, result.UnavailableReason);
        Assert.Null(result.Entity); Assert.Null(result.Distance);
    }

    [Fact]
    public void Pick_PixelCenterImageQuerySeparatesOutsideImageFromValidBackground()
    {
        var projection = new CameraProjection(new(MathF.PI / 2, 1, 11), Matrix4x4.CreateTranslation(0, 0, 6), 321, 161);
        var image = new ViewportImageBounds(16, 14, 642, 322);
        var snapshot = Capture(Scene(Cube(First, Matrix4x4.Identity)));
        var hit = snapshot.Pick(projection, image, 337, 175);
        Assert.Equal(PreviewPickKind.Hit, hit.Kind); Assert.Equal(new EntityId(First), hit.Entity); Assert.Equal(4.5, hit.Distance!.Value, 5);
        Assert.Equal(PreviewPickKind.Miss, snapshot.Pick(projection, image, 16, 14).Kind);
        foreach (var point in new[] { (658d, 175d), (337d, 336d), (15d, 175d), (337d, 13d) })
        {
            var result = snapshot.Pick(projection, image, point.Item1, point.Item2);
            Assert.Equal(PreviewPickKind.Unavailable, result.Kind); Assert.Equal(PreviewPickUnavailableReason.OutsideImage, result.UnavailableReason);
        }
    }

    [Fact]
    public void Pick_UnprojectableFarEndpointIsUnavailableEvenForEmptyScene()
    {
        var projection = new CameraProjection(new(MathF.PI / 2, 1, 1e20f), Matrix4x4.Identity, 321, 161);
        var snapshot = Capture(Scene());
        var result = snapshot.Pick(projection, new(0, 0, 321, 161), 160, 80);
        Assert.Equal(PreviewPickKind.Unavailable, result.Kind); Assert.Equal(PreviewPickUnavailableReason.InvalidMath, result.UnavailableReason);
        Assert.Null(result.Entity); Assert.Null(result.Distance); Assert.Same(snapshot, result.Snapshot);
    }

    [Fact]
    public void Capture_UsesExactAuthoringHierarchyProjectionSharedWithPreviewRenderer()
    {
        var parent = BuiltInScene.Definition(transform: new() { ["sx"] = 2, ["sz"] = 3 });
        var child = BuiltInScene.Definition(2, new() { ["yaw"] = Math.PI / 4, ["z"] = -2 });
        var source = BuiltInScene.Project([BuiltInScene.Entity(2, 2, 1), BuiltInScene.Entity(1)], [child, parent]);
        var snapshot = Capture(source);
        using var runtime = new PreviewGameSession(); runtime.Load(source);
        var rendered = runtime.CaptureRenderScene();
        Assert.Equal(rendered.Primitives.Select(p => p.World), snapshot.Primitives.Select(p => p.World));
        var childGeometry = snapshot.Primitives[1];
        Assert.Equal(BuiltInScene.E(2), childGeometry.Entity);
        Assert.InRange(Vector3.Dot(Vector3.TransformNormal(Vector3.UnitX, childGeometry.World), Vector3.TransformNormal(Vector3.UnitZ, childGeometry.World)), -2.50001, -2.49999);
        // Start past the parent cube: only the child is in the near-to-far segment.
        var result = snapshot.Pick(new Ray3(new(0, 0, -2), -Vector3.UnitZ, 20));
        Assert.Equal(PreviewPickKind.Hit, result.Kind); Assert.Equal(childGeometry.Entity, result.Entity);
        Assert.InRange(result.Distance!.Value, 4 - 3 * Math.Sqrt(.5) - .00001, 4 - 3 * Math.Sqrt(.5) + .00001);
    }

    [Fact]
    public void Capture_RejectsNonpersistentEmptyAndDuplicateNormalizedIds()
    {
        Assert.Throws<ArgumentException>(() => Capture(new("legacy", 0, [])));
        Assert.Throws<ArgumentException>(() => Capture(new(Guid.Empty.ToString("D"), 0, [])));
        Assert.Throws<ArgumentException>(() => Capture(Scene(new SceneEntity { Id = "legacy" })));
        Assert.Throws<ArgumentException>(() => Capture(Scene(new SceneEntity { Id = Guid.Empty.ToString("D") })));
        var a = Cube(First, Matrix4x4.Identity); var b = Cube(First, Matrix4x4.Identity); b.Id = First.ToString("B");
        Assert.Throws<ArgumentException>(() => Capture(Scene(a, b)));
        Assert.Throws<ArgumentNullException>(() => PreviewPickSnapshot.Capture(null!, PreviewPickSnapshot.CubeGeometryProfile));
        Assert.Throws<ArgumentNullException>(() => PreviewPickSnapshot.Capture(Scene(), null!));
    }

    [Fact]
    public void Capture_RetainsAll64ProjectedEntriesAndCanHitLast()
    {
        var cubes = Enumerable.Range(1, 64).Select(i => Cube(new Guid(i, 0, 0, new byte[8]), Matrix4x4.CreateTranslation(i == 64 ? 0 : i + 10, 0, 0))).ToArray();
        var snapshot = Capture(Scene(cubes));
        Assert.Equal(64, snapshot.Primitives.Length);
        Assert.Equal(63, snapshot.Primitives[63].DrawOrder);
        Assert.Equal(snapshot.Primitives[63].Entity, snapshot.Pick(Ray).Entity);
        Assert.Equal(5.5, snapshot.Pick(Ray).Distance);
    }

    private static SceneEntity Cube(Guid id, Matrix4x4 world) => new() { Id = id.ToString("D"), Primitive = ScenePrimitive.FromPrimitive(new(world, Vector3.One)) };
    private static SceneSnapshot Scene(params SceneEntity[] entities) => new(SceneGuid.ToString("D"), 7, entities);
    private static PreviewPickSnapshot Capture(SceneSnapshot scene) => PreviewPickSnapshot.Capture(scene, PreviewPickSnapshot.CubeGeometryProfile);
}
