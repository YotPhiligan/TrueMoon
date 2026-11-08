using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Preview;

public sealed partial class ScenePreview
{
    private PreviewViewState? _viewCurrent, _viewDesired, _waitingCamera, _cameraInFlight;
    private PreviewPickSnapshot? _viewPicks;
    private PreviewPresentedView? _viewReceipt;
    private CancellationTokenSource? _cameraEnd;
    private TaskCompletionSource? _cameraPresented, _cameraIdle;
    private Task _cameraTask = Task.CompletedTask;
    private bool _cameraRunning, _viewChanging;
    private bool _presentationSuspended;

    /// <summary>Pauses UI presentation admission/waits without replacing the physical render surface or child.</summary>
    /// <param name="suspended">True while the consumer cannot present, for example a hidden/minimized UI.</param>
    public void SetPresentationSuspended(bool suspended)
    {
        lock(_stateGate)
        {
            if(_closed||_presentationSuspended==suspended)return;
            _presentationSuspended=suspended;
            if(suspended){_viewReceipt=null;_presented=null;CancelCameraCore();return;}
            if(_viewDesired!=null&&(_viewCurrent!=_viewDesired||_viewReceipt?.State!=_viewDesired))
            {
                _waitingCamera=_viewDesired;
                if(_cameraIdle==null||_cameraIdle.Task.IsCompleted)_cameraIdle=new(TaskCreationOptions.RunContinuationsAsynchronously);
                StartCameraWorkerCore();
            }
        }
    }

    /// <summary>Gets the latest desired view. A control reply or upload never implies presentation.</summary>
    public PreviewViewState? DesiredView { get { lock (_stateGate) return _viewDesired; } }
    /// <summary>Gets the last actual receipt on the active runtime/surface, possibly behind a pending desired camera.</summary>
    public PreviewPresentedView? PresentedView
    { get { lock (_stateGate) return _viewReceipt != null && DescribeCore(_viewReceipt.Frame) != null ? _viewReceipt : null; } }
    /// <summary>Gets completion of the current bounded update batch, including actual presentation; lifecycle changes cancel it.</summary>
    public Task CameraUpdates { get { lock (_stateGate) return _cameraIdle?.Task ?? Task.CompletedTask; } }

    /// <summary>Opens an explicit camera/content context before the first frame. Ordinary ShowAsync stays static/untagged.</summary>
    /// <param name="scene">Complete committed source.</param><param name="view">Caller-owned opening/view/content identity and camera.</param>
    /// <param name="width">Physical width.</param><param name="height">Physical height.</param><param name="token">Readiness cancellation.</param>
    /// <returns>Document readiness after actual presentation, not merely load/control acceptance.</returns>
    public Task<ScenePreviewDocument> ShowViewAsync(ResolvedSceneSnapshot scene, PreviewViewState view, int width, int height, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(scene); ArgumentNullException.ThrowIfNull(view);
        if (view.Context.SceneId != scene.Id.Value || view.Context.SceneRevision != scene.Revision) throw new ArgumentException("View/content mismatch.", nameof(view));
        view.Camera.CreateProjection(width, height);
        return ShowCoreAsync(scene, width, height, token, view);
    }

