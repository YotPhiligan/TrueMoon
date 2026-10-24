using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Editor.Editing;

/// <summary>App-owned bounded preview scheduler. Borrows the engine; keeps only the newest waiting committed snapshot.</summary>
public sealed class EditorPreview : IDisposable, IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly Func<ResolvedSceneSnapshot, CancellationToken, Task<ScenePreviewDocument>> _show;
    private readonly Func<Task> _stop;
    private ResolvedSceneSnapshot? _requested, _waiting;
    private CancellationTokenSource? _cancellation;
    private Task _pending = Task.CompletedTask;
    private Task? _stopping;
    private bool _active, _busy, _closed;
    private ScenePreviewDocument? _presented;
    private Exception? _error;

    /// <summary>Creates a coordinator borrowing explicit engine operations; show must finish only after actual presentation.</summary>
    /// <param name="show">Projects and presents a committed snapshot, honouring cancellation.</param>
    /// <param name="stop">Fences, drains and reaps the engine preview; never stops an unrelated game.</param>
    public EditorPreview(Func<ResolvedSceneSnapshot, CancellationToken, Task<ScenePreviewDocument>> show, Func<Task> stop)
    { ArgumentNullException.ThrowIfNull(show); ArgumentNullException.ThrowIfNull(stop); _show = show; _stop = stop; }

    /// <summary>Gets completion of accepted preview work.</summary>
    public Task Pending { get { lock (_gate) return _stopping is { IsCompleted: false } ? _stopping : _pending; } }
    /// <summary>Reads state without publishing UI notifications.</summary>
    /// <returns>Last actual presentation and any operation error.</returns>
    public EditorPreviewState Read() { lock (_gate) return new(_active, _busy || _stopping is { IsCompleted: false }, _presented, _error); }

    /// <summary>Enables preview or explicitly retries the current committed snapshot.</summary>
    /// <param name="scene">Complete immutable authoring state.</param><returns>False while stopping or closed.</returns>
    public bool Show(ResolvedSceneSnapshot scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        lock (_gate)
        {
            if (_closed || _stopping is { IsCompleted: false }) return false;
            _active = true; Queue(scene); return true;
        }
    }

    /// <summary>Queues a changed revision only when preview is enabled. A failed revision is not retried every frame.</summary>
    /// <param name="scene">Latest committed state.</param>
    public void Synchronize(ResolvedSceneSnapshot scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        lock (_gate) if (_active && !_closed && _stopping is not { IsCompleted: false } && !ReferenceEquals(scene, _requested)) Queue(scene);
    }

    private void Queue(ResolvedSceneSnapshot scene)
    {
        _requested = _waiting = scene; _error = null;
        if (_busy) return;
        _busy = true; _cancellation = new();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = completion.Task;
        _ = Task.Run(async () =>
        {
            while (true)
            {
                ResolvedSceneSnapshot current; CancellationToken token;
                lock (_gate)
                {
                    if (!_active || _waiting == null)
                    { _busy = false; _cancellation!.Dispose(); _cancellation = null; completion.TrySetResult(); return; }
                    current = _waiting; _waiting = null; token = _cancellation!.Token;
                }
                try
                {
                    var shown = await _show(current, token).ConfigureAwait(false);
                    if (shown.SceneId != current.Id || shown.Revision != current.Revision)
                        throw new InvalidOperationException("Preview acknowledged a different document revision.");
                    lock (_gate) if (_active && !token.IsCancellationRequested) { _presented = shown; _error = null; }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception error) { lock (_gate) _error = error; }
            }
        });
    }

    /// <summary>Disables updates, drops the waiting snapshot and drains accepted work before engine cleanup. Failure is retryable.</summary>
    /// <returns>Shared actual stop completion.</returns>
    public Task StopAsync()
    {
        lock (_gate)
        {
            if (_stopping is { IsCompleted: false }) return _stopping;
            _active = false; _waiting = _requested = null; _presented = null;
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
