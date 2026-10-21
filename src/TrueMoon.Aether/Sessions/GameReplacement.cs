using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Frames;

namespace TrueMoon.Aether.Sessions;

/// <summary>Serializes explicit game replacement and at most one old-build recovery on borrowed session resources.</summary>
public sealed class GameReplacement
{
    private readonly SessionSupervisor _supervisor;
    private readonly FrameSession _frames;
    private readonly ReplacementDeadlines _deadlines;
    /// <summary>Creates a controller; the caller continues owning supervisor and frames.</summary>
    /// <param name="supervisor">Borrowed process/control owner.</param><param name="frames">Its borrowed frame session.</param>
    /// <param name="deadlines">Optional immutable limits; defaults total45s/forward18s/cleanup10s.</param>
    public GameReplacement(SessionSupervisor supervisor, FrameSession frames, ReplacementDeadlines? deadlines = null)
    {
        ArgumentNullException.ThrowIfNull(supervisor); ArgumentNullException.ThrowIfNull(frames);
        if (!ReferenceEquals(frames.Supervisor, supervisor)) throw new ArgumentException("Frames belong to another supervisor.", nameof(frames));
        _supervisor = supervisor; _frames = frames; _deadlines = (deadlines ?? new()) with { }; _deadlines.Validate();
    }
    /// <summary>Captures a paused tick boundary, reaps the old child and restores the candidate before returning the previous mode.</summary>
    /// <remarks>Expected identity/source/scene preflight precedes Pause. Caller cancellation after old Stop may recover the old build;
    /// explicit Stop/App close always prevent recovery launch. A failed accepted transaction throws GameReplacementException with the original cause.</remarks>
    /// <param name="request">Frozen candidate and current editor selection.</param><param name="token">Caller cancellation; cleanup remains owned.</param>
    /// <returns>Ready candidate identity and the checkpoint used for restore.</returns>
    public async Task<GameReplacementResult> ReplaceAsync(GameReplacementRequest request, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Candidate);
        ArgumentNullException.ThrowIfNull(request.ExpectedIdentity); ArgumentNullException.ThrowIfNull(request.Scene);
        token.ThrowIfCancellationRequested();
        _supervisor.EnsureReplacementOpen();
        using var owner = await _frames.ReserveReplacementAsync(_deadlines, token).ConfigureAwait(false);
        using var forward = CancellationTokenSource.CreateLinkedTokenSource(token, owner.Interrupted.Token, owner.Total.Token);
        forward.CancelAfter(_deadlines.Forward);
        var phase = "Preflight"; var stoppedOld = false; var pausedOld = false; var wasPlaying = false;
        var rollbackAttempted = false; var rollbackSucceeded = false;
        GameCheckpoint? checkpoint = null; ViewportSurface? surface = null;
        (SceneSnapshot Scene, GameBuildArtifact? Build, string Behavior)? old = null;
        IDisposable? candidatePin = null, oldPin = null;
        try
        {
            // Drain pre-existing frame/control operations after closing normal command admission.
            surface = await _frames.SurfaceForReplacementAsync(owner, forward.Token).ConfigureAwait(false);
            await _supervisor.StatusForReplacementAsync(owner, forward.Token).ConfigureAwait(false);
            old = _supervisor.Configuration ?? throw new InvalidOperationException("No running game configuration.");
            if (_supervisor.Status.Identity != request.ExpectedIdentity || request.Candidate.SourceRevision != request.ExpectedSourceRevision ||
                old.Value.Scene.Id != request.Scene.Id || old.Value.Scene.Revision != request.Scene.Revision)
                throw new InvalidDataException("Replacement selection is stale or scene revision is incompatible.");
            if (surface?.Suspended == true) throw new InvalidOperationException("Resume a visible surface before replacing its game.");
            GameBuildArtifact.Open(request.Candidate.DirectoryPath, request.ExpectedIdentity.HostContractId, request.Candidate.BuildId);
            candidatePin = request.Candidate.Pin();
            if (old.Value.Build is { } previous)
            {
                GameBuildArtifact.Open(previous.DirectoryPath, request.ExpectedIdentity.HostContractId, previous.BuildId);
                oldPin = previous.Pin();
            }
            forward.Token.ThrowIfCancellationRequested();
            wasPlaying = _supervisor.Status.State == SessionState.Playing;
            phase = "Pause";
            if (wasPlaying) await SetModeAsync(false, owner, forward.Token).ConfigureAwait(false);
            pausedOld = true; phase = "Capture";
            checkpoint = await _supervisor.CaptureForReplacementAsync(owner, forward.Token).ConfigureAwait(false);
            checkpoint.ValidateFor(request.ExpectedIdentity.HostContractId, request.Scene);
            forward.Token.ThrowIfCancellationRequested(); _supervisor.Admission.Check(owner);
            phase = "StopOld"; stoppedOld = true;
            await _frames.StopForReplacementAsync(forward.Token).ConfigureAwait(false);
            phase = "Candidate";
            await StartAsync(request.Scene, request.Candidate, checkpoint, surface, "normal", owner, forward.Token).ConfigureAwait(false);
            phase = "CandidateMode";
            if (wasPlaying) await SetModeAsync(true, owner, forward.Token).ConfigureAwait(false);
            forward.Token.ThrowIfCancellationRequested(); _supervisor.Admission.Check(owner);
            return new(request.ExpectedIdentity, _supervisor.Status.Identity!, checkpoint, wasPlaying);
        }
        catch (Exception cause)
        {
            Exception? recoveryError = null;
            try
            {
                if (_supervisor.Status.State == SessionState.CleanupFailed)
                    throw new IOException("Cleanup remains unconfirmed; replacement/recovery launch is blocked.", _supervisor.Status.Failure?.CleanupError);
                if (owner.Interrupted.IsCancellationRequested)
                {
                    using var cleanup = _supervisor.Admission.CleanupScope();
                    await _frames.StopForReplacementAsync(cleanup.Token).ConfigureAwait(false);
                }
                else if (!stoppedOld)
                {
                    if (pausedOld && wasPlaying && _supervisor.Status.State == SessionState.Paused &&
                        _supervisor.Status.Identity == request.ExpectedIdentity && !owner.Interrupted.IsCancellationRequested)
                    {
                        using var recovery = CancellationTokenSource.CreateLinkedTokenSource(owner.Total.Token, owner.Interrupted.Token);
                        await SetModeAsync(true, owner, recovery.Token).ConfigureAwait(false);
                    }
                    if (_supervisor.Status.State == SessionState.Faulted)
                    {
                        using var cleanup = _supervisor.Admission.CleanupScope();
                        await _frames.DrainReplacementFailureAsync(cause, cleanup.Token).ConfigureAwait(false);
                    }
                }
                else
                {
                    using var cleanup = _supervisor.Admission.CleanupScope();
                    await _frames.StopForReplacementAsync(cleanup.Token).ConfigureAwait(false);
                    if (!owner.Interrupted.IsCancellationRequested && !owner.Total.IsCancellationRequested && checkpoint != null && old != null)
                    {
                        _supervisor.Admission.Check(owner); rollbackAttempted = true;
                        using var recovery = CancellationTokenSource.CreateLinkedTokenSource(owner.Total.Token, owner.Interrupted.Token);
                        await StartAsync(old.Value.Scene, old.Value.Build, checkpoint, surface, old.Value.Behavior, owner, recovery.Token).ConfigureAwait(false);
                        if (wasPlaying) await SetModeAsync(true, owner, recovery.Token).ConfigureAwait(false);
                        _supervisor.Admission.Check(owner); rollbackSucceeded = true;
                    }
                }
            }
            catch (Exception error)
            {
                recoveryError = error;
                if (_supervisor.Status.State != SessionState.CleanupFailed &&
                    (_supervisor.Status.State is SessionState.Paused or SessionState.Playing || _frames.MappingCount != 0 || _frames.Presentation != null))
                {
                    try
                    {
                        if (!owner.Interrupted.IsCancellationRequested && _supervisor.Status.Identity is { } identity)
                            await _supervisor.ReportFrameFaultAsync(identity, "ReplacementRecovery", error).ConfigureAwait(false);
                        using var cleanup = _supervisor.Admission.CleanupScope();
                        await _frames.DrainReplacementFailureAsync(error, cleanup.Token).ConfigureAwait(false);
                    }
                    catch (Exception drain) { recoveryError = new AggregateException(error, drain); }
                }
            }
            throw new GameReplacementException(phase, cause, rollbackAttempted, rollbackSucceeded, recoveryError);
        }
        finally { oldPin?.Dispose(); candidatePin?.Dispose(); }
    }
    private async Task SetModeAsync(bool playing, ReplacementAdmission.Lease owner, CancellationToken token)
    {
        using var mode = CancellationTokenSource.CreateLinkedTokenSource(token); mode.CancelAfter(TimeSpan.FromSeconds(2));
        await _supervisor.ModeForReplacementAsync(playing, owner, mode.Token).ConfigureAwait(false);
    }
    private async Task StartAsync(SceneSnapshot scene, GameBuildArtifact? build, GameCheckpoint checkpoint, ViewportSurface? surface, string behavior,
        ReplacementAdmission.Lease owner, CancellationToken token)
    {
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(token); startup.CancelAfter(TimeSpan.FromSeconds(15));
        if (surface == null) await _supervisor.StartForReplacementAsync(scene, build, checkpoint, behavior, owner, startup.Token).ConfigureAwait(false);
        else await _frames.StartForReplacementAsync(scene, build, checkpoint, surface.Width, surface.Height, behavior, owner, startup.Token).ConfigureAwait(false);
    }
}
