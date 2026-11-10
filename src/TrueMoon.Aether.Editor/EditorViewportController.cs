using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>UI-parent-owned retained camera/input controller. Borrows App data/preview; owns subscriptions and its capture only.</summary>
public sealed partial class EditorViewportController : IDisposable
{
    private readonly EditorView _view;
    private readonly EditorViewportState _state;
    private readonly EditorWorkspace _workspace;
    private readonly EditorDocumentCommands _documents;
    private readonly EditorViewModel _model;
    private readonly EditorPreview _coordinator;
    private readonly ScenePreview _preview;
    private UiSession? _ui;
    private Element? _outerRoot;
    private IInputContext? _capture;
    private PreviewPresentedView? _start;
    private PreviewCamera? _original;
    private UiViewport _viewport;
    private UiViewport _observed;
    private ulong _selection;
    private float _x,_y;
    private Gesture _gesture;
    private bool _disposed;
    private enum Gesture { None, Click, Orbit, Pan, Translate }
    /// <summary>Creates a caller-owned controller through the registered factory, on the UI owner.</summary>
    /// <param name="arguments">Caller-owned view.</param><param name="state">Borrowed App-owned camera/binding state.</param>
    /// <param name="workspace">Borrowed selection/content owner.</param><param name="documents">Borrowed document admission.</param>
    /// <param name="model">Borrowed common selection actions.</param><param name="coordinator">Borrowed preview drain owner.</param>
    /// <param name="preview">Borrowed camera transport and actual receipts.</param>
    public EditorViewportController(EditorViewportArguments arguments, EditorViewportState state, EditorWorkspace workspace,
        EditorDocumentCommands documents, EditorViewModel model, EditorPreview coordinator, ScenePreview preview)
    {
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(arguments.View);
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(documents);ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(coordinator);ArgumentNullException.ThrowIfNull(preview);
        _view=arguments.View;_state=state;_workspace=workspace;_documents=documents;_model=model;_coordinator=coordinator;_preview=preview;
        _view.VerifyAccess(); _view.Canvas.AttachController(this);
        _view.Canvas.AttachmentChanged+=Attachment;_view.Canvas.FocusChanged+=FocusChanged;
        _view.Invalidated+=OverlayChanged;_workspace.SceneChanged+=SceneChanged;
        Attachment();
    }
    /// <summary>Fresh identity of this UI factory product.</summary>
    public Guid ViewId { get; } = Guid.NewGuid();
    /// <summary>Whether this attached view has the exclusive viewport binding.</summary>
    public bool IsActive => !_disposed && _view.Canvas.IsAttached && _state.ActiveView==ViewId;
    internal bool CanPresent => IsActive && _state.IsVisible;
    /// <summary>Whether this product owns a pointer gesture.</summary>
    public bool IsGesturing => _gesture!=Gesture.None;
    internal bool NeedsFrame => IsGesturing || IsActive&&_ui!=null&&_ui.Viewport!=_observed;
    internal bool Activate() => !_disposed && _view.Canvas.IsAttached && _state.TryAcquire(ViewId);
    internal void Observe(UiSession ui)
    {
        if(_disposed)return;_view.VerifyAccess();_ui=ui;_observed=ui.Viewport;Activate();
        ObserveRoot(ui.Root);
        _view.Canvas.ObserveViewport(ui.Viewport);ObserveVisibility();
        if(IsGesturing&&!GestureCurrent())End(true);
    }
    private void Attachment()
    {
        if(_view.Canvas.IsAttached){Activate();return;}
        End(false); if(_state.Release(ViewId))_ = _coordinator.StopAsync(); ObserveRoot(null);_ui=null;
    }
    private void ObserveRoot(Element? root)
    {
        if(ReferenceEquals(root,_view))root=null;
        if(ReferenceEquals(root,_outerRoot))return;
        if(_outerRoot!=null)_outerRoot.Invalidated-=OverlayChanged;
        _outerRoot=root;
        if(_outerRoot!=null)_outerRoot.Invalidated+=OverlayChanged;
    }
    private void FocusChanged(Element _) { if(!_view.Canvas.IsFocused)End(true); }
    private void OverlayChanged(Invalidation _) { ObserveVisibility();if(IsGesturing&&Blocked())End(true); }
    private void ObserveVisibility()
    {
        if(!IsActive||_ui==null)return;
        var visible=!_ui.Viewport.IsEmpty&&_view.Canvas.Bounds.Width>0&&_view.Canvas.Bounds.Height>0;
        for(Element? e=_view.Canvas;e!=null;e=e.Parent)visible&=e.IsVisible&&e.Opacity>0;
        var changed=_state.IsVisible!=visible;
        _state.SetVisible(ViewId,visible);
        if(changed)_view.Canvas.InvalidatePresentedGeometry();
    }
    private void SceneChanged() { if(IsGesturing)End(false); }
    private bool DocumentReady()
    {
        var d=_documents.Read();
        return !d.Busy&&!d.Exiting&&!d.Closed&&d.ProjectReady&&d.RuntimeIdle&&d.Document is {IsClosed:false,Operation:DocumentOperation.None,Content:{} content} &&
            d.Document.OpeningId==_workspace.DocumentEpoch && ReferenceEquals(content.Document,_workspace.Document?.Document) &&
            (d.Project==null||ReferenceEquals(d.Project,_workspace.Project));
    }
    private bool Blocked() => _view.Documents?.IsVisible==true || AnyPopup(Root());
    private static bool AnyPopup(Element root) => root is OverlayHost {Popup:not null} || root.Children.Any(AnyPopup);
    private Element Root() { Element root=_view;while(root.Parent!=null)root=root.Parent;return root; }
    private PreviewPresentedView? Ready()
    {
        if(!CanPresent||_ui==null||_ui.Viewport.IsEmpty||!DocumentReady()||Blocked())return null;
        var receipt=_preview.PresentedView;
        return receipt!=null&&_state.Matches(ViewId,receipt.State.Context)&&_preview.IsCurrent(receipt)&&
            _view.Canvas.DrawnFrame?.Metadata.PreviewView==receipt.State.Stamp&&
            ReferenceEquals(_view.Canvas.DrawnFrame?.Presentation,receipt.Frame.Presentation)&&
            _view.Canvas.MatchesPresentedLayout(receipt.ImageBounds,receipt.ClipBounds,receipt.LayoutRevision)?receipt:null;
    }
    private bool GestureCurrent(bool interaction=true)
    {
        var receipt=_start;
        if(receipt==null||!IsActive||_ui==null||_ui.Viewport!=_viewport||!DocumentReady()||interaction&&Blocked()||
            !_state.Matches(ViewId,receipt.State.Context)||_preview.Document?.Runtime!=receipt.Frame.Presentation.Surface.Identity||
            !ReferenceEquals(_preview.PresentedView?.Frame.Presentation,receipt.Frame.Presentation)||
            !_view.Canvas.MatchesPresentedLayout(receipt.ImageBounds,receipt.ClipBounds,receipt.LayoutRevision))return false;
        if (_gesture == Gesture.Translate) return TranslationCurrent(receipt);
        return _gesture==Gesture.Click ? _workspace.SelectionRevision==_selection&&_preview.IsCurrent(receipt) :
            _preview.DesiredView?.Camera==_state.Camera;
    }
    internal bool PreviewInput(UiInput input,IInputContext context)
    {
        if(context is UiSession ui)Observe(ui);
        if(!IsGesturing)return false;
        if(!GestureCurrent()){End(false);return false;}
        if(input.Kind==InputKind.FocusLost){End(true);return false;}
        if(input.Kind==InputKind.KeyDown){End(true);return input.Key==UiKey.Escape;}
        if(input.Kind==InputKind.PointerDown){End(true);return false;}
        return false;
    }
    internal bool HandleInput(UiInput input,IInputContext context)
    {
        if(_disposed)return false;
        if(context is UiSession ui)Observe(ui);
        if(IsGesturing)
        {
            if(!GestureCurrent()){End(false);return false;}
            if (_gesture == Gesture.Translate) return HandleTranslation(input);
            if(input.Kind==InputKind.KeyDown&&input.Key==UiKey.Escape){End(true);return true;}
            if(input.Kind==InputKind.PointerMove)
            {
                if(_gesture==Gesture.Click)
                { if(DistanceSquared(input.X,input.Y)>16)End(false); }
                else Move(input.X,input.Y);
                return true;
            }
            var button=_gesture==Gesture.Click?0:_gesture==Gesture.Orbit?1:2;
            if(input.Kind==InputKind.PointerUp&&input.Button==button)
            {
                if(_gesture==Gesture.Click)Pick(input.X,input.Y);
                else {try{Move(input.X,input.Y);}finally{End(false);}}
                return true;
            }
            return input.Kind==InputKind.Wheel;
        }
        var receipt=Ready();if(receipt==null)return false;
        try
        {
            if(input.Kind==InputKind.KeyDown&&input.Key==UiKey.F&&!input.Control&&!input.Shift&&_view.Canvas.IsFocused)
            {
                var selected=receipt.Picks.Primitives.FirstOrDefault(p=>p.Entity.Value.ToString("D")==_workspace.SelectedId);
                if(selected.Entity.Value==Guid.Empty)return false;
                if(EditorCameraNavigation.TryFrame(_state.Camera,selected.World,receipt.Projection.Width,receipt.Projection.Height,out var framed))Publish(receipt,framed);
                return true;
            }
            if(!Inside(receipt,input.X,input.Y)||!ReferenceEquals(Hit(Root(),input.X,input.Y),_view.Canvas))return false;
            if(input.Kind==InputKind.Wheel)
            {
                if(EditorCameraNavigation.TryDolly(_state.Camera,input.WheelDelta,out var dolly))Publish(receipt,dolly);
                return true;
            }
            if(input.Kind!=InputKind.PointerDown||input.Button is <0 or >2)return false;
            if (input.Button == 0 && BeginTranslation(receipt, input, context)) return true;
            context.Focus(_view.Canvas);
            _gesture=input.Button==0?Gesture.Click:input.Button==1?Gesture.Orbit:Gesture.Pan;
            _start=receipt;_original=_state.Camera;_viewport=_ui!.Viewport;_selection=_workspace.SelectionRevision;_x=input.X;_y=input.Y;_capture=context;
            try{context.Capture(_view.Canvas);}catch{End(false);throw;}
            return true;
        }
        catch(OverflowException){End(false);_ = _coordinator.StopAsync();return true;}
    }
    private void Move(float x,float y)
    {
        if(!float.IsFinite(x)||!float.IsFinite(y)||_start==null||_original==null)return;
        try
        {
            PreviewCamera camera; var valid=_gesture==Gesture.Orbit?
                EditorCameraNavigation.TryOrbit(_original,_state.Camera,(double)x-_x,(double)y-_y,out camera):
                EditorCameraNavigation.TryPan(_original,_state.Camera,(double)x-_x,(double)y-_y,_start.ImageBounds.Height,out camera);
            if(valid)Publish(_start,camera);
        }
        catch(OverflowException){End(false);_ = _coordinator.StopAsync();}
    }
    private void Publish(PreviewPresentedView receipt,PreviewCamera camera)
    {
        try{_state.Queue(ViewId,receipt.State.Context,camera);}
        catch(RenderContentException) { } // Reject an unrepresentable candidate without changing the last valid camera.
    }
    private double DistanceSquared(float x,float y) {var dx=(double)x-_x;var dy=(double)y-_y;return dx*dx+dy*dy;}
    private void Pick(float x,float y)
    {
        var receipt=_start!;var selection=_selection;var viewport=_viewport;
        var valid=GestureCurrent()&&DistanceSquared(x,y)<=16&&Inside(receipt,x,y);
        var result=valid?receipt.Picks.Pick(receipt.Projection,receipt.ImageBounds,x,y):default;
        End(false); // Release capture before any selection callbacks, then repeat admission.
        if(!valid||_ui?.Viewport!=viewport||_workspace.SelectionRevision!=selection||Ready() is not {} current||
            current.State!=receipt.State||current.LayoutRevision!=receipt.LayoutRevision||!_preview.IsCurrent(receipt))return;
        if(result.Kind==PreviewPickKind.Hit&&result.Entity is {} id&&_workspace.EntityIds.Contains(id.Value.ToString("D")))_model.Select(id.Value.ToString("D"));
        else if(result.Kind==PreviewPickKind.Miss)_model.ClearSelection();
    }
    internal void CancelInput()=>End(true);
    private void End(bool rollback)
    {
        if(_gesture==Gesture.None)return;
        var translating = _gesture == Gesture.Translate;
        var restore=rollback&&(_gesture is Gesture.Orbit or Gesture.Pan)&&GestureCurrent(false);var original=_original;var receipt=_start;var capture=_capture;
        _gesture=Gesture.None;_capture=null;_start=null;_original=null;
        ClearTranslation();
        try{capture?.ReleaseCapture(_view.Canvas);}
        finally
        {
            if (translating) _view.Canvas.Invalidate();
            if(restore&&original!=null&&receipt!=null&&DocumentReady()&&_state.Matches(ViewId,receipt.State.Context))
                try{Publish(receipt,_state.Camera.WithView(original.World,original.Projection,original.Pivot,original.Distance));}
                catch(OverflowException){_ = _coordinator.StopAsync();}
        }
    }
    private static bool Inside(PreviewPresentedView receipt,float x,float y)=>
        receipt.ImageBounds.TryMapPixel(x,y,receipt.Projection.Width,receipt.Projection.Height,out _,out _)&&
        receipt.ClipBounds.TryMapPixel(x,y,1,1,out _,out _);
    private static Element? Hit(Element element,float x,float y)
    {
        var b=element.Bounds;
        if(!element.IsVisible||!element.IsEffectivelyEnabled||element.IsDisposed||x<b.X||y<b.Y||x>=b.X+b.Width||y>=b.Y+b.Height)return null;
        if(element.ChildClipBounds.Contains(x,y))for(var i=element.Children.Count-1;i>=0;i--){var hit=Hit(element.Children[i],x,y);if(hit!=null)return hit;}
        return element;
    }
    /// <summary>Releases capture/subscriptions immediately. App-owned coordinator retains and drains asynchronous stop work.</summary>
    public void Dispose()
    {
        if(_disposed)return;End(false);_disposed=true;
        _view.Canvas.AttachmentChanged-=Attachment;_view.Canvas.FocusChanged-=FocusChanged;_view.Invalidated-=OverlayChanged;_workspace.SceneChanged-=SceneChanged;
        _view.Canvas.DetachController(this);ObserveRoot(null);_ui=null;
        if(_state.Release(ViewId))_ = _coordinator.StopAsync();
    }
}
