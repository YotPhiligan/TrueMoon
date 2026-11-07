using System.Buffers;
using System.Security.Cryptography;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Runtime;
using TrueMoon.Aether.Input;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Generated.TrueMoon.Aether;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Runtime;

public sealed class SceneWorldProtocolTests
{
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private static readonly ComponentTypeId TypeId = new(Id(1));
    private static readonly ComponentId ComponentId = new(Id(2));
    private static readonly EntityDefinitionId DefinitionId = new(Id(3));
    private static readonly ComponentSchema Schema = new(TypeId, 1,
        [new("value", PropertyValueKind.Integer, PropertyValue.From(3L)), new("label", PropertyValueKind.String, PropertyValue.From(""))]);
    private static SessionIdentity Identity => new(Id(20), 1, Hash, Hash, Hash);

    [Fact]
    public async Task Hello_AdvertisesExactRegistryAndIndependentTransferLimitsWithoutReadiness()
    {
        using var fixture = new Fixture();
        Assert.False(fixture.Protocol.TryCaptureFrame(out _));
        Assert.Throws<InvalidOperationException>(() => fixture.Protocol.CaptureFrame());
        var hello = await fixture.Send(CommandKind.Hello);
        Assert.Equal((1, SceneWorldCodec.MaximumBytes, 32768), (hello.SceneWorldVersion, hello.SceneMaximumBytes, hello.SceneChunkBytes));
        Assert.Equal(fixture.Game.RegistryId.ToString("D"), hello.SceneRegistryId);
        Assert.Equal(("HelloAccepted", -1L, 0L), (hello.Phase, hello.Revision, hello.Tick));
        Assert.Equal("", hello.SceneWorldId); Assert.Equal("", hello.SceneLoadId);
        Assert.Equal((0, 0, 0), (hello.SceneLoadOffset, hello.SceneEntityCount, hello.SceneComponentCount));
        Assert.Null(fixture.Game.ActiveScene); Assert.Equal(0, fixture.Imports);
        Assert.Equal(65536, BoundedControlClient.PayloadLimit);
        Assert.Equal(64, SceneSnapshotAdapter.MaximumEntityCount);
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("missing-codec")]
    [InlineData("preview")]
    public async Task UnsupportedCapability_IsExplicitWithoutPrimitiveFallback(string profile)
    {
        using var game = CreateGame();
        using var legacy = new LegacyGame();
        using var protocol = new RuntimeProtocol(Identity, profile == "legacy" ? legacy : game, 17,
            role: profile == "preview" ? RuntimeRole.EditPreview : RuntimeRole.Game,
            sceneWorldCodec: profile == "missing-codec" ? null : Codec());
        var hello = await protocol.SendAsync(ControlRequest.Create(Identity, 1, CommandKind.Hello));
        Assert.Equal((0, "", 0, 0), (hello.SceneWorldVersion, hello.SceneRegistryId, hello.SceneMaximumBytes, hello.SceneChunkBytes));
        var begin = Begin(Identity, 2, game.RegistryId, new byte[44], Document(1));
        await Assert.ThrowsAsync<NotSupportedException>(() => protocol.SendAsync(begin));
        Assert.Null(game.ActiveScene); Assert.Equal(0, legacy.Loads);
        if (profile == "legacy")
        {
            var load = ControlRequest.Create(Identity, 2, CommandKind.LoadScene);
            load.SceneId = "legacy"; load.Revision = 7; load.Entities = [];
            var loaded = await protocol.SendAsync(load);
            Assert.Equal(("Paused", 7L), (loaded.Phase, loaded.Revision));
            Assert.Equal(1, legacy.Loads);
        }
    }

    [Fact]
    public async Task RestoreRequiredRuntime_RejectsWorldTransferBeforeStagingOrImport()
    {
        using var game = CreateGame(); var codec = Codec();
        using var protocol = new RuntimeProtocol(Identity, game, 17, requireRestore: true, sceneWorldCodec: codec);
        var bytes = codec.Encode(new(Document(1)));
        await protocol.SendAsync(ControlRequest.Create(Identity, 1, CommandKind.Hello));
        await Assert.ThrowsAsync<InvalidOperationException>(() => protocol.SendAsync(Begin(Identity, 2, game.RegistryId, bytes, Document(1))));
        Assert.Null(game.ActiveScene); Assert.Equal((-1L, 0UL), (game.Revision, game.TickNumber));
        Assert.False(protocol.TryCaptureFrame(out _));
        var status = await protocol.SendAsync(ControlRequest.Create(Identity, 2, CommandKind.GetStatus));
        Assert.Equal(("HelloAccepted", "", 0), (status.Phase, status.SceneLoadId, status.SceneLoadOffset));
    }

