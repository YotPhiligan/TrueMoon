using System.Numerics;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using Xunit;

namespace TrueMoon.Aether.Tests.Preview;

public sealed class PreviewGameSessionTests
{
    [Fact]
    public void Load_CopiesPrimitiveTransformsMaterialsAndKeepsEarlierRenderSnapshots()
    {
        var first = new RenderPrimitive(Matrix4x4.CreateScale(2, 3, 4) * Matrix4x4.CreateTranslation(5, -2, 1), new(.1f, .3f, .7f));
        var second = new RenderPrimitive(new(1, .25f, 0, 0, 0, 2, 0, 0, 0, 0, 3, 0, -4, 8, -2, 1), new(.8f, .2f, .4f));
        var source = ScenePrimitive.FromPrimitive(first);
        var entities = new List<SceneEntity>
        {
            new() { Id = "first", X = 99, Y = 98, Z = 97, Primitive = source },
            new() { Id = "marker", X = 15, Interactive = true },
            new() { Id = "second", Primitive = ScenePrimitive.FromPrimitive(second) }
        };
        var snapshot = new SceneSnapshot("authoring", 17, entities);
        using var game = new PreviewGameSession();
        game.Load(snapshot);
        var rendered = game.CaptureRenderScene();

        source.Transform[12] = -1000; source.Red = 1;
        entities.Clear();
        var copy = snapshot.CopyEntities(); copy[0].Primitive!.Transform[0] = 1000; copy[2].Primitive!.Blue = 1;
        for (var i = 0; i < 10; i++) game.Tick();

        Assert.Equal(new CameraTransform(0, 1, 6, 0, 0, 0), rendered.Camera);
        Assert.Equal(new[] { first, second }, rendered.Primitives.ToArray());
        Assert.Equal(17, game.Revision); Assert.Equal(0UL, game.TickNumber);
        Assert.Equal(0xff2080c0u, game.PatternColor);
        Assert.Same(rendered, game.CaptureRenderScene());
        Assert.Equal(first, snapshot.CopyEntities()[0].Primitive!.ToPrimitive());

        game.Load(new SceneSnapshot("replacement", 23, []));
        Assert.Equal(23, game.Revision); Assert.Equal(0UL, game.TickNumber);
        Assert.Empty(game.CaptureRenderScene().Primitives);
        Assert.NotSame(rendered, game.CaptureRenderScene());
        Assert.Equal(new[] { first, second }, rendered.Primitives.ToArray());
        rendered.Validate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Load_EmptyOrMarkerOnlySceneDrawsNoCompatibilityCube(bool markers)
    {
        using var game = new PreviewGameSession();
        Assert.Equal(0UL, game.TickNumber); Assert.Equal(0, game.Revision);
        Assert.Throws<InvalidOperationException>(() => game.CaptureRenderScene());
        game.Tick();
        game.Load(new("empty", 0, markers ? [new() { Id = "group", X = 5 }, new() { Id = "marker", Interactive = true }] : []));
        var rendered = game.CaptureRenderScene();
        Assert.Empty(rendered.Primitives);
        Assert.Equal(new CameraTransform(0, 1, 6, 0, 0, 0), rendered.Camera);
        Assert.Equal(0UL, game.TickNumber); Assert.Equal(0, game.Revision);
        rendered.Validate();
    }

    [Fact]
    public void Load_MaximumPrimitiveScenePreservesEveryOrderedPrimitive()
    {
        var expected = Enumerable.Range(0, PrimitiveScene.MaximumPrimitives)
            .Select(i => new RenderPrimitive(Matrix4x4.CreateTranslation(i, i * 2, -i), new(i / 64f, .5f, .25f))).ToArray();
        var scene = new SceneSnapshot("full", 91, expected.Select((primitive, i) => new SceneEntity
        { Id = "cube-" + i, Primitive = ScenePrimitive.FromPrimitive(primitive) }));
        using var game = new PreviewGameSession(); game.Load(scene);
        Assert.Equal(64, game.CaptureRenderScene().Primitives.Length);
        Assert.Equal(expected, game.CaptureRenderScene().Primitives.ToArray());
        Assert.Equal(91, game.Revision); Assert.Equal(0UL, game.TickNumber);
    }

    [Fact]
    public void Load_NullFailurePreservesLoadedSceneAndRevision()
    {
        using var game = new PreviewGameSession();
        Assert.Equal("scene", Assert.Throws<ArgumentNullException>(() => game.Load(null!)).ParamName);
        Assert.Throws<InvalidOperationException>(() => game.CaptureRenderScene());
        game.Load(new("valid", 8, [new() { Id = "cube", Primitive = ScenePrimitive.FromPrimitive(new(Matrix4x4.Identity, new(.2f, .4f, .6f))) }]));
        var before = game.CaptureRenderScene();
        Assert.Equal("scene", Assert.Throws<ArgumentNullException>(() => game.Load(null!)).ParamName);
        Assert.Equal(8, game.Revision); Assert.Equal(0UL, game.TickNumber);
        Assert.Same(before, game.CaptureRenderScene());
        Assert.Single(before.Primitives);
    }

    [Fact]
    public void Dispose_ClosesConsumerButPreviouslyCapturedImmutableSceneStaysValid()
    {
        var game = new PreviewGameSession();
        var primitive = new RenderPrimitive(Matrix4x4.CreateTranslation(2, 3, 4), new(.2f, .4f, .6f));
        game.Load(new("valid", 9, [new() { Id = "cube", Primitive = ScenePrimitive.FromPrimitive(primitive) }]));
        var rendered = game.CaptureRenderScene();
        game.Dispose(); game.Dispose();
        Assert.Throws<ObjectDisposedException>(() => game.Load(new("closed", 10, [])));
        Assert.Throws<ObjectDisposedException>(game.Tick);
        Assert.Throws<ObjectDisposedException>(() => game.CaptureRenderScene());
        Assert.Equal(primitive, Assert.Single(rendered.Primitives));
        Assert.Equal(new CameraTransform(0, 1, 6, 0, 0, 0), rendered.Camera);
        rendered.Validate();
    }

    [Fact]
    public async Task Protocol_EditPreviewRejectsGameplayWithoutConsumingSequenceAndNeverAdvancesTick()
    {
        var identity = Identity(); using var game = new PreviewGameSession();
        var protocol = new RuntimeProtocol(identity, game, 47, role: RuntimeRole.EditPreview);
        Assert.Throws<InvalidOperationException>(() => protocol.CaptureRenderFrame());
        await Load(protocol, identity);
        var before = protocol.CaptureRenderFrame();
        foreach (var kind in new[] { CommandKind.Step, CommandKind.SetInput, CommandKind.SetMode })
        {
            var denied = ControlRequest.Create(identity, 3, kind); denied.Playing = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => protocol.SendAsync(denied));
            Assert.Equal(0UL, game.TickNumber); Assert.Equal(12, game.Revision);
            Assert.Same(before.Scene, protocol.CaptureRenderFrame().Scene);
        }
        var paused = await protocol.SendAsync(ControlRequest.Create(identity, 3, CommandKind.SetMode));
        Assert.Equal("Paused", paused.Phase); Assert.Equal(3, paused.Stamp.Sequence);
        var checkpoint = await protocol.SendAsync(ControlRequest.Create(identity, 4, CommandKind.CaptureCheckpoint));
        Assert.Equal((int)CheckpointOutcome.Unsupported, checkpoint.CheckpointOutcome); Assert.Null(checkpoint.Checkpoint);
        await Assert.ThrowsAsync<InvalidOperationException>(() => protocol.SendAsync(ControlRequest.Create(identity, 5, CommandKind.RestoreCheckpoint)));
        for (var i = 0; i < 10; i++) protocol.FixedTick();
        var status = await protocol.SendAsync(ControlRequest.Create(identity, 5, CommandKind.GetStatus));
        Assert.Equal("Paused", status.Phase); Assert.Equal(0, status.Tick); Assert.Equal(12, status.Revision);
        Assert.Equal(0UL, protocol.CaptureRenderFrame().Tick); Assert.Same(before.Scene, protocol.CaptureRenderFrame().Scene);
        var stopped = await protocol.SendAsync(ControlRequest.Create(identity, 6, CommandKind.Stop));
        Assert.Equal("Stopping", stopped.Phase); Assert.Equal(0, stopped.Tick);
        Assert.Throws<InvalidOperationException>(() => protocol.CaptureRenderFrame());
    }

