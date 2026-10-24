using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorPreviewTests
{
    private static ResolvedSceneSnapshot Scene(long revision)
    { using var owner = DocumentData.Factory().Create(DocumentData.Empty(revision)); return owner.Read().Resolved; }
    private static TaskCompletionSource<T> Signal<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task Wait(Task task) => task.WaitAsync(TimeSpan.FromSeconds(10));

    [Fact]
    public async Task InactiveSynchronization_DoesNotStartPreviewAndPresentationAppearsOnlyAfterExactAcknowledgement()
    {
        var scene = Scene(7); var started = DocumentData.Signal(); var completion = Signal<ScenePreviewDocument>(); var shows = 0;
        await using var preview = new EditorPreview((snapshot, _) => { Assert.Same(scene, snapshot); Interlocked.Increment(ref shows); started.SetResult(); return completion.Task; }, () => Task.CompletedTask);
        preview.Synchronize(scene); Assert.Equal(0, shows); Assert.False(preview.Read().Active); Assert.False(preview.Read().Busy); Assert.Null(preview.Read().Presented);
        Assert.True(preview.Show(scene)); await Wait(started.Task);
        Assert.True(preview.Read().Active); Assert.True(preview.Read().Busy); Assert.Null(preview.Read().Presented); Assert.False(preview.Pending.IsCompleted);
        var receipt = EditingFixture.Receipt(scene); completion.SetResult(receipt); await Wait(preview.Pending);
        Assert.Same(receipt, preview.Read().Presented); Assert.Equal(7, preview.Read().Presented!.Revision); Assert.False(preview.Read().Busy); Assert.Null(preview.Read().Error);
        preview.Synchronize(scene); Assert.Equal(1, shows); Assert.True(preview.Pending.IsCompleted);
    }

    [Fact]
    public async Task BusyPreview_KeepsCurrentAndOnlyNewestWaitingSnapshotWithoutRetaggingPresentedFrame()
    {
        var first = Scene(7); var superseded = Scene(8); var newest = Scene(9);
        var firstStarted = DocumentData.Signal(); var lastStarted = DocumentData.Signal(); var releaseFirst = Signal<ScenePreviewDocument>(); var releaseLast = Signal<ScenePreviewDocument>();
        var shown = new List<ResolvedSceneSnapshot>(); var concurrent = 0; var maximum = 0;
        await using var preview = new EditorPreview(async (scene, _) =>
        {
            var active = Interlocked.Increment(ref concurrent); maximum = Math.Max(maximum, active); lock (shown) shown.Add(scene);
            try { if (ReferenceEquals(scene, first)) { firstStarted.SetResult(); return await releaseFirst.Task; } lastStarted.SetResult(); return await releaseLast.Task; }
            finally { Interlocked.Decrement(ref concurrent); }
        }, () => Task.CompletedTask);
        try
        {
            Assert.True(preview.Show(first)); await Wait(firstStarted.Task); var pending = preview.Pending;
            preview.Synchronize(superseded); preview.Synchronize(newest); Assert.Same(pending, preview.Pending); Assert.Null(preview.Read().Presented);
            releaseFirst.SetResult(EditingFixture.Receipt(first)); await Wait(lastStarted.Task);
            Assert.Equal(new[] { first, newest }, shown); Assert.Equal(1, maximum); Assert.Equal(7, preview.Read().Presented!.Revision); Assert.True(preview.Read().Busy);
            releaseLast.SetResult(EditingFixture.Receipt(newest)); await Wait(pending);
            Assert.Equal(9, preview.Read().Presented!.Revision); Assert.False(preview.Read().Busy); Assert.Equal(0, concurrent); Assert.Null(preview.Read().Error);
        }
        finally { releaseFirst.TrySetResult(EditingFixture.Receipt(first)); releaseLast.TrySetResult(EditingFixture.Receipt(newest)); }
    }

    [Fact]
    public async Task FailedRevision_IsNotRetriedByRefreshAndExplicitShowCanRecoverSameRevision()
    {
        var scene = Scene(7); var failure = new IOException("preview child failed"); var attempts = 0;
        await using var preview = new EditorPreview((snapshot, _) => Interlocked.Increment(ref attempts) == 1 ? Task.FromException<ScenePreviewDocument>(failure) : Task.FromResult(EditingFixture.Receipt(snapshot)), () => Task.CompletedTask);
        Assert.True(preview.Show(scene)); await Wait(preview.Pending);
        Assert.True(preview.Read().Active); Assert.False(preview.Read().Busy); Assert.Same(failure, preview.Read().Error); Assert.Null(preview.Read().Presented);
        preview.Synchronize(scene); preview.Synchronize(scene); Assert.Equal(1, attempts); Assert.True(preview.Pending.IsCompleted);
        Assert.True(preview.Show(scene)); await Wait(preview.Pending);
        Assert.Equal(2, attempts); Assert.Null(preview.Read().Error); Assert.Equal(7, preview.Read().Presented!.Revision);
    }

    [Fact]
    public async Task FailedCurrentFollowedBySuccessfulWaitingRevision_ClearsObsoleteFailure()
    {
        var first = Scene(7); var latest = Scene(8); var started = DocumentData.Signal(); var release = Signal<ScenePreviewDocument>();
        await using var preview = new EditorPreview((scene, _) =>
        { if (ReferenceEquals(scene, first)) { started.SetResult(); return release.Task; } return Task.FromResult(EditingFixture.Receipt(scene)); }, () => Task.CompletedTask);
        try
        {
            Assert.True(preview.Show(first)); await Wait(started.Task); preview.Synchronize(latest); release.SetException(new IOException("obsolete failure"));
            await Wait(preview.Pending); Assert.Equal(8, preview.Read().Presented!.Revision); Assert.Null(preview.Read().Error); Assert.False(preview.Read().Busy);
        }
        finally { release.TrySetResult(EditingFixture.Receipt(first)); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WrongSceneOrRevisionAcknowledgement_IsRejectedWithoutInventingPresentedState(bool wrongScene)
    {
        var scene = Scene(7); var calls = 0;
        await using var preview = new EditorPreview((_, _) => { calls++; return Task.FromResult(new ScenePreviewDocument(wrongScene ? new(Guid.NewGuid()) : scene.Id, wrongScene ? scene.Revision : scene.Revision + 1, EditingFixture.Receipt(scene).Runtime)); }, () => Task.CompletedTask);
        Assert.True(preview.Show(scene)); await Wait(preview.Pending);
        Assert.Null(preview.Read().Presented); Assert.IsType<InvalidOperationException>(preview.Read().Error); Assert.False(preview.Read().Busy);
        preview.Synchronize(scene); Assert.Equal(1, calls);
    }

    [Fact]
    public async Task FailedNewerRevision_PreservesOriginalPresentedRevisionWithoutRetaggingIt()
    {
        var first = Scene(7); var later = Scene(8); var failure = new IOException("projection rejected");
        await using var preview = new EditorPreview((scene, _) => ReferenceEquals(scene, first) ? Task.FromResult(EditingFixture.Receipt(scene)) : Task.FromException<ScenePreviewDocument>(failure), () => Task.CompletedTask);
        Assert.True(preview.Show(first)); await Wait(preview.Pending); var original = preview.Read().Presented;
        preview.Synchronize(later); await Wait(preview.Pending);
        Assert.Same(original, preview.Read().Presented); Assert.Equal(7, preview.Read().Presented!.Revision); Assert.Same(failure, preview.Read().Error); Assert.True(preview.Read().Active);
    }

    [Fact]
    public async Task Stop_CancelsDropsWaitingAndDrainsIgnoredCancellationBeforeSharedCleanupThenAllowsRestart()
    {
        var scene = Scene(7); var waiting = Scene(8); var entered = DocumentData.Signal(); var release = Signal<ScenePreviewDocument>();
        var shows = 0; var stops = 0; CancellationToken token = default;
        await using var preview = new EditorPreview((snapshot, cancellation) =>
        { Interlocked.Increment(ref shows); token = cancellation; entered.TrySetResult(); return shows == 1 ? release.Task : Task.FromResult(EditingFixture.Receipt(snapshot)); },
            () => { Interlocked.Increment(ref stops); return Task.CompletedTask; });
        try
        {
            Assert.True(preview.Show(scene)); await Wait(entered.Task); preview.Synchronize(waiting);
            var stop = preview.StopAsync(); Assert.Same(stop, preview.StopAsync()); Assert.Same(stop, preview.Pending);
            Assert.True(token.IsCancellationRequested); Assert.False(preview.Read().Active); Assert.True(preview.Read().Busy); Assert.Null(preview.Read().Presented);
            Assert.False(stop.IsCompleted); Assert.Equal(0, stops); Assert.False(preview.Show(waiting)); preview.Synchronize(waiting);
            release.SetResult(EditingFixture.Receipt(scene)); await Wait(stop);
            Assert.Equal(1, shows); Assert.Equal(1, stops); Assert.Null(preview.Read().Presented); Assert.Null(preview.Read().Error); Assert.False(preview.Read().Busy);
            preview.Synchronize(waiting); Assert.Equal(1, shows);
            Assert.True(preview.Show(waiting)); await Wait(preview.Pending); Assert.Equal(2, shows); Assert.Equal(8, preview.Read().Presented!.Revision);
        }
        finally { release.TrySetResult(EditingFixture.Receipt(scene)); }
    }

    [Fact]
    public async Task StopCleanupFailure_IsObservedAndRetryPerformsActualCleanupBeforeClearingError()
    {
        var scene = Scene(7); var failure = new IOException("reap failed"); var calls = 0;
        await using var preview = new EditorPreview((snapshot, _) => Task.FromResult(EditingFixture.Receipt(snapshot)),
            () => ++calls == 1 ? Task.FromException(failure) : Task.CompletedTask);
        Assert.True(preview.Show(scene)); await Wait(preview.Pending);
        var error = await Assert.ThrowsAsync<IOException>(() => preview.StopAsync()); Assert.Same(failure, error); Assert.Same(failure, preview.Read().Error);
        Assert.False(preview.Read().Active); Assert.False(preview.Read().Busy); Assert.Null(preview.Read().Presented);
        await Wait(preview.StopAsync()); Assert.Equal(2, calls); Assert.Null(preview.Read().Error);
    }

    [Fact]
    public async Task Dispose_ClosesAdmissionAndCancelsAcceptedWorkWithoutPublishingLatePresentation()
    {
        var scene = Scene(7); var started = DocumentData.Signal(); var release = Signal<ScenePreviewDocument>(); CancellationToken token = default; var stops = 0;
        var preview = new EditorPreview((_, cancellation) => { token = cancellation; started.SetResult(); return release.Task; }, () => { stops++; return Task.CompletedTask; });
        try
        {
            Assert.True(preview.Show(scene)); await Wait(started.Task); var disposed = preview.DisposeAsync().AsTask();
            Assert.True(token.IsCancellationRequested); Assert.False(disposed.IsCompleted); Assert.False(preview.Show(scene)); preview.Synchronize(scene);
            release.SetResult(EditingFixture.Receipt(scene)); await Wait(disposed);
            Assert.Equal(1, stops); Assert.Null(preview.Read().Presented); Assert.False(preview.Read().Active); Assert.False(preview.Show(scene));
        }
        finally { release.TrySetResult(EditingFixture.Receipt(scene)); await preview.DisposeAsync(); }
    }

    [Fact]
    public void ConstructorAndOperations_RejectMissingCollaboratorsOrScene()
    {
        Assert.Throws<ArgumentNullException>(() => new EditorPreview(null!, () => Task.CompletedTask));
        Assert.Throws<ArgumentNullException>(() => new EditorPreview((scene, _) => Task.FromResult(EditingFixture.Receipt(scene)), null!));
        using var preview = new EditorPreview((scene, _) => Task.FromResult(EditingFixture.Receipt(scene)), () => Task.CompletedTask);
        Assert.Throws<ArgumentNullException>(() => preview.Show(null!)); Assert.Throws<ArgumentNullException>(() => preview.Synchronize(null!));
    }
}
