using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Editor;

/// <summary>App-owned immutable viewport state and exclusive binding admission. Holds no UI controls or native resources.</summary>
public sealed class EditorViewportState : IDisposable
{
    private readonly Lock _gate = new();
    private readonly EditorWorkspace _workspace;
    private readonly ScenePreview _preview;
    private Guid _active, _opening;
    private PreviewCamera _camera = PreviewCamera.Default;
    private PreviewViewContext? _context;
    private bool _disposed;
    private bool _visible;
    private TaskCompletionSource _visibleReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource _visibility = new();
    /// <summary>Completion after the active view becomes able to present. Startup waits here before allocating runtime resources.</summary>
    public Task VisibleReady { get { lock(_gate)return _visibleReady.Task; } }
    /// <summary>Cancelled on hide/suspend, allowing startup to retry after visibility resumes.</summary>
    public CancellationToken VisibilityToken { get { lock(_gate)return _visibility.Token; } }
    /// <summary>Whether the binding can currently present.</summary>
    public bool IsVisible { get { lock(_gate)return _visible; } }
    /// <summary>Updates visibility only for the actual binding owner.</summary>
    /// <param name="view">Exclusive active binding.</param><param name="visible">Whether the consumer can actually present.</param>
    public void SetVisible(Guid view,bool visible)
    {
        lock(_gate)
        {
            if(_disposed||view==Guid.Empty||view!=_active||_visible==visible)return;
            _visible=visible;_preview.SetPresentationSuspended(!visible);
            if(visible)
            {
                if(_visibility.IsCancellationRequested){_visibility.Dispose();_visibility=new();}
                _visibleReady.TrySetResult();
            }
            else { _visibleReady=new(TaskCreationOptions.RunContinuationsAsynchronously);_visibility.Cancel(); }
        }
    }
    /// <summary>Creates state borrowing the workspace and App-owned preview.</summary>
    /// <param name="workspace">UI-owner committed context and selection.</param><param name="preview">App-owned camera/presentation lane.</param>
    public EditorViewportState(EditorWorkspace workspace, ScenePreview preview)
    {
        ArgumentNullException.ThrowIfNull(workspace); ArgumentNullException.ThrowIfNull(preview);
        _workspace = workspace; _preview = preview; _opening = workspace.DocumentEpoch;
        workspace.SceneChanged += SceneChanged;
    }
    /// <summary>Gets the current transient camera. It is preserved across committed revisions in one opening.</summary>
    public PreviewCamera Camera { get { lock (_gate) return _camera; } }
    /// <summary>Gets the exclusive binding, or empty before attachment/after release.</summary>
    public Guid ActiveView { get { lock (_gate) return _active; } }
    /// <summary>Claims an unowned binding, never stealing another live binding.</summary>
    /// <param name="view">Fresh controller identity.</param><returns>Whether this view owns input/render presentation.</returns>
    public bool TryAcquire(Guid view)
    {
        if (view == Guid.Empty) throw new ArgumentException("A binding ID must be nonempty.", nameof(view));
        lock (_gate)
        {
            if (_disposed || _active != Guid.Empty && _active != view) return false;
            if (_active != view) { _active = view; _camera = PreviewCamera.Default; _context = null; }
            return true;
        }
    }
    /// <summary>Releases only the actual binding owner. The controller separately drains borrowed preview work.</summary>
    /// <param name="view">Binding to release.</param><returns>True only for the current owner.</returns>
    public bool Release(Guid view)
    { lock (_gate) { if (_active != view || view==Guid.Empty) return false; SetVisible(view,false);_active = Guid.Empty; _context = null; return true; } }
    /// <summary>Captures committed source on the UI owner before it enters the asynchronous preview queue.</summary>
    /// <param name="scene">Exact current workspace resolved snapshot.</param><returns>Detached immutable context/camera.</returns>
    public PreviewViewState Capture(ResolvedSceneSnapshot scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed,this);
            if (_active == Guid.Empty || !ReferenceEquals(_workspace.Document?.Resolved,scene))
                throw new InvalidOperationException("An active viewport and current committed workspace source are required.");
            _context = new(_workspace.DocumentEpoch,_active,_workspace.ContentToken,scene.Id.Value,scene.Revision);
            return new(_context,_camera);
        }
    }
    /// <summary>Checks opening/view/content admission without creating a new token or reading controls.</summary>
    /// <param name="view">Candidate binding.</param><param name="context">Frozen candidate context.</param><returns>Whether all ownership identities still match.</returns>
    public bool Matches(Guid view, PreviewViewContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_gate) return !_disposed && _active == view && _context == context && context.DocumentEpoch == _workspace.DocumentEpoch &&
            context.ContentId == _workspace.ContentToken && context.SceneRevision == _workspace.Scene.Revision;
    }
    /// <summary>Publishes a validated candidate only when the same context still owns the runtime camera lane.</summary>
    /// <param name="view">Candidate binding.</param><param name="context">Exact committed source context.</param>
    /// <param name="camera">Whole immutable newer camera.</param><returns>Whether the runtime lane accepted the candidate.</returns>
    public bool Queue(Guid view, PreviewViewContext context, PreviewCamera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        lock (_gate)
        {
            if (!Matches(view,context) || !_preview.QueueCamera(context,camera)) return false;
            _camera = camera; return true;
        }
    }
    private void SceneChanged()
    {
        lock (_gate)
        {
            if (_opening != _workspace.DocumentEpoch) { _opening = _workspace.DocumentEpoch; _camera = PreviewCamera.Default; }
            _context = null;
        }
    }
    /// <summary>Detaches App data subscriptions; borrowed preview/workspace are retained by their owners.</summary>
    public void Dispose()
    { lock (_gate) { if(_disposed)return; _disposed=true; _active=Guid.Empty; _context=null; _workspace.SceneChanged-=SceneChanged;
        _visibleReady.TrySetCanceled();_visibility.Cancel();_visibility.Dispose(); } }
}
