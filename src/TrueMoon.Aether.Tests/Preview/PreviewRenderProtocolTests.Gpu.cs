using System.Numerics;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Gpu;
using TrueMoon.Aether.Runtime.Gpu.Surfaces;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Preview;
using TrueMoon.Aether.Runtime.Rendering.Resources;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Runtime.Transport;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Tests.Rendering.Resources;
using TrueMoon.Aether.Tests.Rendering.Snapshots;
using TrueMoon.Aether.Tooling;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Mithril;
using Xunit;

namespace TrueMoon.Aether.Tests.Preview;

public sealed partial class PreviewRenderProtocolTests
{
    [Fact]
    public async Task MeshPreviewUsesActualStampAndReleasesWorldAfterFrameWork()
    {
        var id = new SessionIdentity(Guid.NewGuid(), 1, new('A', 64), BuildIdentity.PreviewId, new('C', 64));
        var view = new PreviewViewState(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 7), PreviewCamera.Default);
        var assets = ResourceTestFixture.Content();
        var shear = Matrix4x4.Identity;
        shear.M12 = .25f;
        var cube = new RenderPrimitive(Matrix4x4.CreateTranslation(-2, 0, 0), new(.5f, .25f, 1));
        var payload = new PreviewRenderPayload(new(view.Context.SceneId), view.Context.SceneRevision, new GameBuildRenderContent(assets.ProjectId, assets.Entries),
            [new(new(Guid.NewGuid()), shear, RenderContentProfile.CubeAssetId, RenderContentProfile.CubeBindingSetAssetId, true, 2, false, Vector3.One),
             new(new(Guid.NewGuid()), cube.World, Guid.Empty, Guid.Empty, false, 0, false, cube.Color)]);
        var worldCreations = 0;
        var game = new PreviewGameSession(new Factory<World>(() => { worldCreations++; return new(); }));
        var protocol = new RuntimeProtocol(id, game, Environment.ProcessId, role: RuntimeRole.EditPreview) { PreviewSurfaceAvailable = true };
        ulong sequence = 0;
        ControlRequest Next(CommandKind kind) => ControlRequest.Create(id, ++sequence, kind);
        var hello = await protocol.SendAsync(Next(CommandKind.Hello));
        Assert.Equal(PreviewRenderPayload.Version, hello.PreviewRenderVersion);
        Assert.Equal(0, worldCreations);
        var loadId = Guid.NewGuid().ToString("D");
        var begin = Next(CommandKind.BeginPreviewRender);
        begin.SceneLoadId = loadId;
        begin.SceneId = view.Context.SceneId.ToString("D");
        begin.Revision = view.Context.SceneRevision;
        begin.SceneBytes = payload.Length;
        begin.SceneDigest = payload.Digest.ToString();
        begin.PreviewView = PreviewViewCodec.Encode(view);
        await protocol.SendAsync(begin);
        for (var offset = 0; offset < payload.Length;)
        {
            var length = Math.Min(PreviewRenderPayload.ChunkBytes, payload.Length - offset);
            var append = Next(CommandKind.AppendPreviewRender);
            append.SceneLoadId = loadId;
            append.SceneOffset = offset;
            append.SceneChunk = payload.CopyChunk(offset, length);
            await protocol.SendAsync(append);
            offset += length;
        }
        var activate = Next(CommandKind.ActivatePreviewRender);
        activate.SceneLoadId = loadId;
        var loaded = await protocol.SendAsync(activate);
        Assert.Equal("Paused", loaded.Phase);
        Assert.Equal(view.Stamp, PreviewViewCodec.ReadStamp(loaded.PreviewViewStamp!));
        Assert.False(loaded.RenderViewReady);
        Assert.Equal(0, loaded.RenderViewVersion);
        Assert.Equal(1, worldCreations);
        var world = game.ReadRenderScene().World;
        Assert.Equal(id.GameBuildId, game.ReadRenderScene().Content.BuildId.ToString(), ignoreCase: true);

