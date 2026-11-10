using System.Numerics;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using TrueMoon.Aether.Rendering.Snapshots;
using TrueMoon.Aether.Tests.Rendering.Resources;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering.Snapshots;

public sealed class SnapshotLifecycleTests
{
    [Fact]
    public async Task LastMeshCleanupFailureRetainsSnapshotPayloadCpuLeaseAndAllocationUntilRetry()
    {
        await using var t = await SnapshotFixture.CreateAsync(); var data = await t.CaptureAsync();
        await t.Binding.DisposeAsync(); var upload = t.Backend.CreatedUploads[0]; upload.DisposeFailuresRemaining = 1;
        await Assert.ThrowsAsync<AggregateException>(() => t.View.DisposeAsync().AsTask());
        Assert.False(t.View.IsReleased); Assert.Equal(1, t.View.Counts.Snapshots); Assert.Equal(data.PayloadBytes, t.View.Counts.PayloadBytes);
        Assert.Equal(2, t.Host.Content.Counts.LeaseCount); Assert.Equal(1, t.Device.Counts.EntryCount);
        Assert.Equal(256L, t.Device.AllocationBudget.Counts.GeometryBytes); Assert.False(upload.IsReleased); Assert.Equal(1, upload.DisposeCalls);
        Assert.NotNull(t.View.CleanupError); Assert.Null(t.View.Current);
        await t.View.DisposeAsync(); Assert.True(t.View.IsReleased); Assert.Equal(0, t.View.Counts.PayloadBytes); Assert.Equal(0, t.View.Counts.Snapshots);
        Assert.Equal(1, t.Host.Content.Counts.LeaseCount); Assert.Equal(0, t.Device.Counts.EntryCount);
        Assert.Equal(0L, t.Device.AllocationBudget.Counts.GeometryBytes); Assert.True(upload.IsReleased); Assert.Equal(2, upload.DisposeCalls); Assert.Null(t.View.CleanupError);
    }

