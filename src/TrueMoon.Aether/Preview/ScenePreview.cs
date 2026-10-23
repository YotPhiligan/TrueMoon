using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Preview;

/// <summary>Authoring revision bound to one isolated built-in preview runtime generation.</summary>
/// <param name="SceneId">Persistent scene identity.</param><param name="Revision">Exact committed document revision.</param>
/// <param name="Runtime">Preview child identity and generation.</param>
public sealed record ScenePreviewDocument(SceneId SceneId, long Revision, SessionIdentity Runtime);

/// <summary>Actual presented frame associated with its exact authoring document and runtime.</summary>
/// <param name="Document">Document and runtime stamp.</param><param name="Frame">Original surface-issued ticket.</param>
public sealed record ScenePreviewFrame(ScenePreviewDocument Document, ViewportFrame Frame);

/// <summary>Serializes static Edit preview replacement through existing frame/process owners.
/// Borrows FrameSession; its AetherHost owns and disposes this controller.</summary>
public sealed class ScenePreview : IAsyncDisposable
{
    private readonly FrameSession _frames;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _stateGate = new();
    private CancellationTokenSource? _work;
    private int _fence;
    private bool _closed, _ownsSession;
    private (SceneId Id, long Revision)? _target;
    private ScenePreviewDocument? _document;
    private ScenePreviewFrame? _presented;
    private Exception? _error;
    private Task? _dispose;

    /// <summary>Creates a controller over borrowed frames. Do not run game/preview commands concurrently on this owner.</summary>
    /// <param name="frames">App-owned frame/process graph, shared with explicit game workflow.</param>
    public ScenePreview(FrameSession frames) { ArgumentNullException.ThrowIfNull(frames); _frames = frames; }

    /// <summary>Gets the ready preview document; null after stop, exit, replacement or a pending first presentation.</summary>
    public ScenePreviewDocument? Document
    { get { lock (_stateGate) return _document != null && MatchesRuntime(_document.Runtime) &&
        (_work == null || _presented != null || _frames.Presentation?.Surface.Suspended == true) ? _document : null; } }
    /// <summary>Gets the last actually presented frame on the current surface; never retags an old frame with a new revision.</summary>
    public ScenePreviewFrame? LastPresented
    { get { lock (_stateGate) return _presented != null && DescribeCore(_presented.Frame) != null ? _presented : null; } }
    /// <summary>Gets the last admitted preview/cleanup error. Preflight rejection leaves the working preview unchanged.</summary>
    public Exception? Error => Volatile.Read(ref _error);

    /// <summary>Projects before replacing a working preview, then stops/reaps the previous child and starts a fresh built-in generation.
    /// Completes only after Presented acknowledges the first frame. Concurrent Show/Resize is rejected, not queued.</summary>
    /// <param name="scene">Complete immutable resolved authoring snapshot.</param>
    /// <param name="width">Physical width,16..1920.</param><param name="height">Physical height,16..1080.</param>
    /// <param name="token">Cancellation before readiness; cleanup always retains ownership until drained.</param>
    /// <returns>The exact document/runtime stamp after first presentation.</returns>
    public async Task<ScenePreviewDocument> ShowAsync(ResolvedSceneSnapshot scene, int width, int height, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var fence = BeginPreflight(); ValidateSize(width, height, false);
        if (_frames.TransportKind != ViewportTransportKind.Gpu) throw new NotSupportedException("Edit preview requires the explicit primitive GPU viewport.");
        var projection = PrimitiveSceneAdapter.Project(scene, token);
        if (!await _gate.WaitAsync(0, token).ConfigureAwait(false)) throw new InvalidOperationException("A preview update is already in progress.");
        try
        {
            lock (_stateGate)
            {
                CheckFence(fence, token);
                if (!_ownsSession && _frames.Supervisor.Status.State != SessionState.Stopped ||
                    _frames.Supervisor.Status.State != SessionState.Stopped && _frames.Supervisor.ActiveRole != RuntimeRole.EditPreview)
                    throw new InvalidOperationException("Stop the game explicitly before opening Edit preview.");
                if (_document is { } previous && previous.SceneId == scene.Id && scene.Revision < previous.Revision)
                    throw new InvalidOperationException("Stale authoring revision. Stop before opening a different authoring session.");
                _work = CancellationTokenSource.CreateLinkedTokenSource(token);
                _document = null; _presented = null; _target = null; Volatile.Write(ref _error, null);
            }
            try
            {
                if (_ownsSession) { await _frames.StopAsync().ConfigureAwait(false); _ownsSession = false; }
                CancellationToken work;
                lock (_stateGate)
                {
                    CheckFence(fence, token);
                    _ownsSession = true; _target = (scene.Id, scene.Revision); work = _work!.Token;
                }
                await _frames.StartPreviewAsync(projection, width, height, work).ConfigureAwait(false);
                lock (_stateGate)
                {
                    CheckFence(fence, work);
                    var presented = _presented ?? throw new InvalidOperationException("Acknowledge preview frames through ScenePreview.Presented.");
                    _document = presented.Document;
                    return _document;
                }
            }
            catch (Exception error) { await FailAsync(error).ConfigureAwait(false); throw; }
            finally { EndWork(); }
        }
        finally { _gate.Release(); }
    }