        await using var app = App.Build(context => context.UseAether(options =>
        {
            options.RuntimeFactory = _ => new TrueMoon.Aether.Tests.Replacements.ReplacementRuntimeFixture();
            options.HostContractId = id.HostContractId;
            options.GameBuildId = id.GameBuildId;
            options.ReportDirectory = Path.Combine(Path.GetTempPath(), "TrueMoon-preview-gpu-" + Guid.NewGuid().ToString("N"));
        }));
        await app.StartAsync();
        var lifetime = app.Services.Resolve<IAppLifetime>()!;
        var parent = new UnitParentProcessEventsHandler(new UnitLaunchSettings(), lifetime, app.Services.Resolve<IEventsSource<UnitParentProcessEventsHandler>>()!);
        var server = new PipesInvocationServer<IEngineControl>(app.Services.Resolve<IEventsSource<PipesInvocationServer<IEngineControl>>>()!,
            new EngineControlInvocationServerHandler(protocol, app.Services.Resolve<IEventsSource<EngineControlInvocationServerHandler>>()!), "preview-gpu-" + Guid.NewGuid().ToString("N"));
        await using var meshes = new RuntimeMeshRenderHost(new Factory<RenderResourceHost, IFactory<IRenderResourceBackend, RenderDeviceRequest>>(factory => new(new(), factory)));
        var launch = new SessionLaunch(id, server.Id, Path.GetTempPath()) { Role = RuntimeRole.EditPreview };
        await using var runtime = new AetherRuntimeHost(app.Services.Resolve<RuntimeEngine>()!, lifetime, parent, game, new FrameProducer(), protocol, server, launch, 1, meshes: meshes);
        IGpuSurfaceFrameSource source = runtime;
        var firstBackend = new ResourceTestBackend();
        var firstRequest = new GpuSurfaceRenderRequest(new GpuFrameStamp(id, 1, 1, 0, 2), 320, 180, new ResourceTestBackendFactory(firstBackend));
        var first = await source.CaptureAsync(firstRequest, CancellationToken.None);
        first.Validate(firstRequest.Stamp, firstRequest.Width, firstRequest.Height);
        Assert.Equal(view.Stamp, first.Metadata.PreviewView);
        Assert.Equal(cube, Assert.Single(first.PreviewOverlay));
        Assert.Equal(shear, Assert.Single(first.Snapshot!.Snapshot.Draws).World);
        source.Produced(first); // Synthetic producer completion; never a compositor presentation.
        var produced = await protocol.SendAsync(Next(CommandKind.GetStatus));
        Assert.False(produced.RenderViewReady);
        Assert.Null(first.Metadata.RenderView);
        await first.Snapshot.DisposeAsync();
        await source.DetachAsync();
        Assert.True(firstBackend.IsReleased);
        Assert.False(world.IsDisposed);

        var camera = view.Camera.WithView(Matrix4x4.CreateTranslation(1, 2, 6), view.Camera.Projection, view.Camera.Pivot, 6);
        var nextView = new PreviewViewState(view.Context, camera);
        var update = Next(CommandKind.SetPreviewView);
        update.PreviewView = PreviewViewCodec.Encode(nextView);
        await protocol.SendAsync(update);
        var secondBackend = new ResourceTestBackend { DisposeFailuresRemaining = 1 };
        var resized = new GpuSurfaceRenderRequest(new GpuFrameStamp(id, 2, 1, 0, 2), 640, 360, new ResourceTestBackendFactory(secondBackend));
        var current = await source.CaptureAsync(resized, CancellationToken.None);
        current.Validate(resized.Stamp, resized.Width, resized.Height);
        Assert.Throws<InvalidDataException>(() => current.Validate(firstRequest.Stamp, resized.Width, resized.Height));
        Assert.Equal(nextView.Stamp, current.Metadata.PreviewView);
        Assert.NotEqual(first.Metadata.PreviewView, current.Metadata.PreviewView);
        Assert.Equal(camera.CreateProjection(640, 360).ViewProjection, current.Snapshot!.Snapshot.ViewProjection);
        Assert.Same(world, game.ReadRenderScene().World);
        Assert.Equal(1, worldCreations);
        Assert.Equal(0ul, game.TickNumber);

        var held = new ControlledSubmission();
        var submission = await current.Snapshot.SubmitAsync(new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(_ => held), CancellationToken.None);
        try
        {
            await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var stopping = runtime.DisposeAsync().AsTask();
            await held.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(stopping.IsCompleted);
            Assert.False(world.IsDisposed);
            Assert.False(held.IsReleased);
            held.Complete();
            await Assert.ThrowsAnyAsync<Exception>(() => stopping.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(submission.IsReleased);
            Assert.False(secondBackend.IsReleased);
            Assert.False(world.IsDisposed);
        }
        finally { held.Complete(); }

        await runtime.DisposeAsync();
        Assert.True(secondBackend.IsReleased);
        Assert.True(world.IsDisposed);
    }
}