    /// <summary>Replaces the single latest waiting camera. At most one command/presentation wait is in flight.</summary>
    /// <param name="context">Exact opening/view/content; older contexts cannot update a new document.</param>
    /// <param name="camera">Whole newer camera, or an exact desired no-op.</param><returns>False while changing/closed/unready/foreign.</returns>
    public bool QueueCamera(PreviewViewContext context, PreviewCamera camera)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(camera);
        lock (_stateGate)
        {
            if (_closed || _viewChanging || _work != null || _viewDesired == null || _document == null ||
                context != _viewDesired.Context || !MatchesRuntime(_document.Runtime)) return false;
            if (camera == _viewDesired.Camera) return true;
            if (camera.Revision <= _viewDesired.Camera.Revision) throw new InvalidDataException("Camera revision must advance without reuse.");
            var surface = _frames.Presentation!.Surface;
            camera.CreateProjection(surface.Suspended ? 640 : surface.Width, surface.Suspended ? 360 : surface.Height);
            _viewDesired = _waitingCamera = new(context, camera);
            if (_cameraIdle == null || _cameraIdle.Task.IsCompleted) _cameraIdle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            StartCameraWorkerCore(); return true;
        }
    }

    /// <summary>Checks runtime/surface/context/desired-camera admission without requiring latest FrameId or acknowledging again.</summary>
    /// <param name="receipt">A previously actual-presented immutable receipt.</param><returns>True only while its semantic camera/content/surface remains current; caller must additionally check layout/input/selection intent.</returns>
    public bool IsCurrent(PreviewPresentedView receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        lock (_stateGate) return !_presentationSuspended && _viewReceipt!=null && !_viewChanging && _work == null && receipt.State == _viewDesired &&
            ReferenceEquals(receipt.Picks, _viewPicks) && DescribeCore(receipt.Frame) != null;
    }

    /// <summary>Acknowledges actual UI presentation and freezes image/clip/layout with its original camera and pick source.</summary>
    /// <param name="frame">Exact drawn ticket.</param><param name="image">Actually drawn logical image.</param><param name="clip">Effective logical clip.</param>
    /// <param name="layoutRevision">Positive revision of the active UI binding's geometry.</param><returns>False for invalid/stale/unknown capture or rejected presentation ack.</returns>
    public bool Presented(ViewportFrame frame, ViewportImageBounds image, ViewportImageBounds clip, ulong layoutRevision)
    {
        ArgumentNullException.ThrowIfNull(frame);
        lock (_stateGate)
        {
            if (_presentationSuspended || layoutRevision == 0 || !image.TryMapPixel(image.Left, image.Top, 1, 1, out _, out _) ||
                !clip.TryMapPixel(clip.Left, clip.Top, 1, 1, out _, out _)) return false;
            var state = CaptureStateCore(frame);
            if (state == null || _viewPicks == null || DescribeCore(frame) == null) return false;
            var projection = state.Camera.CreateProjection(frame.Presentation.Surface.Width, frame.Presentation.Surface.Height);
            if (!_frames.Presented(frame)) return false;
            _presented = new(new(new(state.Context.SceneId), state.Context.SceneRevision, frame.Presentation.Surface.Identity), frame);
            _viewReceipt = new(frame, state, projection, _viewPicks, image, clip, layoutRevision);
            AcknowledgeCameraCore(frame); return true;
        }
    }

    private PreviewViewState? CaptureStateCore(ViewportFrame frame)
    {
        var stamp = frame.Metadata.PreviewView;
        if (stamp == null) return null;
        if (_viewCurrent?.Stamp == stamp) return _viewCurrent;
        if (_cameraInFlight?.Stamp == stamp) return _cameraInFlight;
        if (_viewReceipt?.State.Stamp == stamp && _viewReceipt.State.Context == _viewDesired?.Context) return _viewReceipt.State;
        return null;
    }
    private void AcknowledgeCameraCore(ViewportFrame frame)
    { if (_cameraInFlight?.Stamp == frame.Metadata.PreviewView) _cameraPresented?.TrySetResult(); }

    private async Task PauseCameraAsync(CancellationToken token)
    {
        Task task;
        lock (_stateGate)
        {
            if (_viewChanging || _work != null) throw new InvalidOperationException("A preview update is already in progress.");
            _viewChanging = true; CancelCameraCore(); task = _cameraTask;
        }
        try { await task.WaitAsync(token).ConfigureAwait(false); }
        catch { lock (_stateGate) _viewChanging = false; throw; }
    }
    private async Task EnterViewGateAsync(CancellationToken token)
    {
        await PauseCameraAsync(token).ConfigureAwait(false);
        try
        {
            if (!await _gate.WaitAsync(0, token).ConfigureAwait(false)) throw new InvalidOperationException("A preview update is already in progress.");
        }
        catch { EndViewChange(); throw; }
    }
    private void CancelCameraCore()
    { _waitingCamera = null; _cameraIdle?.TrySetCanceled(); _cameraEnd?.Cancel(); }
    private void EndViewChange()
    {
        lock (_stateGate)
        {
            _viewChanging = false;
            if (_viewCurrent != null && _viewDesired != null && _viewDesired != _viewCurrent && _document != null && MatchesRuntime(_document.Runtime))
            {
                _waitingCamera = _viewDesired;
                _cameraIdle = new(TaskCreationOptions.RunContinuationsAsynchronously); StartCameraWorkerCore();
            }
        }
    }
    private void InitializeViewCore(PreviewViewState? view, PreviewPickSnapshot? picks)
    { _viewCurrent = _viewDesired = view; _viewPicks = picks; _viewReceipt = null; _cameraInFlight = null; }

    private void StartCameraWorkerCore()
    {
        if (_cameraRunning || _viewChanging || _closed || _presentationSuspended || _waitingCamera == null || _frames.Presentation?.Surface.Suspended != false) return;
        _cameraRunning = true; _cameraEnd?.Dispose(); var end = new CancellationTokenSource(); _cameraEnd = end;
        _cameraTask = Task.Run(() => RunCameraAsync(end.Token));
    }
    private async Task RunCameraAsync(CancellationToken token)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            while (true)
            {
                PreviewViewState view; ScenePreviewDocument document; Task presented;
                lock (_stateGate)
                {
                    if (token.IsCancellationRequested || _closed || _presentationSuspended || _waitingCamera == null || _document == null || !MatchesRuntime(_document.Runtime)) break;
                    view = _waitingCamera; _waitingCamera = null; document = _document;
                    _cameraInFlight = view; _cameraPresented = new(TaskCreationOptions.RunContinuationsAsynchronously); presented = _cameraPresented.Task;
                }
                // Lifecycle cancellation releases presentation waits first. An accepted control exchange drains under its own bounded supervisor deadline.
                await _frames.Supervisor.UpdatePreviewViewAsync(view, document.Runtime, CancellationToken.None).ConfigureAwait(false);
                lock (_stateGate)
                {
                    if (_viewDesired?.Context != view.Context || !MatchesRuntime(document.Runtime)) break;
                    _viewCurrent = view;
                }
                await presented.WaitAsync(TimeSpan.FromSeconds(15), token).ConfigureAwait(false);
                lock (_stateGate) { _cameraInFlight = null; _cameraPresented = null; }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            lock (_stateGate) _cameraIdle?.TrySetException(error);
            await FailAsync(error).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
            lock (_stateGate)
            {
                _cameraRunning = false; _cameraInFlight = null; _cameraPresented = null;
                if (_waitingCamera != null && !_viewChanging && !_closed) StartCameraWorkerCore();
                else _cameraIdle?.TrySetResult();
            }
        }
    }
}
