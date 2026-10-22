using System.Numerics;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Interactions;
using TrueMoon.Aether.Rendering;
using Xunit;

namespace TrueMoon.Aether.Tests.Interactions;

public sealed class PrimitiveRaycastTests
{
    private static readonly CameraTransform Camera = new(0, 0, 0, 0, 0, 0);
    private static InteractionBody Box(string id, float z, bool interactive = true) => new(id, new(Matrix4x4.CreateTranslation(0, 0, z), Vector3.One), interactive);

    [Fact]
    public void NearestSurface_WinsRegardlessOfEnumerationAndOpaqueDisabledBodyBlocksTarget()
    {
        var far = Box("target", -3); var near = Box("blocker", -1, false);
        foreach (var bodies in new[] { new[] { far, near }, new[] { near, far } })
        {
            var hit = PrimitiveRaycast.Cast(bodies, Camera, 4)!.Value;
            Assert.Equal("blocker", hit.EntityId); Assert.Equal(.5f, hit.Distance); Assert.False(hit.Interactive);
        }
        Assert.Equal("target", PrimitiveRaycast.Cast([far], Camera, 4)!.Value.EntityId);
    }

    [Fact]
    public void ExactReach_IsInclusiveWithAdjacentFloatingPointBoundaries()
    {
        var body = Box("edge", -4.5f);
        Assert.Null(PrimitiveRaycast.Cast([body], Camera, MathF.BitDecrement(4)));
        Assert.Equal(4, PrimitiveRaycast.Cast([body], Camera, 4)!.Value.Distance);
        Assert.Equal("edge", PrimitiveRaycast.Cast([body], Camera, MathF.BitIncrement(4))!.Value.EntityId);
    }

    [Theory]
    [InlineData(0)] [InlineData(.5f)]
    public void InsideOrOnSurface_HitsAtZeroAndDoesNotSeeThroughBody(float x)
    {
        var hit = PrimitiveRaycast.Cast([Box("inside", 0), Box("behind", -2)], Camera with { X = x }, 4)!.Value;
        Assert.Equal("inside", hit.EntityId); Assert.Equal(0, hit.Distance);
    }

    [Fact]
    public void EmptyMissParallelAndBehind_DoNotInventTargets()
    {
        Assert.Null(PrimitiveRaycast.Cast([], Camera, 4));
        Assert.Null(PrimitiveRaycast.Cast([Box("parallel", -2)], Camera with { X = .501f }, 4));
        Assert.Null(PrimitiveRaycast.Cast([Box("behind", 2)], Camera, 4));
        Assert.Equal(1.5f, PrimitiveRaycast.Cast([Box("edge", -2)], Camera with { X = .5f }, 4)!.Value.Distance);
        Assert.Equal("behind", PrimitiveRaycast.Cast([Box("behind", 2)], Camera with { Yaw = MathF.PI }, 4)!.Value.EntityId);
    }

    [Fact]
    public void AffineBoxes_KeepWorldDistanceAndRejectFalsePositiveWorldAabbHits()
    {
        var scaled = new InteractionBody("scaled", new(Matrix4x4.CreateScale(2, 1, 4), Vector3.One), true);
        Assert.Equal(3, PrimitiveRaycast.Cast([scaled], Camera with { Z = 5 }, 4)!.Value.Distance);
        var rotated = scaled with { Primitive = new(Matrix4x4.CreateRotationY(MathF.PI / 4), Vector3.One) };
        Assert.Equal(3 - MathF.Sqrt(.5f), PrimitiveRaycast.Cast([rotated], Camera with { Z = 3 }, 4)!.Value.Distance, 5);
        var shear = Matrix4x4.Identity; shear.M21 = 1;
        var sheared = scaled with { Primitive = new(shear, Vector3.One) };
        Assert.Equal(2.5f, PrimitiveRaycast.Cast([sheared], Camera with { X = .7f, Y = .4f, Z = 3 }, 4)!.Value.Distance);
        Assert.Null(PrimitiveRaycast.Cast([sheared], Camera with { X = .9f, Y = -.4f, Z = 3 }, 4));
    }

    [Fact]
    public void NearParallelAndPitchedRays_KeepSmallNonzeroDirectionAndCameraOrientation()
    {
        Assert.Equal(2.5f, PrimitiveRaycast.Cast([Box("thin-edge", 0)], Camera with { X = .5001f, Z = 3, Yaw = .0001f }, 4)!.Value.Distance, 4);
        var raised = new InteractionBody("raised", new(Matrix4x4.CreateTranslation(0, 2, -2), Vector3.One), true);
        var hit = PrimitiveRaycast.Cast([raised], Camera with { Pitch = MathF.PI / 4 }, 4)!.Value;
        Assert.Equal("raised", hit.EntityId); Assert.Equal(1.5f * MathF.Sqrt(2), hit.Distance, 4);
        var tiny = raised with { Primitive = new(Matrix4x4.CreateScale(float.Epsilon, 1, 1), Vector3.One) };
        Assert.Throws<InvalidDataException>(() => PrimitiveRaycast.Cast([tiny], Camera, 4));
    }

    [Fact]
    public void Ties_PreferBlockerThenOrdinalIdIndependentlyOfInputOrder()
    {
        var a = Box("a", -2); var b = Box("b", -2);
        Assert.Equal("a", PrimitiveRaycast.Cast([b, a], Camera, 4)!.Value.EntityId);
        Assert.Equal("a", PrimitiveRaycast.Cast([a, b], Camera, 4)!.Value.EntityId);
        b = b with { Interactive = false };
        Assert.Equal("b", PrimitiveRaycast.Cast([a, b], Camera, 4)!.Value.EntityId);
        Assert.Equal("b", PrimitiveRaycast.Cast([b, a], Camera, 4)!.Value.EntityId);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)] [InlineData(1000.01f)]
    public void InvalidReach_IsRejected(float reach) => Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveRaycast.Cast([], Camera, reach));

    [Fact]
    public void InvalidCameraBodiesAndDuplicateIds_AreRejectedEvenBeyondReach()
    {
        Assert.Throws<InvalidDataException>(() => PrimitiveRaycast.Cast([], Camera with { X = float.NaN }, 4));
        Assert.Throws<ArgumentNullException>(() => PrimitiveRaycast.Cast(null!, Camera, 4));
        Assert.Throws<InvalidDataException>(() => PrimitiveRaycast.Cast([Box("same", -2), Box("same", -20)], Camera, 4));
        Assert.Throws<InvalidDataException>(() => PrimitiveRaycast.Cast([new("bad", default, true)], Camera, 4));
        Assert.Throws<InvalidDataException>(() => PrimitiveRaycast.Cast([Box(" ", -20)], Camera, 4));
        Assert.NotNull(PrimitiveRaycast.Cast([Box("valid", -1000)], Camera, 1000));
    }

    [Theory]
    [InlineData(64)] [InlineData(65)]
    public void BodyCount_IsBounded(int count)
    {
        var bodies = Enumerable.Range(0, count).Select(i => Box(i.ToString("D2"), -2));
        if (count == 65) Assert.Throws<InvalidDataException>(() => PrimitiveRaycast.Cast(bodies, Camera, 4));
        else Assert.Equal("00", PrimitiveRaycast.Cast(bodies, Camera, 4)!.Value.EntityId);
    }
}
