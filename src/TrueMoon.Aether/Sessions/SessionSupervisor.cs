using System.Diagnostics;
using System.Security.Cryptography;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Input;

namespace TrueMoon.Aether.Sessions;

public enum SessionState { Stopped, Starting, Paused, Playing, Stopping, Faulted, CleanupFailed }
public sealed record SessionFailure(string Phase, Exception Cause, Exception? CleanupError = null);
public sealed record SessionStatus(SessionState State, SessionIdentity? Identity = null, int? ProcessId = null,
    bool HelloAccepted = false, bool SceneLoaded = false, ulong Tick = 0, SessionFailure? Failure = null, long? SceneRevision = null);
/// <summary>Immutable per-generation child launch configuration.</summary>
/// <param name="Identity">Expected runtime identity.</param><param name="Endpoint">Private control endpoint.</param>
/// <param name="ReportDirectory">Runtime diagnostics destination.</param><param name="Behavior">Probe behavior selector.</param>
/// <param name="Build">Optional selected game artifact.</param><param name="RequireRestore">Requires restore before any readiness/frame publication.</param>
public sealed record SessionLaunch(SessionIdentity Identity, string Endpoint, string ReportDirectory, string Behavior = "normal",
    GameBuildReference? Build = null, bool RequireRestore = false);
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
public interface IRuntimeFactory : IFactory<IRuntimeLease, SessionLaunch> { }

// All remote work is serialized. Stop cancels the current local wait before
// entering the gate; a cancelled/expired command is never replayed.
public sealed class SessionSupervisor : IAsyncDisposable
{
    private readonly IFactory<IRuntimeLease, SessionLaunch> _factory;
    private readonly SessionDeadlines _deadlines;
    private readonly string _hostId, _gameId, _directory;
    private string _sceneId = "";
    private readonly Action<SessionStatus>? _trace;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Guid _sessionId = Guid.NewGuid();
    private ulong _generation, _sequence;
    private long _lastAcknowledgement;
    private IRuntimeLease? _lease;
    internal IRuntimeLease? ActiveRuntime => Volatile.Read(ref _lease);
    private Task? _cleanup;
    internal ReplacementAdmission Admission { get; } = new();
    internal int StopVersion => Volatile.Read(ref _fence);
    internal (SceneSnapshot Scene, GameBuildArtifact? Build, string Behavior)? Configuration { get; private set; }
    internal void EnsureReplacementOpen() => ObjectDisposedException.ThrowIf(_disposed || Volatile.Read(ref _closing) != 0, this);
    private CancellationTokenSource? _work;
    private Task _monitor = Task.CompletedTask;
    private SessionStatus _status = new(SessionState.Stopped);
    private int _fence, _closing;
    private bool _disposed;
    public SessionStatus Status => Volatile.Read(ref _status);

