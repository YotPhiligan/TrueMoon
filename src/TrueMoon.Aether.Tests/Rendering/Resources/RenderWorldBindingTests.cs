using System.Numerics;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering.Resources;

public sealed class RenderWorldBindingTests
{
    [Fact]
    public async Task InitializeAndReload_PublishOnlyAfterReadyAndAtomicallyRebindEveryRendererWithoutChangingOtherState()
    {
        var oldContent = ResourceTestFixture.Content(); var newContent = ResourceTestFixture.ChangedContent(oldContent, changeMesh: true);
        using var world = World(oldContent, out var first, out var second);
        var backend = new ResourceTestBackend(holdUploads: true);
        await using var host = new RenderResourceHost(new(), new ResourceTestBackendFactory(backend));
        var oldScope = host.Content.Acquire(oldContent); var newScope = host.Content.Acquire(newContent);
        var device = host.Create(ResourceTestFixture.Request(oldContent, world.Id));
        var binding = host.Create(new RenderWorldBindingRequest(device, world, oldScope));
        var originalFirst = world.GetComponent<MeshRenderer>(first); var originalSecond = world.GetComponent<MeshRenderer>(second);
        Assert.Null(binding.Current); Assert.Equal(0UL, binding.Revision); Assert.Equal(0, backend.CreateCalls);
        var initializing = binding.InitializeAsync(); var upload = await backend.WaitForUploadAsync(1);
        await upload.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(binding.Current); Assert.Equal(0UL, binding.Revision); Assert.False(initializing.IsCompleted);
        upload.Complete(); await initializing.WaitAsync(TimeSpan.FromSeconds(10));
        var previous = binding.Current!; Assert.Equal(RenderPreparationState.Ready, previous.State); Assert.Equal(1UL, binding.Revision);
        oldScope.Dispose();
        var reloading = binding.ReloadAsync(newScope); var replacementUpload = await backend.WaitForUploadAsync(2);
        await replacementUpload.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Same(previous, binding.Current); Assert.Equal(1UL, binding.Revision); Assert.False(reloading.IsCompleted);
        Assert.Equal(originalFirst, world.GetComponent<MeshRenderer>(first)); Assert.Equal(originalSecond, world.GetComponent<MeshRenderer>(second));
        Assert.Equal(512L, device.AllocationBudget.Counts.GeometryBytes);
        world.SetComponent(second, new UnrelatedState(91));
        replacementUpload.Complete(); var result = await reloading.WaitAsync(TimeSpan.FromSeconds(10)); newScope.Dispose();
        Assert.True(result.Committed); Assert.Null(result.CleanupError); Assert.Equal(2UL, binding.Revision);
        Assert.NotSame(previous, binding.Current); Assert.True(previous.IsReleased);
        var newMesh = newContent.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId).Key;
        var newBindings = newContent.Resolve(RenderContentProfile.CubeBindingSetAssetId, RenderContentProfile.BindingSetKindId).Key;
        Assert.Equal(originalFirst with { Mesh = newMesh, Bindings = newBindings }, world.GetComponent<MeshRenderer>(first));
        Assert.Equal(originalSecond with { Mesh = newMesh, Bindings = newBindings }, world.GetComponent<MeshRenderer>(second));
        Assert.Equal(new UnrelatedState(91), world.GetComponent<UnrelatedState>(second));
        Assert.Equal(new Vector3(2, 0, 0), world.GetComponent<LocalTransform>(second).Position);
        Assert.Equal(newContent.Digest, binding.Current!.Manifest.Digest);
        Assert.Equal(newMesh, device.ValidateHandle(binding.Current.GetMeshHandle(newMesh)));
        Assert.Equal(1, upload.DisposeCalls); Assert.Equal(0, replacementUpload.DisposeCalls);
        Assert.Equal(1, device.Counts.EntryCount); Assert.Equal(256L, device.AllocationBudget.Counts.GeometryBytes);
        Assert.Equal(3, host.Content.Counts.RevisionCount); Assert.Equal(1, host.Content.Counts.LeaseCount);
        await binding.DisposeAsync(); await binding.DisposeAsync();
        Assert.True(binding.IsReleased); Assert.Null(binding.Current); Assert.False(world.IsDisposed);
        Assert.Equal(0, host.Content.Counts.LeaseCount); Assert.Equal(0, device.Counts.EntryCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Reload_ChangedMembershipOrRendererValueDuringPreparationRejectsCandidateAndPreservesCurrent(bool membership)
    {
        var content = ResourceTestFixture.Content(); var candidate = ResourceTestFixture.ChangedContent(content, changeMesh: true);
        using var world = World(content, out var first, out _);
        var backend = new ResourceTestBackend(holdUploads: true);
        await using var host = new RenderResourceHost(new(), new ResourceTestBackendFactory(backend));
        using var oldScope = host.Content.Acquire(content); using var candidateScope = host.Content.Acquire(candidate);
        var device = host.Create(ResourceTestFixture.Request(content, world.Id));
        await using var binding = host.Create(new RenderWorldBindingRequest(device, world, oldScope));
        var initialize = binding.InitializeAsync(); (await backend.WaitForUploadAsync(1)).Complete(); await initialize;
        var current = binding.Current; var original = world.GetComponent<MeshRenderer>(first);
        var reload = binding.ReloadAsync(candidateScope); var upload = await backend.WaitForUploadAsync(2);
        await upload.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (membership)
        {
            world.DestroyEntity(first); var replacement = world.CreateEntity();
            world.AddComponent(replacement, original); first = replacement;
        }
        else world.SetComponent(first, original with { TintLinear = new(.125f, .25f, .5f) });
        var changed = world.GetComponent<MeshRenderer>(first);
        upload.Complete();
        var error = await Assert.ThrowsAsync<RenderResourceException>(() => reload.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(RenderResourceError.Conflict, error.Error); Assert.Same(current, binding.Current); Assert.Equal(1UL, binding.Revision);
        Assert.Equal(changed, world.GetComponent<MeshRenderer>(first)); Assert.Equal(original.Mesh, changed.Mesh);
        Assert.True(upload.IsReleased); Assert.Equal(1, upload.DisposeCalls);
        Assert.Equal(1, device.Counts.EntryCount); Assert.Equal(256L, device.AllocationBudget.Counts.GeometryBytes);
        Assert.False(world.IsFaulted); Assert.False(current!.IsReleased);
    }

    [Fact]
    public async Task Reload_ChangedPinnedKeyReportsConflictBeforeRevalidatingAgainstTheOldManifest()
    {
        var content = ResourceTestFixture.Content(); var candidate = ResourceTestFixture.ChangedContent(content, changeMesh: true);
        using var world = World(content, out var first, out _); var backend = new ResourceTestBackend(holdUploads: true);
        await using var host = new RenderResourceHost(new(), new ResourceTestBackendFactory(backend));
        using var scope = host.Content.Acquire(content); using var candidateScope = host.Content.Acquire(candidate);
        var device = host.Create(ResourceTestFixture.Request(content, world.Id));
        await using var binding = host.Create(new RenderWorldBindingRequest(device, world, scope));
        var initialize = binding.InitializeAsync(); (await backend.WaitForUploadAsync(1)).Complete(); await initialize;
        var current = binding.Current!; var original = world.GetComponent<MeshRenderer>(first);
        var handle = current.GetMeshHandle(original.Mesh);
        var reload = binding.ReloadAsync(candidateScope); var upload = await backend.WaitForUploadAsync(2);
        await upload.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var changed = original with { Mesh = candidate.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId).Key };
        world.SetComponent(first, changed); upload.Complete();
        var error = await Assert.ThrowsAsync<RenderResourceException>(() => reload.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(RenderResourceError.Conflict, error.Error); Assert.Same(current, binding.Current); Assert.Equal(1UL, binding.Revision);
        Assert.Equal(changed, world.GetComponent<MeshRenderer>(first)); Assert.False(world.IsFaulted);
        Assert.Equal(original.Mesh, device.ValidateHandle(handle)); Assert.True(upload.IsReleased);
        Assert.Equal(1, device.Counts.EntryCount); Assert.Equal(256L, device.AllocationBudget.Counts.GeometryBytes);
    }

    [Fact]
    public async Task Reload_CancellationDrainsCandidateAndLeavesOldManifestKeysAndHandleLive()
    {
        var content = ResourceTestFixture.Content(); var candidate = ResourceTestFixture.ChangedContent(content, changeMesh: true);
        using var world = World(content, out var first, out _); var backend = new ResourceTestBackend(holdUploads: true);
        await using var host = new RenderResourceHost(new(), new ResourceTestBackendFactory(backend));
        using var oldScope = host.Content.Acquire(content); using var candidateScope = host.Content.Acquire(candidate);
        var device = host.Create(ResourceTestFixture.Request(content, world.Id));
        await using var binding = host.Create(new RenderWorldBindingRequest(device, world, oldScope));
        var initialize = binding.InitializeAsync(); (await backend.WaitForUploadAsync(1)).Complete(); await initialize;
        var old = binding.Current!; var renderer = world.GetComponent<MeshRenderer>(first); var handle = old.GetMeshHandle(renderer.Mesh);
        using var cancel = new CancellationTokenSource(); var reload = binding.ReloadAsync(candidateScope, cancel.Token);
        var upload = await backend.WaitForUploadAsync(2); await upload.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reload.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Same(old, binding.Current); Assert.Equal(1UL, binding.Revision); Assert.Equal(content.Digest, old.Manifest.Digest);
        Assert.Equal(renderer, world.GetComponent<MeshRenderer>(first)); Assert.Equal(renderer.Mesh, device.ValidateHandle(handle));
        Assert.True(upload.CancellationObserved.Task.IsCompleted); Assert.True(upload.IsReleased);
        Assert.False(device.Counts.AdmissionClosed); Assert.Equal(1, device.Counts.EntryCount);
    }

    [Fact]
    public async Task Reload_MissingBindingOrPeakAllocationFailureDoesNotPublishAndCompatibleCandidateCanRetry()
    {
        var content = ResourceTestFixture.Content(); using var world = World(content, out var first, out _);
        var backend = new ResourceTestBackend(); await using var host = new RenderResourceHost(new(), new ResourceTestBackendFactory(backend), new() { MaximumGeometryBytes = 256 });
        using var oldScope = host.Content.Acquire(content); var device = host.Create(ResourceTestFixture.Request(content, world.Id));
        await using var binding = host.Create(new RenderWorldBindingRequest(device, world, oldScope)); await binding.InitializeAsync();
        var current = binding.Current; var original = world.GetComponent<MeshRenderer>(first);
        var incomplete = new RenderContentManifest(content.ProjectId, content.BuildId, content.Generation,
            content.Entries.Where(entry => entry.Key.KindId != RenderContentProfile.BindingSetKindId).ToArray());
        using var incompleteScope = host.Content.Acquire(incomplete);
        var missing = await Assert.ThrowsAsync<RenderContentException>(() => binding.ReloadAsync(incompleteScope));
        Assert.Equal(RenderContentError.MissingAsset, missing.Error); Assert.Equal(1, backend.CreateCalls);
        using var tooLarge = host.Content.Acquire(ResourceTestFixture.ChangedContent(content, changeMesh: true));
        var budget = await Assert.ThrowsAsync<RenderResourceException>(() => binding.ReloadAsync(tooLarge));
        Assert.Equal(RenderResourceError.LimitExceeded, budget.Error); Assert.Same(current, binding.Current);
        Assert.Equal(1UL, binding.Revision); Assert.Equal(original, world.GetComponent<MeshRenderer>(first));
        Assert.Equal(256L, device.AllocationBudget.Counts.GeometryBytes); Assert.Equal(1, device.Counts.EntryCount);
        using var compatible = host.Content.Acquire(ResourceTestFixture.ChangedContent(content));
        var retry = await binding.ReloadAsync(compatible);
        Assert.True(retry.Committed); Assert.Null(retry.CleanupError); Assert.Equal(2UL, binding.Revision);
        Assert.NotEqual(original.Bindings, world.GetComponent<MeshRenderer>(first).Bindings);
        Assert.Equal(original.Mesh, world.GetComponent<MeshRenderer>(first).Mesh);
        Assert.Equal(2, backend.CreateCalls); Assert.Equal(256L, device.AllocationBudget.Counts.GeometryBytes);
    }

    [Fact]
    public async Task Reload_CommittedCandidateSurvivesOldCleanupFailureAndDisposalRetriesOnlyRetainedOwner()
    {
        var content = ResourceTestFixture.Content(); var candidate = ResourceTestFixture.ChangedContent(content, changeMesh: true);
        using var world = World(content, out var first, out _); var backend = new ResourceTestBackend();
        await using var host = new RenderResourceHost(new(), new ResourceTestBackendFactory(backend));
        var oldScope = host.Content.Acquire(content); var candidateScope = host.Content.Acquire(candidate);
        var device = host.Create(ResourceTestFixture.Request(content, world.Id));
        var binding = host.Create(new RenderWorldBindingRequest(device, world, oldScope)); await binding.InitializeAsync(); oldScope.Dispose();
        var old = binding.Current!; var oldUpload = Assert.Single(backend.CreatedUploads); oldUpload.DisposeFailuresRemaining = 1;
        var result = await binding.ReloadAsync(candidateScope); candidateScope.Dispose();
        Assert.True(result.Committed); Assert.NotNull(result.CleanupError); Assert.Equal(2UL, binding.Revision);
        Assert.NotSame(old, binding.Current); Assert.Equal(candidate.Digest, binding.Current!.Manifest.Digest);
        Assert.Equal(candidate.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId).Key, world.GetComponent<MeshRenderer>(first).Mesh);
        Assert.False(old.IsReleased); Assert.Equal(2, device.Counts.EntryCount); Assert.Equal(1, device.Counts.PendingCleanup);
        Assert.Equal(512L, device.AllocationBudget.Counts.GeometryBytes); Assert.Equal(1, oldUpload.DisposeCalls);
        await binding.DisposeAsync(); await binding.DisposeAsync();
        Assert.True(binding.IsReleased); Assert.True(old.IsReleased); Assert.False(world.IsDisposed);
        Assert.Equal(2, oldUpload.DisposeCalls); Assert.Equal(1, backend.CreatedUploads[1].DisposeCalls);
        Assert.Equal(0, device.Counts.EntryCount); Assert.Equal(0L, device.AllocationBudget.Counts.GeometryBytes);
        Assert.Equal(0, host.Content.Counts.LeaseCount); Assert.Equal(0, host.Content.Counts.RevisionCount);
    }

    [Fact]
    public async Task Dispose_CancelsBeforeWaitingForInitializationAndKeepsWorldBorrowedUntilLateUploadDrains()
    {
        var content = ResourceTestFixture.Content(); using var world = World(content, out _, out _);
        var backend = new ResourceTestBackend(holdUploads: true) { IgnoreCancellation = true };
        await using var host = new RenderResourceHost(new(), new ResourceTestBackendFactory(backend));
        using var scope = host.Content.Acquire(content); var device = host.Create(ResourceTestFixture.Request(content, world.Id));
        var binding = host.Create(new RenderWorldBindingRequest(device, world, scope)); var initialize = binding.InitializeAsync();
        var upload = await backend.WaitForUploadAsync(1); await upload.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var disposal = binding.DisposeAsync().AsTask();
        try
        {
            await upload.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(disposal.IsCompleted); Assert.Null(binding.Current); Assert.False(binding.IsReleased);
            Assert.False(world.IsDisposed); Assert.Equal(256L, device.AllocationBudget.Counts.GeometryBytes);
            upload.Complete(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialize.WaitAsync(TimeSpan.FromSeconds(10)));
            await disposal.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(binding.IsReleased); Assert.False(world.IsDisposed); Assert.Equal(0UL, binding.Revision);
            Assert.True(upload.IsReleased); Assert.False(upload.DisposedBeforePrepareExit);
            Assert.Equal(0L, device.AllocationBudget.Counts.GeometryBytes);
        }
        finally { upload.Complete(); }
    }

    [Fact]
    public async Task Binding_RejectsWrongWorldBeforeCpuRetainAndReloadRequiresInitialization()
    {
        var content = ResourceTestFixture.Content(); using var world = World(content, out _, out _);
        using var other = new World(); var backend = new ResourceTestBackend();
        await using var host = new RenderResourceHost(new(), new ResourceTestBackendFactory(backend));
        using var scope = host.Content.Acquire(content); var device = host.Create(ResourceTestFixture.Request(content, world.Id));
        var foreign = Assert.Throws<RenderResourceException>(() => host.Create(new RenderWorldBindingRequest(device, other, scope)));
        Assert.Equal(RenderResourceError.InvalidIdentity, foreign.Error); Assert.Equal(1, host.Content.Counts.LeaseCount);
        Assert.False(other.IsDisposed); Assert.Equal(0, backend.CreateCalls);
        await using var binding = host.Create(new RenderWorldBindingRequest(device, world, scope));
        var uninitialized = await Assert.ThrowsAsync<RenderResourceException>(() => binding.ReloadAsync(scope));
        Assert.Equal(RenderResourceError.Conflict, uninitialized.Error); Assert.Null(binding.Current); Assert.Equal(0UL, binding.Revision);
        await binding.InitializeAsync();
        var twice = await Assert.ThrowsAsync<RenderResourceException>(() => binding.InitializeAsync());
        Assert.Equal(RenderResourceError.Conflict, twice.Error); Assert.Equal(1UL, binding.Revision);
        Assert.Equal(1, backend.CreateCalls);
    }

    [Theory]
    [InlineData(4096, true)]
    [InlineData(4097, false)]
    public async Task Initialize_RendererCeilingAdmits4096AndRejects4097BeforeBackendActivation(int rendererCount, bool admitted)
    {
        var content = ResourceTestFixture.Content(); using var world = new World();
        var mesh = content.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId).Key;
        var bindings = content.Resolve(RenderContentProfile.CubeBindingSetAssetId, RenderContentProfile.BindingSetKindId).Key;
        var renderer = new MeshRenderer(mesh, bindings, true, uint.MaxValue, true, Vector3.One);
        for (var i = 0; i < rendererCount; i++) world.AddComponent(world.CreateEntity(), renderer);
        var backend = new ResourceTestBackend(); var factory = new ResourceTestBackendFactory(backend);
        await using var host = new RenderResourceHost(new(), factory);
        using var scope = host.Content.Acquire(content); var device = host.Create(ResourceTestFixture.Request(content, world.Id));
        await using var binding = host.Create(new RenderWorldBindingRequest(device, world, scope));
        Assert.Equal(4096, RenderWorldBinding.MaximumRenderers); Assert.Equal(rendererCount, world.EntityCount);
        Assert.Equal(0, factory.Calls); Assert.Equal(0, backend.CreateCalls); Assert.Equal(2, host.Content.Counts.LeaseCount);
        if (admitted)
        {
            await binding.InitializeAsync();
            Assert.Equal(1UL, binding.Revision); Assert.Equal(RenderPreparationState.Ready, binding.Current!.State);
            Assert.Equal(mesh, device.ValidateHandle(binding.Current.GetMeshHandle(mesh)));
            Assert.Equal(1, factory.Calls); Assert.Equal(1, backend.CreateCalls); Assert.Equal(1, device.Counts.EntryCount);
            Assert.Equal(256L, device.AllocationBudget.Counts.GeometryBytes);
        }
        else
        {
            var error = await Assert.ThrowsAsync<RenderResourceException>(() => binding.InitializeAsync());
            Assert.Equal(RenderResourceError.LimitExceeded, error.Error); Assert.Null(binding.Current); Assert.Equal(0UL, binding.Revision);
            Assert.Equal(0, factory.Calls); Assert.Equal(0, backend.CreateCalls); Assert.Equal(0, device.Counts.EntryCount);
            Assert.Equal(0, device.Counts.LivePreparations); Assert.Equal(0L, device.AllocationBudget.Counts.GeometryBytes);
        }
        Assert.Equal(rendererCount, world.EntityCount); Assert.False(world.IsFaulted);
        Assert.Equal(2, host.Content.Counts.LeaseCount);
        await binding.DisposeAsync();
        Assert.True(binding.IsReleased); Assert.False(world.IsDisposed); Assert.Equal(1, host.Content.Counts.LeaseCount);
        Assert.Equal(0, device.Counts.EntryCount); Assert.Equal(0L, device.AllocationBudget.Counts.GeometryBytes);
    }

