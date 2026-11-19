using System.Threading.Channels;

namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Caller-owned read-only observer of one model root, dependency closure and import settings.</summary>
/// <remarks>Dispose before releasing a selection. App Stop also fences admission and drains live watches.
/// A new published import or requested setting gets a new watch. Observations do not authorize builds.</remarks>
public sealed class ModelSourceWatch : IAsyncDisposable
{
    private static readonly TimeSpan CoalesceDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan CaptureBudget = TimeSpan.FromSeconds(5);

    private readonly IFactory<ModelSourceCapture, ModelSourceCaptureArguments> _captures;
    private readonly ModelSourceWatchArguments _arguments;
    private readonly TimeProvider _time;
    private readonly ModelSourceWatchHost _owner;
    private readonly string _directory;
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Channel<byte> _wake = Channel.CreateBounded<byte>(new BoundedChannelOptions(1)
        { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });
    private readonly Task _worker;
    private FileSystemWatcher? _watcher;
    private volatile HashSet<string> _paths;
    private volatile ModelSourceObservation? _observation;
    private volatile bool _notificationsFailed;
    private bool _closed, _released;
    private Task? _cleanup;

    internal ModelSourceWatch(IFactory<ModelSourceCapture, ModelSourceCaptureArguments> captures,
        ModelSourceWatchArguments arguments, TimeProvider time, ModelSourceWatchHost owner)
    {
        _captures = captures;
        _arguments = arguments;
        _time = time;
        _owner = owner;
        _directory = Path.TrimEndingDirectorySeparator(arguments.Source.Directory);
        _paths = WatchPaths([arguments.Source.RootName]);
        _worker = Task.Run(RunAsync);
    }

    /// <summary>Reads the last completed observation without I/O, or null while the initial check is pending.</summary>
    public ModelSourceObservation? Read() => _observation;

    /// <summary>Whether the caller or App has closed admission and fenced publication.</summary>
    public bool IsClosed { get { lock (_gate) return _closed; } }

    private async Task RunAsync()
    {
        var token = _stop.Token;
        var notified = false;
        var attemptedNotifications = false;
        var sequence = 0L;
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_notificationsFailed && _watcher != null)
                {
                    _watcher.Dispose();
                    _watcher = null;
                }
                var result = await CaptureAsync(token).ConfigureAwait(false);
                if (!attemptedNotifications && result.Status != ModelImportFreshnessStatus.Unavailable)
                {
                    attemptedNotifications = true;
                    AttachNotifications();
                    // Recheck the closure after attaching to cover edits between the first capture and subscription.
                    if (_watcher != null && !_notificationsFailed) result = await CaptureAsync(token).ConfigureAwait(false);
                }
                lock (_gate)
                {
                    if (_closed) return;
                    _observation = new(++sequence, result, notified, _watcher != null && !_notificationsFailed);
                }
                notified = await WaitForCheckAsync(token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private async Task<ModelImportFreshness> CaptureAsync(CancellationToken token)
    {
        using var budget = new CancellationTokenSource(CaptureBudget, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, budget.Token);
        await using var capture = _captures.Create(_arguments.Source)
            ?? throw new InvalidOperationException("The model capture factory returned null.");
        try
        {
            var result = await capture.CheckFreshnessAsync(_arguments.Published, _arguments.GeometryScale, linked.Token)
                .ConfigureAwait(false);
            if (capture.CompletedCapture is { } captured)
                _paths = WatchPaths(captured.Files.Select(file => file.Name));
            return result;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested && budget.IsCancellationRequested)
        {
            return new(ModelImportFreshnessStatus.Unavailable, null, null,
                _arguments.GeometryScale != _arguments.Published.Candidate.GeometryScale,
                new TimeoutException("Model source observation exceeded its capture budget."));
        }
    }

    private async Task<bool> WaitForCheckAsync(CancellationToken token)
    {
        using var poll = new CancellationTokenSource(_arguments.PollInterval, _time);
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(token, poll.Token);
        try { await _wake.Reader.ReadAsync(wait.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested && poll.IsCancellationRequested) { return false; }

        await Task.Delay(CoalesceDelay, _time, token).ConfigureAwait(false);
        _wake.Reader.TryRead(out _);
        return true;
    }

    private HashSet<string> WatchPaths(IEnumerable<string> names)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            for (string? path = Path.GetFullPath(Path.Combine(_directory, name));
                path != null && !StringComparer.OrdinalIgnoreCase.Equals(path, _directory) && paths.Add(path);
                path = Path.GetDirectoryName(path)) { }
        }
        return paths;
    }

    private void AttachNotifications()
    {
        if (!_arguments.UseFileSystemNotifications || Path.GetDirectoryName(_directory) == null) return;
        try
        {
            _watcher = new(_directory)
            {
                IncludeSubdirectories = true,
                InternalBufferSize = 8192,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _watcher.Changed += (_, e) => Hint(e.FullPath);
            _watcher.Created += (_, e) => Hint(e.FullPath);
            _watcher.Deleted += (_, e) => Hint(e.FullPath);
            _watcher.Renamed += (_, e) =>
            {
                if (_paths.Contains(e.FullPath) || _paths.Contains(e.OldFullPath)) _wake.Writer.TryWrite(0);
            };
            _watcher.Error += (_, _) => { _notificationsFailed = true; _wake.Writer.TryWrite(0); };
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or
            System.Security.SecurityException) { _notificationsFailed = true; }
    }

    private void Hint(string path)
    {
        if (_paths.Contains(path)) _wake.Writer.TryWrite(0);
    }

    /// <summary>Closes publication and waits for capture, timer and native watcher cleanup.</summary>
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _closed = true;
            if (_cleanup == null || _cleanup.IsFaulted && !_released) _cleanup = Task.Run(DrainAsync);
            return new(_cleanup);
        }
    }

    private async Task DrainAsync()
    {
        _stop.Cancel();
        try { await _worker.ConfigureAwait(false); }
        finally
        {
            _watcher?.Dispose();
            _watcher = null;
            _stop.Dispose();
            _owner.Release(this);
            lock (_gate) _released = true;
        }
    }
}