    public SessionSupervisor(IFactory<IRuntimeLease, SessionLaunch> factory, string hostId, string gameId, string directory,
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

    public Task StartAsync(SceneSnapshot scene, string behavior = "normal", CancellationToken token = default)
        => StartCoreAsync(scene, null, null, behavior, token);

    /// <summary>Explicitly starts a validated artifact on a fresh generation. Build completion never calls this automatically.</summary>
    public Task StartAsync(SceneSnapshot scene, GameBuildArtifact build, string behavior = "normal", CancellationToken token = default)
    { ArgumentNullException.ThrowIfNull(build); return StartCoreAsync(scene, build, null, behavior, token); }

    /// <summary>Starts a fresh fixed-build runtime and restores a compatible snapshot before readiness.</summary>
    public Task StartAsync(SceneSnapshot scene, GameCheckpoint checkpoint, string behavior = "normal", CancellationToken token = default)
    { ArgumentNullException.ThrowIfNull(checkpoint); return StartCoreAsync(scene, null, checkpoint, behavior, token); }

    /// <summary>Starts a selected artifact and restores state before mode/frame readiness.</summary>
    public Task StartAsync(SceneSnapshot scene, GameBuildArtifact build, GameCheckpoint checkpoint, string behavior = "normal", CancellationToken token = default)
    { ArgumentNullException.ThrowIfNull(build); ArgumentNullException.ThrowIfNull(checkpoint); return StartCoreAsync(scene, build, checkpoint, behavior, token); }

    internal Task StartForReplacementAsync(SceneSnapshot scene, GameBuildArtifact? build, GameCheckpoint checkpoint, string behavior,
        ReplacementAdmission.Lease owner, CancellationToken token) => StartCoreAsync(scene, build, checkpoint, behavior, token, owner);

    private async Task StartCoreAsync(SceneSnapshot scene, GameBuildArtifact? build, GameCheckpoint? checkpoint, string behavior, CancellationToken token,
        ReplacementAdmission.Lease? owner = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        checkpoint?.ValidateFor(_hostId, scene);
        var fence = Volatile.Read(ref _fence);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        var phase = "Launch";
        try
        {
            ObjectDisposedException.ThrowIf(_disposed || Volatile.Read(ref _closing) != 0, this);
            Admission.Check(owner);
            if (fence != Volatile.Read(ref _fence)) throw new OperationCanceledException("Start superseded by Stop.");
            if (_lease != null) throw new InvalidOperationException("Stop/reap the previous runtime before Start.");
            if (build != null) GameBuildArtifact.Open(build.DirectoryPath, _hostId, build.BuildId);
            var identity = new SessionIdentity(_sessionId, checked(++_generation), _hostId, build?.BuildId ?? _gameId,
                Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            _work?.Dispose(); _work = new(); _sequence = 0;
            Publish(new(SessionState.Starting, identity));
            try
            {
                using var startup = CancellationTokenSource.CreateLinkedTokenSource(token, _work.Token);
                startup.CancelAfter(_deadlines.Startup);
                _lease = _factory.Create(new(identity, $"engine-{Guid.NewGuid():N}", _directory, behavior,
                    build == null ? null : new(build.DirectoryPath, build.BuildId), checkpoint != null));
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
                _sceneId = scene.Id;
                Publish(Status with { SceneLoaded = true, Tick = unchecked((ulong)loaded.Tick), SceneRevision = scene.Revision });
                if (checkpoint != null)
                {
                    phase = "RestoreCheckpoint";
                    var restored = await ExchangeAsync(CommandKind.RestoreCheckpoint, r => r.Checkpoint = GameCheckpointCodec.Encode(checkpoint), startup.Token).ConfigureAwait(false);
                    if (restored.Phase != "Paused" || restored.Revision != scene.Revision || restored.Checkpoint is { Length: > 0 })
                        throw new InvalidDataException("Malformed restore acknowledgement.");
                    if (restored.CheckpointOutcome is (int)CheckpointOutcome.Unsupported or (int)CheckpointOutcome.RestoreRejected)
                        throw new CheckpointRejectedException((CheckpointOutcome)restored.CheckpointOutcome);
                    if (restored.CheckpointOutcome != (int)CheckpointOutcome.Restored || unchecked((ulong)restored.Tick) != checkpoint.State.Tick)
                        throw new InvalidDataException("Restore acknowledgement did not apply the requested state.");
                    Publish(Status with { Tick = checkpoint.State.Tick });
                }
                phase = "SetMode";
                var mode = await ExchangeAsync(CommandKind.SetMode, r => r.Playing = false, startup.Token).ConfigureAwait(false);
                if (mode.Phase != "Paused") throw new InvalidDataException("Paused acknowledgement missing.");
                Publish(Status with { State = SessionState.Paused, Tick = unchecked((ulong)mode.Tick) });
                Configuration = (scene, build, behavior);
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
        if (kind is not (CommandKind.CaptureCheckpoint or CommandKind.RestoreCheckpoint) &&
            (reply.CheckpointOutcome != 0 || reply.Checkpoint is { Length: > 0 })) throw new InvalidDataException("Unexpected checkpoint response.");
        if (kind != CommandKind.SetInput && reply.InputOutcome != 0) throw new InvalidDataException("Unexpected input response.");
    }

    public Task<ControlReply> GetStatusAsync(CancellationToken token = default) => CommandAsync(CommandKind.GetStatus, null, token);
    public Task<ControlReply> StepAsync(CancellationToken token = default) => CommandAsync(CommandKind.Step, null, token);
    /// <summary>Stages transient input only on the exact expected runtime. Acceptance does not advance simulation.</summary>
    /// <param name="identity">Captured runtime identity; never silently retargeted after replacement.</param>
    /// <param name="input">Validated bounded input.</param><param name="token">Cancellation token.</param>
    /// <returns>Accepted/Unsupported input outcome and the observed simulation tick.</returns>
    public Task<ControlReply> SendInputAsync(SessionIdentity identity, GameInputState input, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(identity); identity.Validate(); input.Validate();
        return CommandAsync(CommandKind.SetInput, r =>
        { r.InputCaptured = input.Captured; r.InputButtons = (int)input.Buttons; r.InputYaw = input.Yaw; r.InputPitch = input.Pitch; r.InputInteract = input.InteractPressed; }, token, expectedIdentity: identity);
    }
    /// <summary>Captures a paused tick boundary. Only a validated explicit rejection leaves the session ready.</summary>
    public async Task<GameCheckpoint> CaptureCheckpointAsync(CancellationToken token = default)
    {
        var reply = await CommandAsync(CommandKind.CaptureCheckpoint, null, token).ConfigureAwait(false);
        return GameCheckpointCodec.Decode(reply.Checkpoint!);
    }
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
    internal Task<ControlReply> StatusForReplacementAsync(ReplacementAdmission.Lease owner, CancellationToken token)
        => CommandAsync(CommandKind.GetStatus, null, token, owner: owner);
    internal Task<ControlReply> ModeForReplacementAsync(bool playing, ReplacementAdmission.Lease owner, CancellationToken token)
        => CommandAsync(CommandKind.SetMode, r => r.Playing = playing, token, playing, owner: owner);
    internal async Task<GameCheckpoint> CaptureForReplacementAsync(ReplacementAdmission.Lease owner, CancellationToken token)
        => GameCheckpointCodec.Decode((await CommandAsync(CommandKind.CaptureCheckpoint, null, token, owner: owner).ConfigureAwait(false)).Checkpoint!);
    internal Task<ControlReply> SurfaceForReplacementAsync(FrameSurface surface, ReplacementAdmission.Lease owner, CancellationToken token)
        => CommandAsync(CommandKind.ConfigureSurface, r => r.Surface = surface.ToWire(), token, surfaceGeneration: surface.Generation, owner: owner);
    private async Task<ControlReply> CommandAsync(CommandKind kind, Action<ControlRequest>? configure, CancellationToken token, bool? expectedPlaying = null,
        ulong? surfaceGeneration = null, ReplacementAdmission.Lease? owner = null, SessionIdentity? expectedIdentity = null)
    {
        var fence = Volatile.Read(ref _fence);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed || Volatile.Read(ref _closing) != 0, this);
            Admission.Check(owner);
            if (expectedIdentity != null && Status.Identity != expectedIdentity) throw new OperationCanceledException("Input belongs to an earlier runtime.");
            if (fence != Volatile.Read(ref _fence)) throw new OperationCanceledException("Command superseded by Stop.");
            if (Status.State is not (SessionState.Paused or SessionState.Playing)) throw new InvalidOperationException("Runtime is not Ready.");
            if (kind == CommandKind.Step && Status.State != SessionState.Paused) throw new InvalidOperationException("Step requires Paused.");
            if (kind == CommandKind.CaptureCheckpoint && Status.State != SessionState.Paused) throw new InvalidOperationException("Pause before capturing a checkpoint.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token, _work!.Token);
            deadline.CancelAfter(_deadlines.Command);
            var acknowledgedRejection = false;
            try
            {
                var reply = await ExchangeAsync(kind, configure, deadline.Token).ConfigureAwait(false);
                if (kind == CommandKind.SetInput && (reply.InputOutcome is not ((int)GameInputOutcome.Accepted) and not ((int)GameInputOutcome.Unsupported) ||
                    reply.Phase != Status.State.ToString() || Status.State == SessionState.Paused && unchecked((ulong)reply.Tick) != Status.Tick))
                    throw new InvalidDataException("Input acknowledgement changed paused tick/mode or omitted its outcome.");
                if (kind == CommandKind.CaptureCheckpoint)
                {
                    if (reply.Phase != "Paused" || reply.Revision != Status.SceneRevision || unchecked((ulong)reply.Tick) != Status.Tick)
                        throw new InvalidDataException("Capture changed paused mode/tick/revision.");
                    if (reply.CheckpointOutcome is (int)CheckpointOutcome.Unsupported or (int)CheckpointOutcome.CaptureRejected)
                    {
                        if (reply.Checkpoint is { Length: > 0 }) throw new InvalidDataException("Rejected capture returned state.");
                        ApplyStatus(reply); acknowledgedRejection = true;
                        throw new CheckpointRejectedException((CheckpointOutcome)reply.CheckpointOutcome);
                    }
                    if (reply.CheckpointOutcome != (int)CheckpointOutcome.Captured || reply.Checkpoint == null)
                        throw new InvalidDataException("Missing checkpoint capture acknowledgement.");
                    var captured = GameCheckpointCodec.Decode(reply.Checkpoint);
                    captured.ValidateFor(_hostId, new(_sceneId, Status.SceneRevision!.Value, []));
                    if (!string.Equals(captured.SourceGameBuildId, Status.Identity!.GameBuildId, StringComparison.OrdinalIgnoreCase) || captured.State.Tick != Status.Tick)
                        throw new InvalidDataException("Captured checkpoint build/tick mismatch.");
                }
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
            catch (CheckpointRejectedException) when (acknowledgedRejection) { throw; }
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
        using var cleanupWait = Admission.CleanupScope();
        try { await CleanupAsync(cleanupDeadline: cleanupWait.Token).ConfigureAwait(false); }
        catch (Exception cleanup) { Publish(Status with { State = SessionState.CleanupFailed, Failure = new(phase, error, cleanup) }); }
    }
    private async Task CleanupAsync(CancellationToken gracefulDeadline = default, CancellationToken cleanupDeadline = default)
    {
        if (_lease == null) return;
        if (_cleanup == null || _cleanup.IsFaulted || _cleanup.IsCanceled)
        {
            _cleanup = CleanupLeaseAsync(_lease, gracefulDeadline);
            _ = _cleanup.ContinueWith(t => _ = t.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        await _cleanup.WaitAsync(cleanupDeadline).ConfigureAwait(false);
        _lease = null; _cleanup = null;
    }
    private static async Task CleanupLeaseAsync(IRuntimeLease lease, CancellationToken gracefulDeadline)
    {
        await lease.StopAsync(gracefulDeadline).ConfigureAwait(false);
        await lease.DisposeAsync().ConfigureAwait(false);
        if (!lease.ResourcesReleased) throw new IOException("Runtime cleanup not confirmed.");
    }
    public Task StopAsync() { Admission.Interrupt(); return StopCoreAsync(); }
    internal Task StopForReplacementAsync(CancellationToken token) => StopCoreAsync(token);
    private async Task StopCoreAsync(CancellationToken cleanupDeadline = default)
    {
        Interlocked.Increment(ref _fence); CancelWork();
        await _gate.WaitAsync(cleanupDeadline).ConfigureAwait(false);
        var monitor = _monitor;
        try
        {
            if (_lease == null) { Publish(Status with { State = SessionState.Stopped }); return; }
            var previous = Status;
            Publish(Status with { State = SessionState.Stopping });
            try
            {
                using var graceful = CancellationTokenSource.CreateLinkedTokenSource(cleanupDeadline);
                graceful.CancelAfter(TimeSpan.FromSeconds(3));
                // A failed/cancelled command fences remote work: do not replay Stop on that channel.
                if (previous.State is SessionState.Paused or SessionState.Playing)
                {
                    try { await ExchangeAsync(CommandKind.Stop, null, graceful.Token).ConfigureAwait(false); }
                    catch (Exception error) { Publish(Status with { Failure = new("StopAcknowledgement", error) }); }
                }
                await CleanupAsync(graceful.Token, cleanupDeadline).ConfigureAwait(false);
                Publish(Status with { State = SessionState.Stopped });
            }
            catch (Exception error)
            {
                Publish(Status with { State = SessionState.CleanupFailed,
                    Failure = previous.Failure is { } failure ? failure with { CleanupError = error } : new("Cleanup", error) });
                throw;
            }
        }
        finally { _gate.Release(); await monitor.WaitAsync(cleanupDeadline).ConfigureAwait(false); }
    }
    public async ValueTask DisposeAsync()
    {
        CloseAdmission();
        await Admission.DrainAsync().ConfigureAwait(false);
        await StopAsync().ConfigureAwait(false);
        _disposed = true; _work?.Dispose(); _work = null;
    }
    internal void CloseAdmission() { Interlocked.Exchange(ref _closing, 1); Admission.Interrupt(); }
    private void CancelWork()
    {
        try { _work?.Cancel(); }
        catch (ObjectDisposedException) { /* Concurrent repeated Stop/Dispose already fenced it. */ }
    }
}
