using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Aether.Tests.Scenes.Authoring;
using Xunit;
using static TrueMoon.Aether.Tests.Preview.PreviewFixture;

namespace TrueMoon.Aether.Tests.Preview;

public sealed class ScenePreviewTests
{
    [Fact]
    public async Task Show_BindsRevisionToBuiltInRuntimeAndActualPresentedTicketWithoutChangingAuthoring()
    {
        await using var fixture = new PreviewFixture(); await using var preview = new ScenePreview(fixture.Frames);
        using var authoring = AuthoringTestData.Session(); var before = authoring.Read();
        Assert.Null(preview.Document); Assert.Empty(fixture.Runtimes.Leases);
        var showing = preview.ShowAsync(before.Resolved, 32, 16);
        var surface = await fixture.ConfiguredAsync(); var ticket = surface.Issue(Metadata());
        Assert.False(showing.IsCompleted); Assert.Null(preview.Document); Assert.Null(preview.LastPresented);
        var described = Assert.IsType<ScenePreviewFrame>(preview.Describe(ticket));
        Assert.Equal(before.Document.Id, described.Document.SceneId); Assert.Equal(7, described.Document.Revision);
        Assert.Equal(surface.Surface.Identity, described.Document.Runtime);
        Assert.True(preview.Presented(ticket)); var document = await showing.WaitAsync(Deadline);
        Assert.Equal(described.Document, document); Assert.Equal(document, preview.Document);
        Assert.Same(ticket, preview.LastPresented!.Frame); Assert.Equal(document, preview.LastPresented.Document);
        var lease = Assert.Single(fixture.Runtimes.Leases);
        Assert.Equal(RuntimeRole.EditPreview, lease.Launch.Role); Assert.Equal(BuildIdentity.PreviewId, lease.Launch.Identity.GameBuildId);
        Assert.Null(lease.Launch.Build); Assert.False(lease.Launch.RequireRestore);
        Assert.Equal(7, lease.Game.Revision); Assert.Equal(0UL, lease.Game.TickNumber);
        Assert.Equal(2, lease.Game.CaptureRenderScene().Primitives.Length);
        Assert.Equal(0, fixture.Frames.MappingCount); Assert.Same(before, authoring.Read());
        await preview.StopAsync().WaitAsync(Deadline);
        Assert.Null(preview.Document); Assert.Null(preview.LastPresented); Assert.Null(preview.Describe(ticket)); Assert.False(preview.Presented(ticket));
        Assert.True(lease.ResourcesReleased); Assert.True(Assert.Single(fixture.Viewport.Connections).Disposed);
    }

    [Fact]
    public async Task Show_TransformUndoAndRefreshReplaceChildrenAfterReapAndNeverRetagOldFrames()
    {
        await using var fixture = new PreviewFixture(); await using var preview = new ScenePreview(fixture.Frames);
        using var authoring = AuthoringTestData.Session();
        var initial = preview.ShowAsync(authoring.Read().Resolved, 32, 16); var oldTicket = await PresentAsync(fixture, preview); var old = await initial;
        Assert.True(authoring.Apply(AuthoringTestData.Batch(7, 12)).Succeeded);
        var edited = authoring.Read(); var editing = preview.ShowAsync(edited.Resolved, 32, 16);
        var secondSurface = await fixture.ConfiguredAsync();
        Assert.True(fixture.Runtimes.Leases[0].ResourcesReleased); Assert.True(fixture.Viewport.Connections[0].Disposed);
        Assert.Null(preview.Document); Assert.Null(preview.LastPresented); Assert.Null(preview.Describe(oldTicket)); Assert.False(preview.Presented(oldTicket));
        var secondTicket = secondSurface.Issue(Metadata()); Assert.True(preview.Presented(secondTicket)); var second = await editing;
        Assert.Equal(8, second.Revision); Assert.Equal(old.Runtime.SessionId, second.Runtime.SessionId); Assert.True(second.Runtime.Generation > old.Runtime.Generation);
        Assert.NotEqual(old.Runtime.Nonce, second.Runtime.Nonce);
        Assert.Equal(12, fixture.Runtimes.Leases[1].Game.CaptureRenderScene().Primitives[0].World.M41);
        Assert.Same(edited, authoring.Read());
        Assert.True(authoring.Undo(AuthoringTestData.Operation(8)).Succeeded);
        var undone = authoring.Read(); var undoShow = preview.ShowAsync(undone.Resolved, 32, 16); await PresentAsync(fixture, preview); var third = await undoShow;
        Assert.Equal(9, third.Revision); Assert.Equal(7, undone.ContentRevision); Assert.Equal(3, fixture.Runtimes.Leases[2].Game.CaptureRenderScene().Primitives[0].World.M41);
        var refresh = preview.ShowAsync(undone.Resolved, 32, 16); await PresentAsync(fixture, preview); var fourth = await refresh;
        Assert.Equal(9, fourth.Revision); Assert.True(fourth.Runtime.Generation > third.Runtime.Generation);
        Assert.Equal(1, fixture.Runtimes.MaxLive); Assert.Equal(4, fixture.Runtimes.Leases.Count);
        Assert.All(fixture.Runtimes.Leases.Take(3), lease => Assert.Equal(1, lease.Disposals));
    }