    [Fact]
    public async Task Host_RejectsSecondLiveBindingBeforeCpuRetainAndAdmitsReplacementAfterSuccessfulRelease()
    {
        var content = ResourceTestFixture.Content(); using var world = World(content, out _, out _);
        var backend = new ResourceTestBackend(); var factory = new ResourceTestBackendFactory(backend);
        await using var host = new RenderResourceHost(new(), factory);
        using var scope = host.Content.Acquire(content); var device = host.Create(ResourceTestFixture.Request(content, world.Id));
        var request = new RenderWorldBindingRequest(device, world, scope);
        var first = host.Create(request); await first.InitializeAsync();
        var current = first.Current!; var upload = Assert.Single(backend.CreatedUploads);
        var full = Assert.Throws<RenderResourceException>(() => host.Create(request));
        Assert.Equal(RenderResourceError.LimitExceeded, full.Error); Assert.Same(current, first.Current);
        Assert.Equal(1UL, first.Revision); Assert.Equal(2, host.Content.Counts.LeaseCount);
        Assert.Equal(1, device.Counts.LivePreparations); Assert.Equal(1, device.Counts.EntryCount);
        Assert.Equal(1, backend.CreateCalls); Assert.Equal(0, upload.DisposeCalls);
        await first.DisposeAsync();
        Assert.True(first.IsReleased); Assert.True(current.IsReleased); Assert.Equal(1, upload.DisposeCalls);
        Assert.Equal(1, host.Content.Counts.LeaseCount); Assert.Equal(0, device.Counts.LivePreparations);
        await using var replacement = host.Create(request);
        Assert.NotSame(first, replacement); Assert.Equal(2, host.Content.Counts.LeaseCount);
        Assert.Null(replacement.Current); Assert.Equal(0UL, replacement.Revision);
        await replacement.InitializeAsync();
        Assert.Equal(RenderPreparationState.Ready, replacement.Current!.State); Assert.Equal(1UL, replacement.Revision);
        Assert.Equal(2, backend.CreateCalls); Assert.Equal(1, factory.Calls); Assert.False(world.IsDisposed);
        await replacement.DisposeAsync();
        Assert.True(replacement.IsReleased); Assert.Equal(1, host.Content.Counts.LeaseCount);
        Assert.Equal(0, device.Counts.EntryCount); Assert.Equal(0L, device.AllocationBudget.Counts.GeometryBytes);
    }

    private static World World(RenderContentManifest content, out EntityHandle first, out EntityHandle second)
    {
        var world = new World(); first = world.CreateEntity(); second = world.CreateEntity();
        var mesh = content.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId).Key;
        var bindings = content.Resolve(RenderContentProfile.CubeBindingSetAssetId, RenderContentProfile.BindingSetKindId).Key;
        world.AddComponent(first, new MeshRenderer(mesh, bindings, true, uint.MaxValue, true, new(.5f, .25f, .75f)));
        world.AddComponent(second, new MeshRenderer(mesh, bindings, false, 0, false, new(.125f, .25f, .5f)));
        world.AddComponent(first, LocalTransform.Identity); world.AddComponent(second, LocalTransform.Identity with { Position = new(2, 0, 0) });
        world.AddComponent(second, new UnrelatedState(17)); return world;
    }
    private readonly record struct UnrelatedState(int Value);
}
