using System.Diagnostics;

namespace TrueMoon.Alloy;

/// <summary>Session lifetime, independent of whether a clean frame needs drawing.</summary>
public enum UiSessionState
{
    /// <summary>Live session with a nonempty viewport.</summary>
    Active,
    /// <summary>Live session with a zero-sized viewport.</summary>
    Suspended,
    /// <summary>Terminal graphics failure; explicit disposal is required.</summary>
    Faulted,
    /// <summary>Preflight passed and owned cleanup is in progress.</summary>
    Disposing,
    /// <summary>Owned cleanup has completed, possibly with reported errors.</summary>
    Disposed
}

/// <summary>Outcome reported by a host after one presentation attempt.</summary>
public enum UiPresentationOutcome
{
    /// <summary>The host accepted the frame for presentation; this is not a GPU completion signal.</summary>
    Presented,
    /// <summary>The host declined presentation, for example during swapchain recreation.</summary>
    Skipped,
    /// <summary>Presentation threw or otherwise failed.</summary>
    Failed
}

/// <summary>Fixed-size CPU timing aggregates. Update includes its nested phases; totals must not be added together.</summary>
/// <param name="Attempts">Started operations, including an operation still in progress.</param>
/// <param name="Completed">Successful operations, including clean or suspended Update calls.</param>
/// <param name="Failed">Operations that threw.</param>
/// <param name="Skipped">Presentation attempts declined without an error; zero for other phases.</param>
/// <param name="Total">CPU wall time across finished attempts, including failures and skips.</param>
/// <param name="Last">CPU wall time of the last finished attempt.</param>
/// <param name="Maximum">Largest finished attempt.</param>
public readonly record struct UiPhaseSnapshot(long Attempts, long Completed, long Failed, long Skipped, TimeSpan Total, TimeSpan Last, TimeSpan Maximum);

/// <summary>FIFO statistics since diagnostics were enabled. Owner-inline InvokeAsync calls bypass this queue.</summary>
/// <param name="InitialPending">Commands already queued when observation began.</param>
/// <param name="Pending">Currently queued commands, excluding a callback already running.</param>
/// <param name="HighWater">Largest observed pending depth, including the initial backlog.</param>
/// <param name="Accepted">Commands accepted after observation began.</param>
/// <param name="Started">Callbacks removed for execution.</param>
/// <param name="Completed">Callbacks that returned normally.</param>
/// <param name="Failed">Callbacks that threw, including exceptions captured by InvokeAsync tasks.</param>
/// <param name="Canceled">Accepted pending commands removed by cancellation.</param>
/// <param name="Rejected">Commands rejected by capacity or closed-queue checks; pre-canceled requests are excluded.</param>
/// <param name="Discarded">Pending commands removed by disposal or terminal graphics failure.</param>
public readonly record struct UiQueueSnapshot(int InitialPending, int Pending, int HighWater, long Accepted, long Started, long Completed, long Failed, long Canceled, long Rejected, long Discarded);

/// <summary>An immutable, thread-safe observation without tree, callback, device or exception references.</summary>
/// <param name="SessionId">Process-local stable session identity.</param>
/// <param name="WindowId">Optional process-local Hosting window identity supplied at enable time.</param>
/// <param name="State">Current session lifetime; Suspended describes viewport size, not idle scheduling.</param>
/// <param name="Viewport">Last accepted viewport.</param>
/// <param name="RenderingFailure">First terminal failure classification, retained after disposal.</param>
/// <param name="Queue">FIFO counters.</param>
/// <param name="Update">Whole Update timings, including dispatch and nested phases.</param>
/// <param name="Measure">Root measure invocation timings; retained element caches may skip subtree work.</param>
/// <param name="Arrange">Root arrange invocation timings.</param>
/// <param name="Render">Surface render timings.</param>
/// <param name="Present">Host-reported presentation timings, excluding Update and FramePresented observers.</param>
public readonly record struct UiDiagnosticsSnapshot(long SessionId, long? WindowId, UiSessionState State, UiViewport Viewport,
    UiRenderingFailureKind? RenderingFailure, UiQueueSnapshot Queue, UiPhaseSnapshot Update, UiPhaseSnapshot Measure,
    UiPhaseSnapshot Arrange, UiPhaseSnapshot Render, UiPhaseSnapshot Present);