    [Fact]
    public async Task CompleteTransfer_ActivatesFullCustomSceneBeforeTicksFramesAndMetadata()
    {
        using var fixture = new Fixture();
        var source = Document(65, labels: true); var payload = fixture.Codec.Encode(new(source));
        Assert.True(payload.Length > BoundedControlClient.PayloadLimit);
        await fixture.Send(CommandKind.Hello);
        var operation = await fixture.Begin(payload, source);
        Assert.Equal((operation, 0, "HelloAccepted"), (fixture.Last!.SceneLoadId, fixture.Last.SceneLoadOffset, fixture.Last.Phase));
        await AssertNotReady(fixture);
        await fixture.AppendAll(payload);
        Assert.Equal(payload.Length, fixture.Last!.SceneLoadOffset);
        Assert.Equal(0, fixture.Imports); Assert.Null(fixture.Game.ActiveScene);
        var activated = await fixture.Send(CommandKind.ActivateSceneWorld, request => fixture.StampTransfer(request));
        var active = Assert.IsType<SceneWorld>(fixture.Game.ActiveScene);
        Assert.Equal(("Paused", source.Revision, 0L), (activated.Phase, activated.Revision, activated.Tick));
        Assert.Equal((65, 65), (activated.SceneEntityCount, activated.SceneComponentCount));
        Assert.Equal(active.World.Id.ToString("D"), activated.SceneWorldId);
        Assert.Equal((operation, payload.Length), (activated.SceneLoadId, activated.SceneLoadOffset));
        Assert.Equal((65, 0), (fixture.Imports, fixture.Game.Ticks));
        foreach (var entity in source.Entities)
        {
            var expected = checked((int)entity.Changes.PropertyOverrides.Single(p => p.PropertyId == "value").Value.GetInteger());
            Assert.Equal(new Counter(expected, 1024), active.World.GetComponent<Counter>(active.Entities[entity.Id]));
        }
        Assert.True(fixture.Protocol.TryCaptureFrame(out var first)); Assert.Equal(0UL, first.Tick);
        fixture.Protocol.FixedTick(); Assert.Equal(0, fixture.Game.Ticks);
        await fixture.Send(CommandKind.Step);
        await fixture.Send(CommandKind.SetMode, request => request.Playing = true);
        fixture.Protocol.FixedTick();
        var status = await fixture.Send(CommandKind.GetStatus);
        Assert.Equal(("Playing", 2L, 2), (status.Phase, status.Tick, fixture.Game.Ticks));
        var firstEntity = source.Entities[0];
        Assert.Equal(new Counter(43, 1024), active.World.GetComponent<Counter>(active.Entities[firstEntity.Id]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Send(CommandKind.BeginSceneWorld,
            request => CopyBegin(request, Begin(Identity, 0, fixture.Game.RegistryId, payload, source))));
    }

    [Theory]
    [InlineData("load-id-empty")] [InlineData("load-id-n")] [InlineData("load-id-space")]
    [InlineData("registry-empty")] [InlineData("registry-foreign")]
    [InlineData("scene-empty")] [InlineData("scene-n")]
    [InlineData("negative-revision")] [InlineData("negative-bytes")]
    [InlineData("below-header")] [InlineData("above-maximum")]
    [InlineData("initial-offset")] [InlineData("initial-chunk")]
    [InlineData("digest-empty")] [InlineData("digest-short")] [InlineData("digest-nonhex")]
    public async Task Begin_RejectsInvalidMetadataWithoutConsumingTheOneTransfer(string invalid)
    {
        using var fixture = new Fixture();
        await fixture.Send(CommandKind.Hello);
        var payload = fixture.Codec.Encode(new(Document(1)));
        var request = Begin(Identity, fixture.Next, fixture.Game.RegistryId, payload, Document(1));
        switch (invalid)
        {
            case "load-id-empty": request.SceneLoadId = Guid.Empty.ToString("D"); break;
            case "load-id-n": request.SceneLoadId = Guid.NewGuid().ToString("N"); break;
            case "load-id-space": request.SceneLoadId = " " + request.SceneLoadId; break;
            case "registry-empty": request.SceneRegistryId = ""; break;
            case "registry-foreign": request.SceneRegistryId = Guid.NewGuid().ToString("D"); break;
            case "scene-empty": request.SceneId = Guid.Empty.ToString("D"); break;
            case "scene-n": request.SceneId = Guid.NewGuid().ToString("N"); break;
            case "negative-revision": request.Revision = -1; break;
            case "negative-bytes": request.SceneBytes = -1; break;
            case "below-header": request.SceneBytes = SceneWorldCodec.HeaderBytes - 1; break;
            case "above-maximum": request.SceneBytes = SceneWorldCodec.MaximumBytes + 1; break;
            case "initial-offset": request.SceneOffset = 1; break;
            case "initial-chunk": request.SceneChunk = [1]; break;
            case "digest-empty": request.SceneDigest = ""; break;
            case "digest-short": request.SceneDigest = new('A', 63); break;
            case "digest-nonhex": request.SceneDigest = new('Z', 64); break;
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Protocol.SendAsync(request));
        Assert.Null(fixture.Game.ActiveScene); Assert.Equal((0, 0), (fixture.Imports, fixture.Game.Ticks));
        var operation = await fixture.Begin(payload, Document(1));
        Assert.Equal(operation, fixture.Last!.SceneLoadId); Assert.Equal(0, fixture.Last.SceneLoadOffset);
    }

    [Theory]
    [InlineData(SceneWorldCodec.HeaderBytes)]
    [InlineData(SceneWorldCodec.MaximumBytes)]
    public async Task Begin_AcceptsInclusiveEnvelopeBoundsAndAbortDoesNotAdmitAnotherTransfer(int length)
    {
        using var fixture = new Fixture(); await fixture.Send(CommandKind.Hello);
        var operation = await fixture.Begin(new byte[length], Document(1));
        var aborted = await fixture.Send(CommandKind.AbortSceneWorld, request => fixture.StampTransfer(request));
        Assert.Equal((operation, 0, "HelloAccepted"), (aborted.SceneLoadId, aborted.SceneLoadOffset, aborted.Phase));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Begin(new byte[44], Document(1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Send(CommandKind.AppendSceneWorld,
            request => { fixture.StampTransfer(request); request.SceneChunk = [1]; }));
        Assert.Null(fixture.Game.ActiveScene); Assert.False(fixture.Protocol.TryCaptureFrame(out _));
    }

    [Theory]
    [InlineData("null")] [InlineData("empty")] [InlineData("short")]
    [InlineData("oversized")] [InlineData("negative-offset")] [InlineData("future-offset")]
    [InlineData("foreign-operation")] [InlineData("foreign-registry")]
    public async Task Append_RejectsMalformedChunkWithoutChangingCurrentOffset(string invalid)
    {
        using var fixture = new Fixture(); await fixture.Send(CommandKind.Hello);
        var bytes = new byte[32769]; await fixture.Begin(bytes, Document(1));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Send(CommandKind.AppendSceneWorld, request =>
        {
            fixture.StampTransfer(request); request.SceneChunk = new byte[32768];
            switch (invalid)
            {
                case "null": request.SceneChunk = null; break;
                case "empty": request.SceneChunk = []; break;
                case "short": request.SceneChunk = new byte[32767]; break;
                case "oversized": request.SceneChunk = new byte[32769]; break;
                case "negative-offset": request.SceneOffset = -1; break;
                case "future-offset": request.SceneOffset = 1; break;
                case "foreign-operation": request.SceneLoadId = Guid.NewGuid().ToString("D"); break;
                case "foreign-registry": request.SceneRegistryId = Guid.NewGuid().ToString("D"); break;
            }
        }));
        var status = await fixture.Send(CommandKind.GetStatus);
        Assert.Equal(0, status.SceneLoadOffset);
        await fixture.Send(CommandKind.AppendSceneWorld, request =>
        { fixture.StampTransfer(request); request.SceneChunk = bytes[..32768]; });
        Assert.Equal(32768, fixture.Last!.SceneLoadOffset);
        await fixture.Send(CommandKind.AppendSceneWorld, request =>
        { fixture.StampTransfer(request); request.SceneOffset = 32768; request.SceneChunk = bytes[32768..]; });
        Assert.Equal(32769, fixture.Last!.SceneLoadOffset);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Send(CommandKind.AppendSceneWorld, request =>
        { fixture.StampTransfer(request); request.SceneOffset = 32769; request.SceneChunk = [0]; }));
        Assert.Equal(0, fixture.Imports); Assert.Null(fixture.Game.ActiveScene);
    }

