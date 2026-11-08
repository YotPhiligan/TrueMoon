using System.Numerics;
using System.Threading.Channels;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Transport;
using Xunit;
using static TrueMoon.Aether.Tests.Preview.PreviewFixture;

namespace TrueMoon.Aether.Tests.Preview;

public sealed class ScenePreviewViewTests
{
    [Fact]
    public async Task UiSuspend_CancelsPendingWaitRetainsLatestCameraAndResumesSameChildAndSurface()
    {
        await using var fixture=new PreviewFixture();await using var preview=new ScenePreview(fixture.Frames);
        var scene=Scene();var view=View(scene);var show=preview.ShowViewAsync(scene,view,32,16);
        var surface=await fixture.ConfiguredAsync();Present(preview,surface,view);await show;
        var old=preview.PresentedView!;var updates=ObserveUpdates(fixture);
        Assert.True(preview.QueueCamera(view.Context,Move(view.Camera,2)));await Next(updates);var pending=preview.CameraUpdates;
        preview.SetPresentationSuspended(true);await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);
        Assert.Null(preview.PresentedView);Assert.False(preview.IsCurrent(old));Assert.False(Present(preview,surface,view,2));
        var latest=Move(preview.DesiredView!.Camera,3);Assert.True(preview.QueueCamera(view.Context,latest));
        var queuedWhileHidden=preview.CameraUpdates;Assert.False(queuedWhileHidden.IsCompleted);Assert.False(updates.Reader.TryRead(out _));
        preview.SetPresentationSuspended(false);Assert.False(queuedWhileHidden.IsCompleted);
        var resumed=await Next(updates);Assert.Equal(latest,resumed.Camera);
        Assert.Null(preview.PresentedView);Assert.False(preview.IsCurrent(old));
        Assert.True(Present(preview,surface,resumed,3));await queuedWhileHidden.WaitAsync(Deadline);
        Assert.True(preview.IsCurrent(preview.PresentedView!));Assert.Single(fixture.Runtimes.Leases);Assert.Same(surface,fixture.Frames.Presentation);
    }

    private static PreviewViewState View(ResolvedSceneSnapshot scene) => new(new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),scene.Id.Value,scene.Revision),PreviewCamera.Default);
    private static PreviewCamera Move(PreviewCamera old, float x) => old.WithView(Matrix4x4.CreateTranslation(x,1,6),old.Projection,old.Pivot,old.Distance);
    private static bool Present(ScenePreview preview, ViewportPresentation surface, PreviewViewState view, ulong frame = 1)
        => preview.Presented(surface.Issue(Metadata(frame) with { PreviewView=view.Stamp }),new(16,14,800,450),new(0,0,900,500),1);
    private static Channel<PreviewViewState> ObserveUpdates(PreviewFixture fixture)
    {
        var updates=Channel.CreateUnbounded<PreviewViewState>();
        fixture.Runtimes.AfterSend=(request,reply)=> { if (request.Kind==(int)CommandKind.SetPreviewView) updates.Writer.TryWrite(PreviewViewCodec.Decode(request.PreviewView!)); return reply; };
        return updates;
    }
    private static async Task<PreviewViewState> Next(Channel<PreviewViewState> updates) => await updates.Reader.ReadAsync().AsTask().WaitAsync(Deadline);

    [Fact]
    public async Task ShowView_RequiresActualAckAndFreezesCameraGeometryLayoutAndExactTicket()
    {
        await using var fixture=new PreviewFixture(); await using var preview=new ScenePreview(fixture.Frames);
        var scene=Scene(); var view=View(scene); var show=preview.ShowViewAsync(scene,view,32,16); var surface=await fixture.ConfiguredAsync();
        var frame=surface.Issue(Metadata() with { PreviewView=view.Stamp });
        Assert.False(show.IsCompleted); Assert.Null(preview.PresentedView); Assert.NotNull(preview.Describe(frame));
        Assert.False(preview.Presented(frame,new(0,0,800,450),new(0,0,900,500),0)); Assert.False(show.IsCompleted);
        Assert.True(preview.Presented(frame,new(16,14,800,450),new(0,0,900,500),1)); await show.WaitAsync(Deadline);
        var receipt=Assert.IsType<PreviewPresentedView>(preview.PresentedView);
        Assert.Same(frame,receipt.Frame); Assert.Equal(view,receipt.State); Assert.Equal(32,receipt.Projection.Width); Assert.Equal(16,receipt.Projection.Height);
        Assert.Equal(scene.Id,receipt.Picks.Scene); Assert.Equal(scene.Revision,receipt.Picks.Revision); Assert.Equal(2,receipt.Picks.Primitives.Length);
        Assert.Equal(new(16,14,800,450),receipt.ImageBounds); Assert.Equal(new(0,0,900,500),receipt.ClipBounds); Assert.Equal(1ul,receipt.LayoutRevision);
        Assert.Equal(view.Camera.CreateProjection(32,16).ViewProjection,receipt.Projection.ViewProjection);
        Assert.True(preview.IsCurrent(receipt));
        var newer=surface.Issue(Metadata(2) with { PreviewView=view.Stamp });
        Assert.True(preview.IsCurrent(receipt)); Assert.Same(receipt,preview.PresentedView); // received/uploaded is not presented
        Assert.True(preview.Presented(newer,new(16,14,800,450),new(0,0,900,500),1));
        Assert.True(preview.IsCurrent(receipt)); Assert.NotSame(receipt,preview.PresentedView); // FrameId alone does not invalidate a click
    }

    [Fact]
    public async Task Camera_CoalescesOneHundredSamplesAndWaitsForActualPresentationWithoutNewRuntimeOrSurface()
    {
        await using var fixture=new PreviewFixture(); await using var preview=new ScenePreview(fixture.Frames);
        var scene=Scene(); var initial=View(scene); var show=preview.ShowViewAsync(scene,initial,32,16); var surface=await fixture.ConfiguredAsync(); Assert.True(Present(preview,surface,initial)); await show;
        var oldReceipt=preview.PresentedView!; var updates=ObserveUpdates(fixture); var camera=Move(initial.Camera,1);
        Assert.True(preview.QueueCamera(initial.Context,camera)); var first=await Next(updates);
        for (var i=2;i<=100;i++) { camera=Move(camera,i); Assert.True(preview.QueueCamera(initial.Context,camera)); }
        var pending=preview.CameraUpdates;
        Assert.False(pending.IsCompleted); Assert.False(preview.IsCurrent(oldReceipt)); Assert.Same(oldReceipt,preview.PresentedView);
        Assert.False(updates.Reader.TryRead(out _));
        Assert.True(Present(preview,surface,initial,2)); Assert.False(pending.IsCompleted); Assert.Equal(initial.Camera,preview.PresentedView!.State.Camera);
        Assert.True(Present(preview,surface,first,3)); var last=await Next(updates);
        Assert.Equal(101ul,last.Camera.Revision); Assert.Equal(100f,last.Camera.World.M41); Assert.False(pending.IsCompleted);
        Assert.True(Present(preview,surface,last,4)); await pending.WaitAsync(Deadline);
        Assert.True(preview.IsCurrent(preview.PresentedView!)); Assert.False(preview.IsCurrent(oldReceipt));
        var child=Assert.Single(fixture.Runtimes.Leases); Assert.Equal(2,child.Commands.Count(k=>k==CommandKind.SetPreviewView));
        Assert.DoesNotContain(CommandKind.Step,child.Commands); Assert.Equal(0ul,child.Game.TickNumber); Assert.Equal(7,child.Game.Revision);
        Assert.Single(fixture.Viewport.Connections); Assert.Same(surface,fixture.Frames.Presentation); Assert.Equal(0,child.Stops);
        Assert.True(preview.QueueCamera(initial.Context,camera)); Assert.Same(pending,preview.CameraUpdates); Assert.False(updates.Reader.TryRead(out _));
    }

    [Theory]
    [InlineData("untagged")] [InlineData("camera")] [InlineData("document")] [InlineData("view")] [InlineData("content")] [InlineData("scene")] [InlineData("revision")]
    public async Task Presented_UnknownOrStaleStampNeverRetagsImageOrCompletesReadiness(string part)
    {
        await using var fixture=new PreviewFixture(); await using var preview=new ScenePreview(fixture.Frames);
        var scene=Scene(); var view=View(scene); var showing=preview.ShowViewAsync(scene,view,32,16); var surface=await fixture.ConfiguredAsync();
        var stamp=part switch { "camera"=>view.Stamp with {CameraRevision=2}, "document"=>view.Stamp with {DocumentEpoch=Guid.NewGuid()},
            "view"=>view.Stamp with {ViewId=Guid.NewGuid()}, "content"=>view.Stamp with {ContentId=Guid.NewGuid()},
            "scene"=>view.Stamp with {SceneId=Guid.NewGuid()}, "revision"=>view.Stamp with {SceneRevision=8}, _=>null };
        var frame=surface.Issue(Metadata() with {PreviewView=stamp});
        Assert.Null(preview.Describe(frame)); Assert.False(preview.Presented(frame));
        Assert.False(preview.Presented(frame,new(0,0,32,16),new(0,0,32,16),1)); Assert.Null(preview.PresentedView); Assert.False(showing.IsCompleted);
        Assert.True(Present(preview,surface,view,2)); await showing.WaitAsync(Deadline); Assert.Equal(view,preview.PresentedView!.State);
    }

    [Fact]
    public async Task Stop_CancelsPresentationWaitAndDrainsCameraBeforeReapingWithoutFurtherUiFrames()
    {
        await using var fixture=new PreviewFixture(); await using var preview=new ScenePreview(fixture.Frames);
        var scene=Scene(); var view=View(scene); var show=preview.ShowViewAsync(scene,view,32,16); var surface=await fixture.ConfiguredAsync(); Present(preview,surface,view); await show;
        var receipt=preview.PresentedView!; var updates=ObserveUpdates(fixture);
        preview.QueueCamera(view.Context,Move(view.Camera,2)); await Next(updates); var pending=preview.CameraUpdates;
        Assert.False(pending.IsCompleted); await preview.StopAsync().WaitAsync(Deadline);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);
        Assert.Null(preview.PresentedView); Assert.Null(preview.DesiredView); Assert.False(preview.IsCurrent(receipt));
        Assert.True(Assert.Single(fixture.Runtimes.Leases).ResourcesReleased); Assert.True(Assert.Single(fixture.Viewport.Connections).Disposed);
        Assert.False(preview.QueueCamera(view.Context,Move(view.Camera,3)));
    }

    [Fact]
    public async Task ShowView_NewOpeningOfSameSceneCancelsOldCameraAndCannotPublishOldReceipt()
    {
        await using var fixture=new PreviewFixture(); await using var preview=new ScenePreview(fixture.Frames);
        var scene=Scene(8); var old=View(scene); var showing=preview.ShowViewAsync(scene,old,32,16); var surface=await fixture.ConfiguredAsync(); Present(preview,surface,old); await showing;
        var receipt=preview.PresentedView!; var updates=ObserveUpdates(fixture); preview.QueueCamera(old.Context,Move(old.Camera,2)); await Next(updates);
        var reopened=Scene(7); var next=View(reopened); var replacement=preview.ShowViewAsync(reopened,next,32,16); var changed=await fixture.ConfiguredAsync();
        Assert.True(fixture.Runtimes.Leases[0].ResourcesReleased); Assert.Null(preview.PresentedView); Assert.False(preview.IsCurrent(receipt));
        Assert.False(preview.Presented(receipt.Frame,receipt.ImageBounds,receipt.ClipBounds,receipt.LayoutRevision));
        Assert.False(preview.QueueCamera(old.Context,Move(old.Camera,3)));
        Assert.True(Present(preview,changed,next)); await replacement.WaitAsync(Deadline);
        Assert.Equal(next,preview.PresentedView!.State); Assert.Equal(2,fixture.Runtimes.Leases.Count); Assert.Equal(1,fixture.Runtimes.MaxLive);
    }

    [Fact]
    public async Task Resize_SuspendsLatestCameraAndResumesSameChildWithFreshActualReceipt()
    {
        await using var fixture=new PreviewFixture(); await using var preview=new ScenePreview(fixture.Frames);
        var scene=Scene(); var view=View(scene); var show=preview.ShowViewAsync(scene,view,32,16); var first=await fixture.ConfiguredAsync(); Present(preview,first,view); await show;
        var old=preview.PresentedView!; var updates=ObserveUpdates(fixture);
        await preview.ResizeAsync(0,0); await fixture.ConfiguredAsync(); Assert.Null(preview.PresentedView); Assert.False(preview.IsCurrent(old));
        var camera=Move(view.Camera,2); Assert.True(preview.QueueCamera(view.Context,camera)); var pending=preview.CameraUpdates;
        Assert.False(pending.IsCompleted); Assert.False(updates.Reader.TryRead(out _));
        var resize=preview.ResizeAsync(64,32); var surface=await fixture.ConfiguredAsync(); Assert.False(resize.IsCompleted);
        Present(preview,surface,view); await resize.WaitAsync(Deadline); var next=await Next(updates);
        Assert.Equal(camera,next.Camera); Assert.False(preview.IsCurrent(preview.PresentedView!));
        Present(preview,surface,next,2); await preview.CameraUpdates.WaitAsync(Deadline);
        Assert.Equal(64,preview.PresentedView!.Projection.Width); Assert.Equal(32,preview.PresentedView.Projection.Height);
        Assert.True(preview.IsCurrent(preview.PresentedView)); Assert.Single(fixture.Runtimes.Leases); Assert.Single(fixture.Viewport.Connections);
        Assert.True(pending.IsCanceled); Assert.False(preview.IsCurrent(old));
    }

    [Theory]
    [InlineData("capability")] [InlineData("stamp")]
    public async Task ShowView_IncompatibleOrWrongAcknowledgementFailsPreflightAndReaps(string failure)
    {
        await using var fixture=new PreviewFixture(); await using var preview=new ScenePreview(fixture.Frames);
        fixture.Runtimes.AfterSend=(request,reply)=>
        { if (failure=="capability" && request.Kind==(int)CommandKind.Hello) reply.PreviewViewVersion=0;
          if (failure=="stamp" && request.Kind==(int)CommandKind.LoadScene) reply.PreviewViewStamp=null; return reply; };
        var scene=Scene(); await Assert.ThrowsAsync<InvalidDataException>(()=>preview.ShowViewAsync(scene,View(scene),32,16));
        Assert.Empty(fixture.Viewport.Connections); Assert.Null(preview.PresentedView); Assert.True(Assert.Single(fixture.Runtimes.Leases).ResourcesReleased);
    }

    [Fact]
    public async Task Queue_InvalidCandidateOrForeignContextPreservesWorkingDesiredView()
    {
        await using var fixture=new PreviewFixture(); await using var preview=new ScenePreview(fixture.Frames);
        var scene=Scene(); var view=View(scene); var show=preview.ShowViewAsync(scene,view,32,16); var surface=await fixture.ConfiguredAsync(); Present(preview,surface,view); await show;
        Assert.False(preview.QueueCamera(view.Context with {ContentId=Guid.NewGuid()},Move(view.Camera,1)));
        var reused=new PreviewCamera(Matrix4x4.Identity,view.Camera.Projection,view.Camera.Pivot,6,1);
        Assert.Throws<InvalidDataException>(()=>preview.QueueCamera(view.Context,reused));
        var bad=new PreviewCamera(view.Camera.World,new(1e-30f,.1f,100),view.Camera.Pivot,6,2);
        Assert.Throws<RenderContentException>(()=>preview.QueueCamera(view.Context,bad));
        Assert.Equal(view,preview.DesiredView); Assert.True(preview.IsCurrent(preview.PresentedView!));
    }

    [Fact]
    public async Task Stop_DrainsAcceptedControlBeforeChildReapAndNeverNeedsAnotherUiCallback()
    {
        await using var fixture=new PreviewFixture(); await using var preview=new ScenePreview(fixture.Frames);
        var scene=Scene(); var view=View(scene); var show=preview.ShowViewAsync(scene,view,32,16); var surface=await fixture.ConfiguredAsync(); Present(preview,surface,view); await show;
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Runtimes.BeforeSend=async (request,token)=> { if(request.Kind==(int)CommandKind.SetPreviewView) {entered.TrySetResult(); await release.Task.WaitAsync(token);} };
        preview.QueueCamera(view.Context,Move(view.Camera,1)); await entered.Task.WaitAsync(Deadline); var pending=preview.CameraUpdates;
        var stopping=preview.StopAsync(); Assert.True(pending.IsCanceled); Assert.False(stopping.IsCompleted); Assert.False(fixture.Runtimes.Leases[0].ResourcesReleased);
        release.TrySetResult(); await stopping.WaitAsync(Deadline); Assert.True(fixture.Runtimes.Leases[0].ResourcesReleased);
        Assert.Null(preview.PresentedView); Assert.Null(preview.DesiredView);
    }

    [Fact]
    public async Task Camera_ControlFailureRetainsOriginalErrorAndCleanupCanBeRetried()
    {
        await using var fixture=new PreviewFixture(); await using var preview=new ScenePreview(fixture.Frames);
        var scene=Scene(); var view=View(scene); var show=preview.ShowViewAsync(scene,view,32,16); var surface=await fixture.ConfiguredAsync(); Present(preview,surface,view); await show;
        var error=new IOException("camera control failed");
        fixture.Runtimes.BeforeSend=(request,_)=>request.Kind==(int)CommandKind.SetPreviewView ? Task.FromException(error) : Task.CompletedTask;
        preview.QueueCamera(view.Context,Move(view.Camera,1)); var pending=preview.CameraUpdates;
        Assert.Same(error,await Assert.ThrowsAsync<IOException>(()=>pending.WaitAsync(Deadline)));
        await preview.StopAsync().WaitAsync(Deadline); Assert.Same(error,preview.Error); Assert.Null(preview.PresentedView);
        Assert.True(fixture.Runtimes.Leases[0].ResourcesReleased); Assert.True(fixture.Viewport.Connections[0].Disposed);
        fixture.Runtimes.BeforeSend=null; var retry=preview.ShowViewAsync(scene,View(scene),32,16); var next=await fixture.ConfiguredAsync();
        Assert.True(Present(preview,next,preview.DesiredView!)); await retry.WaitAsync(Deadline); Assert.Null(preview.Error);
    }

    [Fact]
    public async Task Presented_RejectedOlderTicketCannotPublishReceiptOrCompleteCameraWait()
    {
        await using var fixture=new PreviewFixture(); await using var preview=new ScenePreview(fixture.Frames);
        var scene=Scene(); var view=View(scene); var show=preview.ShowViewAsync(scene,view,32,16); var surface=await fixture.ConfiguredAsync(); Present(preview,surface,view); await show;
        var receipt=preview.PresentedView!; var updates=ObserveUpdates(fixture); preview.QueueCamera(view.Context,Move(view.Camera,1)); var next=await Next(updates);
        var oldTicket=surface.Issue(Metadata(2) with {PreviewView=next.Stamp});
        var actual=surface.Issue(Metadata(3) with {PreviewView=next.Stamp}); var pending=preview.CameraUpdates;
        Assert.False(preview.Presented(oldTicket,receipt.ImageBounds,receipt.ClipBounds,1)); Assert.False(pending.IsCompleted); Assert.Same(receipt,preview.PresentedView);
        Assert.True(preview.Presented(actual,receipt.ImageBounds,receipt.ClipBounds,1)); await pending.WaitAsync(Deadline);
        Assert.Same(actual,preview.PresentedView!.Frame); Assert.Equal(next,preview.PresentedView.State);
    }
}
