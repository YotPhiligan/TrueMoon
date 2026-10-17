using Engine.Probe.Contracts;
using System.Security.Cryptography;
using System.Diagnostics;

namespace EngineSessionProbe;

public enum SessionState { Stopped, Starting, Paused, Playing, Stopping, Faulted, CleanupFailed }
public sealed record SessionFailure(string Phase, Exception Cause, Exception? CleanupError = null);
public sealed record SessionStatus(SessionState State, SessionIdentity? Identity = null, int? ProcessId = null,
    bool HelloAccepted = false, bool SceneLoaded = false, ulong Tick = 0, SessionFailure? Failure = null, long? SceneRevision = null);
public sealed record SessionLaunch(SessionIdentity Identity, string Endpoint, string ReportDirectory, string Behavior = "normal");
public sealed record SessionDeadlines
{
    public TimeSpan Startup { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan Command { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan HeartbeatResponse { get; init; } = TimeSpan.FromSeconds(5);
    internal void Validate()
    {
        foreach (var value in new[] { Startup, Command, HeartbeatInterval, HeartbeatResponse })
            if (value <= TimeSpan.Zero || value > TimeSpan.FromMinutes(2)) throw new ArgumentOutOfRangeException(nameof(value));
    }
}

public interface IRuntimeLease : IAsyncDisposable
{
    int ProcessId { get; }
    Task Exited { get; }
    bool ResourcesReleased { get; }
    Task LaunchAsync(CancellationToken token);
    Task<ControlReply> SendAsync(ControlRequest request, CancellationToken token);
    Task StopAsync(CancellationToken gracefulDeadline = default);
}
public interface IRuntimeFactory { IRuntimeLease Create(SessionLaunch launch); }

// All remote work is serialized. Stop cancels the current local wait before
// entering the gate; a cancelled/expired command is never replayed.
public sealed class SessionSupervisor : IAsyncDisposable
{
    private readonly IRuntimeFactory _factory;
    private readonly SessionDeadlines _deadlines;
    private readonly string _hostId, _gameId, _directory;
    private readonly Action<SessionStatus>? _trace;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Guid _sessionId = Guid.NewGuid();
    private ulong _generation, _sequence;
    private long _lastAcknowledgement;
    private IRuntimeLease? _lease;
    private CancellationTokenSource? _work;
    private Task _monitor = Task.CompletedTask;
    private SessionStatus _status = new(SessionState.Stopped);
    private int _fence, _closing;
    private bool _disposed;
    public SessionStatus Status => Volatile.Read(ref _status);

    public SessionSupervisor(IRuntimeFactory factory, string hostId, string gameId, string directory,
        SessionDeadlines? deadlines = null, Action<SessionStatus>? trace = null)
    {
        ArgumentNullException.ThrowIfNull(factory); ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        new SessionIdentity(_sessionId, 1, hostId, gameId, new string('0', 64)).Validate();
        _factory = factory; _hostId = hostId; _gameId = gameId; _directory = directory;
        _deadlines = deadlines ?? new(); _deadlines.Validate(); _trace = trace;
    }
    private void Publish(SessionStatus value)
    {
        Volatile.Write(ref _status, value);
        try { _trace?.Invoke(value); } catch { /* Diagnostics cannot prevent cleanup. */ }
    }

    public async Task StartAsync(SceneSnapshot scene, string behavior = "normal", CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var fence = Volatile.Read(ref _fence);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        var phase = "Launch";
        try
        {
            ObjectDisposedException.ThrowIf(_disposed || Volatile.Read(ref _closing) != 0, this);
            if (fence != Volatile.Read(ref _fence)) throw new OperationCanceledException("Start superseded by Stop.");
            if (_lease != null) throw new InvalidOperationException("Stop/reap the previous runtime before Start.");
            var identity = new SessionIdentity(_sessionId, checked(++_generation), _hostId, _gameId,
                Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            _work?.Dispose(); _work = new(); _sequence = 0;
            Publish(new(SessionState.Starting, identity));
            try
            {
                using var startup = CancellationTokenSource.CreateLinkedTokenSource(token, _work.Token);
                startup.CancelAfter(_deadlines.Startup);
                _lease = _factory.Create(new(identity, $"engine-{Guid.NewGuid():N}", _directory, behavior));
                await _lease.LaunchAsync(startup.Token).ConfigureAwait(false);
                Publish(Status with { ProcessId = _lease.ProcessId });
                phase = "Hello";
                var hello = await ExchangeAsync(CommandKind.Hello, null, startup.Token).ConfigureAwait(false);
                if (hello.Phase != "HelloAccepted") throw new InvalidDataException("Hello acknowledgement phase mismatch.");
                Publish(Status with { HelloAccepted = true });
                phase = "LoadScene";
                var loaded = await ExchangeAsync(CommandKind.LoadScene, r =>
                { r.SceneId = scene.Id; r.Revision = scene.Revision; r.Entities = scene.CopyEntities(); }, startup.Token).ConfigureAwait(false);
                if (loaded.Revision != scene.Revision || loaded.Phase != "Paused") throw new InvalidDataException("Scene acknowledgement mismatch.");
                Publish(Status with { SceneLoaded = true, Tick = unchecked((ulong)loaded.Tick), SceneRevision = scene.Revision });
                phase = "SetMode";
                var mode = await ExchangeAsync(CommandKind.SetMode, r => r.Playing = false, startup.Token).ConfigureAwait(false);
                if (mode.Phase != "Paused") throw new InvalidDataException("Paused acknowledgement missing.");
                Publish(Status with { State = SessionState.Paused, Tick = unchecked((ulong)mode.Tick) });
                _monitor = MonitorAsync(_lease, _work.Token);
            }
            catch (Exception error)
            {
                await FailAsync(phase, error).ConfigureAwait(false);
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    private async Task<ControlReply> ExchangeAsync(CommandKind kind, Action<ControlRequest>? configure, CancellationToken token)
    {
        var lease = _lease ?? throw new InvalidOperationException("No runtime.");
        var identity = Status.Identity!;
        var request = ControlRequest.Create(identity, checked(++_sequence), kind); configure?.Invoke(request);
        var pending = lease.SendAsync(request, token);
        // Observe late completion even when a peer/fake ignores local cancellation.
        _ = pending.ContinueWith(t => _ = t.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        var first = await Task.WhenAny(pending, lease.Exited).WaitAsync(token).ConfigureAwait(false);
        if (first == lease.Exited && !pending.IsCompleted) throw new IOException("Runtime exited before acknowledgement.");
        var reply = await pending.WaitAsync(token).ConfigureAwait(false);
        ValidateReply(identity, unchecked((ulong)request.Sequence), kind, lease.ProcessId, reply);
        _lastAcknowledgement = Stopwatch.GetTimestamp();
        return reply;
    }
    public static void ValidateReply(SessionIdentity identity, ulong sequence, CommandKind kind, int pid, ControlReply reply)
    {
        if (reply == null || reply.Stamp == null || reply.Stamp.Identity() != identity ||
            unchecked((ulong)reply.Stamp.Sequence) != sequence || reply.Stamp.Kind != (int)kind || reply.ProcessId != pid)
            throw new InvalidDataException("Stale/mismatched runtime reply identity, sequence, operation or PID.");
    }

    public Task<ControlReply> GetStatusAsync(CancellationToken token = default) => CommandAsync(CommandKind.GetStatus, null, token);
    public Task<ControlReply> StepAsync(CancellationToken token = default) => CommandAsync(CommandKind.Step, null, token);
    public Task<ControlReply> ConfigureSurfaceAsync(FrameSurface surface, CancellationToken token = default)
    {
        surface.Validate();
        return CommandAsync(CommandKind.ConfigureSurface, r =>
        {
            if (r.Identity() != surface.Identity) throw new InvalidDataException("Surface belongs to a different session.");
            r.Surface = surface.ToWire();
        }, token, surfaceGeneration: surface.Generation);
    }
    public async Task ReportFrameFaultAsync(SessionIdentity identity, string phase, Exception error)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Status.Identity == identity && Status.State is SessionState.Paused or SessionState.Playing)
                await FailAsync(phase, error).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
    public Task<ControlReply> SetPlayingAsync(bool playing, CancellationToken token = default)
        => CommandAsync(CommandKind.SetMode, r => r.Playing = playing, token, playing);
    private async Task<ControlReply> CommandAsync(CommandKind kind, Action<ControlRequest>? configure, CancellationToken token, bool? expectedPlaying = null, ulong? surfaceGeneration = null)
    {
        var fence = Volatile.Read(ref _fence);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed || Volatile.Read(ref _closing) != 0, this);
            if (fence != Volatile.Read(ref _fence)) throw new OperationCanceledException("Command superseded by Stop.");
            if (Status.State is not (SessionState.Paused or SessionState.Playing)) throw new InvalidOperationException("Runtime is not Ready.");
            if (kind == CommandKind.Step && Status.State != SessionState.Paused) throw new InvalidOperationException("Step requires Paused.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token, _work!.Token);
            deadline.CancelAfter(_deadlines.Command);
            try
            {
                var reply = await ExchangeAsync(kind, configure, deadline.Token).ConfigureAwait(false);
                if (surfaceGeneration != null && unchecked((ulong)reply.SurfaceGeneration) != surfaceGeneration)
                    throw new InvalidDataException("Surface acknowledgement mismatch.");
                if (kind == CommandKind.SetMode && reply.Phase != (expectedPlaying == true ? "Playing" : "Paused"))
                    throw new InvalidDataException("Mode acknowledgement did not apply the requested mode.");
                if (kind == CommandKind.Step && (reply.Phase != "Paused" || unchecked((ulong)reply.Tick) != checked(Status.Tick + 1)))
                    throw new InvalidDataException("Step acknowledgement did not apply exactly one tick.");
                if (kind == CommandKind.GetStatus && (reply.Phase != Status.State.ToString() || Status.State == SessionState.Paused && unchecked((ulong)reply.Tick) != Status.Tick))
                    throw new InvalidDataException("Status changed a paused runtime or its mode.");
                ApplyStatus(reply); return reply;
            }
            catch (Exception error) { await FailAsync(kind.ToString(), error).ConfigureAwait(false); throw; }
        }
        finally { _gate.Release(); }
    }
    private void ApplyStatus(ControlReply reply)
    {
        if (reply.Phase is not ("Paused" or "Playing")) throw new InvalidDataException("Unexpected runtime status phase.");
        var tick = unchecked((ulong)reply.Tick);
        if (reply.Revision != Status.SceneRevision) throw new InvalidDataException("Runtime scene revision mismatch.");
        if (tick < Status.Tick) throw new InvalidDataException("Runtime tick regressed.");
        Publish(Status with { State = reply.Phase == "Playing" ? SessionState.Playing : SessionState.Paused, Tick = tick });
    }
    private async Task MonitorAsync(IRuntimeLease lease, CancellationToken token)
    {
        try
        {
            while (true)
            {
                await Task.WhenAny(Task.Delay(_deadlines.HeartbeatInterval, token), lease.Exited).WaitAsync(token).ConfigureAwait(false);
                await _gate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    if (!ReferenceEquals(_lease, lease)) return;
                    try
                    {
                        if (lease.Exited.IsCompleted) throw new IOException("Owned runtime exited unexpectedly.");
                        using var heartbeat = CancellationTokenSource.CreateLinkedTokenSource(token);
                        var remaining = _deadlines.HeartbeatResponse - Stopwatch.GetElapsedTime(_lastAcknowledgement);
                        if (remaining <= TimeSpan.Zero) throw new TimeoutException("Runtime heartbeat deadline expired.");
                        heartbeat.CancelAfter(remaining);
                        var reply = await ExchangeAsync(CommandKind.GetStatus, null, heartbeat.Token).ConfigureAwait(false);
                        if (reply.Phase != Status.State.ToString() || Status.State == SessionState.Paused && unchecked((ulong)reply.Tick) != Status.Tick)
                            throw new InvalidDataException("Heartbeat changed runtime mode/paused tick.");
                        ApplyStatus(reply);
                    }
                    catch (Exception error)
                    {
                        if (!token.IsCancellationRequested) await FailAsync(lease.Exited.IsCompleted ? "ProcessExit" : "Heartbeat", error).ConfigureAwait(false);
                        return;
                    }
                }
                finally { _gate.Release(); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
    private async Task FailAsync(string phase, Exception error)
    {
        CancelWork();
        Publish(Status with { State = SessionState.Faulted, Failure = new(phase, error) });
        try { await CleanupAsync().ConfigureAwait(false); }
        catch (Exception cleanup) { Publish(Status with { State = SessionState.CleanupFailed, Failure = new(phase, error, cleanup) }); }
    }
    private async Task CleanupAsync(CancellationToken gracefulDeadline = default)
    {
        if (_lease == null) return;
        await _lease.StopAsync(gracefulDeadline).ConfigureAwait(false);
        await _lease.DisposeAsync().ConfigureAwait(false);
        if (!_lease.ResourcesReleased) throw new IOException("Runtime cleanup not confirmed.");
        _lease = null;
    }
    public async Task StopAsync()
    {
        Interlocked.Increment(ref _fence); CancelWork();
        await _gate.WaitAsync().ConfigureAwait(false);
        var monitor = _monitor;
        try
        {
            if (_lease == null) { Publish(Status with { State = SessionState.Stopped }); return; }
            var previous = Status;
            Publish(Status with { State = SessionState.Stopping });
            try
            {
                using var graceful = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                // A failed/cancelled command fences remote work: do not replay Stop on that channel.
                if (previous.State is SessionState.Paused or SessionState.Playing)
                {
                    try { await ExchangeAsync(CommandKind.Stop, null, graceful.Token).ConfigureAwait(false); }
                    catch (Exception error) { Publish(Status with { Failure = new("StopAcknowledgement", error) }); }
                }
                await CleanupAsync(graceful.Token).ConfigureAwait(false);
                Publish(Status with { State = SessionState.Stopped });
            }
            catch (Exception error)
            {
                Publish(Status with { State = SessionState.CleanupFailed,
                    Failure = previous.Failure is { } failure ? failure with { CleanupError = error } : new("Cleanup", error) });
                throw;
            }
        }
        finally { _gate.Release(); await monitor.ConfigureAwait(false); }
    }
    public async ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _closing, 1);
        await StopAsync().ConfigureAwait(false);
        _disposed = true; _work?.Dispose(); _work = null;
    }
    private void CancelWork()
    {
        try { _work?.Cancel(); }
        catch (ObjectDisposedException) { /* Concurrent repeated Stop/Dispose already fenced it. */ }
    }
}
