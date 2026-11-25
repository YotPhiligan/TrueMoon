using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Tests.Rendering.Snapshots;
using Xunit;
using System.Numerics;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Rendering;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Runtime.Transport;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Tests.Rendering.Resources;

namespace TrueMoon.Aether.Tests.Runtime;

public sealed class RequiredMeshProtocolTests
{
    [Fact]
    public async Task Playing_RequiresActualCurrentPresentation_AndResizePausesUntilNewFence()
    {
        var id = Identity(); using var game = new MeshGame(); using var protocol = new RuntimeProtocol(id, game, 17);
        Assert.Equal(1, (await protocol.SendAsync(ControlRequest.Create(id, 1, CommandKind.Hello))).RenderViewVersion);
        await Load(protocol, id); var play = ControlRequest.Create(id, 3, CommandKind.SetMode); play.Playing = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => protocol.SendAsync(play));
        var stamp = game.Stamp(); Produce(protocol, stamp);
        await Assert.ThrowsAsync<InvalidOperationException>(() => protocol.SendAsync(play));
        protocol.FixedTick(); Assert.Equal(0UL, game.TickNumber);
        var ack = ControlRequest.Create(id, 3, CommandKind.PresentRenderView); ack.RenderViewStamp = stamp.Encode();
        Assert.True((await protocol.SendAsync(ack)).RenderViewReady);
        play.Sequence = 4; Assert.Equal("Playing", (await protocol.SendAsync(play)).Phase);
        protocol.FixedTick(); Assert.Equal(1UL, game.TickNumber);
        protocol.DetachRenderSurface(); protocol.FixedTick(); Assert.Equal(1UL, game.TickNumber);
        var stale = ControlRequest.Create(id, 5, CommandKind.PresentRenderView); stale.RenderViewStamp = stamp.Encode();
        Assert.False((await protocol.SendAsync(stale)).RenderViewReady);
        var resized = stamp with { ViewId = Guid.NewGuid(), SurfaceGeneration = stamp.SurfaceGeneration + 1, Sequence = 1 };
        Produce(protocol, resized); stale.Sequence = 6; stale.RenderViewStamp = resized.Encode();
        var resumed = await protocol.SendAsync(stale); Assert.Equal("Playing", resumed.Phase); Assert.True(resumed.RenderViewReady);
        protocol.FixedTick(); Assert.Equal(2UL, game.TickNumber);
        Assert.Equal(new[] { true, false, true }, game.Playback);
    }
    [Fact]
    public async Task ContentChangeDuringGpuFlight_OldActualPresentationCannotResumePlaying_AndCurrentFrameCanRetry()
    {
        var id = Identity(); using var game = new MeshGame(); using var protocol = new RuntimeProtocol(id, game, 17);
        await protocol.SendAsync(ControlRequest.Create(id, 1, CommandKind.Hello)); await Load(protocol, id);
        var initial = game.Stamp(); Produce(protocol, initial);
        var ack = ControlRequest.Create(id, 3, CommandKind.PresentRenderView); ack.RenderViewStamp = initial.Encode();
        Assert.True((await protocol.SendAsync(ack)).RenderViewReady);
        var play = ControlRequest.Create(id, 4, CommandKind.SetMode); play.Playing = true; await protocol.SendAsync(play);
        game.Content = ResourceTestFixture.ChangedContent(game.Content); protocol.FixedTick(); Assert.Equal(1UL, game.TickNumber);
        var inFlight = initial with { Sequence = initial.Sequence + 1, Tick = game.TickNumber }; Produce(protocol, inFlight);
        ack.Sequence = 5; ack.RenderViewStamp = inFlight.Encode(); var obsolete = await protocol.SendAsync(ack);
        Assert.False(obsolete.RenderViewReady); Assert.Equal("Paused", obsolete.Phase); protocol.FixedTick(); Assert.Equal(1UL, game.TickNumber);
        var current = game.Stamp() with { ViewId = initial.ViewId, BindingRevision = initial.BindingRevision + 1, Sequence = initial.Sequence + 2, Tick = game.TickNumber };
        Produce(protocol, current); ack.Sequence = 6;
        Assert.False((await protocol.SendAsync(ack)).RenderViewReady);
        ack.Sequence = 7; ack.RenderViewStamp = (current with { Sequence = current.Sequence + 100 }).Encode();
        Assert.False((await protocol.SendAsync(ack)).RenderViewReady);
        ack.Sequence = 8; ack.RenderViewStamp = current.Encode(); var resumed = await protocol.SendAsync(ack);
        Assert.True(resumed.RenderViewReady); Assert.Equal("Playing", resumed.Phase); protocol.FixedTick(); Assert.Equal(2UL, game.TickNumber);
        ack.Sequence = 9; ack.RenderViewStamp = inFlight.Encode(); var staleAfterReady = await protocol.SendAsync(ack);
        Assert.True(staleAfterReady.RenderViewReady); Assert.False(staleAfterReady.RenderViewPresentationAccepted);
        Assert.Equal(new[] { true, false, true }, game.Playback);
    }
    [Fact]
    public async Task Playback_NotifiesActualTransitionsAndStopWithoutAdvancingPausedTicks()
    {
        var id = Identity(); using var game = new PlaybackGame(); var protocol = new RuntimeProtocol(id, game, 17);
        await protocol.SendAsync(ControlRequest.Create(id, 1, CommandKind.Hello)); await Load(protocol, id);
        Assert.Empty(game.Playback); protocol.FixedTick(); Assert.Equal(0UL, game.TickNumber);
        var play = ControlRequest.Create(id, 3, CommandKind.SetMode); play.Playing = true;
        await protocol.SendAsync(play); protocol.FixedTick(); Assert.Equal(1UL, game.TickNumber);
        play.Sequence = 4; await protocol.SendAsync(play); Assert.Equal(new[] { true }, game.Playback);
        await protocol.SendAsync(ControlRequest.Create(id, 5, CommandKind.SetMode));
        protocol.FixedTick(); Assert.Equal(1UL, game.TickNumber);
        await protocol.SendAsync(ControlRequest.Create(id, 6, CommandKind.Step)); Assert.Equal(2UL, game.TickNumber);
        play.Sequence = 7; await protocol.SendAsync(play);
        await protocol.SendAsync(ControlRequest.Create(id, 8, CommandKind.Stop)); protocol.FixedTick();
        Assert.Equal(new[] { true, false, true, false }, game.Playback); Assert.Equal(2UL, game.TickNumber);
        protocol.Dispose(); protocol.Dispose(); Assert.False(game.Disposed);
    }
    [Fact]
    public async Task PlaybackFailure_BlocksFurtherTicksAndDisposeRetriesPauseWithoutOwningGame()
    {
        var id = Identity(); using var game = new PlaybackGame(); var protocol = new RuntimeProtocol(id, game, 17);
        await protocol.SendAsync(ControlRequest.Create(id, 1, CommandKind.Hello)); await Load(protocol, id);
        var play = ControlRequest.Create(id, 3, CommandKind.SetMode); play.Playing = true; await protocol.SendAsync(play);
        game.FailPause = true;
        Assert.Throws<IOException>(() => protocol.Dispose()); protocol.FixedTick(); Assert.Equal(0UL, game.TickNumber);
        Assert.Equal("Faulted", (await protocol.SendAsync(ControlRequest.Create(id, 4, CommandKind.GetStatus))).Phase);
        await Assert.ThrowsAsync<InvalidOperationException>(() => protocol.SendAsync(ControlRequest.Create(id, 5, CommandKind.Step)));
        game.FailPause = false; protocol.Dispose(); Assert.False(game.Disposed);
        Assert.Equal(new[] { true, false, false }, game.Playback);
    }
    [Fact]
    public async Task HeadlessLegacyPlayIsIndependent_MeshProfileRejectsCpuSurfaceAndForeignPayloadKinds()
    {
        var id = Identity(); using var legacy = new LegacyGame(); using var protocol = new RuntimeProtocol(id, legacy, 17);
        Assert.Equal(0, (await protocol.SendAsync(ControlRequest.Create(id, 1, CommandKind.Hello))).RenderViewVersion);
        await Load(protocol, id); var play = ControlRequest.Create(id, 3, CommandKind.SetMode); play.Playing = true;
        Assert.Equal("Playing", (await protocol.SendAsync(play)).Phase);
        using var mesh = new MeshGame(); using var required = new RuntimeProtocol(id, mesh, 18);
        await required.SendAsync(ControlRequest.Create(id, 1, CommandKind.Hello)); await Load(required, id);
        Assert.False(required.TryCaptureFrame(out _));
        await Assert.ThrowsAsync<NotSupportedException>(() => required.SendAsync(ControlRequest.Create(id, 3, CommandKind.ConfigureSurface)));
        var invalid = ControlRequest.Create(id, 3, CommandKind.GetStatus); invalid.RenderViewStamp = RenderSnapshotStampTests.Stamp().Encode();
        await Assert.ThrowsAsync<InvalidDataException>(() => required.SendAsync(invalid));
    }
    private static void Produce(RuntimeProtocol protocol, RenderSnapshotStamp stamp) =>
        protocol.SurfaceProduced(new([], new FrameMetadata(0, 1, stamp.Tick, 0, 1) { RenderView = stamp }));
    private static SessionIdentity Identity() => new(Guid.NewGuid(), 1, new('A', 64), new('B', 64), new('C', 64));
    private static Task<ControlReply> Load(RuntimeProtocol protocol, SessionIdentity id)
    { var request = ControlRequest.Create(id, 2, CommandKind.LoadScene); request.SceneId = "mesh"; request.Revision = 7; request.Entities = []; return protocol.SendAsync(request); }
    internal class LegacyGame : IGameSession
    {
        public ulong TickNumber { get; private set; }
        public long Revision { get; private set; }
        public uint PatternColor => 0xff000000;
        public void Load(SceneSnapshot scene) => Revision = scene.Revision;
        public void Tick() => TickNumber++;
        public virtual void Dispose() { }
    }
    internal sealed class MeshGame : LegacyGame, IRenderSnapshotGameSession, IGamePlaybackSession
    {
        internal readonly List<bool> Playback = [];
        public void SetPlaying(bool playing) => Playback.Add(playing);
        private readonly World _world = new();
        internal RenderContentManifest Content = BuiltinRenderContent.Create(Guid.NewGuid(), RenderContentHash.Compute("build"u8), Guid.NewGuid());
        internal ulong CameraRevision = 1;
        public RenderSceneSource ReadRenderScene() => new(_world, Content, new(new Guid("458ddc79-8803-4dc0-b24b-9445695d980a")), Revision,
            RenderCameraSource.External(Matrix4x4.CreateTranslation(0, 0, 6), PerspectiveCamera.Default), CameraRevision);
        public RenderSnapshotStamp Stamp() => RenderSnapshotStampTests.Stamp() with { WorldId = _world.Id, SceneId = ReadRenderScene().SceneId.Value,
            SourceRevision = Revision, CameraRevision = CameraRevision, ManifestHash = Convert.ToHexString(Content.Digest.ToArray()) };
        public override void Dispose() => _world.Dispose();
    }
    internal sealed class PlaybackGame : LegacyGame, IGamePlaybackSession
    {
        internal readonly List<bool> Playback = [];
        internal bool FailPause, Disposed;
        public void SetPlaying(bool playing) { Playback.Add(playing); if (!playing && FailPause) throw new IOException("pause"); }
        public override void Dispose() => Disposed = true;
    }
}
