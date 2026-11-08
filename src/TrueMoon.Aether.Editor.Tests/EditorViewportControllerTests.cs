using System.Numerics;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Preview;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorViewportControllerTests
{
    [Fact]
    public void RenderResize_UsesNewProjectionAndRejectsLetterboxAndForeignCapture()
    {
        using var t=new ViewportInputFixture();t.Start();var old=t.Preview.PresentedView!;var surface=t.Frames.Presentation;
        var resize=t.Preview.ResizeAsync(360,360);ViewportInputFixture.WaitUntil(()=>t.Frames.Presentation!=surface);
        t.Present(resize);var current=t.Preview.PresentedView!;
        Assert.Equal(360,current.Projection.Width);Assert.Equal(360,current.Projection.Height);Assert.False(t.Preview.IsCurrent(old));
        Assert.True(current.ImageBounds.Width< t.View.Canvas.Bounds.Width-100);
        var b=t.View.Canvas.Bounds;var camera=t.State.Camera;var selected=t.Workspace.SelectedId;
        t.Click(b.X+5,b.Y+b.Height/2);t.Input(InputKind.Wheel,b.X+5,b.Y+b.Height/2,wheel:1);
        Assert.Same(camera,t.State.Camera);Assert.Equal(selected,t.Workspace.SelectedId);Assert.False(t.Controller.IsGesturing);
        var right=t.Point(DocumentData.Child);t.Ui.Capture(t.View.Entities.Values.First());
        t.Input(InputKind.PointerDown,right.X,right.Y,1);Assert.False(t.Controller.IsGesturing);Assert.Same(camera,t.State.Camera);
        t.Ui.ReleaseCapture(t.View.Entities.Values.First());t.Click(right.X,right.Y);
        Assert.Equal(DocumentData.Child.Value.ToString("D"),t.Workspace.SelectedId);
    }

    [Fact]
    public void CommittedEditUndoRedo_PreserveCameraRenewContentAndInvalidateCapturedGesture()
    {
        using var t=new ViewportInputFixture();t.Start();t.Drag(1);var p=t.Center;t.Input(InputKind.PointerUp,p.X+24,p.Y+10,1);t.Present();
        var camera=t.State.Camera;var epoch=t.Workspace.DocumentEpoch;var token=t.Workspace.ContentToken;var receipt=t.Preview.PresentedView!;
        t.Drag(2);Assert.True(t.Owner.Document.Apply(DocumentData.Edit(t.Content.Document.Revision,2)).Succeeded);t.SyncSource();
        Assert.False(t.Controller.IsGesturing);Assert.NotEqual(token,t.Workspace.ContentToken);Assert.Equal(epoch,t.Workspace.DocumentEpoch);
        var committedCamera=t.State.Camera;Assert.True(committedCamera.Revision>camera.Revision);Assert.False(t.State.Matches(t.Controller.ViewId,receipt.State.Context));
        Assert.True(t.Owner.Document.Undo(DocumentData.Operation(t.Content.Document.Revision)).Succeeded);t.SyncSource();Assert.Same(committedCamera,t.State.Camera);
        Assert.True(t.Owner.Document.Redo(DocumentData.Operation(t.Content.Document.Revision)).Succeeded);t.SyncSource();Assert.Same(committedCamera,t.State.Camera);
    }

    [Fact]
    public void Visibility_FencesOnlyActualOwnerAndSuspendsWithoutLosingDesiredCamera()
    {
        using var t=new ViewportInputFixture();t.Start();var owner=t.Controller.ViewId;var ready=t.State.VisibleReady;
        t.State.SetVisible(Guid.NewGuid(),false);Assert.True(t.State.IsVisible);Assert.True(ready.IsCompletedSuccessfully);
        t.State.SetVisible(owner,false);Assert.False(t.State.IsVisible);Assert.False(t.State.VisibleReady.IsCompleted);
        Assert.True(t.State.VisibilityToken.IsCancellationRequested);var camera=t.State.Camera;
        Assert.True(EditorCameraNavigation.TryDolly(camera,1,out var moved));
        Assert.True(t.State.Queue(owner,t.Preview.DesiredView!.Context,moved));Assert.Same(moved,t.State.Camera);
        t.State.SetVisible(owner,true);Assert.True(t.State.VisibleReady.IsCompletedSuccessfully);Assert.False(t.State.VisibilityToken.IsCancellationRequested);
        t.Present();Assert.Same(moved,t.Preview.PresentedView!.State.Camera);Assert.Single(t.Runtime.Leases);
    }

    [Theory] [InlineData(1)] [InlineData(2)]
    public void Navigation_RetainsInvalidDraftAndDocumentAndUsesSameChildAndSurface(int button)
    {
        using var t=new ViewportInputFixture();t.Start();t.Model.Select(DocumentData.Child.Value.ToString("D"));
        var field=t.Inspector.Fields.Single(f=>f.Key=="x");field.Value="invalid";
        var content=t.Content;var selected=t.Workspace.SelectedId;var surface=t.Frames.Presentation;
        t.Drag(button);var moved=t.State.Camera;Assert.NotEqual(PreviewCamera.Default.World,moved.World);
        var p=t.Center;t.Input(InputKind.PointerUp,p.X+24,p.Y+10,button);t.Present();
        Assert.False(t.Controller.IsGesturing);Assert.True(t.View.Canvas.IsFocused);
        Assert.Same(content.Document,t.Content.Document);Assert.Equal(content.UndoCount,t.Content.UndoCount);Assert.False(t.Owner.Document.Read().IsDirty);
        Assert.Equal(selected,t.Workspace.SelectedId);Assert.Same(field,t.Inspector.Fields.Single(f=>f.Key=="x"));Assert.Equal("invalid",field.Value);Assert.True(field.HasError);
        Assert.Single(t.Runtime.Leases);Assert.Same(surface,t.Frames.Presentation);Assert.Equal(0ul,t.Runtime.Leases[0].Game.TickNumber);
    }

    [Theory] [InlineData("escape")] [InlineData("focus")] [InlineData("foreign-capture")] [InlineData("popup")]
    public void Cancellation_ReleasesCaptureAndRestoresOriginalPoseWithNewRevision(string kind)
    {
        using var t=new ViewportInputFixture();t.Start();t.Drag(1);var moving=t.State.Camera;
        if(kind=="escape")t.Ui.HandleInput(new(InputKind.KeyDown,Key:UiKey.Escape));
        else if(kind=="focus")t.Ui.HandleInput(new(InputKind.FocusLost));
        else if(kind=="foreign-capture")t.Ui.Capture(t.View.Entities.Values.First());
        else t.View.Overlays.Open(t.View.Canvas,new Button("popup"),new(400,200,10,10),t.Ui);
        Assert.False(t.Controller.IsGesturing);Assert.Equal(PreviewCamera.Default.World,t.State.Camera.World);
        Assert.Equal(moving.Revision+1,t.State.Camera.Revision);t.Present();
        var point=t.Center;var result=t.Input(InputKind.PointerUp,point.X,point.Y,1);
        if(kind!="foreign-capture")Assert.False(result.PointerCaptured);
        Assert.Equal(PreviewCamera.Default.World,t.State.Camera.World);
    }

    [Fact]
    public void Picking_UsesSharedSelectionAndDraftPolicyForSameDifferentAndBackground()
    {
        using var t=new ViewportInputFixture();t.Start();var left=t.Point(DocumentData.Parent);var right=t.Point(DocumentData.Child);
        t.Model.Select(DocumentData.Child.Value.ToString("D"));var field=t.Inspector.Fields.Single(f=>f.Key=="x");field.Value="draft";
        t.Click(right.X,right.Y);Assert.Equal(DocumentData.Child.Value.ToString("D"),t.Model.SelectedId);Assert.Equal("draft",field.Value);Assert.True(t.Inspector.HasDraft);
        t.Click(left.X,left.Y);Assert.Equal(DocumentData.Parent.Value.ToString("D"),t.Model.SelectedId);Assert.False(t.Inspector.HasDraft);
        Assert.Equal("-1.2",t.Inspector.Fields.Single(f=>f.Key=="x").Value);
        var b=t.View.Canvas.ImageBounds;t.Click(b.X+b.Width/2,b.Y+10);
        Assert.Null(t.Workspace.SelectedId);Assert.Null(t.Model.SelectedId);Assert.False(t.Model.IsInspectorVisible);Assert.False(t.Inspector.HasDraft);
        Assert.Equal(0,t.Content.UndoCount);
    }

    [Theory] [InlineData("same")] [InlineData("clear")] [InlineData("other")]
    public void Click_OlderThanTreeOrClearIntentCannotPublishSelection(string intent)
    {
        using var t=new ViewportInputFixture();t.Start();var right=t.Point(DocumentData.Child);
        t.Input(InputKind.PointerDown,right.X,right.Y);
        if(intent=="clear")t.Model.ClearSelection();else t.Model.Select((intent=="same"?DocumentData.Parent:DocumentData.Child).Value.ToString("D"));
        var selected=t.Workspace.SelectedId;var version=t.Workspace.SelectionRevision;
        t.Input(InputKind.PointerUp,right.X,right.Y);
        Assert.False(t.Controller.IsGesturing);Assert.Equal(selected,t.Workspace.SelectedId);Assert.Equal(version,t.Workspace.SelectionRevision);
    }

    [Theory] [InlineData("drag")] [InlineData("pending")] [InlineData("runtime")] [InlineData("chrome")] [InlineData("popup")]
    public void Picking_RejectsDragPendingCameraRuntimeChromeAndOverlay(string reason)
    {
        using var t=new ViewportInputFixture();t.Start();var right=t.Point(DocumentData.Child);var selected=t.Workspace.SelectedId;
        if(reason=="pending"){t.Drag(1);var p=t.Center;t.Input(InputKind.PointerUp,p.X+24,p.Y+10,1);}
        if(reason=="runtime")t.Owner.RuntimeIdle=false;
        if(reason=="popup")t.View.Overlays.Open(t.View.Canvas,new Button("popup"),new(400,200,10,10),t.Ui);
        if(reason=="chrome"){var b=t.View.Commands.Values.First().Bounds;right=(b.X+2,b.Y+2);}
        t.Input(InputKind.PointerDown,right.X,right.Y);if(reason=="drag")t.Input(InputKind.PointerMove,right.X+8,right.Y);
        t.Input(InputKind.PointerUp,right.X+(reason=="drag"?8:0),right.Y);
        Assert.Equal(selected,t.Workspace.SelectedId);Assert.False(t.Controller.IsGesturing);
    }

    [Fact]
    public void WheelAndFrame_AreCameraOnlyAndFrameRequiresFocusedCanvasAndRenderableSelection()
    {
        using var t=new ViewportInputFixture();t.Start();var p=t.Center;
        t.Ui.Focus(t.View.Entities.Values.First());t.Input(InputKind.Wheel,p.X,p.Y,wheel:1);
        Assert.False(t.View.Canvas.IsFocused);Assert.InRange(t.State.Camera.Distance,5.1642f,5.1643f);t.Present();
        t.Model.Select(DocumentData.Child.Value.ToString("D"));var before=t.State.Camera;
        t.Ui.HandleInput(new(InputKind.KeyDown,Key:UiKey.F));Assert.Same(before,t.State.Camera);
        t.Ui.Focus(t.View.Canvas);Assert.True(t.Ui.HandleInput(new(InputKind.KeyDown,Key:UiKey.F)).Handled);
        Assert.InRange(Vector3.Distance(new(1.2f,1,0),t.State.Camera.Pivot),0,.00001f);t.Present();
        Assert.Equal(0,t.Content.UndoCount);Assert.False(t.Owner.Document.Read().IsDirty);
    }

    [Theory] [InlineData(1f)] [InlineData(1.5f)] [InlineData(2f)]
    public void DpiResizeAndHide_RequireFreshDrawBeforeInputAndKeepRetainedTree(float scale)
    {
        using var t=new ViewportInputFixture(wrap:true);t.Start();var canvas=t.View.Canvas;var old=t.Preview.PresentedView!;
        t.Ui.Resize(new((int)(1280*scale)+2,(int)(800*scale),scale));t.Controller.Observe(t.Ui);
        Assert.False(canvas.MatchesPresentedLayout(old.ImageBounds,old.ClipBounds,old.LayoutRevision));
        var p=t.Center;Assert.False(t.Input(InputKind.PointerDown,p.X,p.Y,1).PointerCaptured);
        t.Present();Assert.Same(canvas,t.View.Canvas);Assert.True(t.Preview.PresentedView!.LayoutRevision>old.LayoutRevision);
        canvas.IsVisible=false;Assert.False(t.State.IsVisible);Assert.Null(t.Preview.PresentedView);
        canvas.IsVisible=true;Assert.True(t.State.IsVisible);Assert.Null(t.Preview.PresentedView);
        p=t.Center;Assert.False(t.Input(InputKind.PointerDown,p.X,p.Y,1).PointerCaptured);t.Present();
        Assert.True(t.Input(InputKind.PointerDown,p.X,p.Y,1).PointerCaptured);t.Ui.HandleInput(new(InputKind.KeyDown,Key:UiKey.Escape));
        t.Ui.Root.IsVisible=false;Assert.False(t.State.IsVisible);Assert.Null(t.Preview.PresentedView);
        t.Ui.Root.IsVisible=true;Assert.True(t.State.IsVisible);Assert.False(canvas.CanAcknowledgeDraw);t.Present();
    }

    [Fact]
    public void SameImmutableDocumentReopen_RenewsEpochResetsCameraAndRejectsOldView()
    {
        using var t=new ViewportInputFixture();t.Start();t.Drag(2);var p=t.Center;t.Input(InputKind.PointerUp,p.X+24,p.Y+10,2);t.Present();
        var original=t.Content.Document;var epoch=t.Workspace.DocumentEpoch;var token=t.Workspace.ContentToken;var receipt=t.Preview.PresentedView!;
        t.Owner.Run(DocumentCommand.Open,"input.aetherscene");t.SyncSource();
        Assert.Same(original,t.Content.Document);Assert.NotEqual(epoch,t.Workspace.DocumentEpoch);Assert.NotEqual(token,t.Workspace.ContentToken);
        Assert.Same(PreviewCamera.Default,t.State.Camera);Assert.False(t.State.Matches(t.Controller.ViewId,receipt.State.Context));
        Assert.False(t.State.Queue(t.Controller.ViewId,receipt.State.Context,receipt.State.Camera));
    }

    [Fact]
    public void Binding_IsExclusiveAndDisposedOwnerDrainsWithoutDisposingBorrowedServices()
    {
        using var t=new ViewportInputFixture();t.Start();var foreign=Guid.NewGuid();
        Assert.False(t.State.TryAcquire(foreign));Assert.False(t.State.Release(foreign));
        t.Drag(1);t.Controller.Dispose();Assert.False(t.Controller.IsGesturing);Assert.Equal(Guid.Empty,t.State.ActiveView);
        t.State.SetVisible(Guid.Empty,true);Assert.False(t.State.IsVisible);
        TrueMoon.Aether.Editor.Tests.DocumentsUi.DocumentUiFixture.Wait(t.Coordinator.Pending);
        Assert.True(Assert.Single(t.Runtime.Leases).ResourcesReleased);Assert.False(t.Owner.Document.Read().IsClosed);
        Assert.True(t.State.TryAcquire(foreign));Assert.Equal(foreign,t.State.ActiveView);Assert.Same(PreviewCamera.Default,t.State.Camera);
    }
}