    [Fact]
    public async Task SuspensionAndEarlyDeviceStopCannotFreeResourcesStillUsedByAcceptedSubmission()
    {
        await using var t = await SnapshotFixture.CreateAsync(); var data = await t.CaptureAsync(); var owner = new ControlledSubmission();
        var use = await t.View.SubmitAsync((await t.View.TakeLatestAsync())!, new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(_ => owner));
        await owner.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await t.View.CaptureAsync(0, t.Configuration with { Width = 0, Height = 0, SurfaceGeneration = 2 });
            Assert.Null(t.View.Current); Assert.False(t.View.Counts.HasPending); Assert.Equal(1, t.View.Counts.Snapshots);
            Assert.False(owner.Cancelled.Task.IsCompleted); Assert.False(use.Completion.IsCompleted);
            await t.Binding.DisposeAsync(); Assert.Equal(2, t.Host.Content.Counts.LeaseCount);
            var retained = await Assert.ThrowsAsync<RenderResourceException>(() => t.Device.DisposeAsync().AsTask()); Assert.Equal(RenderResourceError.CleanupFailed, retained.Error);
            Assert.True(t.View.Counts.Closed);
            Assert.False(t.Device.IsReleased); Assert.Equal(0, t.Backend.DisposeCalls); Assert.Equal(0, t.Backend.CreatedUploads[0].DisposeCalls);
            Assert.Equal(data.PayloadBytes, t.View.Counts.PayloadBytes); Assert.Equal(256L, t.Device.AllocationBudget.Counts.GeometryBytes);
            owner.Complete(); await use.Completion.WaitAsync(TimeSpan.FromSeconds(10)); await t.View.CollectAsync();
            Assert.True(use.IsReleased); Assert.Equal(0, t.View.Counts.PayloadBytes); Assert.Equal(1, t.Host.Content.Counts.LeaseCount);
            Assert.Equal(1, t.Backend.CreatedUploads[0].DisposeCalls); await t.Device.DisposeAsync(); Assert.True(t.Device.IsReleased);
        }
        finally { owner.Complete(); await use.DisposeAsync(); }
    }
    [Fact]
    public async Task HostStopRetainsDependenciesWhenSubmissionCleanupFailsThenRetriesInOwnershipOrder()
    {
        await using var t = await SnapshotFixture.CreateAsync(); await t.CaptureAsync(); var owner = new ControlledSubmission { CleanupFailures = 1 };
        var use = await t.View.SubmitAsync((await t.View.TakeLatestAsync())!, new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(_ => owner));
        await owner.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); owner.Complete(); await use.Completion;
        t.Scope.Dispose();
        await Assert.ThrowsAsync<AggregateException>(() => t.Host.DisposeAsync().AsTask());
        Assert.False(t.Host.IsReleased); Assert.False(t.View.IsReleased); Assert.False(t.Binding.IsReleased); Assert.False(t.Device.IsReleased);
        Assert.NotNull(t.Binding.Current); Assert.Equal(0, t.Backend.DisposeCalls); Assert.Equal(0, t.Backend.CreatedUploads[0].DisposeCalls);
        Assert.Equal(1, t.View.Counts.Submissions); Assert.True(t.Host.Content.Counts.LeaseCount >= 2);
        await t.Host.DisposeAsync();
        Assert.True(t.Host.IsReleased); Assert.True(t.View.IsReleased); Assert.True(t.Binding.IsReleased); Assert.True(t.Device.IsReleased);
        Assert.True(use.IsReleased); Assert.Equal(2, owner.CleanupCalls); Assert.Equal(1, t.Backend.DisposeCalls);
        Assert.Equal(0, t.Host.Content.Counts.LeaseCount); Assert.Equal(0, t.Device.AllocationBudget.Counts.GeometryBytes); Assert.False(t.World.IsDisposed);
    }
    [Fact]
    public async Task CleanupSuccessWithoutBackendReleaseProofRemainsCountedUntilRetry()
    {
        await using var t = await SnapshotFixture.CreateAsync(); await t.CaptureAsync(); var owner = new ControlledSubmission { ReportReleased = false };
        var use = await t.View.SubmitAsync((await t.View.TakeLatestAsync())!, new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(_ => owner));
        await owner.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); owner.Complete(); await use.Completion;
        var failure = await Assert.ThrowsAsync<AggregateException>(() => t.View.CollectAsync());
        Assert.Contains(failure.Flatten().InnerExceptions, error => error is RenderResourceException { Error: RenderResourceError.CleanupFailed });
        Assert.False(use.IsReleased); Assert.Equal(1, t.View.Counts.Submissions); Assert.Equal(3, t.Host.Content.Counts.LeaseCount);
        owner.ReportReleased = true; await t.View.CollectAsync();
        Assert.True(use.IsReleased); Assert.Equal(0, t.View.Counts.Submissions); Assert.Equal(2, owner.CleanupCalls);
    }
    [Fact]
    public async Task StopCancelsCaptureWaitingBehindResourceReloadWithoutWaitingForUnrelatedUpload()
    {
        await using var t = await SnapshotFixture.CreateAsync(); await t.CaptureAsync();
        var upload = new GatedUpload(); t.Backend.CreateOverride = (_, _) => upload;
        using var candidate = t.Host.Content.Acquire(ResourceTestFixture.ChangedContent(t.Content, changeMesh: true));
        var reload = t.Binding.ReloadAsync(candidate);
        await upload.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var capture = t.CaptureAsync(1);
        try
        {
            Assert.False(capture.IsCompleted); await t.View.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture);
            Assert.True(t.View.IsReleased); Assert.False(reload.IsCompleted); Assert.Equal(0, t.View.Counts.PayloadBytes);
            Assert.Equal(1UL, t.Binding.Revision); Assert.False(upload.IsReleased);
        }
        finally { upload.Complete(); await reload.WaitAsync(TimeSpan.FromSeconds(10)); }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AppRegistersOneLazyNonOwningViewFactoryAndPreservesProviderLifecycle(bool di)
    {
        var backend = new ResourceTestBackend(); var factory = new ResourceTestBackendFactory(backend);
        var app = App.Builder(context => { if (di) context.UseDI(); }).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<IRenderResourceBackend, RenderDeviceRequest>>(_ => factory));
            context.UseAetherEcs(); context.UseAetherRenderResources();
        }).Build();
        World? world = null; RenderManifestLease? scope = null;
        try
        {
            await app.StartAsync(); var views = Assert.Single(app.Services.ResolveAll<IFactory<RenderSnapshotView, RenderSnapshotViewRequest>>());
            Assert.False(views is IDisposable); Assert.False(views is IAsyncDisposable); Assert.Equal(0, factory.Calls);
            var host = app.Services.Resolve<RenderResourceHost>()!; var content = ResourceTestFixture.Content();
            scope = app.Services.Resolve<IFactory<RenderManifestLease, RenderContentManifest>>()!.Create(content);
            world = app.Services.Resolve<IFactory<World>>()!.Create() ?? throw new InvalidOperationException("World factory returned null.");
            var device = app.Services.Resolve<IFactory<RenderDeviceOwner, RenderDeviceRequest>>()!.Create(ResourceTestFixture.Request(content, world.Id));
            var binding = app.Services.Resolve<IFactory<RenderWorldBinding, RenderWorldBindingRequest>>()!.Create(new(device, world!, scope));
            var view = views.Create(new(binding, new(Guid.NewGuid()), 5)); Assert.Equal(0, factory.Calls);
            var notReady = await Assert.ThrowsAsync<RenderResourceException>(() => view.CaptureAsync(0,
                new(RenderCameraSource.External(Matrix4x4.Identity, PerspectiveCamera.Default), 640, 360)));
            Assert.Equal(RenderResourceError.Closed, notReady.Error); Assert.Equal(0, view.Counts.Snapshots);
            await binding.InitializeAsync(); // empty World but content closure still prepares the explicit catalog
            var data = await view.CaptureAsync(0, new(RenderCameraSource.External(Matrix4x4.Identity, PerspectiveCamera.Default), 640, 360));
            Assert.Empty(data!.Draws); Assert.Equal(1, factory.Calls); Assert.Equal(256, data.PayloadBytes);
            var duplicate = Assert.Throws<RenderResourceException>(() => views.Create(new(binding, new(Guid.NewGuid()), 5))); Assert.Equal(RenderResourceError.LimitExceeded, duplicate.Error);
            scope.Dispose(); scope = null; await app.DisposeAsync();
            Assert.True(view.IsReleased); Assert.True(binding.IsReleased); Assert.True(device.IsReleased); Assert.True(host.IsReleased);
            Assert.Equal(0, view.Counts.PayloadBytes); Assert.Equal(0, host.Content.Counts.LeaseCount); Assert.False(world.IsDisposed);
            Assert.Equal(1, backend.DisposeCalls); Assert.All(backend.CreatedUploads, upload => Assert.Equal(1, upload.DisposeCalls));
        }
        finally { scope?.Dispose(); await app.DisposeAsync(); world?.Dispose(); }
    }
    private sealed class GatedUpload : IRenderMeshUpload
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsReady { get; private set; }
        public bool IsReleased { get; private set; }
        internal void Complete() => _ready.TrySetResult();
        public async Task PrepareAsync(CancellationToken cancellationToken) { Entered.TrySetResult(); await _ready.Task.WaitAsync(cancellationToken); IsReady = true; }
        public ValueTask DisposeAsync() { IsReleased = true; return ValueTask.CompletedTask; }
    }
}