    [Fact]
    public async Task CopiedChunksAndStaleAbort_CannotAlterStagedPayloadOrOperation()
    {
        using var fixture = new Fixture(); await fixture.Send(CommandKind.Hello);
        var source = Document(1); var bytes = fixture.Codec.Encode(new(source));
        var operation = await fixture.Begin(bytes, source);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Send(CommandKind.AbortSceneWorld, request =>
        { fixture.StampTransfer(request); request.SceneLoadId = Guid.NewGuid().ToString("D"); }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Begin(bytes, source));
        await fixture.AppendAll(bytes, mutateRequests: true);
        var active = await fixture.Send(CommandKind.ActivateSceneWorld, request => fixture.StampTransfer(request));
        Assert.Equal(operation, active.SceneLoadId);
        Assert.Equal((1, 1, 1), (active.SceneEntityCount, active.SceneComponentCount, fixture.Imports));
        Assert.Equal(new Counter(41, 0), fixture.Game.ActiveScene!.World.GetComponent<Counter>(fixture.Game.ActiveScene.Entities[source.Entities[0].Id]));
    }

    [Theory]
    [InlineData("session")] [InlineData("generation")] [InlineData("host")]
    [InlineData("build")] [InlineData("nonce")] [InlineData("control")]
    [InlineData("frame")] [InlineData("snapshot")] [InlineData("sequence")]
    public async Task ExistingHeaderFences_ApplyToNewChunkCommandsWithoutClearingStage(string invalid)
    {
        using var fixture = new Fixture(); await fixture.Send(CommandKind.Hello);
        var bytes = new byte[44]; var operation = await fixture.Begin(bytes, Document(1));
        var request = ControlRequest.Create(Identity, fixture.Next, CommandKind.AbortSceneWorld); fixture.StampTransfer(request);
        switch (invalid)
        {
            case "session": request.SessionId = Guid.NewGuid().ToString("D"); break;
            case "generation": request.Generation++; break;
            case "host": request.HostContractId = new('B', 64); break;
            case "build": request.GameBuildId = new('B', 64); break;
            case "nonce": request.Nonce = new('B', 64); break;
            case "control": request.ControlVersion++; break;
            case "frame": request.FrameVersion++; break;
            case "snapshot": request.SnapshotVersion++; break;
            case "sequence": request.Sequence--; break;
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Protocol.SendAsync(request));
        var status = await fixture.Send(CommandKind.GetStatus);
        Assert.Equal((operation, 0), (status.SceneLoadId, status.SceneLoadOffset));
        Assert.Null(fixture.Game.ActiveScene);
        await fixture.Send(CommandKind.AbortSceneWorld, item => fixture.StampTransfer(item));
    }

