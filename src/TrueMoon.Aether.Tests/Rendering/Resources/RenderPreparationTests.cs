using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering.Resources;

public sealed class RenderPreparationTests
{
    [Fact]
    public async Task PendingPreparations_ShareOneReadinessFenceAndPublishOnlyAfterUploadCompletion()
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        using var scope = catalog.Acquire(manifest);
        var backend = new ResourceTestBackend(holdUploads: true);
        var factory = new ResourceTestBackendFactory(backend);
        await using var owner = new RenderDeviceOwner(catalog, factory, ResourceTestFixture.Request(manifest));
        var first = owner.Create(scope);
        var second = owner.Create(scope);
        var upload = await backend.WaitForUploadAsync(1);
        try
        {
            await upload.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(first.Completion.IsCompleted);
            Assert.False(second.Completion.IsCompleted);
            Assert.Equal(RenderPreparationState.Pending, first.State);
            Assert.Equal(RenderPreparationState.Pending, second.State);
            Assert.Equal((1, 1, 1), (factory.Calls, backend.CreateCalls, owner.Counts.EntryCount));
            upload.Complete();
            await Task.WhenAll(first.Completion, second.Completion).WaitAsync(TimeSpan.FromSeconds(10));

            var key = RenderDeviceOwnerTests.MeshKey(manifest);
            Assert.Equal(RenderPreparationState.Ready, first.State);
            Assert.Equal(RenderPreparationState.Ready, second.State);
            Assert.Equal(first.GetMeshHandle(key), second.GetMeshHandle(key));
            Assert.Equal((1, 1), (backend.CreateCalls, upload.PrepareCalls));
            Assert.Equal(new RenderAllocationCounts(256, 128, 240), owner.AllocationBudget.Counts);
        }
        finally { upload.Complete(); await owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    [Fact]
    public async Task Options_AreCopiedForActiveQueueAndGpuEntryAdmissionBeforeDeviceStarts()
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        var candidateManifest = ResourceTestFixture.ChangedContent(manifest, changeMesh: true);
        using var scope = catalog.Acquire(manifest);
        using var candidateScope = catalog.Acquire(candidateManifest);
        var backend = new ResourceTestBackend(holdUploads: true);
        var options = new RenderResourceOptions
        {
            MaximumActivePreparations = 1, MaximumQueuedPreparations = 1, MaximumGpuEntries = 1
        };
        await using var owner = new RenderDeviceOwner(catalog, new ResourceTestBackendFactory(backend), ResourceTestFixture.Request(manifest), options);
        options.MaximumActivePreparations = 4;
        options.MaximumQueuedPreparations = 16;
        options.MaximumGpuEntries = 2048;
        var first = owner.Create(scope);
        var second = owner.Create(scope);
        var upload = await backend.WaitForUploadAsync(1);
        try
        {
            await upload.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal((1, 1, 2), PreparationCounts(owner));
            Assert.Equal(RenderResourceError.LimitExceeded,
                Assert.Throws<RenderResourceException>(() => owner.Create(scope)).Error);
            Assert.Equal(4, catalog.Counts.LeaseCount);
            upload.Complete();
            await Task.WhenAll(first.Completion, second.Completion).WaitAsync(TimeSpan.FromSeconds(10));
            var originalHandle = first.GetMeshHandle(RenderDeviceOwnerTests.MeshKey(manifest));
            var candidate = owner.Create(candidateScope);
            Assert.Equal(RenderResourceError.LimitExceeded,
                (await Assert.ThrowsAsync<RenderResourceException>(() => candidate.Completion.WaitAsync(TimeSpan.FromSeconds(10)))).Error);

            Assert.True(candidate.IsReleased);
            Assert.Equal((1, 1), (owner.Counts.EntryCount, backend.CreateCalls));
            Assert.Equal(4, catalog.Counts.LeaseCount);
            Assert.Equal(RenderDeviceOwnerTests.MeshKey(manifest), owner.ValidateHandle(originalHandle));
            Assert.Equal(new RenderAllocationCounts(256, 128, 240), owner.AllocationBudget.Counts);
        }
        finally { upload.Complete(); await owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    [Fact]
    public async Task Completion_RemainsPendingUntilUploadReadinessAndRetainsFrozenManifest()
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        using var scope = catalog.Acquire(manifest);
        var backend = new ResourceTestBackend(holdUploads: true);
        await using var owner = Owner(catalog, manifest, backend);
        var preparation = owner.Create(scope);
        var upload = await backend.WaitForUploadAsync(1);
        try
        {
            await upload.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(RenderPreparationState.Pending, preparation.State);
            Assert.False(preparation.Completion.IsCompleted);
            Assert.False(upload.IsReady);
            Assert.False(preparation.IsReleased);
            Assert.Null(preparation.Error);
            Assert.Equal(manifest.ProjectId, preparation.Manifest.ProjectId);
            Assert.Equal(manifest.BuildId, preparation.Manifest.BuildId);
            Assert.Equal(manifest.Generation, preparation.Manifest.Generation);
            Assert.Equal(manifest.Digest, preparation.Manifest.Digest);
            Assert.Equal(new RenderAllocationCounts(256, 128, 240), owner.AllocationBudget.Counts);
            Assert.Equal(2, catalog.Counts.LeaseCount);
            scope.Dispose();
            Assert.Equal(1, catalog.Counts.LeaseCount);
            Assert.Equal(manifest.Digest, preparation.Manifest.Digest);

            upload.Complete();
            await preparation.Completion.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(RenderPreparationState.Ready, preparation.State);
            Assert.True(upload.IsReady);
            Assert.Null(preparation.Error);
            var mesh = RenderDeviceOwnerTests.MeshKey(manifest);
            Assert.Equal(mesh, owner.ValidateHandle(preparation.GetMeshHandle(mesh)));
            await preparation.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(preparation.IsReleased);
            Assert.Equal(RenderPreparationState.Disposed, preparation.State);
            Assert.Equal((0, 0, 0), (owner.Counts.EntryCount, owner.Counts.LivePreparations, catalog.Counts.LeaseCount));
            Assert.Equal(new RenderAllocationCounts(0, 0, 0), owner.AllocationBudget.Counts);
        }
        finally { upload.Complete(); await owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    [Fact]
    public async Task PreparationLimits_AdmitFourActiveSixteenQueuedAndRejectNextBeforeRetain()
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        using var scope = catalog.Acquire(manifest);
        var backend = new ResourceTestBackend(holdUploads: true);
        var factory = new ResourceTestBackendFactory(backend);
        await using var owner = new RenderDeviceOwner(catalog, factory, ResourceTestFixture.Request(manifest));
        var preparations = new List<RenderPreparation>();
        try
        {
            for (var i = 0; i < 3; i++) preparations.Add(owner.Create(scope));
            Assert.Equal((3, 0, 3), PreparationCounts(owner));
            preparations.Add(owner.Create(scope));
            Assert.Equal((4, 0, 4), PreparationCounts(owner));
            for (var i = 0; i < 15; i++) preparations.Add(owner.Create(scope));
            Assert.Equal((4, 15, 19), PreparationCounts(owner));
            preparations.Add(owner.Create(scope));
            var upload = await backend.WaitForUploadAsync(1);
            await upload.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal((4, 16, 20), PreparationCounts(owner));
            Assert.Equal(21, catalog.Counts.LeaseCount);
            var catalogBefore = catalog.Counts;
            var allocationBefore = owner.AllocationBudget.Counts;
            Assert.Equal((1, 1), (factory.Calls, backend.CreateCalls));

            Assert.Equal(RenderResourceError.LimitExceeded,
                Assert.Throws<RenderResourceException>(() => owner.Create(scope)).Error);

            Assert.Equal((4, 16, 20), PreparationCounts(owner));
            Assert.Equal(catalogBefore, catalog.Counts);
            Assert.Equal(allocationBefore, owner.AllocationBudget.Counts);
            Assert.Equal((1, 1), (factory.Calls, backend.CreateCalls));
            Assert.All(preparations, preparation => Assert.False(preparation.Completion.IsCompleted));

            preparations[4].Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preparations[4].Completion.WaitAsync(TimeSpan.FromSeconds(10)));
            await preparations[4].DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal((4, 15, 19), PreparationCounts(owner));
            preparations.Add(owner.Create(scope));
            Assert.Equal((4, 16, 20), PreparationCounts(owner));
            Assert.Equal(21, catalog.Counts.LeaseCount);
            Assert.Equal(1, backend.CreateCalls);
        }
        finally
        {
            foreach (var preparation in preparations) preparation.Cancel();
            foreach (var upload in backend.CreatedUploads) upload.Complete();
            await owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.All(preparations, preparation => Assert.True(preparation.IsReleased));
        Assert.Equal((0, 0, 0), PreparationCounts(owner));
        Assert.Equal(1, catalog.Counts.LeaseCount);
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), owner.AllocationBudget.Counts);
        Assert.False(backend.BackendDisposeBeforeUploads);
        Assert.True(owner.IsReleased);
    }

