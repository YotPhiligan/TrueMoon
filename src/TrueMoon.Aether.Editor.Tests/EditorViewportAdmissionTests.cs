using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorViewportAdmissionTests
{
    [Fact]
    public void Click_AllowsNewFrameIdOfSameSemanticViewWithoutRetargetingItsPickSource()
    {
        using var t=new ViewportInputFixture();t.Start();var before=t.Preview.PresentedView!;var point=t.Point(DocumentData.Child);
        Assert.True(t.Input(InputKind.PointerDown,point.X,point.Y).PointerCaptured);t.Present();
        Assert.NotEqual(before.Frame.Metadata.FrameId,t.Preview.PresentedView!.Frame.Metadata.FrameId);
        Assert.Same(before.Picks,t.Preview.PresentedView.Picks);Assert.True(t.Preview.IsCurrent(before));
        Assert.False(t.Input(InputKind.PointerUp,point.X,point.Y).PointerCaptured);
        Assert.Equal(DocumentData.Child.Value.ToString("D"),t.Workspace.SelectedId);Assert.Equal(0,t.Content.UndoCount);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Camera_ControlAckAndUploadedDrawCannotAdmitPickingBeforeActualPresentation(bool upload)
    {
        using var t=new ViewportInputFixture();t.Start();var before=t.Preview.PresentedView!;var selected=t.Workspace.SelectedId;
        Assert.True(EditorCameraNavigation.TryDolly(t.State.Camera,1,out var camera));Assert.True(t.State.Queue(t.Controller.ViewId,before.State.Context,camera));
        ViewportInputFixture.WaitUntil(()=>t.Runtime.LastView?.Camera==camera);
        if(upload)
        {
            var ticket=t.Issue(t.Preview.DesiredView!);
            Assert.False(t.DrawAndAck(ticket,acknowledge:false));Assert.Same(ticket,t.View.Canvas.DrawnFrame);
        }
        Assert.False(t.Preview.IsCurrent(before));Assert.False(t.Preview.CameraUpdates.IsCompleted);
        var point=t.Point(DocumentData.Child);t.Click(point.X,point.Y);Assert.Equal(selected,t.Workspace.SelectedId);
        t.Present();Assert.True(t.Preview.IsCurrent(t.Preview.PresentedView!));point=t.Point(DocumentData.Child);t.Click(point.X,point.Y);
        Assert.Equal(DocumentData.Child.Value.ToString("D"),t.Workspace.SelectedId);Assert.Single(t.Runtime.Leases);
    }

    [Theory] [InlineData("opening")] [InlineData("project")] [InlineData("stop")] [InlineData("play")] [InlineData("delete")] [InlineData("surface")]
    public void Click_CannotPublishAcrossDocumentProjectStopPlayDeletionOrSurfaceChange(string change)
    {
        using var t=new ViewportInputFixture();t.Start();var point=t.Point(DocumentData.Child);var before=t.Preview.PresentedView!;
        Assert.True(t.Input(InputKind.PointerDown,point.X,point.Y).PointerCaptured);
        switch(change)
        {
            case "opening":t.Owner.Run(DocumentCommand.Open,"input.aetherscene");t.SyncSource();break;
            case "project":t.Workspace.SetProject(null);break;
            case "stop":DocumentUiFixture.Wait(t.Coordinator.StopAsync());break;
            case "play":t.Owner.RuntimeIdle=false;break;
            case "delete":
                Assert.True(t.Owner.Document.EditEntities(new DeleteEntitiesBatch(DocumentData.Operation(t.Content.Document.Revision),[DocumentData.Child],EntityDeletionPolicy.Subtree)).Succeeded);t.SyncSource();break;
            case "surface":
                var resizing=t.Preview.ResizeAsync(320,180);ViewportInputFixture.WaitUntil(()=>t.Frames.Presentation!=before.Frame.Presentation);t.Present(resizing);break;
        }
        var selected=t.Workspace.SelectedId;var intent=t.Workspace.SelectionRevision;
        Assert.False(t.Input(InputKind.PointerUp,point.X,point.Y).PointerCaptured);
        Assert.False(t.Controller.IsGesturing);Assert.Equal(selected,t.Workspace.SelectedId);Assert.Equal(intent,t.Workspace.SelectionRevision);
        Assert.NotEqual(DocumentData.Child.Value.ToString("D"),t.Workspace.SelectedId);
        if(change=="delete")Assert.DoesNotContain(DocumentData.Child.Value.ToString("D"),t.Workspace.EntityIds);
        if(change=="stop")Assert.True(Assert.Single(t.Runtime.Leases).ResourcesReleased);
    }

    [Theory] [InlineData("dpi")] [InlineData("hidden-parent")] [InlineData("geometry")] [InlineData("dispose")] [InlineData("detach")]
    public void ActiveGesture_ContextLossReleasesCaptureAndNeverRollsBackIntoForeignGeometry(string change)
    {
        using var t=new ViewportInputFixture(wrap:true);t.Start();t.Drag(1);var moved=t.State.Camera;
        switch(change)
        {
            case "dpi":t.Ui.Resize(new(1920,1200,1.5f));t.Controller.Observe(t.Ui);break;
            case "hidden-parent":t.Ui.Root.IsVisible=false;break;
            case "geometry":t.View.Canvas.Arrange(new(12,40,800,500));break;
            case "dispose":t.Controller.Dispose();break;
            case "detach":((OverlayHost)t.Ui.Root).Items.Remove(t.View);break;
        }
        var point=t.Center;var result=t.Input(InputKind.PointerUp,point.X+24,point.Y+10,1);
        Assert.False(result.PointerCaptured);Assert.False(t.Controller.IsGesturing);Assert.Same(moved,t.State.Camera);Assert.Equal(0,t.Content.UndoCount);
        if(change=="detach"){Assert.False(t.Controller.IsActive);Assert.Equal(Guid.Empty,t.State.ActiveView);Assert.False(t.View.IsDisposed);}
    }
}
