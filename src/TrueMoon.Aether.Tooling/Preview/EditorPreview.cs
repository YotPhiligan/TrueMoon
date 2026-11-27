using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Runtime.Preview;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Tooling.Preview;

/// <summary>App-owned bounded preview scheduler. Borrows the engine; keeps only the newest waiting committed snapshot.</summary>
public sealed class EditorPreview : IDisposable, IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly Func<ResolvedSceneSnapshot, CancellationToken, Task<ScenePreviewDocument>> _show;
    private readonly Func<ResolvedSceneSnapshot, PreviewViewState>? _capture;
    private readonly Func<ResolvedSceneSnapshot, PreviewViewState, CancellationToken, Task<ScenePreviewDocument>>? _showView;
    private readonly Func<Task> _stop;
    private readonly Func<CapturedScenePreview, CancellationToken, Task<PreparedScenePreview>>? _prepare;
    private readonly Func<PreparedScenePreview, PreviewViewState, CancellationToken, Task<ScenePreviewDocument>>? _showCaptured;
    private readonly Func<PreviewViewContext?, bool>? _setDesired;
    private readonly Func<ScenePreviewDocument, PreviewViewContext, bool>? _canReuse;
    private Request? _requested, _waiting;
    private (RenderContentHash? Digest, PreviewViewContext Context)? _presentedIntent;
    private CancellationTokenSource? _cancellation;
    private Task _pending = Task.CompletedTask;
    private Task? _stopping;
    private bool _active, _busy, _closed;
    private ScenePreviewDocument? _presented;
    private Exception? _error;

    private sealed record Request(ResolvedSceneSnapshot Scene, PreviewViewState? View, CapturedScenePreview? Source);

    /// <summary>Creates a coordinator borrowing explicit engine operations; show must finish only after actual presentation.</summary>
    /// <param name="show">Projects and presents a committed snapshot, honouring cancellation.</param>
    /// <param name="stop">Fences, drains and reaps the engine preview; never stops an unrelated game.</param>
    public EditorPreview(Func<ResolvedSceneSnapshot, CancellationToken, Task<ScenePreviewDocument>> show, Func<Task> stop)
    { ArgumentNullException.ThrowIfNull(show); ArgumentNullException.ThrowIfNull(stop); _show = show; _stop = stop; }
    /// <summary>Creates explicit-view coordination. Capture runs synchronously on the requesting UI owner; the worker receives only frozen values.</summary>
    /// <param name="capture">Captures the current committed source and its exact binding/context.</param>
    /// <param name="show">Presents the frozen source/view and completes after actual presentation.</param>
    /// <param name="stop">Drains and releases preview resources.</param>
    public EditorPreview(Func<ResolvedSceneSnapshot,PreviewViewState> capture,
        Func<ResolvedSceneSnapshot,PreviewViewState,CancellationToken,Task<ScenePreviewDocument>> show, Func<Task> stop)
        : this((_,_)=>throw new InvalidOperationException("Explicit capture required."),stop)
    { ArgumentNullException.ThrowIfNull(capture); ArgumentNullException.ThrowIfNull(show); _capture=capture; _showView=show; }

    internal EditorPreview(Func<ResolvedSceneSnapshot, PreviewViewState> capture,
        Func<PreviewViewContext?, bool> setDesired, Func<ScenePreviewDocument, PreviewViewContext, bool> canReuse,
        Func<PreparedScenePreview, PreviewViewState, CancellationToken, Task<ScenePreviewDocument>> show, Func<Task> stop,
        Func<CapturedScenePreview, CancellationToken, Task<PreparedScenePreview>>? prepare = null)
        : this((_, _) => throw new InvalidOperationException("Committed asset capture required."), stop)
    {
        _capture = capture; _setDesired = setDesired; _canReuse = canReuse; _showCaptured = show;
        _prepare = prepare ?? (static (source, token) => source.PrepareAsync(token));
    }

    internal bool UsesCapturedSource => _prepare != null;

    internal bool Show(CapturedScenePreview source) => Show(source.Scene, source);

    internal void Synchronize(CapturedScenePreview source) => Synchronize(source.Scene, source);

    /// <summary>Gets completion of accepted preview work.</summary>
    public Task Pending { get { lock (_gate) return _stopping is { IsCompleted: false } ? _stopping : _pending; } }
    /// <summary>Reads state without publishing UI notifications.</summary>
    /// <returns>Last actual presentation and any operation error.</returns>
    public EditorPreviewState Read() { lock (_gate) return new(_active, _busy || _stopping is { IsCompleted: false }, _presented, _error); }

    /// <summary>Enables preview or explicitly retries the current committed snapshot.</summary>
    /// <param name="scene">Complete immutable authoring state.</param><returns>False while stopping or closed.</returns>
    public bool Show(ResolvedSceneSnapshot scene) => Show(scene, null);

    private bool Show(ResolvedSceneSnapshot scene, CapturedScenePreview? source)
    {
        ArgumentNullException.ThrowIfNull(scene);
        lock (_gate)
        {
            if (_closed || _stopping is { IsCompleted: false }) return false;
            _active = true; Queue(scene, source, true); return true;
        }
    }

    /// <summary>Queues a changed revision only when preview is enabled. A failed revision is not retried every frame.</summary>
    /// <param name="scene">Latest committed state.</param>
    public void Synchronize(ResolvedSceneSnapshot scene) => Synchronize(scene, null);

    private void Synchronize(ResolvedSceneSnapshot scene, CapturedScenePreview? source)
    {
        ArgumentNullException.ThrowIfNull(scene);
        lock (_gate) if (_active && !_closed && _stopping is not { IsCompleted: false }) Queue(scene, source, false);
    }

    private void Queue(ResolvedSceneSnapshot scene, CapturedScenePreview? source, bool force)
    {
        PreviewViewState? captured;
        try
        {
            if (UsesCapturedSource && source == null) throw new InvalidOperationException("Committed asset capture required.");
            captured = _capture?.Invoke(scene);
        }
        catch (Exception error) { _error = error; return; }
        if (!force && _requested is { } previous && ReferenceEquals(scene, previous.Scene) &&
            source == previous.Source && captured?.Context == previous.View?.Context) return;
        _requested = _waiting = new(scene, captured, source);
        _error = null;
        if (source != null)
        {
            _setDesired!(null);
            _cancellation?.Cancel();
        }
        if (_busy) return;
        _busy = true;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = completion.Task;
        _ = Task.Run(async () =>
        {
            while (true)
            {
                Request current; CancellationToken token;
                lock (_gate)
                {
                    if (!_active || _waiting == null)
                    { _busy = false; completion.TrySetResult(); return; }
                    current = _waiting; _waiting = null;
                    _cancellation = new(); token = _cancellation.Token;
                }
                try
                {
                    if (current.Source is { } source)
                    {
                        await PresentCapturedAsync(current, source, token).ConfigureAwait(false);
                    }
                    else
                    {
                        var shown = current.View == null
                            ? await _show(current.Scene, token).ConfigureAwait(false)
                            : await _showView!(current.Scene, current.View, token).ConfigureAwait(false);
                        CheckDocument(shown, current.Scene);
                        lock (_gate) if (_active && !token.IsCancellationRequested) { _presented = shown; _error = null; }
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception error)
                {
                    lock (_gate) if (IsCurrent(current, token)) { _setDesired?.Invoke(null); _error = error; }
                }
                finally
                {
                    lock (_gate) { _cancellation!.Dispose(); _cancellation = null; }
                }
            }
        });
    }

    private bool IsCurrent(Request request, CancellationToken token) =>
        _active && !token.IsCancellationRequested && ReferenceEquals(_requested, request);

    private static void CheckDocument(ScenePreviewDocument shown, ResolvedSceneSnapshot scene)
    {
        if (shown.SceneId != scene.Id || shown.Revision != scene.Revision)
            throw new InvalidOperationException("Preview acknowledged a different document revision.");
    }

    private async Task PresentCapturedAsync(Request request, CapturedScenePreview source, CancellationToken token)
    {
        var prepared = await _prepare!(source, token).ConfigureAwait(false);
        if (prepared.Source != source) throw new InvalidOperationException("Preview prepared a different committed source.");
        var view = request.View!;
        lock (_gate)
        {
            if (!IsCurrent(request, token)) return;
            if (_presentedIntent is { } intent && _presented is { } presented && intent.Digest == prepared.Digest &&
                intent.Context.DocumentEpoch == view.Context.DocumentEpoch && intent.Context.ViewId == view.Context.ViewId &&
                intent.Context.SceneId == view.Context.SceneId && intent.Context.SceneRevision == view.Context.SceneRevision &&
                _canReuse!(presented, intent.Context) && _setDesired!(intent.Context)) return;

            view = new(view.Context with { ContentId = Guid.NewGuid() }, view.Camera);
            if (!_setDesired!(view.Context)) return;
        }

        var shown = await _showCaptured!(prepared, view, token).ConfigureAwait(false);
        CheckDocument(shown, request.Scene);
        if (shown.ViewContext != view.Context)
            throw new InvalidOperationException("Preview acknowledged a different source context.");
        lock (_gate)
        {
            if (!IsCurrent(request, token)) return;
            _presented = shown;
            _presentedIntent = (prepared.Digest, view.Context);
            _error = null;
        }
    }

    /// <summary>Disables updates, drops the waiting snapshot and drains accepted work before engine cleanup. Failure is retryable.</summary>
    /// <returns>Shared actual stop completion.</returns>
    public Task StopAsync()
    {
        lock (_gate)
        {
            if (_stopping is { IsCompleted: false }) return _stopping;
            _active = false; _waiting = _requested = null; _presented = null;
            _presentedIntent = null;
            _setDesired?.Invoke(null);
            Exception? cancellation = null;
            try { _cancellation?.Cancel(); } catch (Exception error) { cancellation = error; }
            var work = _pending;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _stopping = completion.Task;
            _ = Task.Run(async () =>
            {
                try
                {
                    await work.ConfigureAwait(false);
                    await _stop().ConfigureAwait(false);
                    if (cancellation != null) throw cancellation;
                    lock (_gate) _error = null;
                    completion.TrySetResult();
                }
                catch (Exception error) { lock (_gate) _error = error; completion.TrySetException(error); }
            });
            return _stopping;
        }
    }
    /// <summary>Closes admission permanently and drains the borrowed engine preview.</summary>
    public ValueTask DisposeAsync() { lock (_gate) _closed = true; return new(StopAsync()); }
    /// <inheritdoc />
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}

/// <summary>Immutable preview activity; Presented retains the original revision rather than the latest requested revision.</summary>
/// <param name="Active">Automatic updates after committed edits are enabled.</param><param name="Busy">Show or stop is draining.</param>
/// <param name="Presented">Last actual presented revision, possibly older than the document.</param><param name="Error">Recoverable preview/cleanup fault.</param>
public sealed record EditorPreviewState(bool Active, bool Busy, ScenePreviewDocument? Presented, Exception? Error);