    [Theory]
    [InlineData("operation")]
    [InlineData("registry")]
    public async Task StaleActivationStamp_PreservesVerifiedStageForItsCorrectOperation(string invalid)
    {
        using var fixture = new Fixture(); await fixture.Send(CommandKind.Hello);
        var source = Document(1); var bytes = fixture.Codec.Encode(new(source));
        var operation = await fixture.Begin(bytes, source); await fixture.AppendAll(bytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Send(CommandKind.ActivateSceneWorld, request =>
        {
            fixture.StampTransfer(request);
            if (invalid == "operation") request.SceneLoadId = Guid.NewGuid().ToString("D");
            else request.SceneRegistryId = Guid.NewGuid().ToString("D");
        }));
        Assert.Null(fixture.Game.ActiveScene); Assert.Equal(0, fixture.Imports);
        var status = await fixture.Send(CommandKind.GetStatus);
        Assert.Equal((operation, bytes.Length), (status.SceneLoadId, status.SceneLoadOffset));
        var active = await fixture.Send(CommandKind.ActivateSceneWorld, request => fixture.StampTransfer(request));
        Assert.Equal(("Paused", 1, 1, 1), (active.Phase, active.SceneEntityCount, active.SceneComponentCount, fixture.Imports));
    }

    [Theory]
    [InlineData("outer-digest")] [InlineData("inner-digest")] [InlineData("version")]
    [InlineData("scene")] [InlineData("revision")] [InlineData("truncated")]
    public async Task FailedActivation_ConsumesStageWithoutPublishingReadyWorldOrAdmittingReplay(string invalid)
    {
        using var fixture = new Fixture(); await fixture.Send(CommandKind.Hello);
        var source = Document(65, labels: true); var bytes = fixture.Codec.Encode(new(source));
        if (invalid == "inner-digest") bytes[^1] ^= 1;
        if (invalid == "version") bytes[4] = 2;
        await fixture.Begin(bytes, source, request =>
        {
            if (invalid == "outer-digest") request.SceneDigest = new('0', 64);
            if (invalid == "scene") request.SceneId = Guid.NewGuid().ToString("D");
            if (invalid == "revision") request.Revision++;
        });
        if (invalid == "truncated")
            await fixture.Send(CommandKind.AppendSceneWorld, request => { fixture.StampTransfer(request); request.SceneChunk = bytes[..32768]; });
        else await fixture.AppendAll(bytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Send(CommandKind.ActivateSceneWorld, request => fixture.StampTransfer(request)));
        Assert.Null(fixture.Game.ActiveScene); Assert.Equal((-1L, 0UL), (fixture.Game.Revision, fixture.Game.TickNumber));
        Assert.Equal(0, fixture.Imports); Assert.False(fixture.Protocol.TryCaptureFrame(out _));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Send(CommandKind.ActivateSceneWorld, request => fixture.StampTransfer(request)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Begin(bytes, source));
        var status = await fixture.Send(CommandKind.GetStatus);
        Assert.Equal(("HelloAccepted", "", 0, 0), (status.Phase, status.SceneWorldId, status.SceneEntityCount, status.SceneComponentCount));
    }

