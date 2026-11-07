using TrueMoon.Aether.Rendering.Resources;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering.Resources;

public sealed class RenderBackendContractTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task NullFactoryResult_FailsInsteadOfPublishingAndReleasesThePrivateScope(bool uploadFactory)
    {
        using var catalog = new RenderContentCatalog(); var content = ResourceTestFixture.Content(); using var scope = catalog.Acquire(content);
        var backend = new ResourceTestBackend(); var factories = new ResourceTestBackendFactory(backend);
        if (uploadFactory) backend.CreateOverride = (_, _) => null!; else factories.CreateOverride = _ => null!;
        await using var device = new RenderDeviceOwner(catalog, factories, ResourceTestFixture.Request(content));
        var operation = device.Create(scope);
        var error = await Assert.ThrowsAsync<RenderResourceException>(() => operation.Completion.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(RenderResourceError.BackendFailure, error.Error); Assert.Same(error, operation.Error);
        Assert.Equal(RenderPreparationState.Failed, operation.State); Assert.True(operation.IsReleased);
        Assert.Equal(1, catalog.Counts.LeaseCount); Assert.Equal(0, device.Counts.EntryCount);
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), device.AllocationBudget.Counts);
        Assert.Equal(1, factories.Calls); Assert.Equal(uploadFactory ? 1 : 0, backend.CreateCalls);
        await device.DisposeAsync(); Assert.Equal(uploadFactory ? 1 : 0, backend.DisposeCalls);
    }

    [Fact]
    public async Task SuccessfulTaskWithoutConfirmedReadiness_IsNotAReadyResource()
    {
        using var catalog = new RenderContentCatalog(); var content = ResourceTestFixture.Content(); using var scope = catalog.Acquire(content);
        var backend = new ResourceTestBackend(holdUploads: true);
        await using var device = new RenderDeviceOwner(catalog, new ResourceTestBackendFactory(backend), ResourceTestFixture.Request(content));
        var operation = device.Create(scope); var upload = await backend.WaitForUploadAsync(1);
        await upload.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(10)); upload.ReportReady = false; upload.Complete();
        var error = await Assert.ThrowsAsync<RenderResourceException>(() => operation.Completion.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(RenderResourceError.BackendFailure, error.Error); Assert.Equal(RenderPreparationState.Failed, operation.State); Assert.True(operation.IsReleased);
        Assert.False(upload.IsReady); Assert.True(upload.IsReleased); Assert.Equal(1, upload.DisposeCalls);
        Assert.Equal(0, device.Counts.EntryCount); Assert.Equal(1, catalog.Counts.LeaseCount);
        Assert.Equal(new RenderAllocationCounts(0, 0, 0), device.AllocationBudget.Counts);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CompletedDisposalWithoutReleasedProof_RetainsOwnershipUntilExplicitRetry(bool backendContext)
    {
        using var catalog = new RenderContentCatalog(); var content = ResourceTestFixture.Content(); using var scope = catalog.Acquire(content);
        var backend = new ResourceTestBackend(); await using var device = new RenderDeviceOwner(catalog, new ResourceTestBackendFactory(backend), ResourceTestFixture.Request(content));
        var operation = device.Create(scope); await operation.Completion;
        var upload = Assert.Single(backend.CreatedUploads);
        if (backendContext) { await operation.DisposeAsync(); backend.ReportReleased = false; }
        else upload.ReportReleased = false;
        Exception error = backendContext
            ? await Assert.ThrowsAsync<RenderResourceException>(() => device.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10)))
            : await Assert.ThrowsAsync<AggregateException>(() => device.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10)));
        IEnumerable<Exception> causes = error is AggregateException aggregate ? aggregate.Flatten().InnerExceptions : [error];
        Assert.Contains(causes, item => item is RenderResourceException { Error: RenderResourceError.CleanupFailed });
        Assert.False(device.IsReleased); Assert.True(device.Counts.AdmissionClosed);
        Assert.Equal(backendContext ? 0 : 1, device.Counts.EntryCount);
        Assert.Equal(backendContext ? 0L : 256L, device.AllocationBudget.Counts.GeometryBytes);
        Assert.Equal(backendContext ? 1 : 2, catalog.Counts.LeaseCount);
        upload.ReportReleased = true; backend.ReportReleased = true; await device.DisposeAsync();
        Assert.True(device.IsReleased); Assert.True(operation.IsReleased); Assert.True(backend.IsReleased);
        Assert.Equal(0, device.Counts.EntryCount); Assert.Equal(1, catalog.Counts.LeaseCount);
        Assert.Equal(backendContext ? 1 : 2, upload.DisposeCalls); Assert.Equal(backendContext ? 2 : 1, backend.DisposeCalls);
        Assert.False(upload.DisposedBeforePrepareExit);
    }
}
