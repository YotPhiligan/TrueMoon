using System.Numerics;
using Game.Logic;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Gpu.Surfaces;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering;

public sealed class PrimitiveSceneTests
{
    private static PrimitiveScene Scene => new(new(0, 1, 3, 0, 0, 0), 0, new(.2f, .5f, .8f));
    private static GpuFrameStamp Stamp => new(new(Guid.NewGuid(), 2, new('A', 64), new('B', 64), new('C', 64)), 3, 2, 1, 2);
    private static Vector3 Project(Vector3 position, Matrix4x4 matrix)
    { var clip = Vector4.Transform(new Vector4(position, 1), matrix); return new(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W); }

    [Fact]
    public void Transform_ProjectsCenterUpAspectAndVulkanNearFarPlanes()
    {
        var scene = Scene; var transform = scene.CreateTransform(640, 320);
        var center = Project(Vector3.Zero, transform);
        Assert.Equal(0, center.X, 5); Assert.Equal(0, center.Y, 5); Assert.InRange(center.Z, 0, 1);
        Assert.True(Project(Vector3.UnitY, transform).Y < 0);
        Assert.Equal(0, Project(new(0, 0, 3 - scene.Near), transform).Z, 4);
        Assert.Equal(1, Project(new(0, 0, 3 - scene.Far), transform).Z, 4);
        var wide = Project(new(.25f, 0, 0), transform);
        var square = Project(new(.25f, 0, 0), scene.CreateTransform(320, 320));
        Assert.Equal(square.X / 2, wide.X, 5);
    }

    [Fact]
    public void Transform_UsesCameraPoseAndCubeRotation()
    {
        var moved = Scene with { Camera = new(.5f, 1, 3, 0, 0, 0) };
        Assert.True(Project(Vector3.Zero, moved.CreateTransform(320, 240)).X < -.1f);
        var turned = Scene with { Camera = new(0, 1, 3, 0, MathF.PI / 2, 0) };
        Assert.Equal(0, Vector4.Transform(Vector4.UnitW, turned.CreateTransform(320, 240)).W, 5);
        var rotated = Scene with { Rotation = MathF.PI / 2 };
        Assert.True(Project(new(.5f, 0, 0), Scene.CreateTransform(320, 240)).X > .2f);
        Assert.Equal(0, Project(new(.5f, 0, 0), rotated.CreateTransform(320, 240)).X, 5);
    }

    [Theory]
    [InlineData("camera")] [InlineData("rotation")] [InlineData("color-nan")] [InlineData("color-low")]
    [InlineData("color-high")] [InlineData("fov-zero")] [InlineData("fov-pi")] [InlineData("fov-nan")]
    [InlineData("near-zero")] [InlineData("near-negative")] [InlineData("far-equal")] [InlineData("far-less")] [InlineData("far-infinite")]
    public void Validate_RejectsInvalidCameraProjectionOrMaterial(string kind)
    {
        var scene = kind switch
        {
            "camera" => Scene with { Camera = new(float.NaN, 1, 3, 0, 0, 0) }, "rotation" => Scene with { Rotation = float.PositiveInfinity },
            "color-nan" => Scene with { Color = new(0, float.NaN, 1) }, "color-low" => Scene with { Color = new(-.001f, 0, 1) },
            "color-high" => Scene with { Color = new(0, 0, 1.001f) }, "fov-zero" => Scene with { FieldOfView = 0 },
            "fov-pi" => Scene with { FieldOfView = MathF.PI }, "fov-nan" => Scene with { FieldOfView = float.NaN },
            "near-zero" => Scene with { Near = 0 }, "near-negative" => Scene with { Near = -.1f },
            "far-equal" => Scene with { Far = Scene.Near }, "far-less" => Scene with { Far = .01f },
            _ => Scene with { Far = float.PositiveInfinity }
        };
        Assert.Throws<InvalidDataException>(scene.Validate);
    }