    [Fact]
    public async Task Protocol_DefaultGameRoleRetainsStepAndPlayBehavior()
    {
        var identity = Identity(); using var game = new CountingGame();
        var protocol = new RuntimeProtocol(identity, game, 48);
        var launch = new SessionLaunch(identity, "engine-default", "reports");
        Assert.Equal(RuntimeRole.Game, launch.Role);
        await Load(protocol, identity);
        await protocol.SendAsync(ControlRequest.Create(identity, 3, CommandKind.Step));
        Assert.Equal(1UL, game.TickNumber);
        var play = ControlRequest.Create(identity, 4, CommandKind.SetMode); play.Playing = true;
        Assert.Equal("Playing", (await protocol.SendAsync(play)).Phase);
        protocol.FixedTick(); protocol.FixedTick();
        Assert.Equal(3UL, game.TickNumber);
        await Assert.ThrowsAsync<InvalidOperationException>(() => protocol.SendAsync(ControlRequest.Create(identity, 5, CommandKind.Step)));
        await protocol.SendAsync(ControlRequest.Create(identity, 5, CommandKind.SetMode));
        protocol.FixedTick(); Assert.Equal(3UL, game.TickNumber);
        await protocol.SendAsync(ControlRequest.Create(identity, 6, CommandKind.Step));
        Assert.Equal(4UL, game.TickNumber); Assert.Equal(12, game.Revision);
    }

