using System.Threading.Channels;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editor.Projects;

namespace TrueMoon.Aether.Editor.Editing;

/// <summary>Inert, project-owned observer activated by a successful Build. Publishes value snapshots, never UI callbacks.</summary>
/// <remarks>The project owns actual worker/notification drain. Observations do not replace compiler preflight.</remarks>
public sealed class EditorSourceWatch : IDisposable, IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly EditorSourceWatchOptions _options;
    private readonly TimeProvider _time;
    private readonly string _project;
    private readonly Func<GameBuildSourceWatchDescriptor, CancellationToken, Task<EditorSourceScan>> _scan;
    private readonly Func<GameBuildSourceWatchDescriptor, Action<bool>, IEditorSourceHints> _attach;
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });
    private readonly CancellationTokenSource _stop = new();
    private Task? _worker, _cleanup;
    private CancellationTokenSource? _pass;
    private IEditorSourceHints? _hints;
    private long _hintsBaseline, _firstHint, _lastHint, _lastCompleted;
    private bool _closed, _pending, _urgent, _recoverable = true, _hintFailure;
    private GameBuildSourceWatchDescriptor? _descriptor;
    private volatile EditorSourceSnapshot _snapshot;

    /// <summary>Creates an inert product. Its project is the sole owner; options and clock are borrowed/copied.</summary>
    /// <param name="project">Resolved original project.</param><param name="options">Copied bounded tracking policy.</param>
    /// <param name="timeProvider">Scheduling and observation clock.</param>
    public EditorSourceWatch(ResolvedProject project, EditorSourceWatchOptions options, TimeProvider timeProvider)
        : this(project, options, timeProvider, new EditorSourceCensus(timeProvider).ScanAsync,
            HintFactory(options)) { }

    private static Func<GameBuildSourceWatchDescriptor, Action<bool>, IEditorSourceHints> HintFactory(EditorSourceWatchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options); var copied = options.Snapshot();
        return (descriptor, signal) => new EditorSourceHints(descriptor, copied, signal);
    }

    internal EditorSourceWatch(ResolvedProject project, EditorSourceWatchOptions options, TimeProvider timeProvider,
        Func<GameBuildSourceWatchDescriptor, CancellationToken, Task<EditorSourceScan>> scan,
        Func<GameBuildSourceWatchDescriptor, Action<bool>, IEditorSourceHints> hints)
    {
        ArgumentNullException.ThrowIfNull(project); ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(scan); ArgumentNullException.ThrowIfNull(hints);
        _project = project.GameProjectPath; _options = options.Snapshot(); _time = timeProvider; _scan = scan; _attach = hints;
        _snapshot = new(Guid.NewGuid(), 0, null, null, null, 0, 0, EditorSourceState.NoBuild, null, false,
            EditorSourceObservationMode.Polling, 0, null, Array.Empty<string>());
    }

    /// <summary>Fresh owner identity for this project-session product, including reopen of the same manifest.</summary>
    public Guid OwnerKey => Read().OwnerKey;
    /// <summary>Reads one complete immutable publication without filesystem IO.</summary>
    /// <returns>Last observed source state.</returns>
    public EditorSourceSnapshot Read() => _snapshot;

    /// <summary>Binds a successful result after the caller's current-project/context fence.</summary>
    /// <param name="artifact">Actual selected Build result, including a newly verified reuse result.</param>
    public void Bind(GameBuildArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        Bind(artifact.BuildId, artifact.Inputs?.Fingerprint, artifact.Inputs?.SourceWatch,
            artifact.Inputs?.Preparation?.SourceWatchUnavailableReason);
    }

    internal void Bind(string buildId, string? inputFingerprint, GameBuildSourceWatchDescriptor? descriptor, string? unavailableReason = null)
    {
        CancellationTokenSource? previous;
        lock (_gate)
        {
            if (_closed) return;
            if (descriptor != null && !StringComparer.OrdinalIgnoreCase.Equals(descriptor.ProjectPath, _project))
            { descriptor = null; unavailableReason = "Build descriptor belongs to another project."; }
            _descriptor = descriptor; _recoverable = true; _hintFailure = false;
            _pending = true; _urgent = true; _firstHint = _lastHint = _time.GetTimestamp();
            var available = _options.Enabled && descriptor != null;
            _snapshot = _snapshot with { BaselineVersion = _snapshot.BaselineVersion + 1, BuildId = buildId,
                InputFingerprint = inputFingerprint, SourceFingerprint = descriptor?.SourceFingerprint,
                SignalVersion = _snapshot.SignalVersion + 1, State = available ? EditorSourceState.Checking : EditorSourceState.Unavailable,
                ObservedAt = null, IsScanning = false, Reason = available ? null : Bounded(!_options.Enabled ? "Source tracking is disabled." : unavailableReason ?? "Build has no process-local source descriptor."),
                ChangedPaths = Array.Empty<string>() };
            previous = _pass;
            if (available && _worker == null) _worker = Task.Run(RunAsync);
            _wake.Writer.TryWrite(true);
        }
        try { previous?.Cancel(); } catch (ObjectDisposedException) { /* The previous pass already drained. */ }
        catch (Exception error)
        {
            lock (_gate) if (!_closed) _snapshot = _snapshot with { State = EditorSourceState.Unavailable,
                Reason = "Source cancellation failed: " + error.GetType().Name };
        }
    }

    private void Signal(bool failed)
    {
        lock (_gate)
        {
            if (_closed || !_options.Enabled || _descriptor == null || !_recoverable) return;
            _hintFailure |= failed;
            var now = _time.GetTimestamp();
            if (!_pending) _firstHint = now;
            _lastHint = now; _pending = true;
            var changed = _snapshot.State == EditorSourceState.Changed;
            _snapshot = _snapshot with { SignalVersion = _snapshot.SignalVersion + 1,
                State = changed ? EditorSourceState.Changed : EditorSourceState.Checking,
                Mode = _hintFailure ? EditorSourceObservationMode.Polling : _snapshot.Mode,
                Reason = failed ? "Notification coverage failed; complete polling remains active." : _snapshot.Reason };
            _wake.Writer.TryWrite(true);
        }
    }

    private async Task RunAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                GameBuildSourceWatchDescriptor? descriptor; long baseline, signal; TimeSpan delay;
                lock (_gate)
                {
                    descriptor = _descriptor; baseline = _snapshot.BaselineVersion; signal = _snapshot.SignalVersion;
                    delay = !_options.Enabled || descriptor == null || !_recoverable || _snapshot.State == EditorSourceState.Changed
                        ? Timeout.InfiniteTimeSpan : _pending ? (_urgent ? TimeSpan.Zero : Minimum(
                            _options.Debounce - _time.GetElapsedTime(_lastHint), _options.MaximumDebounce - _time.GetElapsedTime(_firstHint)))
                            : _options.PollInterval - _time.GetElapsedTime(_lastCompleted);
                }
                if (descriptor == null || !_options.Enabled || !_recoverable)
                {
                    _hints?.Dispose(); _hints = null;
                    lock (_gate) if (!_closed) _snapshot = _snapshot with { WatcherCount = 0, Mode = EditorSourceObservationMode.Polling };
                }
                if (delay == Timeout.InfiniteTimeSpan || delay > TimeSpan.Zero)
                { await WaitAsync(delay).ConfigureAwait(false); continue; }
                if (descriptor == null) continue;
                if (_hintsBaseline != baseline)
                {
                    // Both replacement and failed attachment retain one resource owner; cleanup failure is retryable at close.
                    _hints?.Dispose(); _hints = null;
                    IEditorSourceHints? hints = null; string? warning = null;
                    try { if (_options.UseFileSystemNotifications) hints = _attach(descriptor, failed => SignalFor(baseline, failed)); }
                    catch (Exception error) { warning = "Notifications unavailable: " + error.GetType().Name + "; complete polling remains active."; }
                    lock (_gate)
                    {
                        _hints = hints; _hintsBaseline = baseline;
                        if (!_closed && _snapshot.BaselineVersion == baseline)
                            _snapshot = _snapshot with { WatcherCount = hints?.Count ?? 0,
                                Mode = hints is { Count: > 0, Warning: null } && !_hintFailure ? EditorSourceObservationMode.NotificationsAndPolling : EditorSourceObservationMode.Polling,
                                Reason = Bounded(warning ?? hints?.Warning ?? _snapshot.Reason) };
                    }
                }
                using var budget = new CancellationTokenSource(_options.ScanBudget, _time);
                using var pass = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token, budget.Token);
                lock (_gate)
                {
                    if (_closed || _snapshot.BaselineVersion != baseline) continue;
                    _pass = pass; signal = _snapshot.SignalVersion; _pending = _urgent = false;
                    _snapshot = _snapshot with { IsScanning = true };
                }
                EditorSourceScan result;
                try { result = await _scan(descriptor, pass.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (pass.IsCancellationRequested)
                { result = EditorSourceScan.Unavailable(budget.IsCancellationRequested ? "Source observation exceeded its cooperative time budget." : "Source observation was superseded."); }
                catch (Exception error) { result = EditorSourceScan.Unavailable("Source observation failed: " + error.GetType().Name); }
                finally { lock (_gate) if (ReferenceEquals(_pass, pass)) _pass = null; }
                lock (_gate)
                {
                    if (_closed || _snapshot.BaselineVersion != baseline) continue;
                    _lastCompleted = _time.GetTimestamp(); _recoverable = result.Recoverable;
                    var changed = _snapshot.State == EditorSourceState.Changed || result.Changed;
                    var clean = result.Available && _snapshot.SignalVersion == signal && !budget.IsCancellationRequested;
                    var state = changed ? EditorSourceState.Changed : clean ? EditorSourceState.ObservedUnchanged :
                        _snapshot.SignalVersion != signal ? EditorSourceState.Checking : EditorSourceState.Unavailable;
                    _snapshot = _snapshot with { ObservationVersion = _snapshot.ObservationVersion + 1, State = state, IsScanning = false,
                        ObservedAt = changed || clean ? _time.GetUtcNow() : _snapshot.ObservedAt,
                        Reason = Bounded(budget.IsCancellationRequested ? "Source observation exceeded its cooperative time budget." : !result.Available ? result.Reason : _hintFailure ? "Notification coverage failed; complete polling remains active." : _hints?.Warning),
                        ChangedPaths = result.Changed ? Array.AsReadOnly(result.ChangedPaths.Take(16).Select(BoundedPath).ToArray()) : _snapshot.ChangedPaths };
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception error)
        {
            lock (_gate)
            {
                _recoverable = false;
                if (!_closed) _snapshot = _snapshot with { State = _snapshot.State == EditorSourceState.Changed ? EditorSourceState.Changed : EditorSourceState.Unavailable,
                    IsScanning = false, Reason = Bounded("Source worker stopped: " + error.GetType().Name) };
            }
        }
        finally
        {
            lock (_gate)
            {
                _worker = null;
                if (!_closed && _pending && _recoverable && _descriptor != null) _worker = Task.Run(RunAsync);
            }
        }
    }

    private void SignalFor(long baseline, bool failed)
    { lock (_gate) { if (_closed || _snapshot.BaselineVersion != baseline) return; Signal(failed); } }

    private async Task WaitAsync(TimeSpan delay)
    {
        using var pending = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        var wake = _wake.Reader.ReadAsync(pending.Token).AsTask();
        try
        {
            if (delay == Timeout.InfiniteTimeSpan) await wake.ConfigureAwait(false);
            else await wake.WaitAsync(delay, _time, _stop.Token).ConfigureAwait(false);
        }
        catch (TimeoutException) { }
        finally
        {
            pending.Cancel();
            try { await wake.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }
    }

    /// <summary>Fences publication and waits for actual worker/native-resource drain.</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    /// <summary>Shares in-progress drain; failed resource cleanup remains owned and can be retried.</summary>
    /// <returns>Actual completion, never just a cancellation request.</returns>
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (!_closed)
            {
                _closed = true;
                _snapshot = _snapshot with { State = EditorSourceState.Stopped, SignalVersion = _snapshot.SignalVersion + 1, IsScanning = false };
            }
            if (_cleanup == null || _cleanup.IsFaulted) _cleanup = Task.Run(CleanupAsync);
            return new(_cleanup);
        }
    }

    private async Task CleanupAsync()
    {
        List<Exception>? errors = null;
        try { _stop.Cancel(); } catch (Exception error) { (errors ??= []).Add(error); }
        try { _hints?.Stop(); } catch (Exception error) { (errors ??= []).Add(error); }
        // The closed epoch already fences every callback. The worker retains sole handle ownership until its IO has drained.
        Task? worker; lock (_gate) worker = _worker;
        if (worker != null) try { await worker.ConfigureAwait(false); } catch (Exception error) { (errors ??= []).Add(error); }
        try { _hints?.Dispose(); _hints = null; } catch (Exception error) { (errors ??= []).Add(error); }
        lock (_gate) _snapshot = _snapshot with { WatcherCount = _hints?.Count ?? 0 };
        if (errors != null) throw new AggregateException(errors);
        _stop.Dispose();
    }

    private static TimeSpan Minimum(TimeSpan first, TimeSpan second) => first < second ? first : second;
    private static string? Bounded(string? value) => value is { Length: > 1024 } ? value[..1024] : value;
    private static string BoundedPath(string value) => value.Length > 512 ? value[..512] : value;
}
