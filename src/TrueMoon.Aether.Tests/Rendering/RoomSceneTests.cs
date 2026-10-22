using System.Buffers;
using System.Numerics;
using Game.Logic;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Input;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Tests.Scenes;
using TrueMoon.Aether.Transport;
using TrueMoon.Generated.TrueMoon.Aether;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering;

public sealed class RoomSceneTests
{
    private static readonly RenderPrimitive Cube = new(Matrix4x4.CreateTranslation(2, 1, -3), new(.2f, .4f, .8f));
    private static SessionIdentity Identity => new(Guid.NewGuid(), 1, new('A', 64), new('B', 64), new('C', 64));
    private static SceneSnapshot Scene => BuiltInSceneTests.Project([BuiltInSceneTests.Entity(1), BuiltInSceneTests.Entity(2)], [BuiltInSceneTests.Definition()]);

    [Fact]
    public void Snapshot_DeepCopiesWirePayloadOnBothIngressAndEgress()
    {
        var wire = ScenePrimitive.FromPrimitive(Cube); var entity = new SceneEntity { Id = "cube", Primitive = wire };
        var scene = new SceneSnapshot("room", 7, [entity]); wire.Transform[12] = 999; wire.Red = 1; entity.Primitive = null;
        var copy = scene.CopyEntities(); Assert.Equal(Cube, copy[0].Primitive!.ToPrimitive());
        copy[0].Primitive!.Transform[0] = 0; copy[0].Primitive!.Blue = 0;
        Assert.Equal(Cube, Assert.Single(scene.CopyEntities()).Primitive!.ToPrimitive());
    }

    [Theory]
    [InlineData("short")] [InlineData("long")] [InlineData("null")] [InlineData("nan")]
    [InlineData("perspective")] [InlineData("singular")] [InlineData("color")]
    public void MalformedWirePrimitive_IsRejectedBeforePublication(string invalid)
    {
        var wire = ScenePrimitive.FromPrimitive(Cube);
        switch (invalid)
        {
            case "short": wire.Transform = new float[15]; break;
            case "long": wire.Transform = new float[17]; break;
            case "null": wire.Transform = null!; break;
            case "nan": wire.Transform[0] = float.NaN; break;
            case "perspective": wire.Transform[3] = .1f; break;
            case "singular": wire.Transform[0] = 0; break;
            case "color": wire.Red = 1.01f; break;
        }
        Assert.Throws<InvalidDataException>(() => new SceneSnapshot("room", 7, [new() { Id = "cube", Primitive = wire }]));
    }

    [Fact]
    public void ControlV3_RoundTripsWorldMatrixColorAndNeutralGroupWithoutAliasing()
    {
        var id = Identity; var request = ControlRequest.Create(id, 2, CommandKind.LoadScene);
        request.SceneId = "room"; request.Revision = 7; request.Entities = [new() { Id = "group" }, new() { Id = "cube", Primitive = ScenePrimitive.FromPrimitive(Cube) }];
        var bytes = new ArrayBufferWriter<byte>(); request.Serialize(bytes); var offset = 0;
        var result = new ControlRequest().Deserialize(bytes.WrittenSpan, ref offset);
        Assert.Equal(3, result.ControlVersion); Assert.Equal(id, result.Identity()); Assert.Equal(bytes.WrittenCount, offset);
        Assert.Equal(7, result.Revision); Assert.Equal("room", result.SceneId); Assert.Null(result.Entities![0].Primitive);
        Assert.Equal(Cube, result.Entities[1].Primitive!.ToPrimitive());
        request.Entities[1].Primitive!.Transform[12] = -100; Assert.Equal(2, result.Entities[1].Primitive!.Transform[12]);
        Assert.Throws<InvalidDataException>(() => (id with { ControlVersion = 2 }).Validate());
        Assert.Equal(1, id.FrameVersion); Assert.Equal(GameCheckpoint.CurrentVersion, id.SnapshotVersion);
    }

    [Theory]
    [InlineData(0)] [InlineData(64)] [InlineData(65)]
    public void ExplicitDrawList_IsBoundedAndCopiedWhileLegacyCubeRemainsCompatible(int count)
    {
        var primitives = Enumerable.Repeat(Cube, count).ToArray();
        if (count > 64) { Assert.Throws<InvalidDataException>(() => new PrimitiveScene(new(0, 1, 6, 0, 0, 0), primitives)); return; }
        var scene = new PrimitiveScene(new(0, 1, 6, 0, 0, 0), primitives); Array.Fill(primitives, default);
        Assert.Equal(count, scene.Primitives.Length); Assert.All(scene.Primitives, value => Assert.Equal(Cube, value)); scene.Validate();
        var legacy = new PrimitiveScene(scene.Camera, .4f, Vector3.One); var one = Assert.Single(legacy.Primitives);
        Assert.Equal(legacy.CreateTransform(640, 360), one.World * legacy.CreateViewProjection(640, 360));
    }

