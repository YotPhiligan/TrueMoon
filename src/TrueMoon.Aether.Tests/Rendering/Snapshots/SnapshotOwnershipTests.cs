using System.Numerics;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using TrueMoon.Aether.Rendering.Snapshots;
using TrueMoon.Aether.Tests.Rendering.Resources;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering.Snapshots;

public sealed class SnapshotOwnershipTests
{
    [Fact]
    public async Task AliasedBackendOwnerIsRejectedWithoutExecutingOrDisposingAnotherAcceptedUse()
    {
        await using var t = await SnapshotFixture.CreateAsync(); await t.CaptureAsync(); var backend = new ControlledSubmission();
        var factory = new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(_ => backend);
        var first = await t.View.SubmitAsync((await t.View.TakeLatestAsync())!, factory);
        await backend.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            t.Advance(1); await t.CaptureAsync(1); var second = await t.View.SubmitAsync((await t.View.TakeLatestAsync())!, factory);
            var failure = await Assert.ThrowsAsync<RenderResourceException>(() => second.Completion); Assert.Equal(RenderResourceError.Conflict, failure.Error);
            await t.View.CollectAsync(); Assert.True(second.IsReleased); Assert.False(first.IsReleased); Assert.False(t.View.Counts.Closed);
            Assert.Equal(0, backend.CleanupCalls); Assert.Equal(1, t.View.Counts.Submissions); Assert.False(first.Completion.IsCompleted);
            backend.Complete(); await first.Completion; await t.View.CollectAsync(); Assert.True(first.IsReleased); Assert.Equal(1, backend.CleanupCalls);
        }
        finally { backend.Complete(); await first.DisposeAsync(); }
    }

    [Fact]
    public async Task CancellationBeforeAdmissionKeepsTheReservationAndNeverInvokesBackendFactory()
    {
        await using var t = await SnapshotFixture.CreateAsync(); var data = await t.CaptureAsync(); var recording = (await t.View.TakeLatestAsync())!;
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); var calls = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => t.View.SubmitAsync(recording,
            new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(_ => { calls++; return new ControlledSubmission(); }), cancellation.Token));
        Assert.Equal(0, calls); Assert.False(recording.IsReleased); Assert.True(t.View.Counts.HasRecording); Assert.Equal(0, t.View.Counts.Submissions);
        Assert.Same(data, recording.Snapshot); await recording.DisposeAsync(); Assert.Null(await t.View.TakeLatestAsync()); Assert.Same(data, t.View.Current);
    }

    [Fact]
    public async Task AcceptedCancellationIsCooperativeAndRetainsUseUntilActualCompletion()
    {
        await using var t = await SnapshotFixture.CreateAsync(); await t.CaptureAsync(); var backend = new ControlledSubmission();
        using var cancellation = new CancellationTokenSource();
        var use = await t.View.SubmitAsync((await t.View.TakeLatestAsync())!, new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(_ => backend), cancellation.Token);
        await backend.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            cancellation.Cancel(); await backend.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(use.Completion.IsCompleted); Assert.False(use.IsReleased); Assert.False(backend.IsReleased);
            Assert.False(t.View.Counts.Closed); Assert.Equal(1, t.View.Counts.Submissions); Assert.Equal(0, backend.CleanupCalls);
            backend.Complete(); await use.Completion; await t.View.CollectAsync(); Assert.True(use.IsReleased); Assert.Equal(1, backend.CleanupCalls); Assert.False(t.View.Counts.Closed);
        }
        finally { backend.Complete(); await use.DisposeAsync(); }
    }

    [Fact]
    public async Task ForeignRecordingAndForeignBindingAreRejectedWithoutTransferringTheirOwners()
    {
        await using var first = await SnapshotFixture.CreateAsync(); await using var second = await SnapshotFixture.CreateAsync();
        var original = await first.CaptureAsync(); var recording = (await first.View.TakeLatestAsync())!; var calls = 0;
        var foreign = await Assert.ThrowsAsync<RenderResourceException>(() => second.View.SubmitAsync(recording,
            new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(_ => { calls++; return new ControlledSubmission(); })));
        Assert.Equal(RenderResourceError.Conflict, foreign.Error); Assert.False(recording.IsReleased); Assert.Equal(0, calls);
        Assert.Same(original, recording.Snapshot); Assert.Equal(0, second.View.Counts.Submissions); Assert.Null(second.View.Current);
        var binding = Assert.Throws<RenderResourceException>(() => second.Host.Create(new RenderSnapshotViewRequest(first.Binding, original.SceneId, 7)));
        Assert.Equal(RenderResourceError.InvalidIdentity, binding.Error); await recording.DisposeAsync(); Assert.False(first.View.Counts.HasRecording);
    }

    [Fact]
    public async Task MailboxCoalescesAndRecordingTransferSharesOneEnvelopeUntilItsLastRoleReleases()
    {
        await using var t = await SnapshotFixture.CreateAsync(); var first = await t.CaptureAsync();
        t.Advance(1); var second = await t.CaptureAsync(1);
        Assert.Equal(1, t.View.Counts.Snapshots); Assert.Equal(second.PayloadBytes, t.View.Counts.PayloadBytes);
        var recording = (await t.View.TakeLatestAsync())!; Assert.Same(second, recording.Snapshot);
        Assert.False(t.View.Counts.HasPending); Assert.True(t.View.Counts.HasRecording);
        var conflict = await Assert.ThrowsAsync<RenderResourceException>(() => t.View.TakeLatestAsync()); Assert.Equal(RenderResourceError.Conflict, conflict.Error);
        t.Advance(2); var third = await t.CaptureAsync(2); Assert.Equal(2, t.View.Counts.Snapshots); Assert.Same(second, recording.Snapshot);
        Assert.Equal(second.PayloadBytes + third.PayloadBytes, t.View.Counts.PayloadBytes);
        await recording.DisposeAsync(); await recording.DisposeAsync(); Assert.True(recording.IsReleased);
        Assert.Equal(1, t.View.Counts.Snapshots); Assert.False(t.View.Counts.HasRecording);
        Assert.Equal(third.PayloadBytes, t.View.Counts.PayloadBytes); Assert.Same(third, t.View.Current);
        Assert.Equal(0UL, first.Tick); Assert.Equal(1UL, second.Tick); Assert.Equal(2UL, third.Tick);
        await t.View.DisposeAsync(); Assert.Equal(0, t.View.Counts.Snapshots); Assert.Equal(0, t.View.Counts.PayloadBytes);
        Assert.Null(t.View.Current); Assert.Equal(2, t.Host.Content.Counts.LeaseCount);
    }

    [Fact]
    public async Task ReloadKeepsOldRevisionAndHandleAliveThroughSubmittedUseUntilActualCompletion()
    {
        await using var t = await SnapshotFixture.CreateAsync(); var original = await t.CaptureAsync();
        var recording = (await t.View.TakeLatestAsync())!; var backend = new ControlledSubmission();
        var use = await t.View.SubmitAsync(recording, new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(_ => backend));
        try
        {
            await backend.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var candidate = t.Host.Content.Acquire(ResourceTestFixture.ChangedContent(t.Content, changeMesh: true));
            var reloaded = await t.Binding.ReloadAsync(candidate); Assert.True(reloaded.Committed);
            Assert.Equal(2, t.Device.Counts.EntryCount); Assert.Equal(original.Draws[0].Mesh, t.Device.ValidateHandle(original.Draws[0].Handle));
            t.Advance(1); var current = await t.CaptureAsync(1);
            Assert.Equal(2UL, current.BindingRevision); Assert.NotEqual(original.ManifestDigest, current.ManifestDigest);
            Assert.NotEqual(original.Draws[0].Mesh, current.Draws[0].Mesh); Assert.Equal(2, t.View.Counts.Snapshots);
            Assert.Equal(0, t.Backend.CreatedUploads[0].DisposeCalls); Assert.Equal(512L, t.Device.AllocationBudget.Counts.GeometryBytes);
            t.World.DestroyEntity(t.First); Assert.Equal(original.Draws[0].Entity, recording.Envelope.Data!.Draws[0].Entity);
            backend.Complete(); await use.Completion.WaitAsync(TimeSpan.FromSeconds(10)); await t.View.CollectAsync();
            Assert.True(use.IsReleased); Assert.Equal(1, backend.CleanupCalls); Assert.Equal(1, t.View.Counts.Snapshots);
            Assert.Equal(1, t.Backend.CreatedUploads[0].DisposeCalls); Assert.Equal(256L, t.Device.AllocationBudget.Counts.GeometryBytes);
            var stale = Assert.Throws<RenderResourceException>(() => t.Device.ValidateHandle(original.Draws[0].Handle)); Assert.Equal(RenderResourceError.StaleHandle, stale.Error);
            Assert.Equal(current.Draws[0].Mesh, t.Device.ValidateHandle(current.Draws[0].Handle));
        }
        finally { backend.Complete(); await use.DisposeAsync(); }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task SubmissionCapacity_RetainsAcceptedUsesAndRejectsTheNextBeforeFactory(int maximum)
    {
        await using var t = await SnapshotFixture.CreateAsync(new() { MaximumSubmissions = maximum });
        var owners = new List<ControlledSubmission>(); var uses = new List<RenderSnapshotSubmission>(); var factoryCalls = 0;
        try
        {
            for (var i = 0; i < maximum; i++)
            {
                t.Advance((ulong)i); await t.CaptureAsync((ulong)i); var recording = (await t.View.TakeLatestAsync())!;
                var owner = new ControlledSubmission(); owners.Add(owner);
                var use = await t.View.SubmitAsync(recording, new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(data =>
                { Interlocked.Increment(ref factoryCalls); Assert.True(t.View.Counts.Submissions >= 1); Assert.Equal(t.Device.Identity, data.Identity); return owner; }));
                uses.Add(use); await owner.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            t.Advance((ulong)maximum); await t.CaptureAsync((ulong)maximum); var rejected = (await t.View.TakeLatestAsync())!;
            var error = await Assert.ThrowsAsync<RenderResourceException>(() => t.View.SubmitAsync(rejected,
                new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(_ => { Interlocked.Increment(ref factoryCalls); return new ControlledSubmission(); })));
            Assert.Equal(RenderResourceError.LimitExceeded, error.Error); Assert.Equal(maximum, factoryCalls); Assert.Equal(maximum, t.View.Counts.Submissions);
            Assert.False(rejected.IsReleased); Assert.True(t.View.Counts.HasRecording); await rejected.DisposeAsync();
            foreach (var owner in owners) owner.Complete();
            await Task.WhenAll(uses.Select(use => use.Completion)).WaitAsync(TimeSpan.FromSeconds(10)); await t.View.CollectAsync();
            Assert.Equal(0, t.View.Counts.Submissions); Assert.Equal(1, t.View.Counts.Snapshots); Assert.All(uses, use => Assert.True(use.IsReleased));
            Assert.All(owners, owner => Assert.Equal(1, owner.CleanupCalls));
        }
        finally { foreach (var owner in owners) owner.Complete(); foreach (var use in uses) await use.DisposeAsync(); }
    }

    [Fact]
    public async Task CancelledStopWaitLeavesActualInFlightOwnershipUntilBackendCompletes()
    {
        await using var t = await SnapshotFixture.CreateAsync(); await t.CaptureAsync();
        var backend = new ControlledSubmission(); var use = await t.View.SubmitAsync((await t.View.TakeLatestAsync())!, new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(_ => backend));
        await backend.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            using var wait = new CancellationTokenSource(); var stopping = t.View.StopAsync(wait.Token);
            await backend.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10)); wait.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stopping);
            Assert.True(t.View.Counts.Closed); Assert.False(t.View.IsReleased); Assert.False(use.IsReleased);
            Assert.False(backend.IsReleased); Assert.Equal(0, t.Backend.CreatedUploads[0].DisposeCalls); Assert.True(t.Host.Content.Counts.LeaseCount >= 3);
            var closed = await Assert.ThrowsAsync<RenderResourceException>(() => t.View.TakeLatestAsync()); Assert.Equal(RenderResourceError.Closed, closed.Error);
            backend.Complete(); await t.View.DisposeAsync();
            Assert.True(use.IsReleased); Assert.True(t.View.IsReleased); Assert.Equal(1, backend.CleanupCalls);
            Assert.Equal(0, t.View.Counts.PayloadBytes); Assert.Equal(2, t.Host.Content.Counts.LeaseCount);
        }
        finally { backend.Complete(); await use.DisposeAsync(); }
    }

    [Fact]
    public async Task MissingCompletionEvidenceFencesTheViewAndCleanupFailureRetainsOwnerForRetry()
    {
        await using var t = await SnapshotFixture.CreateAsync(); var data = await t.CaptureAsync();
        var backend = new ControlledSubmission { ReportComplete = false, CleanupFailures = 1 };
        var use = await t.View.SubmitAsync((await t.View.TakeLatestAsync())!, new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(_ => backend));
        await backend.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); backend.Complete();
        var unknown = await Assert.ThrowsAsync<RenderResourceException>(() => use.Completion); Assert.Equal(RenderResourceError.UnknownOutcome, unknown.Error);
        Assert.Same(unknown, use.Error); Assert.True(t.View.Counts.Closed); Assert.Same(data, t.View.Current);
        Assert.True(t.Device.Counts.AdmissionClosed);
        var closedPreparation = Assert.Throws<RenderResourceException>(() => t.Device.Create(t.Scope)); Assert.Equal(RenderResourceError.Closed, closedPreparation.Error);
        var closedBudget = Assert.Throws<RenderResourceException>(() => t.Device.AllocationBudget.Reserve(1, 0, 0)); Assert.Equal(RenderResourceError.Closed, closedBudget.Error);
        await Assert.ThrowsAsync<AggregateException>(() => t.View.CollectAsync()); Assert.False(use.IsReleased);
        Assert.Equal(1, t.View.Counts.Submissions); Assert.False(backend.IsReleased); Assert.Equal(data.PayloadBytes, t.View.Counts.PayloadBytes);
        var closed = await Assert.ThrowsAsync<RenderResourceException>(() => t.CaptureAsync(1)); Assert.Equal(RenderResourceError.Closed, closed.Error);
        await t.View.CollectAsync(); Assert.True(use.IsReleased); Assert.True(backend.IsReleased); Assert.Equal(2, backend.CleanupCalls);
        Assert.Equal(0, t.View.Counts.Submissions); Assert.True(t.View.Counts.Closed); Assert.Same(unknown, use.Error);
        await t.View.DisposeAsync(); Assert.Equal(0, t.View.Counts.Snapshots); Assert.Equal(2, t.Host.Content.Counts.LeaseCount);
    }

    [Fact]
    public async Task PartialFactoryFailureIsTrackedBeforeInvocationAndRetainsItsCleanupOwner()
    {
        await using var t = await SnapshotFixture.CreateAsync(); var data = await t.CaptureAsync(); var partial = new PartialOwner(); var original = new IOException("partial creation");
        var use = await t.View.SubmitAsync((await t.View.TakeLatestAsync())!, new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(snapshot =>
        {
            Assert.Same(data, snapshot); Assert.Equal(1, t.View.Counts.Submissions); Assert.Equal(3, t.Host.Content.Counts.LeaseCount);
            throw new RenderResourceCreationException(original, partial);
        }));
        var failure = await Assert.ThrowsAsync<RenderResourceCreationException>(() => use.Completion); Assert.Same(original, failure.InnerException); Assert.Same(partial, failure.CleanupOwner);
        await Assert.ThrowsAsync<AggregateException>(() => t.View.CollectAsync()); Assert.False(use.IsReleased); Assert.Equal(1, t.View.Counts.Submissions);
        await t.View.CollectAsync(); Assert.True(use.IsReleased); Assert.Equal(2, partial.Disposals); Assert.Equal(0, t.View.Counts.Submissions);
    }

    [Fact]
    public async Task BackendExecuteFailurePreservesOriginalOutcomeAndRequiresSafeCleanup()
    {
        await using var t = await SnapshotFixture.CreateAsync(); await t.CaptureAsync(); var backend = new ControlledSubmission(); var original = new IOException("execute");
        var use = await t.View.SubmitAsync((await t.View.TakeLatestAsync())!, new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(_ => backend));
        await backend.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); backend.Fail(original);
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => use.Completion)); Assert.True(t.View.Counts.Closed); Assert.False(use.IsReleased);
        await t.View.CollectAsync(); Assert.Same(original, use.Error); Assert.True(use.IsReleased); Assert.True(backend.IsReleased); Assert.Equal(1, backend.CleanupCalls);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CameraGenerationAndSuspendInvalidateUnsubmittedRecordingAndAllowPausedResume(bool suspend)
    {
        await using var t = await SnapshotFixture.CreateAsync(); var data = await t.CaptureAsync(); var recording = (await t.View.TakeLatestAsync())!;
        var settings = suspend ? t.Configuration with { Width = 0, Height = 0, SurfaceGeneration = 2 } :
            t.Configuration with { Camera = RenderCameraSource.External(Matrix4x4.CreateTranslation(1, 0, 6), PerspectiveCamera.Default), CameraRevision = 2 };
        var next = await t.View.CaptureAsync(0, settings); Assert.Equal(suspend, next == null);
        if (suspend) Assert.Null(t.View.Current); else Assert.Same(next, t.View.Current);
        var calls = 0;
        var error = await Assert.ThrowsAsync<RenderResourceException>(() => t.View.SubmitAsync(recording,
            new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(_ => { calls++; return new ControlledSubmission(); })));
        Assert.Equal(RenderResourceError.Conflict, error.Error); Assert.Equal(0, calls); Assert.False(recording.IsReleased);
        Assert.Same(data, recording.Snapshot); await recording.DisposeAsync();
        Assert.Equal(suspend ? 0 : 1, t.View.Counts.Snapshots);
        var resumed = await t.View.CaptureAsync(0, t.Configuration with { CameraRevision = 3, SurfaceGeneration = 3 });
        Assert.NotNull(resumed); Assert.True(resumed!.Sequence > data.Sequence);
    }

    [Fact]
    public async Task CandidateCountPressurePreservesThePreviousCompleteFrame()
    {
        await using var t = await SnapshotFixture.CreateAsync(new() { MaximumSnapshots = 1 }); var original = await t.CaptureAsync(); t.Advance(1);
        var error = await Assert.ThrowsAsync<RenderResourceException>(() => t.CaptureAsync(1)); Assert.Equal(RenderResourceError.LimitExceeded, error.Error);
        Assert.Same(original, t.View.Current); Assert.Equal(1, t.View.Counts.Snapshots); Assert.Equal(original.PayloadBytes, t.View.Counts.PayloadBytes);
        Assert.Equal(3, t.Host.Content.Counts.LeaseCount); Assert.Equal(1, t.Backend.CreateCalls);
    }

    private sealed class PartialOwner : IAsyncDisposable
    {
        internal int Disposals;
        public ValueTask DisposeAsync() { if (++Disposals == 1) throw new IOException("partial cleanup"); return ValueTask.CompletedTask; }
    }
}
