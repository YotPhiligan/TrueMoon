using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering.Resources;

public sealed class RenderResourceAppTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Registration_IsLazyPreservesBackendFactoryAndUsesNonOwningFactoryAliases(bool di)
    {
        var backend = new ResourceTestBackend(); var backends = new ResourceTestBackendFactory(backend);
        var app = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<IRenderResourceBackend, RenderDeviceRequest>>(_ => backends));
            Assert.Same(context, context.UseAetherRenderResources());
        }).Build();
        try
        {
            Assert.Equal(0, backends.Calls); await app.StartAsync(); Assert.Equal(0, backends.Calls);
            var host = Assert.Single(app.Services.ResolveAll<RenderResourceHost>());
            var scopes = Assert.Single(app.Services.ResolveAll<IFactory<RenderManifestLease, RenderContentManifest>>());
            var devices = Assert.Single(app.Services.ResolveAll<IFactory<RenderDeviceOwner, RenderDeviceRequest>>());
            var preparations = Assert.Single(app.Services.ResolveAll<IFactory<RenderPreparation, RenderPreparationRequest>>());
            var bindings = Assert.Single(app.Services.ResolveAll<IFactory<RenderWorldBinding, RenderWorldBindingRequest>>());
            Assert.Same(backends, Assert.Single(app.Services.ResolveAll<IFactory<IRenderResourceBackend, RenderDeviceRequest>>()));
            Assert.Empty(app.Services.ResolveAll<IRenderResourceBackend>());
            Assert.Empty(app.Services.ResolveAll<RenderContentCatalog>());
            Assert.Empty(app.Services.ResolveAll<RenderDeviceOwner>());
            Assert.Empty(app.Services.ResolveAll<RenderPreparation>());
            foreach (var factory in new object[] { scopes, devices, preparations, bindings })
            { Assert.NotSame(host, factory); Assert.False(factory is IDisposable); Assert.False(factory is IAsyncDisposable); }
            var content = ResourceTestFixture.Content();
            using var scope = scopes.Create(content);
            var device = devices.Create(ResourceTestFixture.Request(content));
            Assert.Equal(0, backends.Calls);
            var prepared = preparations.Create(new(device, scope)); await prepared.Completion;
            Assert.Equal(RenderPreparationState.Ready, prepared.State);
            Assert.Equal(1, backends.Calls); Assert.Equal(1, backend.CreateCalls);
            await prepared.DisposeAsync(); await device.DisposeAsync();
            Assert.Equal(0, host.DeviceCount); Assert.Equal(1, backend.DisposeCalls);
            Assert.True(prepared.IsReleased); Assert.True(device.IsReleased);
            Assert.Equal(0L, device.AllocationBudget.Counts.GeometryBytes);
        }
        finally { await app.DisposeAsync(); }
        Assert.Equal(1, backend.DisposeCalls);
        Assert.All(backend.CreatedUploads, upload => Assert.Equal(1, upload.DisposeCalls));
        Assert.False(backend.BackendDisposeBeforeUploads);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AppShutdown_ReleasesOutstandingFactoryProductsBeforeCatalogExactlyOnce(bool di)
    {
        var backend = new ResourceTestBackend(); var backends = new ResourceTestBackendFactory(backend);
        var app = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<IRenderResourceBackend, RenderDeviceRequest>>(_ => backends));
            context.UseAetherRenderResources();
        }).Build();
        RenderResourceHost? host = null;
        try
        {
            await app.StartAsync(); host = app.Services.Resolve<RenderResourceHost>()!;
            var content = ResourceTestFixture.Content(); var scope = host.Content.Acquire(content);
            var device = host.Create(ResourceTestFixture.Request(content));
            var prepared = host.Create(new RenderPreparationRequest(device, scope));
            await prepared.Completion; scope.Dispose();
            Assert.Equal(1, host.Content.Counts.LeaseCount);
            await app.DisposeAsync();
            Assert.True(prepared.IsReleased); Assert.True(device.IsReleased); Assert.True(host.IsReleased);
            Assert.True(host.Content.IsReleased); Assert.Equal(0, host.Content.Counts.RevisionCount);
            Assert.Equal(0, host.Content.Counts.LeaseCount); Assert.Equal(0L, device.AllocationBudget.Counts.GeometryBytes);
            Assert.Equal(new[] { "upload:1", "backend" }, backend.DisposalLog.ToArray());
            await prepared.DisposeAsync(); await device.DisposeAsync(); await host.DisposeAsync();
            Assert.Equal(1, backend.DisposeCalls); Assert.Equal(1, Assert.Single(backend.CreatedUploads).DisposeCalls);
        }
        finally { if (host is not { IsReleased: true }) await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Registration_CopiesOptionsBeforeLazyHostResolutionAndRejectsDuplicateModule(bool di)
    {
        RenderResourceOptions? supplied = null;
        var app = Builder(di).Setup(context => context.UseAetherRenderResources(options =>
        { supplied = options; options.MaximumCpuLeases = 2; })).Build();
        supplied!.MaximumCpuLeases = 16384;
        try
        {
            await app.StartAsync(); var host = app.Services.Resolve<RenderResourceHost>()!;
            var content = ResourceTestFixture.Content();
            using var first = host.Content.Acquire(content); using var second = host.Content.Acquire(content);
            var error = Assert.Throws<RenderResourceException>(() => host.Content.Acquire(content));
            Assert.Equal(RenderResourceError.LimitExceeded, error.Error);
            Assert.Equal(2, host.Content.Counts.LeaseCount); Assert.Equal(1, host.Content.Counts.ScopeCount);
        }
        finally { await app.DisposeAsync(); }
        Assert.Throws<InvalidOperationException>(() => Builder(di).Setup(context =>
        { context.UseAetherRenderResources(); context.UseAetherRenderResources(); }).Build());
        Assert.Throws<ArgumentOutOfRangeException>(() => Builder(di).Setup(context =>
            context.UseAetherRenderResources(options => options.MaximumGeometryBytes = -1)).Build());
    }

    [Fact]
    public async Task Stop_ClosesCpuFactoriesAndSceneAdmissionBeforePendingNativeDrainCompletes()
    {
        var backend = new ResourceTestBackend(holdUploads: true) { IgnoreCancellation = true };
        var factory = new ResourceTestBackendFactory(backend);
        var app = Builder(false).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<IRenderResourceBackend, RenderDeviceRequest>>(_ => factory));
            context.UseAetherRenderResources();
        }).Build();
        RenderManifestLease? scope = null; ResourceTestUpload? upload = null;
        try
        {
            await app.StartAsync(); var host = app.Services.Resolve<RenderResourceHost>()!;
            var scopes = app.Services.Resolve<IFactory<RenderManifestLease, RenderContentManifest>>()!;
            var content = ResourceTestFixture.Content(); scope = scopes.Create(content);
            var device = host.Create(ResourceTestFixture.Request(content));
            var operation = host.Create(new RenderPreparationRequest(device, scope));
            upload = await backend.WaitForUploadAsync(1); await upload.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var waitCancellation = new CancellationTokenSource(); var stopping = host.StopAsync(waitCancellation.Token);
            waitCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stopping.WaitAsync(TimeSpan.FromSeconds(10)));
            await upload.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(host.Content.Counts.AdmissionClosed); Assert.False(host.Content.IsReleased);
            Assert.False(host.IsReleased); Assert.False(device.IsReleased); Assert.False(upload.IsReleased);
            Assert.Equal(2, host.Content.Counts.LeaseCount); Assert.Equal(256L, device.AllocationBudget.Counts.GeometryBytes);
            var rejected = Assert.Throws<RenderResourceException>(() => scopes.Create(content));
            Assert.Equal(RenderResourceError.Closed, rejected.Error);
            var sceneRejected = Assert.Throws<RenderResourceException>(() => RenderResourceComponents.Register(PrimitiveEcsComponents.Register(new()), scope));
            Assert.Equal(RenderResourceError.Closed, sceneRejected.Error); Assert.Equal(2, host.Content.Counts.LeaseCount);
            scope.Dispose(); upload.Complete();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.Completion.WaitAsync(TimeSpan.FromSeconds(10)));
            await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(host.IsReleased); Assert.True(device.IsReleased); Assert.True(host.Content.IsReleased);
            Assert.Equal(0, host.Content.Counts.LeaseCount); Assert.Equal(0, host.Content.Counts.RevisionCount);
            Assert.Equal(0L, device.AllocationBudget.Counts.GeometryBytes); Assert.Equal(1, upload.DisposeCalls); Assert.Equal(1, backend.DisposeCalls);
        }
        finally { scope?.Dispose(); upload?.Complete(); await app.DisposeAsync(); }
    }

    [Fact]
    public async Task HostCleanup_KeepsLiveCpuScopeAndRetriesOnlyFailedCatalogStage()
    {
        var backend = new ResourceTestBackend(); var catalog = new RenderContentCatalog();
        var host = new RenderResourceHost(catalog, new ResourceTestBackendFactory(backend));
        var content = ResourceTestFixture.Content(); var scope = catalog.Acquire(content);
        var device = host.Create(ResourceTestFixture.Request(content));
        var prepared = host.Create(new RenderPreparationRequest(device, scope)); await prepared.Completion;
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.DisposeAsync().AsTask());
        Assert.False(host.IsReleased); Assert.False(catalog.IsReleased); Assert.True(catalog.Counts.AdmissionClosed);
        Assert.True(device.IsReleased); Assert.True(prepared.IsReleased); Assert.Equal(1, catalog.Counts.LeaseCount);
        Assert.Equal(1, backend.DisposeCalls); Assert.Equal(1, Assert.Single(backend.CreatedUploads).DisposeCalls);
        scope.Dispose(); await host.DisposeAsync(); await host.DisposeAsync();
        Assert.True(host.IsReleased); Assert.True(catalog.IsReleased); Assert.Equal(0, catalog.Counts.LeaseCount);
        Assert.Equal(1, backend.DisposeCalls);
    }

    [Fact]
    public async Task Host_RejectsForeignDeviceProductsAndRequiresOldDeviceReleaseBeforeReplacement()
    {
        var content = ResourceTestFixture.Content();
        await using var first = new RenderResourceHost(new(), new ResourceTestBackendFactory(new()));
        await using var second = new RenderResourceHost(new(), new ResourceTestBackendFactory(new()));
        using var scope = first.Content.Acquire(content);
        var device = first.Create(ResourceTestFixture.Request(content));
        var foreign = Assert.Throws<RenderResourceException>(() => second.Create(new RenderPreparationRequest(device, scope)));
        Assert.Equal(RenderResourceError.InvalidIdentity, foreign.Error);
        Assert.Equal(0, second.DeviceCount);
        var full = Assert.Throws<RenderResourceException>(() => first.Create(ResourceTestFixture.Request(content, generation: 2)));
        Assert.Equal(RenderResourceError.LimitExceeded, full.Error); Assert.Equal(1, first.DeviceCount);
        await device.DisposeAsync();
        var replacement = first.Create(ResourceTestFixture.Request(content, generation: 2));
        Assert.NotEqual(device.Id, replacement.Id); Assert.Equal(2UL, replacement.Generation);
        Assert.Equal(1, first.DeviceCount); await replacement.DisposeAsync();
        Assert.Equal(0, first.DeviceCount);
    }

    private static IAppBuilder Builder(bool di) => App.Builder(context => { if (di) context.UseDI(); });
}
