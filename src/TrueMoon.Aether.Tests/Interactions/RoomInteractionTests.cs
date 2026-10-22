using System.Numerics;
using Game.Logic;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Input;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using Xunit;

namespace TrueMoon.Aether.Tests.Interactions;

public sealed class RoomInteractionTests
{
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private static SceneEntity Box(string id, float x, float z, bool interactive = true) => new()
    { Id = id, Interactive = interactive, Primitive = ScenePrimitive.FromPrimitive(new(Matrix4x4.CreateTranslation(x, 0, z), new(.2f, .4f, .8f))) };
    private static SceneSnapshot Scene => new("room", 7, [Box("near", 0, 0), Box("far", 0, -2), Box("side", 2, 0)]);
    private static void Pose(RoomGame game, float x = 0, float z = 3) => game.RestoreCheckpoint(game.CaptureCheckpoint() with { Camera = new(x, 0, z, 0, 0, 0) });

    [Fact]
    public void Interact_ChangesOnlyNearestTargetMaterialAndKeepsIndependentObjectStates()
    {
        using var game = new RoomGame(); game.Load(Scene); Pose(game);
        game.Tick(new(GameButtons.None, 0, 0, true));
        var state = game.CaptureCheckpoint(); Assert.Equal(new[] { "near" }, state.Objects.Entries.Where(e => e.Active).Select(e => e.EntityId));
        var frame = game.CaptureRenderScene(); Assert.Equal(RoomGame.ActiveColor, frame.Primitives[0].Color); Assert.Equal(new(.2f, .4f, .8f), frame.Primitives[1].Color);
        game.Tick(); Assert.Equal(state.Objects, game.CaptureCheckpoint().Objects);
        Pose(game, 2); game.Tick(new(GameButtons.None, 0, 0, true));
        Assert.Equal(new[] { "near", "side" }, game.CaptureCheckpoint().Objects.Entries.Where(e => e.Active).Select(e => e.EntityId));
        Pose(game); game.Tick(new(GameButtons.None, 0, 0, true));
        Assert.Equal(new[] { "side" }, game.CaptureCheckpoint().Objects.Entries.Where(e => e.Active).Select(e => e.EntityId));
        Assert.Equal(new(.2f, .4f, .8f), game.CaptureRenderScene().Primitives[0].Color);
        Assert.Equal(RoomGame.ActiveColor, game.CaptureRenderScene().Primitives[2].Color);
        Assert.All(Scene.CopyEntities(), e => Assert.Equal(new(.2f, .4f, .8f), e.Primitive!.ToPrimitive().Color));
    }

    [Theory]
    [InlineData("outside")] [InlineData("occluded")] [InlineData("disabled")] [InlineData("miss")]
    public void UnreachableOccludedDisabledAndMissedTargets_DoNotToggleAnything(string scenario)
    {
        var entities = scenario switch
        {
            "occluded" => new[] { Box("blocker", 0, 1, false), Box("target", 0, 0) },
            "disabled" => [Box("target", 0, 0, false)], "miss" => [Box("target", 2, 0)], _ => [Box("target", 0, 0)]
        };
        using var game = new RoomGame(); game.Load(new("room", 7, entities)); Pose(game, z: scenario == "outside" ? 4.501f : 3);
        var before = game.CaptureCheckpoint(); game.Tick(new(GameButtons.None, 0, 0, true));
        var after = game.CaptureCheckpoint(); Assert.False(after.Interaction); Assert.Equal(before.Objects, after.Objects);
        Assert.Equal(before.Camera, after.Camera); Assert.Equal(1UL, after.Tick);
    }

    [Theory]
    [InlineData("missing")] [InlineData("unknown")] [InlineData("inconsistent")]
    public void InvalidObjectSetRestore_IsRejectedBeforeAnyCameraTickOrStateMutation(string invalid)
    {
        using var game = new RoomGame(); game.Load(Scene); Pose(game); game.Tick(new(GameButtons.None, 0, 0, true));
        var before = game.CaptureCheckpoint(); var entries = before.Objects.Entries.AsEnumerable();
        var objects = invalid switch { "missing" => new GameObjectStates(entries.Skip(1)),
            "unknown" => new(entries.Append(new("other", false))), _ => before.Objects };
        var bad = before with { Objects = objects, Tick = 999, Camera = new(50, 50, 50, 0, 0, 0), Interaction = invalid != "inconsistent" };
        Assert.Equal(CheckpointOutcome.RestoreRejected, Assert.Throws<CheckpointRejectedException>(() => game.RestoreCheckpoint(bad)).Outcome);
        Assert.Equal(before, game.CaptureCheckpoint()); Assert.Equal(RoomGame.ActiveColor, game.CaptureRenderScene().Primitives[0].Color);
    }