    [Fact]
    public async Task CancelOneSharedPreparation_DoesNotCancelOrReleaseNeighborUpload()
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        var mesh = manifest.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId);
        var aliasKey = mesh.Key with { AssetId = ResourceTestFixture.World };
        var candidateManifest = new RenderContentManifest(manifest.ProjectId, manifest.BuildId, manifest.Generation,
            [mesh, new(aliasKey, mesh.Definition)]);
        using var scope = catalog.Acquire(manifest);
        using var candidateScope = catalog.Acquire(candidateManifest);
        var backend = new ResourceTestBackend(holdUploads: true);
        await using var owner = Owner(catalog, manifest, backend);
        var neighbor = owner.Create(scope);
        var sharedUpload = await backend.WaitForUploadAsync(1);
        try
        {
            await sharedUpload.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            sharedUpload.Complete();
            await neighbor.Completion.WaitAsync(TimeSpan.FromSeconds(10));
            var candidate = owner.Create(candidateScope);
            var uniqueUpload = await backend.WaitForUploadAsync(2);
            await uniqueUpload.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(mesh.Key, sharedUpload.Request.Mesh.Key);
            Assert.Equal(aliasKey, uniqueUpload.Request.Mesh.Key);
            Assert.Equal(RenderPreparationState.Pending, candidate.State);
            Assert.Equal(new RenderAllocationCounts(512, 256, 480), owner.AllocationBudget.Counts);
            candidate.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => candidate.Completion.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(RenderPreparationState.Cancelled, candidate.State);
            await candidate.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

            Assert.False(sharedUpload.CancellationObserved.Task.IsCompleted);
            Assert.Equal(RenderPreparationState.Ready, neighbor.State);
            Assert.Equal((2, 0, 3), (backend.CreateCalls, sharedUpload.DisposeCalls, catalog.Counts.LeaseCount));
            Assert.Equal(1, uniqueUpload.DisposeCalls);
            Assert.True(uniqueUpload.IsReleased);
            Assert.Equal(new RenderAllocationCounts(256, 128, 240), owner.AllocationBudget.Counts);
            Assert.Equal(mesh.Key, owner.ValidateHandle(neighbor.GetMeshHandle(mesh.Key)));
            await neighbor.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(1, sharedUpload.DisposeCalls);
            Assert.Equal(2, catalog.Counts.LeaseCount);
            Assert.Equal(new RenderAllocationCounts(0, 0, 0), owner.AllocationBudget.Counts);
        }
        finally { foreach (var upload in backend.CreatedUploads) upload.Complete(); await owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    [Fact]
    public async Task CancelLastPreparation_DrainsLateSuccessBeforeDisposingUploadAndCpuLease()
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        using var scope = catalog.Acquire(manifest);
        var backend = new ResourceTestBackend(holdUploads: true) { IgnoreCancellation = true };
        await using var owner = Owner(catalog, manifest, backend);
        var preparation = owner.Create(scope);
        var upload = await backend.WaitForUploadAsync(1);
        try
        {
            await upload.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            preparation.Cancel();
            await upload.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var disposal = preparation.DisposeAsync().AsTask();

            Assert.False(disposal.IsCompleted);
            Assert.False(preparation.Completion.IsCompleted);
            Assert.Equal(RenderPreparationState.Cancelled, preparation.State);
            Assert.False(preparation.IsReleased);
            Assert.False(upload.PrepareExited.Task.IsCompleted);
            Assert.Equal(0, upload.DisposeCalls);
            Assert.Equal(1, owner.Counts.EntryCount);
            Assert.Equal(2, catalog.Counts.LeaseCount);
            Assert.Equal(new RenderAllocationCounts(256, 128, 240), owner.AllocationBudget.Counts);
            upload.Complete();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preparation.Completion.WaitAsync(TimeSpan.FromSeconds(10)));
            await disposal.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(upload.IsReady);
            Assert.True(upload.IsReleased);
            Assert.True(preparation.IsReleased);
            Assert.False(upload.DisposedBeforePrepareExit);
            Assert.Equal((1, 0, 1), (upload.DisposeCalls, owner.Counts.EntryCount, catalog.Counts.LeaseCount));
            Assert.Equal(new RenderAllocationCounts(0, 0, 0), owner.AllocationBudget.Counts);
        }
        finally { upload.Complete(); await owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    [Fact]
    public async Task Stop_ClosesAdmissionAndDrainsAcceptedWorkBeforeBackendRelease()
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        using var scope = catalog.Acquire(manifest);
        var backend = new ResourceTestBackend(holdUploads: true) { IgnoreCancellation = true };
        await using var owner = Owner(catalog, manifest, backend);
        var preparation = owner.Create(scope);
        var upload = await backend.WaitForUploadAsync(1);
        try
        {
            await upload.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var stopping = owner.StopAsync();
            await upload.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(owner.Counts.AdmissionClosed);
            Assert.False(stopping.IsCompleted);
            Assert.False(owner.IsReleased);
            Assert.False(preparation.IsReleased);
            Assert.Equal((0, 0), (upload.DisposeCalls, backend.DisposeCalls));
            Assert.Equal(2, catalog.Counts.LeaseCount);
            Assert.Equal(RenderResourceError.Closed,
                Assert.Throws<RenderResourceException>(() => owner.Create(scope)).Error);
            Assert.Equal(1, backend.CreateCalls);
            Assert.Equal(new RenderAllocationCounts(256, 128, 240), owner.AllocationBudget.Counts);
            upload.Complete();
            await stopping.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(owner.IsReleased);
            Assert.True(preparation.IsReleased);
            Assert.True(upload.IsReleased);
            Assert.True(backend.IsReleased);
            Assert.False(upload.DisposedBeforePrepareExit);
            Assert.False(backend.BackendDisposeBeforeUploads);
            Assert.Equal(new[] { "upload:1", "backend" }, backend.DisposalLog.ToArray());
            Assert.Equal((1, 1, 1), (upload.DisposeCalls, backend.DisposeCalls, catalog.Counts.LeaseCount));
            Assert.Equal(new RenderAllocationCounts(0, 0, 0), owner.AllocationBudget.Counts);
            await owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal((1, 1), (upload.DisposeCalls, backend.DisposeCalls));
        }
        finally { upload.Complete(); await owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    [Fact]
    public async Task StopWaitCancellation_DoesNotAbandonOwnedCleanupOrReportReleased()
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        using var scope = catalog.Acquire(manifest);
        var backend = new ResourceTestBackend(holdUploads: true) { IgnoreCancellation = true };
        await using var owner = Owner(catalog, manifest, backend);
        var preparation = owner.Create(scope);
        var upload = await backend.WaitForUploadAsync(1);
        using var cancelWait = new CancellationTokenSource();
        try
        {
            await upload.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var wait = owner.StopAsync(cancelWait.Token);
            await upload.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancelWait.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait.WaitAsync(TimeSpan.FromSeconds(10)));

            Assert.True(owner.Counts.AdmissionClosed);
            Assert.False(owner.IsReleased);
            Assert.False(preparation.IsReleased);
            Assert.Equal((0, 0), (upload.DisposeCalls, backend.DisposeCalls));
            Assert.Equal(2, catalog.Counts.LeaseCount);
            Assert.Equal(new RenderAllocationCounts(256, 128, 240), owner.AllocationBudget.Counts);
            upload.Complete();
            await owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(owner.IsReleased);
            Assert.True(preparation.IsReleased);
            Assert.False(upload.DisposedBeforePrepareExit);
            Assert.False(backend.BackendDisposeBeforeUploads);
            Assert.Equal(new[] { "upload:1", "backend" }, backend.DisposalLog.ToArray());
            Assert.Equal((1, 1, 1), (upload.DisposeCalls, backend.DisposeCalls, catalog.Counts.LeaseCount));
            Assert.Equal(new RenderAllocationCounts(0, 0, 0), owner.AllocationBudget.Counts);
        }
        finally { upload.Complete(); await owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    [Fact]
    public async Task UploadFailure_PreservesCauseAndCleansResourcesBeforeCompletion()
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        using var scope = catalog.Acquire(manifest);
        var cause = new InvalidOperationException("upload preparation marker");
        var backend = new ResourceTestBackend { PrepareError = cause };
        await using var owner = Owner(catalog, manifest, backend);
        var preparation = owner.Create(scope);

        Assert.Same(cause, await Assert.ThrowsAsync<InvalidOperationException>(() => preparation.Completion.WaitAsync(TimeSpan.FromSeconds(10))));
        var upload = Assert.Single(backend.CreatedUploads);
        Assert.Same(cause, preparation.Error);
        Assert.Equal(RenderPreparationState.Failed, preparation.State);
        Assert.True(preparation.IsReleased);
        Assert.True(upload.IsReleased);
        Assert.False(upload.IsReady);
        Assert.False(upload.DisposedBeforePrepareExit);
        Assert.Equal((1, 1), (upload.PrepareCalls, upload.DisposeCalls));
        Assert.Equal((0, 0, 1), (owner.Counts.EntryCount, owner.Counts.PendingCleanup, catalog.Counts.LeaseCount));
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), owner.AllocationBudget.Counts);
        await preparation.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, upload.DisposeCalls);
    }

    [Fact]
    public async Task AlreadyCancelledToken_NeverCreatesBackendAndReleasesRetainedCpuScope()
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        using var scope = catalog.Acquire(manifest);
        var backend = new ResourceTestBackend();
        var factory = new ResourceTestBackendFactory(backend);
        await using var owner = new RenderDeviceOwner(catalog, factory, ResourceTestFixture.Request(manifest));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var preparation = owner.Create(scope, cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preparation.Completion.WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.Equal(RenderPreparationState.Cancelled, preparation.State);
        Assert.True(preparation.IsReleased);
        Assert.Equal((0, 0, 0), (factory.Calls, backend.CreateCalls, owner.Counts.EntryCount));
        Assert.Equal(1, catalog.Counts.LeaseCount);
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), owner.AllocationBudget.Counts);
        await preparation.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(RenderPreparationState.Disposed, preparation.State);
        Assert.Equal(0, backend.DisposeCalls);
    }

    [Fact]
    public async Task UnknownUploadOutcome_FencesAdmissionAndRetainsFailedCleanupBytesAndOriginalCause()
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        using var scope = catalog.Acquire(manifest);
        var cause = new RenderResourceException(RenderResourceError.UnknownOutcome, "unknown completion marker");
        var backend = new ResourceTestBackend(holdUploads: true);
        await using var owner = Owner(catalog, manifest, backend);
        var preparation = owner.Create(scope);
        var upload = await backend.WaitForUploadAsync(1);
        try
        {
            await upload.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            upload.DisposeFailuresRemaining = 1;
            upload.Fail(cause);
            var error = await Assert.ThrowsAsync<AggregateException>(() => preparation.Completion.WaitAsync(TimeSpan.FromSeconds(10)));

            Assert.Contains(cause, error.Flatten().InnerExceptions);
            Assert.Contains(error.Flatten().InnerExceptions, failure => failure.Message == "upload cleanup marker");
            Assert.Same(error, preparation.Error);
            Assert.Equal(RenderPreparationState.Failed, preparation.State);
            Assert.False(preparation.IsReleased);
            Assert.False(upload.IsReleased);
            Assert.False(owner.IsReleased);
            Assert.True(owner.Counts.AdmissionClosed);
            Assert.Equal((1, 1, 1), (owner.Counts.EntryCount, owner.Counts.PendingCleanup, owner.Counts.LivePreparations));
            Assert.Equal((1, 0, 2), (upload.DisposeCalls, backend.DisposeCalls, catalog.Counts.LeaseCount));
            Assert.Equal(new RenderAllocationCounts(256, 128, 240), owner.AllocationBudget.Counts);
            Assert.Equal(RenderResourceError.Closed, Assert.Throws<RenderResourceException>(() => owner.Create(scope)).Error);
            Assert.Equal(RenderResourceError.Closed, Assert.Throws<RenderResourceException>(() => owner.AllocationBudget.Reserve(0, 0, 0)).Error);
            await owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(owner.IsReleased);
            Assert.True(preparation.IsReleased);
            Assert.True(upload.IsReleased);
            Assert.Same(error, preparation.Error);
            Assert.Contains(cause, Assert.IsType<AggregateException>(preparation.Error).Flatten().InnerExceptions);
            Assert.Equal((2, 1, 1), (upload.DisposeCalls, backend.DisposeCalls, catalog.Counts.LeaseCount));
            Assert.Equal(new RenderAllocationCounts(0, 0, 0), owner.AllocationBudget.Counts);
            Assert.False(backend.BackendDisposeBeforeUploads);
        }
        finally { upload.Complete(); await owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    [Fact]
    public async Task FailedCandidateBudget_LeavesOldReadyResourceAndAllowsRetryAfterOldRelease()
    {
        using var catalog = new RenderContentCatalog();
        var manifest = ResourceTestFixture.Content();
        var candidateManifest = ResourceTestFixture.ChangedContent(manifest, changeMesh: true);
        using var oldScope = catalog.Acquire(manifest);
        using var candidateScope = catalog.Acquire(candidateManifest);
        var backend = new ResourceTestBackend();
        var options = new RenderResourceOptions
        {
            MaximumGeometryBytes = 256, MaximumUploadStagingBytes = 128, MaximumCpuStagingBytes = 240
        };
        await using var owner = new RenderDeviceOwner(catalog, new ResourceTestBackendFactory(backend), ResourceTestFixture.Request(manifest), options);
        var old = owner.Create(oldScope);
        await old.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        var oldKey = RenderDeviceOwnerTests.MeshKey(manifest);
        var oldHandle = old.GetMeshHandle(oldKey);
        Assert.Equal(new RenderAllocationCounts(256, 128, 240), owner.AllocationBudget.Counts);
        var candidate = owner.Create(candidateScope);

        Assert.Equal(RenderResourceError.LimitExceeded,
            (await Assert.ThrowsAsync<RenderResourceException>(() => candidate.Completion.WaitAsync(TimeSpan.FromSeconds(10)))).Error);

        Assert.True(candidate.IsReleased);
        Assert.Equal(RenderPreparationState.Failed, candidate.State);
        Assert.Equal(oldKey, owner.ValidateHandle(oldHandle));
        Assert.Equal(1, owner.Counts.EntryCount);
        Assert.Equal(new RenderAllocationCounts(256, 128, 240), owner.AllocationBudget.Counts);
        Assert.Equal(3, catalog.Counts.LeaseCount);
        Assert.Equal(2, backend.CreateCalls);
        Assert.True(backend.CreatedUploads[1].IsReleased);
        await old.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), owner.AllocationBudget.Counts);
        var retry = owner.Create(candidateScope);
        await retry.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        var candidateKey = RenderDeviceOwnerTests.MeshKey(candidateManifest);

        Assert.NotEqual(oldKey.Revision, candidateKey.Revision);
        Assert.Equal(candidateKey, owner.ValidateHandle(retry.GetMeshHandle(candidateKey)));
        Assert.Equal(RenderResourceError.StaleHandle,
            Assert.Throws<RenderResourceException>(() => owner.ValidateHandle(oldHandle)).Error);
        Assert.Equal(new RenderAllocationCounts(256, 128, 240), owner.AllocationBudget.Counts);
        Assert.Equal(3, backend.CreateCalls);
        await retry.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.All(backend.CreatedUploads, upload => Assert.Equal(1, upload.DisposeCalls));
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), owner.AllocationBudget.Counts);
        Assert.Equal(2, catalog.Counts.LeaseCount);
    }

    [Fact]
    public async Task CleanupFailure_RetainsOnlyFailedEntryAndBytesAndRetriesWithoutRepeatingSuccessfulStage()
    {
        var source = ResourceTestFixture.Content();
        var mesh = source.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId);
        var aliasKey = mesh.Key with { AssetId = ResourceTestFixture.World };
        var manifest = new RenderContentManifest(source.ProjectId, source.BuildId, source.Generation,
            [mesh, new(aliasKey, mesh.Definition)]);
        using var catalog = new RenderContentCatalog();
        using var scope = catalog.Acquire(manifest);
        var backend = new ResourceTestBackend();
        await using var owner = Owner(catalog, manifest, backend);
        var preparation = owner.Create(scope);
        await preparation.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        var first = backend.CreatedUploads[0];
        var second = backend.CreatedUploads[1];
        second.DisposeFailuresRemaining = 1;
        var cleanup = await Assert.ThrowsAsync<AggregateException>(() => preparation.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.Contains(cleanup.Flatten().InnerExceptions, error => error.Message == "upload cleanup marker");
        Assert.False(preparation.IsReleased);
        Assert.True(first.IsReleased);
        Assert.False(second.IsReleased);
        Assert.Equal((1, 1), (first.DisposeCalls, second.DisposeCalls));
        Assert.Equal((1, 1, 1), (owner.Counts.EntryCount, owner.Counts.PendingCleanup, owner.Counts.LivePreparations));
        Assert.Equal(2, catalog.Counts.LeaseCount);
        Assert.Equal(new RenderAllocationCounts(256, 128, 240), owner.AllocationBudget.Counts);
        Assert.Equal(0, backend.DisposeCalls);
        await preparation.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(preparation.IsReleased);
        Assert.True(second.IsReleased);
        Assert.Equal((1, 2), (first.DisposeCalls, second.DisposeCalls));
        Assert.Equal((0, 0, 0), (owner.Counts.EntryCount, owner.Counts.PendingCleanup, owner.Counts.LivePreparations));
        Assert.Equal(1, catalog.Counts.LeaseCount);
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), owner.AllocationBudget.Counts);
        await owner.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal((1, 2, 1), (first.DisposeCalls, second.DisposeCalls, backend.DisposeCalls));
        Assert.False(backend.BackendDisposeBeforeUploads);
    }

    private static RenderDeviceOwner Owner(RenderContentCatalog catalog, RenderContentManifest manifest, ResourceTestBackend backend)
        => new(catalog, new ResourceTestBackendFactory(backend), ResourceTestFixture.Request(manifest));

    private static (int Active, int Queued, int Live) PreparationCounts(RenderDeviceOwner owner)
        => (owner.Counts.ActivePreparations, owner.Counts.QueuedPreparations, owner.Counts.LivePreparations);
}
