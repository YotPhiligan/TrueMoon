using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering.Resources;

public sealed class RenderDeviceOwnerTests
{
    [Fact]
    public async Task Constructor_IsLazyAndLeavesBorrowedCatalogOwnedByCaller()
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        using var scope = catalog.Acquire(manifest);
        var backend = new ResourceTestBackend();
        var factory = new ResourceTestBackendFactory(backend);
        var request = ResourceTestFixture.Request(manifest, generation: 19);
        var owner = new RenderDeviceOwner(catalog, factory, request);

        Assert.NotEqual(Guid.Empty, owner.Id);
        Assert.Equal(request.Identity, owner.Identity);
        Assert.Equal(19ul, owner.Generation);
        Assert.Equal((0, 0, 0, 0, 0, false), Counts(owner));
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), owner.AllocationBudget.Counts);
        Assert.Equal((0, 0), (factory.Calls, backend.CreateCalls));
        Assert.False(owner.IsReleased);
        await owner.DisposeAsync();
        await owner.DisposeAsync();

        Assert.True(owner.IsReleased);
        Assert.True(owner.Counts.AdmissionClosed);
        Assert.Equal((0, 0), (factory.Calls, backend.DisposeCalls));
        Assert.False(catalog.IsReleased);
        Assert.False(scope.IsReleased);
        Assert.Equal(1, catalog.Counts.LeaseCount);
        using var another = catalog.Acquire(manifest);
        Assert.Equal(2, catalog.Counts.LeaseCount);
    }

    [Theory]
    [InlineData("session")]
    [InlineData("world")]
    [InlineData("project")]
    [InlineData("build")]
    [InlineData("catalog")]
    public void Identity_RejectsEveryMissingScopeDimension(string dimension)
    {
        var identity = ResourceTestFixture.Request(ResourceTestFixture.Content()).Identity;
        identity = dimension switch
        {
            "session" => identity with { SessionId = Guid.Empty },
            "world" => identity with { WorldId = Guid.Empty },
            "project" => identity with { ProjectId = Guid.Empty },
            "build" => identity with { BuildId = default },
            _ => identity with { CatalogGeneration = Guid.Empty }
        };
        Assert.Equal(RenderResourceError.InvalidIdentity,
            Assert.Throws<RenderResourceException>(() => identity.Validate()).Error);
    }

    [Theory]
    [InlineData("project")]
    [InlineData("build")]
    [InlineData("catalog")]
    public async Task Create_RejectsEveryManifestIdentityMismatchBeforeBackendOrRetain(string dimension)
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        using var scope = catalog.Acquire(manifest);
        var request = ResourceTestFixture.Request(manifest);
        request = request with { Identity = dimension switch
        {
            "project" => request.Identity with { ProjectId = ResourceTestFixture.World },
            "build" => request.Identity with { BuildId = RenderContentHash.Compute("other-build"u8) },
            _ => request.Identity with { CatalogGeneration = ResourceTestFixture.World }
        } };
        var backend = new ResourceTestBackend();
        var factory = new ResourceTestBackendFactory(backend);
        await using var owner = new RenderDeviceOwner(catalog, factory, request);
        var before = catalog.Counts;

        Assert.Equal(RenderResourceError.InvalidIdentity,
            Assert.Throws<RenderResourceException>(() => owner.Create(scope)).Error);

        Assert.Equal(before, catalog.Counts);
        Assert.Equal((0, 0), (factory.Calls, backend.CreateCalls));
        Assert.Equal((0, 0, 0, 0, 0, false), Counts(owner));
    }

    [Fact]
    public async Task Create_RejectsForeignCatalogDespiteIdenticalFrozenContent()
    {
        using var catalog = new RenderContentCatalog();
        using var foreignCatalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        using var scope = foreignCatalog.Acquire(manifest);
        var backend = new ResourceTestBackend();
        var factory = new ResourceTestBackendFactory(backend);
        await using var owner = new RenderDeviceOwner(catalog, factory, ResourceTestFixture.Request(manifest));
        var foreignBefore = foreignCatalog.Counts;

        Assert.Equal(RenderResourceError.InvalidIdentity,
            Assert.Throws<RenderResourceException>(() => owner.Create(scope)).Error);

        Assert.Equal(foreignBefore, foreignCatalog.Counts);
        Assert.Equal(0, catalog.Counts.LeaseCount);
        Assert.Equal((0, 0), (factory.Calls, backend.CreateCalls));
        Assert.NotEqual(catalog.Id, scope.CatalogId);
    }

    [Fact]
    public async Task ValidateHandle_RejectsDefaultForeignOwnerWrongDeviceAndWrongSlotGeneration()
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        using var scope = catalog.Acquire(manifest);
        var backend = new ResourceTestBackend();
        await using var owner = new RenderDeviceOwner(catalog, new ResourceTestBackendFactory(backend), ResourceTestFixture.Request(manifest));
        await using var preparation = owner.Create(scope);
        await preparation.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        var key = MeshKey(manifest);
        var handle = preparation.GetMeshHandle(key);

        Assert.Equal(owner.Id, handle.OwnerId);
        Assert.Equal(owner.Generation, handle.DeviceGeneration);
        Assert.Equal(key, owner.ValidateHandle(handle));
        var calls = backend.CreateCalls;
        foreach (var invalid in new[]
        {
            default(RenderResourceHandle),
            handle with { OwnerId = ResourceTestFixture.Session },
            handle with { DeviceGeneration = 2 },
            handle with { Slot = -1 },
            handle with { Slot = int.MaxValue },
            handle with { SlotGeneration = 0 },
            handle with { SlotGeneration = handle.SlotGeneration + 1 }
        })
            Assert.Equal(RenderResourceError.StaleHandle,
                Assert.Throws<RenderResourceException>(() => owner.ValidateHandle(invalid)).Error);
        Assert.Equal(calls, backend.CreateCalls);
        Assert.Equal(key, owner.ValidateHandle(handle));
        foreach (var unpinned in new[]
        {
            key with { Revision = RenderContentHash.Compute("unpinned mesh revision"u8) },
            key with { ProjectId = ResourceTestFixture.World },
            key with { KindId = RenderContentProfile.MaterialKindId }
        })
            Assert.Equal(RenderResourceError.InvalidIdentity,
                Assert.Throws<RenderResourceException>(() => preparation.GetMeshHandle(unpinned)).Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SeparateWorldOrDeviceOwners_RejectEachOthersHandles(bool changeDeviceGeneration)
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        using var scope = catalog.Acquire(manifest);
        var firstBackend = new ResourceTestBackend();
        var secondBackend = new ResourceTestBackend();
        var firstRequest = ResourceTestFixture.Request(manifest);
        var secondRequest = changeDeviceGeneration
            ? ResourceTestFixture.Request(manifest, generation: 2)
            : ResourceTestFixture.Request(manifest, worldId: ResourceTestFixture.Generation);
        await using var firstOwner = new RenderDeviceOwner(catalog, new ResourceTestBackendFactory(firstBackend), firstRequest);
        await using var secondOwner = new RenderDeviceOwner(catalog, new ResourceTestBackendFactory(secondBackend), secondRequest);
        await using var first = firstOwner.Create(scope);
        await using var second = secondOwner.Create(scope);
        await Task.WhenAll(first.Completion, second.Completion).WaitAsync(TimeSpan.FromSeconds(10));
        var key = MeshKey(manifest);
        var firstHandle = first.GetMeshHandle(key);
        var secondHandle = second.GetMeshHandle(key);

        Assert.NotEqual(firstHandle.OwnerId, secondHandle.OwnerId);
        Assert.Equal(firstRequest.Identity, firstOwner.Identity);
        Assert.Equal(secondRequest.Identity, secondOwner.Identity);
        Assert.Equal(RenderResourceError.StaleHandle,
            Assert.Throws<RenderResourceException>(() => firstOwner.ValidateHandle(secondHandle)).Error);
        Assert.Equal(RenderResourceError.StaleHandle,
            Assert.Throws<RenderResourceException>(() => secondOwner.ValidateHandle(firstHandle)).Error);
        Assert.Equal(key, firstOwner.ValidateHandle(firstHandle));
        Assert.Equal(key, secondOwner.ValidateHandle(secondHandle));
        Assert.Equal((1, 1), (firstBackend.CreateCalls, secondBackend.CreateCalls));
    }

    [Fact]
    public async Task SharedMesh_UsesOneUploadAndWaitsForLastPreparationRelease()
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        using var scope = catalog.Acquire(manifest);
        var backend = new ResourceTestBackend();
        var factory = new ResourceTestBackendFactory(backend);
        await using var owner = new RenderDeviceOwner(catalog, factory, ResourceTestFixture.Request(manifest));
        var first = owner.Create(scope);
        var second = owner.Create(scope);
        await Task.WhenAll(first.Completion, second.Completion).WaitAsync(TimeSpan.FromSeconds(10));
        var key = MeshKey(manifest);
        var handle = first.GetMeshHandle(key);
        var handleCopy = handle;
        var upload = Assert.Single(backend.CreatedUploads);

        Assert.Equal(handle, second.GetMeshHandle(key));
        Assert.Equal((1, 1, 1), (factory.Calls, backend.CreateCalls, upload.PrepareCalls));
        Assert.Equal(3, catalog.Counts.LeaseCount);
        Assert.Equal(new RenderAllocationCounts(256, 128, 240), owner.AllocationBudget.Counts);
        await first.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        await first.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(first.IsReleased);
        Assert.False(second.IsReleased);
        Assert.Equal(2, catalog.Counts.LeaseCount);
        Assert.Equal(key, owner.ValidateHandle(handleCopy));
        Assert.Equal(0, upload.DisposeCalls);
        Assert.Equal(new RenderAllocationCounts(256, 128, 240), owner.AllocationBudget.Counts);
        await second.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(1, upload.DisposeCalls);
        Assert.True(upload.IsReleased);
        Assert.Equal((0, 0, 0, 0, 0, false), Counts(owner));
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), owner.AllocationBudget.Counts);
        Assert.Equal(1, catalog.Counts.LeaseCount);
        Assert.Equal(RenderResourceError.StaleHandle,
            Assert.Throws<RenderResourceException>(() => owner.ValidateHandle(handleCopy)).Error);
        Assert.Equal(0, backend.DisposeCalls);
    }

    [Fact]
    public async Task EqualPayloadWithDifferentLogicalMeshKeys_UsesDistinctUploads()
    {
        var source = ResourceTestFixture.Content();
        var mesh = source.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId);
        var aliasKey = mesh.Key with { AssetId = ResourceTestFixture.World };
        var manifest = new RenderContentManifest(source.ProjectId, source.BuildId, source.Generation,
            [mesh, new(aliasKey, mesh.Definition)]);
        using var catalog = new RenderContentCatalog();
        using var scope = catalog.Acquire(manifest);
        var backend = new ResourceTestBackend();
        await using var owner = new RenderDeviceOwner(catalog, new ResourceTestBackendFactory(backend), ResourceTestFixture.Request(manifest));
        var preparation = owner.Create(scope);
        await preparation.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        var first = preparation.GetMeshHandle(mesh.Key);
        var second = preparation.GetMeshHandle(aliasKey);

        Assert.Equal(mesh.Key.Revision, aliasKey.Revision);
        Assert.NotEqual(first, second);
        Assert.Equal(mesh.Key, owner.ValidateHandle(first));
        Assert.Equal(aliasKey, owner.ValidateHandle(second));
        Assert.Equal(2, backend.CreateCalls);
        Assert.Equal(2, owner.Counts.EntryCount);
        Assert.Equal(new RenderAllocationCounts(512, 256, 480), owner.AllocationBudget.Counts);
        await preparation.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.All(backend.CreatedUploads, upload => Assert.Equal(1, upload.DisposeCalls));
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), owner.AllocationBudget.Counts);
        Assert.Equal(0, owner.Counts.EntryCount);
    }

    [Fact]
    public async Task BackendFactoryFailure_PreservesCauseAndDoesNotLeaveEntriesOrCpuRetains()
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        using var scope = catalog.Acquire(manifest);
        var backend = new ResourceTestBackend();
        var cause = new InvalidOperationException("backend creation marker");
        var factory = new ResourceTestBackendFactory(backend) { CreateOverride = _ => throw cause };
        await using var owner = new RenderDeviceOwner(catalog, factory, ResourceTestFixture.Request(manifest));
        var preparation = owner.Create(scope);

        Assert.Same(cause, await Assert.ThrowsAsync<InvalidOperationException>(() => preparation.Completion.WaitAsync(TimeSpan.FromSeconds(10))));
        Assert.Same(cause, preparation.Error);
        Assert.Equal(RenderPreparationState.Failed, preparation.State);
        Assert.True(preparation.IsReleased);
        Assert.Equal((1, 0, 0), (factory.Calls, backend.CreateCalls, owner.Counts.EntryCount));
        Assert.Equal(1, catalog.Counts.LeaseCount);
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), owner.AllocationBudget.Counts);
        await preparation.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        await owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, backend.DisposeCalls);
        Assert.True(owner.IsReleased);
    }

    [Fact]
    public async Task BackendFactoryPartialCreation_RetainsFailedCleanupOwnerUntilRetry()
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        using var scope = catalog.Acquire(manifest);
        var backend = new ResourceTestBackend { DisposeFailuresRemaining = 1 };
        var cause = new InvalidOperationException("partial backend creation marker");
        var failure = new RenderResourceCreationException(cause, backend);
        var factory = new ResourceTestBackendFactory(backend) { CreateOverride = _ => throw failure };
        await using var owner = new RenderDeviceOwner(catalog, factory, ResourceTestFixture.Request(manifest));
        var preparation = owner.Create(scope);

        Assert.Same(failure, await Assert.ThrowsAsync<RenderResourceCreationException>(() => preparation.Completion.WaitAsync(TimeSpan.FromSeconds(10))));
        Assert.Same(failure, preparation.Error);
        Assert.Same(cause, failure.InnerException);
        Assert.Same(backend, failure.CleanupOwner);
        Assert.True(preparation.IsReleased);
        Assert.Equal((1, 0, 0), (factory.Calls, backend.CreateCalls, backend.DisposeCalls));
        Assert.Equal(1, catalog.Counts.LeaseCount);
        var cleanup = await Assert.ThrowsAsync<AggregateException>(() => owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.Contains(cleanup.Flatten().InnerExceptions, error => error.Message == "backend cleanup marker");
        Assert.False(owner.IsReleased);
        Assert.False(backend.IsReleased);
        Assert.True(owner.Counts.AdmissionClosed);
        Assert.Equal((1, 1), (factory.Calls, backend.DisposeCalls));
        Assert.Equal(0, owner.Counts.EntryCount);
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), owner.AllocationBudget.Counts);
        await owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(owner.IsReleased);
        Assert.True(backend.IsReleased);
        Assert.Equal((1, 2), (factory.Calls, backend.DisposeCalls));
        Assert.Equal(1, catalog.Counts.LeaseCount);
    }

    [Fact]
    public async Task UploadFactoryPartialCreation_RetainsEntryAndCpuScopeForCleanupRetry()
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        using var scope = catalog.Acquire(manifest);
        var backend = new ResourceTestBackend();
        var cause = new InvalidOperationException("upload factory creation marker");
        ResourceTestUpload? partial = null;
        RenderResourceCreationException? creation = null;
        backend.CreateOverride = (request, ordinal) =>
        {
            partial = new ResourceTestUpload(request, false, false, 256, 128, 240, null, backend.DisposalLog, ordinal)
                { DisposeFailuresRemaining = 1 };
            creation = new RenderResourceCreationException(cause, partial);
            throw creation;
        };
        await using var owner = new RenderDeviceOwner(catalog, new ResourceTestBackendFactory(backend), ResourceTestFixture.Request(manifest));
        var preparation = owner.Create(scope);
        var error = await Assert.ThrowsAsync<AggregateException>(() => preparation.Completion.WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.Contains(creation!, error.Flatten().InnerExceptions);
        Assert.Same(cause, creation!.InnerException);
        Assert.Same(partial, creation.CleanupOwner);
        Assert.Same(error, preparation.Error);
        Assert.False(preparation.IsReleased);
        Assert.False(partial!.IsReleased);
        Assert.Equal((1, 0, 1, 0), (backend.CreateCalls, partial.PrepareCalls, partial.DisposeCalls, backend.DisposeCalls));
        Assert.Equal((1, 1, 1), (owner.Counts.EntryCount, owner.Counts.PendingCleanup, owner.Counts.LivePreparations));
        Assert.Equal(2, catalog.Counts.LeaseCount);
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), owner.AllocationBudget.Counts);
        await owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(owner.IsReleased);
        Assert.True(preparation.IsReleased);
        Assert.True(partial.IsReleased);
        Assert.Same(error, preparation.Error);
        Assert.Equal((2, 1, 0), (partial.DisposeCalls, backend.DisposeCalls, owner.Counts.EntryCount));
        Assert.Equal(1, catalog.Counts.LeaseCount);
        Assert.Equal(new[] { "upload:1", "upload:1", "backend" }, backend.DisposalLog.ToArray());
    }

    internal static RenderAssetKey MeshKey(RenderContentManifest manifest)
        => manifest.Entries.First(entry => entry.Key.KindId == RenderContentProfile.MeshKindId).Key;

    private static (int Entries, int Active, int Queued, int Live, int Cleanup, bool Closed) Counts(RenderDeviceOwner owner)
    {
        var counts = owner.Counts;
        return (counts.EntryCount, counts.ActivePreparations, counts.QueuedPreparations,
            counts.LivePreparations, counts.PendingCleanup, counts.AdmissionClosed);
    }
}