    [Fact]
    public void LegacyGames_RejectPerObjectStateWithoutPartialRestore()
    {
        foreach (var game in new ICheckpointGameSession[] { new ProbeGame(), new CameraGame() })
        {
            using (game)
            {
                game.Load(Scene); var before = game.CaptureCheckpoint();
                var bad = before with { Tick = 99, Objects = new([new("near", true)]) };
                Assert.Throws<CheckpointRejectedException>(() => game.RestoreCheckpoint(bad)); Assert.Equal(before, game.CaptureCheckpoint());
            }
        }
    }

    [Fact]
    public void InvalidCameraAndTickOverflow_LeaveCompleteRoomStateUnchanged()
    {
        using var game = new RoomGame(); game.Load(Scene); Pose(game); game.Tick(new(GameButtons.None, 0, 0, true));
        var before = game.CaptureCheckpoint();
        Assert.Throws<InvalidDataException>(() => game.RestoreCheckpoint(before with { Camera = before.Camera with { X = float.NaN } }));
        Assert.Equal(before, game.CaptureCheckpoint());
        var maximum = before with { Tick = ulong.MaxValue }; game.RestoreCheckpoint(maximum);
        Assert.Throws<OverflowException>(() => game.Tick(new(GameButtons.Right, 0, 0, true)));
        Assert.Equal(maximum, game.CaptureCheckpoint());
    }

    [Fact]
    public async Task RuntimeWrongObjectSet_IsRejectedWithoutMutationAndKeepsFramesFenced()
    {
        var id = new SessionIdentity(Guid.NewGuid(), 1, Hash, Hash, Hash);
        using var game = new RoomGame(); var runtime = new RuntimeProtocol(id, game, 11, requireRestore: true); await Load(runtime, id);
        var before = game.CaptureCheckpoint();
        var reply = await Restore(runtime, id, before with { Objects = new([new("unknown", false)]), Tick = 99 });
        Assert.Equal((int)CheckpointOutcome.RestoreRejected, reply.CheckpointOutcome);
        Assert.Equal(before, game.CaptureCheckpoint()); Assert.Throws<InvalidOperationException>(() => runtime.CaptureRenderFrame());
    }

    [Fact]
    public async Task RuntimePulseAndFreshRestore_PreserveCompleteObjectStateAndDoNotReplayInput()
    {
        var identity = new SessionIdentity(Guid.NewGuid(), 1, Hash, Hash, Hash);
        using var game = new RoomGame(); var runtime = new RuntimeProtocol(identity, game, 11, requireRestore: true);
        await Load(runtime, identity); var start = game.CaptureCheckpoint() with { Camera = new(0, 0, 3, 0, 0, 0) };
        await Restore(runtime, identity, start);
        var input = ControlRequest.Create(identity, 4, CommandKind.SetInput); input.InputCaptured = true; input.InputInteract = true;
        await runtime.SendAsync(input); await runtime.SendAsync(ControlRequest.Create(identity, 5, CommandKind.Step));
        await runtime.SendAsync(ControlRequest.Create(identity, 6, CommandKind.Step));
        var state = game.CaptureCheckpoint(); Assert.Equal(2UL, state.Tick); Assert.True(state.Interaction);
        Assert.Equal(new[] { "near" }, state.Objects.Entries.Where(e => e.Active).Select(e => e.EntityId));
        var capture = await runtime.SendAsync(ControlRequest.Create(identity, 7, CommandKind.CaptureCheckpoint));
        Assert.Equal(state, GameCheckpointCodec.Decode(capture.Checkpoint!).State);
        var nextId = identity with { Generation = 2 }; using var next = new RoomGame(); var replacement = new RuntimeProtocol(nextId, next, 12, requireRestore: true);
        await Load(replacement, nextId); await Restore(replacement, nextId, GameCheckpointCodec.Decode(capture.Checkpoint!).State);
        Assert.Equal(state, next.CaptureCheckpoint()); Assert.Equal(RoomGame.ActiveColor, replacement.CaptureRenderFrame().Scene.Primitives[0].Color);
        await replacement.SendAsync(ControlRequest.Create(nextId, 4, CommandKind.Step));
        Assert.Equal(state.Objects, next.CaptureCheckpoint().Objects); Assert.Equal(state.Camera, next.CaptureCheckpoint().Camera);
    }
    private static async Task Load(RuntimeProtocol runtime, SessionIdentity id)
    {
        await runtime.SendAsync(ControlRequest.Create(id, 1, CommandKind.Hello)); var load = ControlRequest.Create(id, 2, CommandKind.LoadScene);
        load.SceneId = Scene.Id; load.Revision = Scene.Revision; load.Entities = Scene.CopyEntities(); await runtime.SendAsync(load);
    }
    private static Task<ControlReply> Restore(RuntimeProtocol runtime, SessionIdentity id, GameCheckpointState state)
    {
        var restore = ControlRequest.Create(id, 3, CommandKind.RestoreCheckpoint); restore.Checkpoint = GameCheckpointCodec.Encode(new(Hash, Hash, Scene.Id, Scene.Revision, state));
        return runtime.SendAsync(restore);
    }
}