    /// <summary>Resizes the active preview, or suspends with0x0. Resume waits for actual presentation on the new surface.</summary>
    /// <param name="width">Physical width or zero when suspended.</param><param name="height">Physical height or zero when suspended.</param>
    /// <param name="token">Cancellation before resize readiness.</param><returns>Completion after new-surface readiness.</returns>
    public async Task ResizeAsync(int width, int height, CancellationToken token = default)
    {
        var fence = BeginPreflight(); ValidateSize(width, height, true);
        if (!await _gate.WaitAsync(0, token).ConfigureAwait(false)) throw new InvalidOperationException("A preview update is already in progress.");
        try
        {
            lock (_stateGate)
            {
                CheckFence(fence, token);
                if (_document == null || !MatchesRuntime(_document.Runtime)) throw new InvalidOperationException("No ready Edit preview.");
                _presented = null; _work = CancellationTokenSource.CreateLinkedTokenSource(token); Volatile.Write(ref _error, null);
            }
            try
            {
                await _frames.ResizeAsync(width, height, _work.Token).ConfigureAwait(false);
                lock (_stateGate) CheckFence(fence, _work.Token);
            }
            catch (Exception error) { await FailAsync(error).ConfigureAwait(false); throw; }
            finally { EndWork(); }
        }
        finally { _gate.Release(); }
    }

    /// <summary>Describes a ticket only while its exact preview generation/surface is active. Does not acknowledge presentation.</summary>
    /// <param name="frame">Surface-issued ticket.</param><returns>Its original document stamp, or null for stale/foreign/stopped frames.</returns>
    public ScenePreviewFrame? Describe(ViewportFrame frame)
    { ArgumentNullException.ThrowIfNull(frame); lock (_stateGate) return DescribeCore(frame); }

    /// <summary>Acknowledges only an actually drawn/presented current ticket; call from the UI FramePresented callback.</summary>
    /// <param name="frame">Exact ticket retained with the displayed image.</param><returns>False for stale/foreign/closed tickets.</returns>
    public bool Presented(ViewportFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        lock (_stateGate)
        {
            var value = DescribeCore(frame);
            if (value == null || !_frames.Presented(frame)) return false;
            _presented = value; return true;
        }
    }

    private ScenePreviewFrame? DescribeCore(ViewportFrame frame)
    {
        var target = _target;
        if (_closed || target == null || !ReferenceEquals(_frames.Presentation, frame.Presentation) ||
            !MatchesRuntime(frame.Presentation.Surface.Identity) || _frames.Supervisor.Status.SceneRevision != target.Value.Revision) return null;
        return new(new(target.Value.Id, target.Value.Revision, frame.Presentation.Surface.Identity), frame);
    }
    private bool MatchesRuntime(SessionIdentity identity) => _frames.Supervisor.ActiveRole == RuntimeRole.EditPreview &&
        _frames.Supervisor.Status.Identity == identity && _frames.Supervisor.Status.State == SessionState.Paused &&
        _frames.Presentation?.Surface.Identity == identity;

    /// <summary>Fences pending work immediately and drains the preview's child/connection. Does not stop unrelated game sessions.</summary>
    /// <returns>Cleanup completion; failed cleanup remains owned and can be retried.</returns>
    public Task StopAsync()
    {
        Exception? cancellation = null;
        lock (_stateGate)
        {
            _fence++; _document = null; _target = null; _presented = null;
            try { _work?.Cancel(); } catch (Exception error) { cancellation = error; }
        }
        return StopCoreAsync(cancellation);
    }
    private async Task StopCoreAsync(Exception? cancellation)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_ownsSession) { await _frames.StopAsync().ConfigureAwait(false); _ownsSession = false; }
            if (cancellation != null) throw cancellation;
        }
        catch (Exception error)
        {
            if (cancellation != null && !ReferenceEquals(error, cancellation))
            { var combined = new AggregateException(cancellation, error); Volatile.Write(ref _error, combined); throw combined; }
            Volatile.Write(ref _error, error); throw;
        }
        finally { _gate.Release(); }
    }
    private async Task FailAsync(Exception original)
    {
        lock (_stateGate) { _document = null; _target = null; _presented = null; }
        Volatile.Write(ref _error, original);
        try { if (_ownsSession) { await _frames.StopAsync().ConfigureAwait(false); _ownsSession = false; } }
        catch (Exception cleanup)
        {
            var combined = new AggregateException("Preview failed; cleanup remains owned until Stop retry.", original, cleanup);
            Volatile.Write(ref _error, combined); throw combined;
        }
    }
    internal void CloseAdmission() { lock (_stateGate) _closed = true; }
    private int BeginPreflight() { lock (_stateGate) { ObjectDisposedException.ThrowIf(_closed, this); return _fence; } }
    private void CheckFence(int fence, CancellationToken token)
    { ObjectDisposedException.ThrowIf(_closed, this); token.ThrowIfCancellationRequested(); if (_fence != fence) throw new OperationCanceledException("Preview update superseded by Stop."); }
    private void EndWork() { lock (_stateGate) { _work?.Dispose(); _work = null; } }
    private static void ValidateSize(int width, int height, bool suspension)
    { if (!(suspension && width == 0 && height == 0) && (width is < 16 or > 1920 || height is < 16 or > 1080)) throw new ArgumentOutOfRangeException(nameof(width), "Invalid preview extent."); }

    /// <summary>Closes admission and drains owned preview work without disposing the borrowed FrameSession. Failed cleanup is retryable.</summary>
    /// <returns>Shared terminal cleanup completion.</returns>
    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completion;
        lock (_stateGate)
        {
            _closed = true;
            if (_dispose is { IsCompletedSuccessfully: true } or { IsCompleted: false }) return new(_dispose);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously); _dispose = completion.Task;
        }
        _ = CompleteAsync(); return new(completion.Task);
        async Task CompleteAsync()
        {
            try { await StopAsync().ConfigureAwait(false); completion.TrySetResult(); }
            catch (Exception error) { completion.TrySetException(error); }
        }
    }
}
