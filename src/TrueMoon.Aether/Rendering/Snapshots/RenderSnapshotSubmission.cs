using TrueMoon.Aether.Rendering.Resources;

namespace TrueMoon.Aether.Rendering.Snapshots;

/// <summary>Backend-owned command recording/submission. The factory returns this owner before native work.</summary>
/// <remarks>Completion requires actual backend proof. Cleanup must confirm safe release after failed/unknown submit, retaining ownership on failure.</remarks>
public interface IRenderSnapshotSubmission : IAsyncDisposable
{
    /// <summary>Records/submits and waits for actual applicable completion, including after cooperative cancellation.</summary>
    /// <param name="cancellationToken">Cooperative request; never permission to free in-use resources.</param><returns>Confirmed completion or original failure.</returns>
    Task ExecuteAsync(CancellationToken cancellationToken);
    /// <summary>Actual submitted work is confirmed complete; a timeout or caller boolean is insufficient.</summary>
    bool IsComplete { get; }
    /// <summary>All backend command/fence/use ownership is safely released.</summary>
    bool IsReleased { get; }
}

/// <summary>Caller-observable accepted use, retained by its view until actual backend cleanup succeeds.</summary>
public sealed class RenderSnapshotSubmission : IAsyncDisposable
{
    private readonly RenderSnapshotView _view;
    private readonly IFactory<IRenderSnapshotSubmission, RenderSnapshotData> _factory;
    private readonly CancellationTokenSource _end;
    private readonly SemaphoreSlim _cleanup = new(1, 1);
    private IAsyncDisposable? _owner;
    private IRenderSnapshotSubmission? _backend;
    private Task _execution = Task.CompletedTask;
    internal RenderSnapshotView.Envelope Envelope { get; }
    internal RenderSnapshotSubmission(RenderSnapshotView view, RenderSnapshotView.Envelope envelope,
        IFactory<IRenderSnapshotSubmission, RenderSnapshotData> factory, CancellationToken token, CancellationToken stop)
    { _view = view; Envelope = envelope; _factory = factory; _end = CancellationTokenSource.CreateLinkedTokenSource(token, stop); }
    /// <summary>Actual execution outcome; success also requires the backend's completion evidence.</summary>
    public Task Completion => _execution;
    /// <summary>Original execution failure, separate from cleanup retry failures.</summary>
    public Exception? Error { get; private set; }
    /// <summary>Whether backend and retained envelope reference were both released.</summary>
    public bool IsReleased { get; internal set; }
    internal bool BackendReleased { get; private set; }
    internal void Start() => _execution = Task.Run(RunAsync);
    private async Task RunAsync()
    {
        try
        {
            _end.Token.ThrowIfCancellationRequested();
            try
            {
                var backend = _factory.Create(Envelope.Data!) ?? throw new RenderResourceException(RenderResourceError.BackendFailure, "Submission factory returned null.");
                _view.ClaimSubmissionOwner(backend); _owner = _backend = backend;
                ResourceGuard.Require(!backend.IsReleased, RenderResourceError.Closed, "Submission factory returned a released owner.");
            }
            catch (RenderResourceCreationException error) { _view.ClaimSubmissionOwner(error.CleanupOwner); _owner = error.CleanupOwner; throw; }
            await _backend.ExecuteAsync(_end.Token).ConfigureAwait(false);
            ResourceGuard.Require(_backend.IsComplete, RenderResourceError.UnknownOutcome, "Backend returned without actual completion evidence.");
        }
        catch (Exception error)
        {
            Error = error;
            if (error is RenderResourceException { Error: RenderResourceError.UnknownOutcome } || _owner != null && _backend?.IsComplete != true) _view.Fence();
            throw;
        }
    }
    internal async Task ReleaseBackendAsync()
    {
        await _cleanup.WaitAsync().ConfigureAwait(false);
        try
        {
            if (BackendReleased) return;
            try { _end.Cancel(); } catch (Exception error) { Error ??= error; }
            try { await _execution.ConfigureAwait(false); } catch { /* Ownership still requires safe backend cleanup. */ }
            if (_owner != null)
            {
                await _owner.DisposeAsync().ConfigureAwait(false);
                ResourceGuard.Require(_backend == null || _backend.IsReleased, RenderResourceError.CleanupFailed, "Submission backend retained native use.");
                _view.ReleaseSubmissionOwner(_owner);
                _owner = null;
            }
            _end.Dispose(); BackendReleased = true;
        }
        finally { _cleanup.Release(); }
    }
    /// <summary>Cancels/drains this accepted use and releases its reference only after backend cleanup; failures remain view-owned for retry.</summary>
    public ValueTask DisposeAsync() => new(_view.ReleaseSubmissionAsync(this));
}

/// <summary>One caller-owned pending-data reservation. Native work starts only inside an accepted submission owner.</summary>
public sealed class RenderSnapshotRecording : IAsyncDisposable
{
    private readonly RenderSnapshotView _view;
    internal RenderSnapshotView.Envelope Envelope { get; }
    internal RenderSnapshotRecording(RenderSnapshotView view, RenderSnapshotView.Envelope envelope) { _view = view; Envelope = envelope; }
    /// <summary>Detached data available while this reservation is live.</summary>
    public RenderSnapshotData Snapshot { get { ObjectDisposedException.ThrowIf(IsReleased, this); return Envelope.Data!; } }
    /// <summary>Reservation was dropped, stopped or transferred to an accepted use.</summary>
    public bool IsReleased { get; internal set; }
    /// <summary>Drops this unsubmitted reservation without starting backend work.</summary>
    public ValueTask DisposeAsync() => new(_view.ReleaseRecordingAsync(this));
}