    [Theory]
    [InlineData("entity-limit")] [InlineData("material")]
    public async Task Show_InvalidProjectionPreservesWorkingChildAndPresentation(string invalid)
    {
        await using var fixture = new PreviewFixture(); await using var preview = new ScenePreview(fixture.Frames);
        var initial = preview.ShowAsync(Scene(), 32, 16); var old = await PresentAsync(fixture, preview); var document = await initial;
        var source = AuthoringTestData.Document(8, invalid == "entity-limit" ? 65 : 2);
        if (invalid == "material")
        {
            var first = source.Entities[0]; var bad = new EntityInstance(first.Id, first.DefinitionId,
                new(propertyOverrides: [new(AuthoringTestData.Appearance, "r", PropertyValue.From(2d))]));
            source = new(source.Id, source.Revision, new[] { bad }.Concat(source.Entities.Skip(1)), source.Definitions, source.Schemas);
        }
        await Assert.ThrowsAsync<SceneResolutionException>(() => preview.ShowAsync(SceneResolver.Resolve(source), 32, 16));
        Assert.Equal(document, preview.Document); Assert.Same(old, preview.LastPresented!.Frame);
        Assert.Single(fixture.Runtimes.Leases); Assert.Equal(0, fixture.Runtimes.Leases[0].Stops); Assert.Null(preview.Error);
        Assert.NotNull(preview.Describe(old));
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(0, 16)] [InlineData(16, 0)] [InlineData(15, 16)]
    [InlineData(32, 15)] [InlineData(1921, 16)] [InlineData(16, 1081)]
    public async Task Show_InvalidExtentDoesNotStopAnExistingPreview(int width, int height)
    {
        await using var fixture = new PreviewFixture(); await using var preview = new ScenePreview(fixture.Frames);
        var showing = preview.ShowAsync(Scene(), 32, 16); var old = await PresentAsync(fixture, preview); var document = await showing;
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => preview.ShowAsync(Scene(8), width, height));
        Assert.Equal(document, preview.Document); Assert.Same(old, preview.LastPresented!.Frame);
        Assert.Single(fixture.Runtimes.Leases); Assert.Equal(0, fixture.Runtimes.Leases[0].Stops);
    }

    [Fact]
    public async Task Show_RejectsCpuStaleRevisionAndConcurrentWorkBeforeReplacement()
    {
        await using (var cpu = new PreviewFixture(ViewportTransportKind.Cpu))
        await using (var preview = new ScenePreview(cpu.Frames))
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => preview.ShowAsync(Scene(), 32, 16));
            Assert.Empty(cpu.Runtimes.Leases); Assert.Null(preview.Document); Assert.Equal(0, cpu.Frames.MappingCount);
        }
        await using var fixture = new PreviewFixture(); await using var gpu = new ScenePreview(fixture.Frames);
        var showing = gpu.ShowAsync(Scene(8), 32, 16); var surface = await fixture.ConfiguredAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => gpu.ShowAsync(Scene(9), 32, 16));
        await Assert.ThrowsAsync<InvalidOperationException>(() => gpu.ResizeAsync(64, 32));
        Assert.Single(fixture.Runtimes.Leases); Assert.True(gpu.Presented(surface.Issue(Metadata()))); var ready = await showing;
        await Assert.ThrowsAsync<InvalidOperationException>(() => gpu.ShowAsync(Scene(7), 32, 16));
        Assert.Equal(ready, gpu.Document); Assert.Equal(0, fixture.Runtimes.Leases[0].Stops);
        await gpu.StopAsync(); var afterStop = gpu.ShowAsync(Scene(7), 32, 16); await PresentAsync(fixture, gpu); Assert.Equal(7, (await afterStop).Revision);
    }

    [Fact]
    public async Task Resize_RequiresNewActualPresentationSuspendsAndResumesWithoutReplacingRuntime()
    {
        await using var fixture = new PreviewFixture(); await using var preview = new ScenePreview(fixture.Frames);
        var showing = preview.ShowAsync(Scene(), 32, 16); var initialTicket = await PresentAsync(fixture, preview); var document = await showing;
        var resize = preview.ResizeAsync(64, 32); var next = await fixture.ConfiguredAsync();
        Assert.False(resize.IsCompleted); Assert.Null(preview.Document); Assert.Null(preview.LastPresented);
        Assert.Null(preview.Describe(initialTicket)); Assert.False(preview.Presented(initialTicket));
        var resized = next.Issue(Metadata()); Assert.True(preview.Presented(resized)); await resize;
        Assert.Equal(document, preview.Document); Assert.Equal(document, preview.LastPresented!.Document); Assert.True(next.Surface.Generation > initialTicket.Presentation.Surface.Generation);
        await preview.ResizeAsync(0, 0); var suspended = await fixture.ConfiguredAsync();
        Assert.True(suspended.Surface.Suspended); Assert.Equal(document, preview.Document); Assert.Null(preview.LastPresented); Assert.Null(preview.Describe(resized));
        var resume = preview.ResizeAsync(16, 16); var final = await fixture.ConfiguredAsync(); Assert.False(resume.IsCompleted);
        Assert.True(preview.Presented(final.Issue(Metadata()))); await resume;
        Assert.Single(fixture.Runtimes.Leases); Assert.Single(fixture.Viewport.Connections);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => preview.ResizeAsync(0, 16)); Assert.Equal(document, preview.Document);
    }

    [Theory]
    [InlineData("launch")] [InlineData("connect")] [InlineData("configure")]
    public async Task Show_SetupFailurePreservesOriginalCauseReapsAndAllowsRetry(string phase)
    {
        await using var fixture = new PreviewFixture(); await using var preview = new ScenePreview(fixture.Frames);
        var original = new IOException("preview " + phase + " failed");
        if (phase == "launch") fixture.Runtimes.LaunchError = original;
        else if (phase == "connect") fixture.Viewport.ConnectError = original;
        else fixture.Viewport.ConfigureError = original;
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => preview.ShowAsync(Scene(), 32, 16)));
        Assert.Same(original, preview.Error); Assert.Null(preview.Document); Assert.Null(preview.LastPresented);
        Assert.True(Assert.Single(fixture.Runtimes.Leases).ResourcesReleased);
        Assert.All(fixture.Viewport.Connections, connection => Assert.True(connection.Disposed));
        fixture.Runtimes.LaunchError = fixture.Viewport.ConnectError = fixture.Viewport.ConfigureError = null;
        var retry = preview.ShowAsync(Scene(), 32, 16); await PresentAsync(fixture, preview); Assert.Equal(7, (await retry).Revision);
        Assert.Equal(2, fixture.Runtimes.Leases.Count); Assert.Equal(1, fixture.Runtimes.MaxLive); Assert.Null(preview.Error);
    }

    [Fact]
    public async Task Show_CallerCancellationBeforePresentationCleansUpAndCanRetry()
    {
        await using var fixture = new PreviewFixture(); await using var preview = new ScenePreview(fixture.Frames);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preview.ShowAsync(Scene(), 32, 16, cancelled.Token)); Assert.Empty(fixture.Runtimes.Leases);
        using var during = new CancellationTokenSource(); var showing = preview.ShowAsync(Scene(), 32, 16, during.Token);
        var old = (await fixture.ConfiguredAsync()).Issue(Metadata()); during.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => showing.WaitAsync(Deadline));
        Assert.True(Assert.Single(fixture.Runtimes.Leases).ResourcesReleased); Assert.Null(preview.Document); Assert.False(preview.Presented(old));
        var retry = preview.ShowAsync(Scene(8), 32, 16); await PresentAsync(fixture, preview); Assert.Equal(8, (await retry).Revision);
    }

    [Fact]
    public async Task Show_CancelledDuringSceneLoadReapsBeforeAnyViewportAllocationAndAllowsRetry()
    {
        await using var fixture = new PreviewFixture(); await using var preview = new ScenePreview(fixture.Frames);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Runtimes.BeforeSend = async (request, token) =>
        {
            if (request.Kind != (int)CommandKind.LoadScene) return;
            entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token);
        };
        using var cancellation = new CancellationTokenSource();
        var showing = preview.ShowAsync(Scene(), 32, 16, cancellation.Token);
        await entered.Task.WaitAsync(Deadline); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => showing.WaitAsync(Deadline));
        Assert.Empty(fixture.Viewport.Connections); Assert.True(Assert.Single(fixture.Runtimes.Leases).ResourcesReleased);
        Assert.Null(preview.Document); Assert.Null(preview.LastPresented);
        fixture.Runtimes.BeforeSend = null;
        var retry = preview.ShowAsync(Scene(), 32, 16); await PresentAsync(fixture, preview); await retry.WaitAsync(Deadline);
        Assert.Equal(1, fixture.Runtimes.MaxLive);
    }

    [Fact]
    public async Task Show_LowerLevelAcknowledgementCannotPublishWithoutPreviewFrameMapping()
    {
        await using var fixture = new PreviewFixture(); await using var preview = new ScenePreview(fixture.Frames);
        var showing = preview.ShowAsync(Scene(), 32, 16); var surface = await fixture.ConfiguredAsync(); var ticket = surface.Issue(Metadata());
        Assert.True(fixture.Frames.Presented(ticket));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => showing.WaitAsync(Deadline));
        Assert.Contains("ScenePreview.Presented", error.Message); Assert.Same(error, preview.Error);
        Assert.Null(preview.Document); Assert.Null(preview.LastPresented); Assert.True(fixture.Runtimes.Leases[0].ResourcesReleased);
        var retry = preview.ShowAsync(Scene(), 32, 16); await PresentAsync(fixture, preview); await retry.WaitAsync(Deadline);
    }

    [Fact]
    public async Task Show_DeliveryFailureBeforePresentationPreservesCauseAndReapsBeforeRetry()
    {
        await using var fixture = new PreviewFixture(); await using var preview = new ScenePreview(fixture.Frames);
        var showing = preview.ShowAsync(Scene(), 32, 16); var old = (await fixture.ConfiguredAsync()).Issue(Metadata());
        var cause = new IOException("preview delivery failed"); fixture.Viewport.Connections[0].Fail(cause);
        Assert.Same(cause, await Assert.ThrowsAsync<IOException>(() => showing.WaitAsync(Deadline)));
        Assert.Same(cause, preview.Error); Assert.Null(preview.Document); Assert.False(preview.Presented(old));
        Assert.True(fixture.Runtimes.Leases[0].ResourcesReleased); Assert.True(fixture.Viewport.Connections[0].Disposed);
        var retry = preview.ShowAsync(Scene(8), 32, 16); await PresentAsync(fixture, preview); Assert.Equal(8, (await retry).Revision);
    }

    [Fact]
    public async Task Show_FailedDeliveryCleanupRetainsChildUntilExplicitSuccessfulStopRetry()
    {
        await using var fixture = new PreviewFixture(); await using var preview = new ScenePreview(fixture.Frames);
        using var cancellation = new CancellationTokenSource();
        var showing = preview.ShowAsync(Scene(), 32, 16, cancellation.Token); await fixture.ConfiguredAsync();
        var connection = fixture.Viewport.Connections[0]; connection.FailDispose = true;
        try
        {
            cancellation.Cancel();
            var error = await Assert.ThrowsAsync<AggregateException>(() => showing.WaitAsync(Deadline));
            Assert.Same(error, preview.Error);
            Assert.Contains(error.Flatten().InnerExceptions, failure => failure is OperationCanceledException);
            Assert.Contains(error.Flatten().InnerExceptions, failure => failure is IOException);
            Assert.False(connection.Disposed); Assert.False(fixture.Runtimes.Leases[0].ResourcesReleased); Assert.Equal(0, fixture.Runtimes.Leases[0].Stops);
            Assert.Null(preview.Document); Assert.Null(preview.LastPresented);
            var blocked = await Assert.ThrowsAsync<AggregateException>(() => preview.ShowAsync(Scene(8), 32, 16).WaitAsync(Deadline));
            Assert.Contains(blocked.Flatten().InnerExceptions, failure => failure is IOException);
            Assert.Single(fixture.Runtimes.Leases);
        }
        finally { connection.FailDispose = false; }
        await preview.StopAsync().WaitAsync(Deadline);
        Assert.True(connection.Disposed); Assert.True(fixture.Runtimes.Leases[0].ResourcesReleased);
        var retry = preview.ShowAsync(Scene(8), 32, 16); await PresentAsync(fixture, preview); Assert.Equal(8, (await retry).Revision);
    }

    [Fact]
    public async Task Stop_FencesLateConfigureAndPresentationUntilActualInflightShowHasDrained()
    {
        await using var fixture = new PreviewFixture(); await using var preview = new ScenePreview(fixture.Frames);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Viewport.BeforeConfigure = (_, _) => release.Task;
        var showing = preview.ShowAsync(Scene(), 32, 16); var presentation = await fixture.ConfiguredAsync(); var ticket = presentation.Issue(Metadata());
        try
        {
            var stop = preview.StopAsync(); Assert.False(stop.IsCompleted);
            Assert.Null(preview.Describe(ticket)); Assert.False(preview.Presented(ticket)); Assert.Null(preview.Document);
            // Even a lower-level late acknowledgement cannot publish the cancelled Show's revision.
            Assert.True(fixture.Frames.Presented(ticket)); release.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => showing.WaitAsync(Deadline)); await stop.WaitAsync(Deadline);
            Assert.Null(preview.Document); Assert.Null(preview.LastPresented); Assert.True(fixture.Runtimes.Leases[0].ResourcesReleased);
        }
        finally { release.TrySetResult(); }
        fixture.Viewport.BeforeConfigure = null;
        var retry = preview.ShowAsync(Scene(8), 32, 16); await PresentAsync(fixture, preview); Assert.Equal(8, (await retry).Revision);
    }

    [Fact]
    public async Task Crash_InvalidatesDocumentTicketsAndLastPresentedBeforeExplicitCleanup()
    {
        await using var fixture = new PreviewFixture(); await using var preview = new ScenePreview(fixture.Frames);
        var showing = preview.ShowAsync(Scene(), 32, 16); var ticket = await PresentAsync(fixture, preview); await showing;
        fixture.Runtimes.Leases[0].Crash();
        await UntilAsync(() => fixture.Supervisor.Status.State == SessionState.Faulted);
        Assert.Null(preview.Document); Assert.Null(preview.LastPresented); Assert.Null(preview.Describe(ticket)); Assert.False(preview.Presented(ticket));
        await preview.StopAsync().WaitAsync(Deadline); Assert.True(fixture.Runtimes.Leases[0].ResourcesReleased); Assert.True(fixture.Viewport.Connections[0].Disposed);
    }

    [Fact]
    public async Task Dispose_ClosesPreviewAdmissionButLeavesBorrowedFramesUsableForGameWorkflow()
    {
        await using var fixture = new PreviewFixture(); var preview = new ScenePreview(fixture.Frames);
        var show = preview.ShowAsync(Scene(), 32, 16); await PresentAsync(fixture, preview); await show;
        await Task.WhenAll(preview.DisposeAsync().AsTask(), preview.DisposeAsync().AsTask()).WaitAsync(Deadline);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => preview.ShowAsync(Scene(), 32, 16));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => preview.ResizeAsync(32, 16));
        Assert.Equal(0, fixture.Viewport.Disposals); Assert.Null(preview.Document);
        var game = fixture.Frames.StartAsync(PrimitiveSceneAdapter.Project(Scene()), 32, 16);
        var presentation = await fixture.ConfiguredAsync(); Assert.True(fixture.Frames.Presented(presentation.Issue(Metadata()))); await game.WaitAsync(Deadline);
        Assert.Equal(RuntimeRole.Game, fixture.Runtimes.Leases[1].Launch.Role);
        await preview.StopAsync(); Assert.Equal(0, fixture.Runtimes.Leases[1].Stops);
        await fixture.Frames.StopAsync(); Assert.Equal(1, fixture.Runtimes.Leases[1].Stops);
    }

    [Fact]
    public async Task Show_DoesNotTakeOverOrStopAnExplicitGameSession()
    {
        await using var fixture = new PreviewFixture(); await using var preview = new ScenePreview(fixture.Frames);
        var game = fixture.Frames.StartAsync(PrimitiveSceneAdapter.Project(Scene()), 32, 16);
        var presentation = await fixture.ConfiguredAsync(); Assert.True(fixture.Frames.Presented(presentation.Issue(Metadata()))); await game;
        await Assert.ThrowsAsync<InvalidOperationException>(() => preview.ShowAsync(Scene(), 32, 16));
        await preview.StopAsync(); await preview.DisposeAsync();
        Assert.Equal(0, fixture.Runtimes.Leases[0].Stops); Assert.False(fixture.Runtimes.Leases[0].ResourcesReleased);
        Assert.Same(presentation, fixture.Frames.Presentation);
    }

    private static async Task<ViewportFrame> PresentAsync(PreviewFixture fixture, ScenePreview preview)
    { var frame = (await fixture.ConfiguredAsync()).Issue(Metadata()); Assert.True(preview.Presented(frame)); return frame; }
    private static async Task UntilAsync(Func<bool> predicate)
    { using var deadline = new CancellationTokenSource(Deadline); while (!predicate()) await Task.Delay(5, deadline.Token); }
}
