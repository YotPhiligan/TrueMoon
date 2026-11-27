using System.Numerics;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Gpu;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Tests.Rendering.Resources;
using TrueMoon.Aether.Tests.Replacements;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Gpu.Surfaces;
using TrueMoon.Aether.Runtime.Rendering.Resources;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Runtime.Transport;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Tooling;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Generated.TrueMoon.Aether;
using TrueMoon.Mithril;
using Xunit;

namespace TrueMoon.Aether.Tests.Runtime;

public sealed class RuntimeMeshRenderHostTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AppRegistration_IsLazy_ReloadSharesGeometry_AndDetachReleasesBeforeBorrowedWorld(bool microsoft)
    {
        var backend = new ResourceTestBackend(); var backends = new ResourceTestBackendFactory(backend);
        await using var app = Builder(microsoft).Setup(context => context.UseAetherSurfaceRendering()).Build();
        await app.StartAsync(); var owner = app.Services.Resolve<RuntimeMeshRenderHost>()!;
        Assert.Equal(0, backends.Calls); Assert.Empty(app.Services.ResolveAll<IRenderResourceBackend>());
        using var world = World(ResourceTestFixture.Content(), out var entity); var source = Source(world, ResourceTestFixture.Content());
        var request = Request(backends);
        var first = await owner.CaptureAsync(source, 0, request, CancellationToken.None);
        var before = first.Metadata.RenderView!; Assert.Equal(1, backend.CreateCalls); await first.Snapshot!.DisposeAsync();
        var changed = ResourceTestFixture.ChangedContent(source.Content); world.SetComponent(entity, new WorldTransform(Matrix4x4.Identity, 1));
        var second = await owner.CaptureAsync(source with { Content = changed }, 1, request with { Stamp = request.Stamp with { FrameId = 2 } }, CancellationToken.None);
        Assert.Equal(1, backend.CreateCalls); Assert.Equal(before.BindingRevision + 1, second.Metadata.RenderView!.BindingRevision);
        Assert.NotEqual(before.ManifestHash, second.Metadata.RenderView.ManifestHash); await second.Snapshot!.DisposeAsync();
        await owner.DetachAsync(); Assert.False(world.IsDisposed); Assert.True(world.IsAlive(entity));
        Assert.Equal(1, backend.DisposeCalls); Assert.True(Assert.Single(backend.CreatedUploads).IsReleased); Assert.False(backend.BackendDisposeBeforeUploads);
        await app.DisposeAsync(); Assert.Equal(1, backend.DisposeCalls);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => owner.CaptureAsync(source, 1, request, CancellationToken.None));
    }
    [Fact]
    public async Task FailedPreparation_RetainsOwnerWhenCleanupFails_AndRetryPreservesPrimaryCauseAndWorld()
    {
        var primary = new InvalidOperationException("native upload marker");
        var backend = new ResourceTestBackend { PrepareError = primary, DisposeFailuresRemaining = 1 };
        var backends = new ResourceTestBackendFactory(backend); var roots = new List<RenderResourceHost>();
        await using var owner = new RuntimeMeshRenderHost(new Factory<RenderResourceHost, IFactory<IRenderResourceBackend, RenderDeviceRequest>>(factory =>
        { var root = new RenderResourceHost(new(), factory); roots.Add(root); return root; }));
        var content = ResourceTestFixture.Content(); using var world = World(content, out var entity);
        var error = await Assert.ThrowsAnyAsync<Exception>(() => owner.CaptureAsync(Source(world, content), 0, Request(backends), CancellationToken.None));
        Assert.Contains(primary.Message, error.ToString());
        await Assert.ThrowsAsync<AggregateException>(() => owner.DetachAsync()); Assert.False(roots[0].IsReleased);
        await owner.DetachAsync(); Assert.True(roots[0].IsReleased); Assert.Equal(2, backend.DisposeCalls);
        Assert.True(world.IsAlive(entity)); Assert.True(Assert.Single(backend.CreatedUploads).IsReleased);
        Assert.Equal(0, roots[0].Content.Counts.LeaseCount);
    }

    [Fact]
    public async Task PreviewHost_UsesExternalProjectionAndRejectsForeignContentEvenAfterDetach()
    {
        var backend = new ResourceTestBackend();
        var backends = new ResourceTestBackendFactory(backend);
        await using var owner = new RuntimeMeshRenderHost(new Factory<RenderResourceHost, IFactory<IRenderResourceBackend, RenderDeviceRequest>>(factory => new(new(), factory)));
        var content = ResourceTestFixture.Content();
        using var world = World(content, out _);
        var viewPose = Matrix4x4.CreateTranslation(0, 1, 6);
        var initial = Source(world, content);
        var projection = new PerspectiveProjection(.82f, .3f, 42);
        var view = new PreviewViewState(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), initial.SceneId.Value, initial.SourceRevision),
            new(viewPose, projection, Vector3.Zero, 6, 3));
        var clear = new Vector3(.04f, .06f, .10f);
        var source = initial with { Camera = RenderCameraSource.External(viewPose, new(.82f, .3f, 42, uint.MaxValue, clear)), CameraRevision = 3 };
        var request = Request(backends);
        var overlay = new RenderPrimitive(Matrix4x4.CreateTranslation(-3, 0, 0), new(0, .5f, 1));
        var frame = await owner.CapturePreviewAsync(source, view, [overlay], request, CancellationToken.None);
        var recording = frame.Snapshot!;
        Assert.Equal(view.Camera.CreateProjection(request.Width, request.Height).ViewProjection, recording.Snapshot.ViewProjection);
        Assert.Equal(clear, recording.Snapshot.ClearColorSrgb);
        Assert.Equal(view.Stamp, frame.Metadata.PreviewView);
        Assert.Null(frame.Metadata.RenderView);
        Assert.Equal(overlay, Assert.Single(frame.PreviewOverlay));
        await recording.DisposeAsync();
        await owner.DetachAsync();
        var changed = new PreviewViewState(view.Context with { ContentId = Guid.NewGuid() }, view.Camera);
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.CapturePreviewAsync(source, changed, [], request, CancellationToken.None));
        Assert.Equal(1, backends.Calls);
        Assert.False(world.IsDisposed);
    }
    [Fact]
    public async Task AppFallback_CancelsPendingUpload_ButRetainsNativeOwnerUntilLateCompletion()
    {
        var backend = new ResourceTestBackend(true) { IgnoreCancellation = true }; var backends = new ResourceTestBackendFactory(backend);
        var roots = new List<RenderResourceHost>();
        var owner = new RuntimeMeshRenderHost(new Factory<RenderResourceHost, IFactory<IRenderResourceBackend, RenderDeviceRequest>>(factory =>
        { var root = new RenderResourceHost(new(), factory); roots.Add(root); return root; }));
        var content = ResourceTestFixture.Content(); using var world = World(content, out _);
        var capture = owner.CaptureAsync(Source(world, content), 0, Request(backends), CancellationToken.None);
        var upload = await backend.WaitForUploadAsync(1); await upload.PrepareEntered.Task;
        var stop = owner.DisposeAsync().AsTask(); await upload.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(stop.IsCompleted); Assert.False(upload.IsReleased); Assert.False(backend.IsReleased); Assert.False(world.IsDisposed);
        upload.Complete(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture); await stop.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(upload.IsReleased); Assert.True(roots[0].IsReleased); Assert.False(upload.DisposedBeforePrepareExit); Assert.False(world.IsDisposed);
    }
    [Fact]
    public async Task RuntimeCleanupFailure_KeepsBorrowedWorldAndProtocolUntilMeshRetirementRetry()
    {
        await using var app = App.Build(context => context.UseAether(options =>
        {
            options.RuntimeFactory = _ => new ReplacementRuntimeFixture(); options.HostContractId = new('A', 64); options.GameBuildId = new('B', 64);
            options.ReportDirectory = Path.Combine(Path.GetTempPath(), "TrueMoon-mesh-cleanup-" + Guid.NewGuid().ToString("N"));
        }));
        await app.StartAsync(); var lifetime = app.Services.Resolve<IAppLifetime>()!;
        var backend = new ResourceTestBackend { DisposeFailuresRemaining = 1 }; var backends = new ResourceTestBackendFactory(backend);
        await using var meshes = new RuntimeMeshRenderHost(new Factory<RenderResourceHost, IFactory<IRenderResourceBackend, RenderDeviceRequest>>(factory => new(new(), factory)));
        var content = ResourceTestFixture.Content(); var world = World(content, out _);
        var data = await meshes.CaptureAsync(Source(world, content), 0, Request(backends), CancellationToken.None); await data.Snapshot!.DisposeAsync();
        var game = new WorldOwner(world); var identity = Request(backends).Stamp.Identity;
        var protocol = new RuntimeProtocol(identity, game, Environment.ProcessId);
        var parent = new UnitParentProcessEventsHandler(new UnitLaunchSettings(), lifetime, app.Services.Resolve<IEventsSource<UnitParentProcessEventsHandler>>()!);
        var server = new PipesInvocationServer<IEngineControl>(app.Services.Resolve<IEventsSource<PipesInvocationServer<IEngineControl>>>()!,
            new EngineControlInvocationServerHandler(protocol, app.Services.Resolve<IEventsSource<EngineControlInvocationServerHandler>>()!), "mesh-cleanup-" + Guid.NewGuid().ToString("N"));
        var launch = new SessionLaunch(identity, server.Id, Path.GetTempPath());
        var runtime = new AetherRuntimeHost(app.Services.Resolve<TrueMoon.Aether.Runtime.Hosting.RuntimeEngine>()!, lifetime, parent, game, new FrameProducer(), protocol, server, launch, 1, meshes: meshes);
        await Assert.ThrowsAnyAsync<Exception>(() => runtime.DisposeAsync().AsTask());
        Assert.False(world.IsDisposed); Assert.Equal(0, game.Disposals); Assert.False(backend.IsReleased);
        Assert.Equal("HelloAccepted", (await protocol.SendAsync(ControlRequest.Create(identity, 1, CommandKind.Hello))).Phase);
        await runtime.DisposeAsync(); Assert.True(world.IsDisposed); Assert.Equal(1, game.Disposals); Assert.True(backend.IsReleased);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => protocol.SendAsync(ControlRequest.Create(identity, 2, CommandKind.GetStatus)));
        await runtime.DisposeAsync(); Assert.Equal(1, game.Disposals);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DuplicateModuleAndInvalidLimits_AreRejected(bool microsoft)
    {
        Assert.Throws<InvalidOperationException>(() => Builder(microsoft).Setup(context => { context.UseAetherSurfaceRendering(); context.UseAetherSurfaceRendering(); }).Build());
        Assert.Throws<ArgumentOutOfRangeException>(() => Builder(microsoft).Setup(context => context.UseAetherSurfaceRendering(options => options.MaximumGeometryBytes = -1)).Build());
    }
    private static IAppBuilder Builder(bool microsoft) => App.Builder(context => { if (microsoft) context.UseDI(); });
    private static GpuSurfaceRenderRequest Request(IFactory<IRenderResourceBackend, RenderDeviceRequest> backends) => new(
        new(new(ResourceTestFixture.Session, 1, new('A', 64), new('B', 64), new('C', 64)), 1, 1, 0, 2), 320, 180, backends);
    private static RenderSceneSource Source(World world, RenderContentManifest content) => new(world, content, new(Guid.NewGuid()), 7,
        RenderCameraSource.External(Matrix4x4.CreateTranslation(0, 0, 6), PerspectiveCamera.Default));
    private static World World(RenderContentManifest content, out EntityHandle entity)
    {
        var world = new World(); entity = world.CreateEntity();
        world.AddComponent(entity, new WorldTransform(Matrix4x4.Identity, 0));
        world.AddComponent(entity, new MeshRenderer(content.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId).Key,
            content.Resolve(RenderContentProfile.CubeBindingSetAssetId, RenderContentProfile.BindingSetKindId).Key, true, uint.MaxValue, false, Vector3.One));
        return world;
    }
    private sealed class WorldOwner(World world) : IGameSession
    {
        internal int Disposals;
        public ulong TickNumber => 0;
        public long Revision => -1;
        public uint PatternColor => 0;
        public void Load(SceneSnapshot scene) { }
        public void Tick() { }
        public void Dispose() { world.Dispose(); Disposals++; }
    }
}