    [Fact]
    public void Transform_RejectsSuspendedExtentAndUnrepresentableCamera()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Scene.CreateTransform(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Scene.CreateTransform(16, 0));
        Assert.Throws<InvalidDataException>(() => (Scene with { Camera = new(float.MaxValue, 1, float.MaxValue, 0, 0, 0) }).CreateTransform(320, 240));
    }

    [Fact]
    public void SceneFrame_ProducesExactMetadataWithoutCpuPixelsAndRejectsMixedWorkloads()
    {
        var stamp = Stamp; var data = GpuFrameData.FromScene(stamp, 123, Scene);
        data.Validate(stamp, 320, 240);
        Assert.Empty(data.Pixels); Assert.Equal(5UL, data.Metadata.FrameId); Assert.Equal(123UL, data.Metadata.Tick); Assert.Equal(1, data.Metadata.Slot);
        Assert.True(data.Metadata.ProducedTimestamp > 0);
        Assert.Throws<InvalidDataException>(() => (data with { Pixels = new byte[320 * 240 * 4] }).Validate(stamp, 320, 240));
        Assert.Throws<InvalidDataException>(() => (data with { Scene = null }).Validate(stamp, 320, 240));
        Assert.Throws<InvalidDataException>(() => data.Validate(stamp with { Slot = 2 }, 320, 240));
        Assert.Throws<ArgumentNullException>(() => GpuFrameData.FromScene(stamp, 0, null!));
        Assert.Throws<ArgumentNullException>(() => GpuFrameData.FromScene(null!, 0, Scene));
    }

    [Fact]
    public async Task RenderCapture_RequiresReadinessCapturesCheckpointCameraAndRejectsStop()
    {
        var identity = Stamp.Identity; using var game = new ProbeGame(); var protocol = new RuntimeProtocol(identity, game, 17);
        Assert.Throws<InvalidOperationException>(() => protocol.CaptureRenderFrame());
        await Load(protocol, identity);
        var before = protocol.CaptureRenderFrame();
        Assert.Equal(0UL, before.Tick); Assert.Equal(new CameraTransform(0, 1, 3, 0, 0, 0), before.Scene.Camera);
        await protocol.SendAsync(ControlRequest.Create(identity, 3, CommandKind.Step));
        var after = protocol.CaptureRenderFrame();
        Assert.Equal(1UL, after.Tick); Assert.Equal(.125f, after.Scene.Camera.X); Assert.Equal(.01f, after.Scene.Camera.Yaw);
        Assert.Equal(1UL, game.TickNumber); Assert.Equal(game.CaptureCheckpoint().Camera, after.Scene.Camera);
        await protocol.SendAsync(ControlRequest.Create(identity, 4, CommandKind.Stop));
        Assert.Throws<InvalidOperationException>(() => protocol.CaptureRenderFrame());
    }

    [Fact]
    public async Task RenderCapture_RejectsPendingRestoreAndUnsupportedCapability()
    {
        var identity = Stamp.Identity; using var game = new ProbeGame();
        var pending = new RuntimeProtocol(identity, game, 17, requireRestore: true); await Load(pending, identity);
        Assert.Throws<InvalidOperationException>(() => pending.CaptureRenderFrame());
        using var plain = new PlainGame(); var unsupported = new RuntimeProtocol(identity, plain, 18); await Load(unsupported, identity);
        Assert.Throws<NotSupportedException>(() => unsupported.CaptureRenderFrame());
        Assert.Equal(0UL, plain.TickNumber);
    }

    private static async Task Load(RuntimeProtocol protocol, SessionIdentity identity)
    {
        await protocol.SendAsync(ControlRequest.Create(identity, 1, CommandKind.Hello));
        var load = ControlRequest.Create(identity, 2, CommandKind.LoadScene); load.SceneId = "room"; load.Revision = 7; load.Entities = [];
        await protocol.SendAsync(load);
    }
    private sealed class PlainGame : IGameSession
    {
        public ulong TickNumber => 0; public long Revision => 7; public uint PatternColor => 0xff2080c0;
        public void Load(SceneSnapshot scene) { } public void Tick() { } public void Dispose() { }
    }
}