    [Fact]
    public async Task RuntimeRoom_UsesAuthoredDrawsAndRetainsOnlySupportedCheckpointStateAcrossFreshLoad()
    {
        var scene = Scene; var id = Identity; using var game = new RoomGame(); var runtime = new RuntimeProtocol(id, game, 11);
        await Load(runtime, id, scene);
        var initial = runtime.CaptureRenderFrame(); Assert.Equal(2, initial.Scene.Primitives.Length); Assert.Equal(7, game.Revision);
        Assert.Equal(new CameraTransform(0, 1.6f, 6, 0, 0, 0), initial.Scene.Camera);
        var input = ControlRequest.Create(id, 3, CommandKind.SetInput); input.InputCaptured = true; input.InputButtons = (int)GameButtons.Right; input.InputInteract = true;
        await runtime.SendAsync(input); await runtime.SendAsync(ControlRequest.Create(id, 4, CommandKind.Step));
        var state = game.CaptureCheckpoint(); Assert.Equal(1UL, state.Tick); Assert.Equal(.05f, state.Camera.X, 5); Assert.False(state.Interaction);
        Assert.Equal(2, state.Objects.Entries.Length); Assert.All(state.Objects.Entries, entry => Assert.False(entry.Active));
        var capture = await runtime.SendAsync(ControlRequest.Create(id, 5, CommandKind.CaptureCheckpoint));
        var checkpoint = GameCheckpointCodec.Decode(capture.Checkpoint!); Assert.Equal(state, checkpoint.State); Assert.Equal(2, checkpoint.Version);
        var replacementId = id with { Generation = 2, GameBuildId = new('D', 64) };
        using var replacementGame = new RoomGame(); var replacement = new RuntimeProtocol(replacementId, replacementGame, 12, requireRestore: true);
        await Load(replacement, replacementId, scene);
        Assert.Throws<InvalidOperationException>(() => replacement.CaptureRenderFrame());
        var restore = ControlRequest.Create(replacementId, 3, CommandKind.RestoreCheckpoint); restore.Checkpoint = capture.Checkpoint;
        var restored = await replacement.SendAsync(restore); Assert.Equal((int)CheckpointOutcome.Restored, restored.CheckpointOutcome);
        Assert.Equal(state, replacementGame.CaptureCheckpoint());
        Assert.Equal(initial.Scene.Primitives.AsEnumerable(), replacement.CaptureRenderFrame().Scene.Primitives.AsEnumerable());
        await replacement.SendAsync(ControlRequest.Create(replacementId, 4, CommandKind.Step));
        Assert.Equal(state.Camera, replacementGame.CaptureCheckpoint().Camera); // staged held input is not checkpoint state
    }

    [Fact]
    public async Task InvalidLoad_DoesNotConsumeSequenceOrMutateGameAndChangedSceneRevisionRejectsRestore()
    {
        var id = Identity; using var game = new RoomGame(); var runtime = new RuntimeProtocol(id, game, 11);
        await runtime.SendAsync(ControlRequest.Create(id, 1, CommandKind.Hello));
        var load = ControlRequest.Create(id, 2, CommandKind.LoadScene); load.SceneId = "room"; load.Revision = 7;
        load.Entities = [new() { Id = "bad", Primitive = new() }];
        await Assert.ThrowsAsync<InvalidDataException>(() => runtime.SendAsync(load)); Assert.Equal(-1, game.Revision);
        load.Entities = Scene.CopyEntities(); await runtime.SendAsync(load); Assert.Equal(7, game.Revision);
        var capture = await runtime.SendAsync(ControlRequest.Create(id, 3, CommandKind.CaptureCheckpoint));
        using var next = new RoomGame(); var replacement = new RuntimeProtocol(id, next, 12, requireRestore: true);
        await Load(replacement, id, new("room", 8, Scene.CopyEntities()));
        var restore = ControlRequest.Create(id, 3, CommandKind.RestoreCheckpoint); restore.Checkpoint = capture.Checkpoint;
        Assert.Equal((int)CheckpointOutcome.RestoreRejected, (await replacement.SendAsync(restore)).CheckpointOutcome);
        Assert.Throws<InvalidOperationException>(() => replacement.CaptureRenderFrame()); Assert.Equal(0UL, next.TickNumber); Assert.Equal(8, next.Revision);
    }

    private static async Task Load(RuntimeProtocol runtime, SessionIdentity identity, SceneSnapshot scene)
    {
        await runtime.SendAsync(ControlRequest.Create(identity, 1, CommandKind.Hello));
        var request = ControlRequest.Create(identity, 2, CommandKind.LoadScene);
        request.SceneId = scene.Id; request.Revision = scene.Revision; request.Entities = scene.CopyEntities(); await runtime.SendAsync(request);
    }
}