/// <summary>Opt-in, fixed-storage session counters and CPU timings. Snapshot is safe on any thread and after session disposal.</summary>
public sealed class UiDiagnostics
{
    private readonly object _gate = new();
    private readonly long _sessionId;
    private readonly long? _windowId;
    private readonly UiPhaseSnapshot[] _phases = new UiPhaseSnapshot[5];
    private UiQueueSnapshot _queue;
    private UiSessionState _state;
    private UiViewport _viewport;
    private UiRenderingFailureKind? _failure;

    internal UiDiagnostics(long sessionId, long? windowId) { _sessionId = sessionId; _windowId = windowId; }

    /// <summary>Copies a consistent observation. No history or per-frame sample collection is retained.</summary>
    public UiDiagnosticsSnapshot Snapshot
    {
        get
        {
            lock (_gate) return new(_sessionId, _windowId, _state, _viewport, _failure, _queue,
                _phases[0], _phases[1], _phases[2], _phases[3], _phases[4]);
        }
    }

    /// <summary>Reports one completed host presentation attempt. Does not render, present or change session lifetime.</summary>
    /// <param name="elapsed">Nonnegative CPU wall time measured around host output only.</param>
    /// <param name="outcome">Presented, skipped or failed; no GPU completion is implied.</param>
    public void RecordPresentation(TimeSpan elapsed, UiPresentationOutcome outcome)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        lock (_gate)
        {
            _phases[4] = _phases[4] with { Attempts = _phases[4].Attempts + 1 };
            RecordEnd(4, elapsed, outcome);
        }
    }

    internal void State(UiSessionState state, UiViewport viewport, UiRenderingFailureKind? failure)
    { lock (_gate) { _state = state; _viewport = viewport; _failure = failure; } }

    internal void InitializeQueue(int pending)
    { lock (_gate) _queue = new(pending, pending, pending, 0, 0, 0, 0, 0, 0, 0); }

    internal void Queue(UiQueueEvent change, int pending, int count = 1)
    {
        lock (_gate)
        {
            _queue = _queue with
            {
                Pending = pending, HighWater = Math.Max(_queue.HighWater, pending),
                Accepted = _queue.Accepted + (change == UiQueueEvent.Accepted ? count : 0),
                Started = _queue.Started + (change == UiQueueEvent.Started ? count : 0),
                Completed = _queue.Completed + (change == UiQueueEvent.Completed ? count : 0),
                Failed = _queue.Failed + (change == UiQueueEvent.Failed ? count : 0),
                Canceled = _queue.Canceled + (change == UiQueueEvent.Canceled ? count : 0),
                Rejected = _queue.Rejected + (change == UiQueueEvent.Rejected ? count : 0),
                Discarded = _queue.Discarded + (change == UiQueueEvent.Discarded ? count : 0)
            };
        }
    }

    internal long Begin(UiPhase phase)
    {
        lock (_gate) _phases[(int)phase] = _phases[(int)phase] with { Attempts = _phases[(int)phase].Attempts + 1 };
        return Stopwatch.GetTimestamp();
    }

    internal void End(UiPhase phase, long started, bool completed)
    {
        var elapsed = Stopwatch.GetElapsedTime(started);
        lock (_gate) RecordEnd((int)phase, elapsed, completed ? UiPresentationOutcome.Presented : UiPresentationOutcome.Failed);
    }

    private void RecordEnd(int phase, TimeSpan elapsed, UiPresentationOutcome outcome)
    {
        var previous = _phases[phase];
        _phases[phase] = previous with
        {
            Completed = previous.Completed + (outcome == UiPresentationOutcome.Presented ? 1 : 0),
            Failed = previous.Failed + (outcome == UiPresentationOutcome.Failed ? 1 : 0),
            Skipped = previous.Skipped + (outcome == UiPresentationOutcome.Skipped ? 1 : 0),
            Total = previous.Total + elapsed, Last = elapsed, Maximum = elapsed > previous.Maximum ? elapsed : previous.Maximum
        };
    }
}

internal enum UiPhase { Update, Measure, Arrange, Render }
internal enum UiQueueEvent { Accepted, Started, Completed, Failed, Canceled, Rejected, Discarded }