    [Fact]
    public async Task Protocol_InvalidPrimitiveLoadRemainsUnreadyAndRetryUsesSameSequence()
    {
        var identity = Identity(); using var game = new PreviewGameSession();
        var protocol = new RuntimeProtocol(identity, game, 49, role: RuntimeRole.EditPreview);
        await protocol.SendAsync(ControlRequest.Create(identity, 1, CommandKind.Hello));
        var load = ControlRequest.Create(identity, 2, CommandKind.LoadScene);
        load.SceneId = "invalid-scene"; load.Revision = 99;
        load.Entities = [new() { Id = "invalid", Primitive = new() { Transform = [1, 2, 3], Red = .5f } }];
        await Assert.ThrowsAsync<InvalidDataException>(() => protocol.SendAsync(load));
        Assert.Equal(0, game.Revision); Assert.Equal(0UL, game.TickNumber);
        Assert.Throws<InvalidOperationException>(() => game.CaptureRenderScene());
        Assert.Throws<InvalidOperationException>(() => protocol.CaptureRenderFrame());

        var primitive = new RenderPrimitive(Matrix4x4.CreateTranslation(1, 2, 3), new(.2f, .4f, .6f));
        load.SceneId = "valid-scene"; load.Revision = 12;
        load.Entities = [new() { Id = "valid", Primitive = ScenePrimitive.FromPrimitive(primitive) }];
        var reply = await protocol.SendAsync(load);
        Assert.Equal(2, reply.Stamp.Sequence); Assert.Equal("Paused", reply.Phase); Assert.Equal(12, reply.Revision);
        var before = protocol.CaptureRenderFrame(); Assert.Equal(primitive, Assert.Single(before.Scene.Primitives));
        load.Sequence = 3; load.Revision = 55;
        await Assert.ThrowsAsync<InvalidOperationException>(() => protocol.SendAsync(load));
        var status = await protocol.SendAsync(ControlRequest.Create(identity, 3, CommandKind.GetStatus));
        Assert.Equal(12, status.Revision); Assert.Equal(0, status.Tick);
        Assert.Same(before.Scene, protocol.CaptureRenderFrame().Scene);
    }

    private static SessionIdentity Identity() => new(Guid.NewGuid(), 1, new('A', 64), new('B', 64), new('C', 64));
    private static async Task Load(RuntimeProtocol protocol, SessionIdentity identity)
    {
        await protocol.SendAsync(ControlRequest.Create(identity, 1, CommandKind.Hello));
        var load = ControlRequest.Create(identity, 2, CommandKind.LoadScene);
        load.SceneId = "static-scene"; load.Revision = 12;
        load.Entities = [new() { Id = "cube", Primitive = ScenePrimitive.FromPrimitive(new(Matrix4x4.CreateTranslation(1, 2, 3), new(.2f, .4f, .6f))) }];
        var reply = await protocol.SendAsync(load);
        Assert.Equal("Paused", reply.Phase); Assert.Equal(12, reply.Revision); Assert.Equal(0, reply.Tick);
    }

    private sealed class CountingGame : IGameSession
    {
        public ulong TickNumber { get; private set; }
        public long Revision { get; private set; }
        public uint PatternColor => 0;
        public void Load(SceneSnapshot scene) => Revision = scene.Revision;
        public void Tick() => TickNumber++;
        public void Dispose() { }
    }
}