    [Fact]
    public async Task CooperativeActivationCancellation_KeepsReadinessFalseAndReleasesStaging()
    {
        using var cancellation = new CancellationTokenSource();
        using var fixture = new Fixture(context => { cancellation.Cancel(); return new Counter(17, 0); });
        await fixture.Send(CommandKind.Hello);
        var source = Document(1); var bytes = fixture.Codec.Encode(new(source));
        await fixture.Begin(bytes, source); await fixture.AppendAll(bytes);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Send(CommandKind.ActivateSceneWorld,
            request => fixture.StampTransfer(request), cancellation.Token));
        Assert.Null(fixture.Game.ActiveScene); Assert.False(fixture.Protocol.TryCaptureFrame(out _));
        Assert.Equal((-1L, 0UL, 1), (fixture.Game.Revision, fixture.Game.TickNumber, fixture.Imports));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Send(CommandKind.AbortSceneWorld, request => fixture.StampTransfer(request)));
    }

    [Fact]
    public async Task Dispose_CancelsAndDrainsCooperativeActivationWithoutOwningGame()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var fixture = new Fixture(context =>
        {
            entered.TrySetResult();
            if (!context.CancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("Protocol disposal did not cancel the cooperative importer.");
            context.CancellationToken.ThrowIfCancellationRequested();
            return new Counter(0, 0);
        });
        await fixture.Send(CommandKind.Hello);
        var source = Document(1); var bytes = fixture.Codec.Encode(new(source));
        await fixture.Begin(bytes, source); await fixture.AppendAll(bytes);
        var activation = Task.Run(() => fixture.Send(CommandKind.ActivateSceneWorld, request => fixture.StampTransfer(request)));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var disposal = Task.Run(fixture.Protocol.Dispose);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => activation).WaitAsync(TimeSpan.FromSeconds(5));
            await disposal.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Null(fixture.Game.ActiveScene);
            Assert.Equal((-1L, 0UL), (fixture.Game.Revision, fixture.Game.TickNumber));
            Assert.Throws<InvalidOperationException>(() => fixture.Game.Tick());
            Assert.False(fixture.Protocol.TryCaptureFrame(out _));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Send(CommandKind.GetStatus));
        }
        finally { fixture.Protocol.Dispose(); }
    }

    [Fact]
    public async Task StopAndProtocolDisposal_DoNotDisposeBorrowedActiveGame()
    {
        using var fixture = new Fixture(); await fixture.Send(CommandKind.Hello);
        var source = Document(1); var bytes = fixture.Codec.Encode(new(source));
        await fixture.Begin(bytes, source); await fixture.AppendAll(bytes);
        await fixture.Send(CommandKind.ActivateSceneWorld, request => fixture.StampTransfer(request));
        var scene = fixture.Game.ActiveScene!;
        await fixture.Send(CommandKind.Stop);
        Assert.False(fixture.Protocol.TryCaptureFrame(out _));
        Assert.Throws<InvalidOperationException>(() => fixture.Protocol.CaptureFrame());
        fixture.Protocol.FixedTick(); Assert.Equal(0, fixture.Game.Ticks);
        fixture.Protocol.Dispose(); fixture.Protocol.Dispose();
        Assert.True(scene.IsReady); Assert.False(scene.World.IsDisposed);
        fixture.Game.Tick(); Assert.Equal(1UL, fixture.Game.TickNumber);
        fixture.Game.Dispose(); Assert.True(scene.World.IsDisposed);
    }

    [Fact]
    public async Task TickFailure_PreservesOriginalErrorAndMakesWorldProtocolTerminallyUnready()
    {
        using var fixture = new Fixture(); await fixture.Send(CommandKind.Hello);
        var source = Document(1); var bytes = fixture.Codec.Encode(new(source));
        await fixture.Begin(bytes, source); await fixture.AppendAll(bytes);
        await fixture.Send(CommandKind.ActivateSceneWorld, request => fixture.StampTransfer(request));
        await fixture.Send(CommandKind.SetMode, request => request.Playing = true);
        var failure = new ArithmeticException("tick failed"); fixture.Game.TickFailure = failure;
        Assert.Same(failure, Assert.Throws<ArithmeticException>(fixture.Protocol.FixedTick));
        var scene = fixture.Game.ActiveScene!;
        Assert.True(fixture.Game.IsFaulted); Assert.True(scene.World.IsFaulted);
        Assert.Equal((0UL, 1), (fixture.Game.TickNumber, fixture.Game.Ticks));
        var status = await fixture.Send(CommandKind.GetStatus);
        Assert.Equal(("Faulted", 0L, scene.World.Id.ToString("D"), 1, 1),
            (status.Phase, status.Tick, status.SceneWorldId, status.SceneEntityCount, status.SceneComponentCount));
        fixture.Protocol.FixedTick(); Assert.Equal(1, fixture.Game.Ticks);
        Assert.False(fixture.Protocol.TryCaptureFrame(out _));
        Assert.Throws<InvalidOperationException>(() => fixture.Protocol.CaptureFrame());
        Assert.Throws<InvalidOperationException>(() => fixture.Protocol.CaptureRenderFrame());
        foreach (var command in new[] { CommandKind.Step, CommandKind.SetMode, CommandKind.SetInput, CommandKind.ConfigureSurface })
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Send(command));
        Assert.Equal(new Counter(41, 0), scene.World.GetComponent<Counter>(scene.Entities[source.Entities[0].Id]));
    }

    [Fact]
    public async Task Dispose_CancellationCallbackFailureStillDrainsActivationAndClosesProtocol()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var leaveImporter = new ManualResetEventSlim();
        var callbackFailure = new ArithmeticException("cancellation callback failed");
        using var fixture = new Fixture(context =>
        {
            using var registration = context.CancellationToken.Register(() => throw callbackFailure);
            entered.TrySetResult();
            if (!context.CancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("Protocol disposal did not signal cancellation.");
            cancelled.TrySetResult();
            if (!leaveImporter.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("Importer was not released.");
            context.CancellationToken.ThrowIfCancellationRequested();
            return new Counter(0, 0);
        });
        await fixture.Send(CommandKind.Hello);
        var source = Document(1); var bytes = fixture.Codec.Encode(new(source));
        await fixture.Begin(bytes, source); await fixture.AppendAll(bytes);
        var activation = Task.Run(() => fixture.Send(CommandKind.ActivateSceneWorld, request => fixture.StampTransfer(request)));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var disposal = Task.Run(fixture.Protocol.Dispose);
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(disposal.IsCompleted);
            leaveImporter.Set();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => activation).WaitAsync(TimeSpan.FromSeconds(5));
            var failure = await Assert.ThrowsAsync<AggregateException>(() => disposal).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Contains(callbackFailure, failure.Flatten().InnerExceptions);
            Assert.Null(fixture.Game.ActiveScene); Assert.Equal((-1L, 0UL), (fixture.Game.Revision, fixture.Game.TickNumber));
            Assert.False(fixture.Protocol.TryCaptureFrame(out _));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Send(CommandKind.GetStatus));
            Assert.Throws<InvalidOperationException>(fixture.Game.Tick);
            fixture.Protocol.Dispose();
        }
        finally { leaveImporter.Set(); }
    }

    [Fact]
    public async Task FullWorldProfile_DoesNotPretendToSupportLegacyCheckpoint()
    {
        var codec = Codec(); var loader = Loader();
        using var game = new CheckpointGame(loader);
        using var protocol = new RuntimeProtocol(Identity, game, 17, sceneWorldCodec: codec);
        ulong sequence = 1;
        await protocol.SendAsync(ControlRequest.Create(Identity, sequence++, CommandKind.Hello));
        var source = Document(1); var bytes = codec.Encode(new(source));
        var begin = Begin(Identity, sequence++, game.RegistryId, bytes, source); await protocol.SendAsync(begin);
        var append = ControlRequest.Create(Identity, sequence++, CommandKind.AppendSceneWorld);
        append.SceneLoadId = begin.SceneLoadId; append.SceneRegistryId = begin.SceneRegistryId; append.SceneChunk = bytes;
        await protocol.SendAsync(append);
        var activate = ControlRequest.Create(Identity, sequence++, CommandKind.ActivateSceneWorld);
        activate.SceneLoadId = begin.SceneLoadId; activate.SceneRegistryId = begin.SceneRegistryId; await protocol.SendAsync(activate);
        var result = await protocol.SendAsync(ControlRequest.Create(Identity, sequence, CommandKind.CaptureCheckpoint));
        Assert.Equal((int)CheckpointOutcome.Unsupported, result.CheckpointOutcome);
        Assert.Null(result.Checkpoint); Assert.Equal(0, game.Captures);
        Assert.Equal((source.Revision, 0UL), (game.Revision, game.TickNumber));
    }

    [Fact]
    public void GeneratedWire_RoundTripsNewFieldsAndKeepsMaximumChunkWithinLegacyControlBudget()
    {
        var registry = Guid.NewGuid(); var operation = Guid.NewGuid();
        var request = ControlRequest.Create(Identity, 47, CommandKind.AppendSceneWorld);
        request.SceneLoadId = operation.ToString("D"); request.SceneRegistryId = registry.ToString("D");
        request.SceneId = Id(10).ToString("D"); request.Revision = 17; request.SceneBytes = SceneWorldCodec.MaximumBytes;
        request.SceneOffset = 32768; request.SceneDigest = new('B', 64);
        request.SceneChunk = Enumerable.Range(0, 32768).Select(value => (byte)value).ToArray();
        request.Checkpoint = [1, 2, 3]; request.InputCaptured = true; request.InputButtons = (int)GameButtons.Forward;
        var buffer = new ArrayBufferWriter<byte>(); request.Serialize(buffer);
        BoundedControlClient.ValidateLength(buffer.WrittenCount);
        Assert.True(buffer.WrittenCount < BoundedControlClient.PayloadLimit);
        var offset = 0; var decoded = new ControlRequest().Deserialize(buffer.WrittenSpan, ref offset);
        Assert.Equal(buffer.WrittenCount, offset); Assert.Equal(Identity, decoded.Identity());
        Assert.Equal((request.SceneLoadId, request.SceneRegistryId, request.SceneId, 17L, SceneWorldCodec.MaximumBytes, 32768, request.SceneDigest),
            (decoded.SceneLoadId, decoded.SceneRegistryId, decoded.SceneId, decoded.Revision, decoded.SceneBytes, decoded.SceneOffset, decoded.SceneDigest));
        Assert.Equal(request.SceneChunk, decoded.SceneChunk); Assert.Equal(new byte[] { 1, 2, 3 }, decoded.Checkpoint);
        Assert.True(decoded.InputCaptured); Assert.Equal((int)GameButtons.Forward, decoded.InputButtons);

        var reply = new ControlReply
        {
            Stamp = ControlRequest.Create(Identity, 47, CommandKind.AppendSceneWorld), Phase = "Paused", Revision = 17,
            SceneWorldVersion = 1, SceneRegistryId = request.SceneRegistryId, SceneMaximumBytes = SceneWorldCodec.MaximumBytes,
            SceneChunkBytes = 32768, SceneLoadId = request.SceneLoadId, SceneLoadOffset = 65536,
            SceneWorldId = Id(11).ToString("D"), SceneEntityCount = 65, SceneComponentCount = 130
        };
        buffer.Clear(); reply.Serialize(buffer); offset = 0;
        var response = new ControlReply().Deserialize(buffer.WrittenSpan, ref offset);
        Assert.Equal(buffer.WrittenCount, offset); Assert.Equal(Identity, response.Stamp.Identity());
        Assert.Equal((1, request.SceneRegistryId, SceneWorldCodec.MaximumBytes, 32768, request.SceneLoadId, 65536, reply.SceneWorldId, 65, 130),
            (response.SceneWorldVersion, response.SceneRegistryId, response.SceneMaximumBytes, response.SceneChunkBytes,
                response.SceneLoadId, response.SceneLoadOffset, response.SceneWorldId, response.SceneEntityCount, response.SceneComponentCount));
        BoundedControlClient.ValidateLength(65536);
        Assert.Throws<InvalidDataException>(() => BoundedControlClient.ValidateLength(65537));
        request.SceneChunk = new byte[65536]; buffer.Clear(); request.Serialize(buffer);
        Assert.Throws<InvalidDataException>(() => BoundedControlClient.ValidateLength(buffer.WrittenCount));
    }

    private static async Task AssertNotReady(Fixture fixture)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Send(CommandKind.Step));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Send(CommandKind.SetMode));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Send(CommandKind.SetInput));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Send(CommandKind.ConfigureSurface));
        fixture.Protocol.FixedTick();
        Assert.Equal(0, fixture.Game.Ticks); Assert.False(fixture.Protocol.TryCaptureFrame(out _));
        Assert.Throws<InvalidOperationException>(() => fixture.Protocol.CaptureFrame());
    }
    private static ControlRequest Begin(SessionIdentity identity, ulong sequence, Guid registry, byte[] bytes, SceneDocument scene) => new()
    {
        SessionId = identity.SessionId.ToString("D"), Generation = unchecked((long)identity.Generation), HostContractId = identity.HostContractId,
        GameBuildId = identity.GameBuildId, Nonce = identity.Nonce, ControlVersion = identity.ControlVersion,
        FrameVersion = identity.FrameVersion, SnapshotVersion = identity.SnapshotVersion, Sequence = unchecked((long)sequence), Kind = (int)CommandKind.BeginSceneWorld,
        SceneLoadId = Guid.NewGuid().ToString("D"), SceneRegistryId = registry.ToString("D"), SceneBytes = bytes.Length,
        SceneDigest = Convert.ToHexString(SHA256.HashData(bytes)), SceneId = scene.Id.Value.ToString("D"), Revision = scene.Revision
    };
    private static void CopyBegin(ControlRequest destination, ControlRequest source)
    {
        destination.SceneLoadId = source.SceneLoadId; destination.SceneRegistryId = source.SceneRegistryId;
        destination.SceneBytes = source.SceneBytes; destination.SceneDigest = source.SceneDigest;
        destination.SceneId = source.SceneId; destination.Revision = source.Revision;
    }
    private static SceneDocument Document(int count, bool labels = false)
    {
        var entities = Enumerable.Range(0, count).Select(index => new EntityInstance(new(Id(100 + index)), DefinitionId,
            new(propertyOverrides: [new(ComponentId, "value", PropertyValue.From(41L + index)),
                new(ComponentId, "label", PropertyValue.From(labels ? new string((char)('a' + index % 26), 1024) : ""))]), null, "Entity " + index, index));
        return new(new(Id(10)), 17, entities, [new(DefinitionId, new([new ComponentData(ComponentId, TypeId, 1)]))], [Schema]);
    }
    private static SceneResolutionService Resolution() => new(new Factory<EntityResolver, SceneDocument>(source => new(source.Definitions, source.Schemas)));
    private static SceneWorldCodec Codec() => new(new SceneFileCodec(new SceneAuthoringSessionFactory(Resolution(), new SceneAuthoringOptions())));
    private static SceneWorldLoader Loader(Func<ComponentLoadContext, Counter>? importer = null) => new(
        new ComponentRegistryBuilder().Register(Schema, importer ?? (context => new Counter(context.GetInt32("value"), context.GetString("label").Length))).Build(),
        new Factory<World>(() => new()), Resolution());
    private static TestGame CreateGame() => new(Loader());
    private static Guid Id(int index) => new($"52644d02-1ec2-4b66-abf7-{index:000000000000}");
    private record struct Counter(int Value, int LabelLength);
    private class TestGame(SceneWorldLoader loader) : SceneWorldGameSession(loader)
    {
        internal int Ticks;
        internal Exception? TickFailure;
        public override uint PatternColor => 0xff123456;
        protected override void TickWorld(SceneWorld scene, ulong nextTick)
        { Ticks++; if (TickFailure != null) throw TickFailure; scene.World.Query<Counter>().ForEachWrite((EntityHandle e, scoped ref Counter value) => value.Value++); }
    }
    private sealed class CheckpointGame(SceneWorldLoader loader) : TestGame(loader), ICheckpointGameSession
    {
        internal int Captures;
        public GameCheckpointState CaptureCheckpoint() { Captures++; throw new InvalidOperationException("unsupported ECS checkpoint"); }
        public void RestoreCheckpoint(GameCheckpointState state) => throw new InvalidOperationException("unsupported ECS checkpoint");
    }
    private sealed class LegacyGame : IGameSession
    {
        internal int Loads;
        public ulong TickNumber { get; private set; }
        public long Revision { get; private set; } = -1;
        public uint PatternColor => 0xff123456;
        public void Load(SceneSnapshot scene) { Loads++; Revision = scene.Revision; }
        public void Tick() => TickNumber++;
        public void Dispose() { }
    }
    private sealed class Fixture : IDisposable
    {
        internal SceneWorldCodec Codec { get; } = SceneWorldProtocolTests.Codec();
        internal TestGame Game { get; }
        internal RuntimeProtocol Protocol { get; }
        internal int Imports;
        internal ulong Next { get; private set; } = 1;
        internal ControlReply? Last;
        private string _operation = "";
        internal Fixture(Func<ComponentLoadContext, Counter>? importer = null)
        {
            Game = new(Loader(context => { Imports++; return importer?.Invoke(context) ?? new Counter(context.GetInt32("value"), context.GetString("label").Length); }));
            Protocol = new(Identity, Game, 17, sceneWorldCodec: Codec);
        }
        internal async Task<ControlReply> Send(CommandKind kind, Action<ControlRequest>? configure = null, CancellationToken token = default)
        {
            var request = ControlRequest.Create(Identity, Next, kind); configure?.Invoke(request);
            var result = await Protocol.SendAsync(request, token); Next++; Last = result; return result;
        }
        internal async Task<string> Begin(byte[] bytes, SceneDocument source, Action<ControlRequest>? configure = null)
        {
            var request = SceneWorldProtocolTests.Begin(Identity, Next, Game.RegistryId, bytes, source); configure?.Invoke(request);
            var result = await Protocol.SendAsync(request); Next++; Last = result; _operation = request.SceneLoadId; return _operation;
        }
        internal void StampTransfer(ControlRequest request) { request.SceneLoadId = _operation; request.SceneRegistryId = Game.RegistryId.ToString("D"); }
        internal async Task AppendAll(byte[] bytes, bool mutateRequests = false)
        {
            for (var offset = 0; offset < bytes.Length; offset += 32768)
            {
                var chunk = bytes.AsSpan(offset, Math.Min(32768, bytes.Length - offset)).ToArray();
                await Send(CommandKind.AppendSceneWorld, request => { StampTransfer(request); request.SceneOffset = offset; request.SceneChunk = chunk; });
                if (mutateRequests) Array.Fill(chunk, (byte)0);
            }
        }
        public void Dispose() { Protocol.Dispose(); Game.Dispose(); }
    }
}
