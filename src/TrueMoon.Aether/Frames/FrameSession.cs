using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Builds;
using TrueMoon.Diagnostics;

namespace TrueMoon.Aether.Frames;

public sealed record FrameDisplay(FrameSurface Surface, FrameReceiver Receiver, TaskCompletionSource Presented);

// Owns local readers/mappings; SessionSupervisor continues to own the child/RPC.
public sealed class FrameSession(SessionSupervisor supervisor, IGameViewportTransport? transport = null,
    IFactory<FrameReceiver, (SharedFrameRing Ring, ulong After, LatencySamples Samples)>? receivers = null) : IAsyncDisposable
{
    private readonly IEventsSource? _events;
    private readonly IDiagnosticTelemetry? _telemetry;
    /// <summary>Creates a frame owner borrowing App diagnostics; ownership of session and transport is unchanged.</summary>
    public FrameSession(SessionSupervisor supervisor, IGameViewportTransport? transport,
        IFactory<FrameReceiver, (SharedFrameRing Ring, ulong After, LatencySamples Samples)>? receivers,
        IEventsSource? events, IDiagnosticTelemetry? telemetry) : this(supervisor, transport, receivers)
    { _events = events; _telemetry = telemetry; }

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<SharedFrameRing> _rings = [];
    private readonly LatencySamples _copies = new();
    private readonly object _workGate = new();
    private int _fence;
    private bool _closing;
    private CancellationTokenSource _work = new();
    private FrameDisplay? _display;
    private FrameSurface? _surface;
    private ViewportPresentation? _presentation;
    private IGameViewportConnection? _connection;
    private bool _transportReleased;
    private ulong _surfaceGeneration, _lastPresented, _lastReceived;
    private Task _watch = Task.CompletedTask;
    private Task? _dispose;
    private Exception? _transportError;
    /// <summary>Retained delivery/cleanup failure, including background errors; cleared by the next admitted Start.</summary>
    public Exception? TransportError => Volatile.Read(ref _transportError);
    public FrameDisplay? Display
    {
        get
        {
            var display = Volatile.Read(ref _display); var status = supervisor.Status;
            return status.State is SessionState.Paused or SessionState.Playing && display?.Surface.Identity == status.Identity ? display : null;
        }
    }
    public SessionSupervisor Supervisor => supervisor;
    /// <summary>Selected transport. CPU remains the default when no strategy was configured.</summary>
    public ViewportTransportKind TransportKind => transport?.Kind ?? ViewportTransportKind.Cpu;
    /// <summary>Current transport-independent presentation; hidden after runtime exit or identity change.</summary>
    public ViewportPresentation? Presentation
    {
        get
        {
            var value = Volatile.Read(ref _presentation); var status = supervisor.Status;
            return status.State is SessionState.Paused or SessionState.Playing && value?.Surface.Identity == status.Identity ? value : null;
        }
    }
    /// <summary>Acknowledges a ticket only after the consumer's actual UI draw/presentation on the active surface.</summary>
    public bool Presented(ViewportFrame frame)
    { var active = Presentation; return active != null && ReferenceEquals(frame?.Presentation, active) && active.Presented(frame); }
    public FrameSurface? Surface => supervisor.Status.State is SessionState.Paused or SessionState.Playing ? Volatile.Read(ref _surface) : null;
    public int MappingCount => _rings.Count;
    public int PeakMappings { get; private set; }
    public long PeakMappingBytes { get; private set; }
    public LatencyReport CopyLatency => _copies.Report();
    public long Received { get; private set; }
    public long Replaced { get; private set; }
    public Task StartAsync(SceneSnapshot scene, int width, int height, string behavior = "normal", CancellationToken token = default)
        => StartCoreAsync(scene, null, null, width, height, behavior, token);

    internal Task StartPreviewAsync(SceneSnapshot scene, int width, int height, CancellationToken token)
        => StartCoreAsync(scene, null, null, width, height, "normal", token, role: RuntimeRole.EditPreview);

    /// <summary>Starts a selected artifact and waits for its acknowledged first presentation.</summary>
    public Task StartAsync(SceneSnapshot scene, GameBuildArtifact build, int width, int height, string behavior = "normal", CancellationToken token = default)
    { ArgumentNullException.ThrowIfNull(build); return StartCoreAsync(scene, build, null, width, height, behavior, token); }

    /// <summary>Restores a fresh fixed-build runtime before configuring a surface or accepting its first frame.</summary>
    public Task StartAsync(SceneSnapshot scene, GameCheckpoint checkpoint, int width, int height, string behavior = "normal", CancellationToken token = default)
    { ArgumentNullException.ThrowIfNull(checkpoint); return StartCoreAsync(scene, null, checkpoint, width, height, behavior, token); }

    /// <summary>Restores a compatible selected artifact before surface/first-presentation readiness.</summary>
    public Task StartAsync(SceneSnapshot scene, GameBuildArtifact build, GameCheckpoint checkpoint, int width, int height, string behavior = "normal", CancellationToken token = default)
    { ArgumentNullException.ThrowIfNull(build); ArgumentNullException.ThrowIfNull(checkpoint); return StartCoreAsync(scene, build, checkpoint, width, height, behavior, token); }

    internal Task StartForReplacementAsync(SceneSnapshot scene, GameBuildArtifact? build, GameCheckpoint checkpoint, int width, int height,
        string behavior, ReplacementAdmission.Lease owner, CancellationToken token)
        => StartCoreAsync(scene, build, checkpoint, width, height, behavior, token, owner);
    internal async Task<ReplacementAdmission.Lease> ReserveReplacementAsync(ReplacementDeadlines deadlines, CancellationToken token)
    {
        var frameFence = Volatile.Read(ref _fence); var controlFence = supervisor.StopVersion;
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_closing, this); supervisor.EnsureReplacementOpen();
            if (frameFence != Volatile.Read(ref _fence) || controlFence != supervisor.StopVersion)
                throw new OperationCanceledException("Queued replacement superseded by Stop.");
            return supervisor.Admission.Begin(deadlines);
        }
        finally { _gate.Release(); }
    }
    internal async Task<ViewportSurface?> SurfaceForReplacementAsync(ReplacementAdmission.Lease owner, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try { supervisor.Admission.Check(owner); return Presentation?.Surface; }
        finally { _gate.Release(); }
    }
    private async Task StartCoreAsync(SceneSnapshot scene, GameBuildArtifact? build, GameCheckpoint? checkpoint, int width, int height, string behavior,
        CancellationToken token, ReplacementAdmission.Lease? owner = null, RuntimeRole role = RuntimeRole.Game)
    {
        var fence = Volatile.Read(ref _fence);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            lock (_workGate)
            {
                ObjectDisposedException.ThrowIf(_closing, this);
                supervisor.Admission.Check(owner);
                if (fence != _fence) throw new OperationCanceledException("Frame start superseded by Stop.");
                if (_rings.Count != 0 || _connection != null || Display != null || supervisor.Status.State is not (SessionState.Stopped or SessionState.Faulted))
                    throw new InvalidOperationException("Stop/reap the previous surface first.");
                _work.Dispose(); _work = new();
            }
            _lastPresented = 0; _lastReceived = 0;
            Volatile.Write(ref _transportError, null);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token, _work.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                if (role == RuntimeRole.EditPreview)
                    await supervisor.StartPreviewAsync(scene, deadline.Token).ConfigureAwait(false);
                else if (owner != null)
                    await supervisor.StartForReplacementAsync(scene, build, checkpoint!, behavior, owner, deadline.Token).ConfigureAwait(false);
                else if (build == null)
                {
                    if (checkpoint == null) await supervisor.StartAsync(scene, behavior, deadline.Token).ConfigureAwait(false);
                    else await supervisor.StartAsync(scene, checkpoint, behavior, deadline.Token).ConfigureAwait(false);
                }
                else if (checkpoint == null) await supervisor.StartAsync(scene, build, behavior, deadline.Token).ConfigureAwait(false);
                else await supervisor.StartAsync(scene, build, checkpoint, behavior, deadline.Token).ConfigureAwait(false);
                await ConfigureCoreAsync(width, height, deadline.Token, owner).ConfigureAwait(false);
            }
            catch (Exception e) { await FaultCoreAsync("FirstFrame", e).ConfigureAwait(false); throw; }
        }
        finally { _gate.Release(); }
    }
    public async Task ResizeAsync(int width, int height, CancellationToken token = default)
    {
        var fence = Volatile.Read(ref _fence);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            supervisor.Admission.Check(null);
            if (fence != Volatile.Read(ref _fence)) throw new OperationCanceledException("Resize superseded by Stop.");
            if (supervisor.Status.State is not (SessionState.Paused or SessionState.Playing)) throw new InvalidOperationException("No active runtime to resize.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token, _work.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            try { await ConfigureCoreAsync(width, height, deadline.Token).ConfigureAwait(false); }
            catch (Exception e) { await FaultCoreAsync("Surface", e).ConfigureAwait(false); throw; }
        }
        finally { _gate.Release(); }
    }
    private async Task DrainReceiverAsync()
    {
        var old = Interlocked.Exchange(ref _display, null);
        if (old == null) return;
        old.Presented.TrySetCanceled(); await old.Receiver.DisposeAsync().ConfigureAwait(false);
        _lastReceived = Math.Max(_lastReceived, old.Receiver.LastFrameId);
        Received += old.Receiver.Received; Replaced += old.Receiver.Replaced;
    }
    private async Task ConfigureCoreAsync(int width, int height, CancellationToken token, ReplacementAdmission.Lease? owner = null)
    {
        var identity = supervisor.Status.Identity ?? throw new InvalidOperationException("No runtime.");
        Interlocked.Exchange(ref _presentation, null)?.Close();
        if (transport != null)
        {
            var viewport = new ViewportSurface(identity, checked(++_surfaceGeneration), width, height, TransportKind); viewport.Validate();
            _connection ??= await transport.ConnectAsync(supervisor.ActiveRuntime ?? throw new InvalidOperationException("No owned runtime."), token).ConfigureAwait(false);
            var presentation = new ViewportPresentation(viewport); Volatile.Write(ref _presentation, presentation);
            await _connection.ConfigureAsync(presentation, token).ConfigureAwait(false);
            if (!viewport.Suspended)
            {
                _watch = WatchTransportAsync(presentation, _connection.Completion, _work.Token);
                await presentation.FirstPresented.WaitAsync(token).ConfigureAwait(false);
            }
            return;
        }
        var surface = FrameSurface.Create(identity, checked(++_surfaceGeneration), width, height);
        // Free old private copies BEFORE allocating the replacement's two copies.
        await DrainReceiverAsync().ConfigureAwait(false);
        SharedFrameRing? ring = null;
        if (!surface.Suspended)
        {
            ring = SharedFrameRing.Create(surface); _rings.Add(ring);
            PeakMappings = Math.Max(PeakMappings, _rings.Count);
            PeakMappingBytes = Math.Max(PeakMappingBytes, _rings.Sum(r => r.Surface.MappingBytes));
        }
        if (owner == null) await supervisor.ConfigureSurfaceAsync(surface, token).ConfigureAwait(false);
        else await supervisor.SurfaceForReplacementAsync(surface, owner, token).ConfigureAwait(false);
        Volatile.Write(ref _surface, surface);
        // Ack proves old producer detached; the local receiver already drained.
        foreach (var old in _rings.Where(r => r != ring).ToArray()) { old.Dispose(); _rings.Remove(old); }
        if (ring == null)
        { Volatile.Write(ref _presentation, new(new(identity, surface.Generation, width, height, ViewportTransportKind.Cpu))); return; }
        var receiver = (receivers ?? new Factory<FrameReceiver, (SharedFrameRing Ring, ulong After, LatencySamples Samples)>(args =>
            new(args.Ring, args.After, args.Samples))).Create((ring, Math.Max(Volatile.Read(ref _lastPresented), _lastReceived), _copies));
        var display = new FrameDisplay(surface, receiver, new(TaskCreationOptions.RunContinuationsAsynchronously));
        Volatile.Write(ref _presentation, new(new(identity, surface.Generation, width, height, ViewportTransportKind.Cpu), display.Presented));
        Volatile.Write(ref _display, display);
        _watch = WatchAsync(display, _work.Token);
        await receiver.FirstFrame.WaitAsync(token).ConfigureAwait(false);
        await display.Presented.Task.WaitAsync(token).ConfigureAwait(false);
    }
    public void Presented(FrameDisplay display, FrameMetadata metadata)
    {
        if (!ReferenceEquals(Display, display) || supervisor.Status.Identity != display.Surface.Identity ||
            supervisor.Status.State is not (SessionState.Paused or SessionState.Playing)) return;
        Volatile.Write(ref _lastPresented, Math.Max(_lastPresented, metadata.FrameId));
        display.Presented.TrySetResult();
    }
    private async Task WatchAsync(FrameDisplay display, CancellationToken token)
    {
        try
        {
            while (!display.Receiver.Completion.IsCompleted)
            {
                await Task.WhenAny(display.Receiver.Completion, Task.Delay(50, token)).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (supervisor.Status.State is SessionState.Faulted or SessionState.CleanupFailed)
                    throw new IOException("Runtime ended while a frame surface was active.");
            }
            await display.Receiver.Completion.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception e)
        {
            display.Presented.TrySetException(e);
            await _gate.WaitAsync().ConfigureAwait(false);
            try { if (!token.IsCancellationRequested && ReferenceEquals(Volatile.Read(ref _display), display)) await FaultCoreAsync("FrameTransport", e).ConfigureAwait(false); }
            finally { _gate.Release(); }
        }
    }
    private async Task FaultCoreAsync(string phase, Exception error)
    {
        var status = supervisor.Status;
        var generation = Presentation?.Surface.Generation;
        IReadOnlyDictionary<string, string?> Properties()
        {
            var properties = AetherDiagnosticEvents.Context(status);
            properties["Phase"] = phase;
            properties["SurfaceGeneration"] = generation?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            properties["Transport"] = TransportKind.ToString();
            return properties;
        }
        using var operation = AetherDiagnosticEvents.Start(_telemetry, "Aether.FrameFault", Properties);
        operation?.Complete(DiagnosticOperationOutcome.Failed, error);
        AetherDiagnosticEvents.Emit(_events, DiagnosticLevel.Error, 1500, "Frame.Fault", "Viewport delivery failed", Properties, error);
        try
        {
            await DrainFaultCoreAsync(phase, error).ConfigureAwait(false);
            AetherDiagnosticEvents.Emit(_events, DiagnosticLevel.Information, 1501, "Frame.FaultDrained", "Viewport fault drained", Properties);
        }
        catch (Exception cleanup)
        {
            AetherDiagnosticEvents.Emit(_events, DiagnosticLevel.Error, 1503, "Frame.FaultCleanupFailed", "Viewport fault cleanup failed", Properties, cleanup);
            throw;
        }
    }
    private async Task DrainFaultCoreAsync(string phase, Exception error)
    {
        Volatile.Write(ref _transportError, error);
        Interlocked.Exchange(ref _presentation, null)?.Close(error);
        _work.Cancel(); await DrainReceiverAsync().ConfigureAwait(false);
        Volatile.Write(ref _surface, null);
        try { await DrainConnectionAsync().ConfigureAwait(false); }
        catch (Exception cleanup)
        {
            var failure = new AggregateException("Viewport failed and its connection remains owned until cleanup retry.", error, cleanup);
            Volatile.Write(ref _transportError, failure); throw failure;
        }
        if (supervisor.Status.Identity is { } identity) await supervisor.ReportFrameFaultAsync(identity, phase, error).ConfigureAwait(false);
        // Retain mappings if ownership/reap remains uncertain; retry Stop must finish it.
        if (supervisor.Status.State != SessionState.CleanupFailed) CloseRings();
    }
    internal async Task DrainReplacementFailureAsync(Exception error, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try { await FaultCoreAsync("Replacement", error).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    private void CloseRings() { foreach (var ring in _rings) ring.Dispose(); _rings.Clear(); }
    private async Task DrainConnectionAsync()
    { if (_connection != null) { await _connection.DisposeAsync().ConfigureAwait(false); _connection = null; } }
    private async Task WatchTransportAsync(ViewportPresentation presentation, Task completion, CancellationToken token)
    {
        try { await completion.ConfigureAwait(false); if (!token.IsCancellationRequested) throw new IOException("Viewport delivery ended unexpectedly."); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            presentation.Close(error);
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!token.IsCancellationRequested && ReferenceEquals(Volatile.Read(ref _presentation), presentation))
                    try { await FaultCoreAsync("FrameTransport", error).ConfigureAwait(false); }
                    catch (Exception cleanup) { Volatile.Write(ref _transportError, cleanup); } // Ownership remains retained; explicit Stop retries it.
            }
            finally { _gate.Release(); }
        }
    }
    public Task StopAsync() { supervisor.Admission.Interrupt(); return StopCoreAsync(); }
    internal Task StopForReplacementAsync(CancellationToken token) => StopCoreAsync(token);
    private async Task StopCoreAsync(CancellationToken token = default)
    {
        lock (_workGate) { Interlocked.Increment(ref _fence); _work.Cancel(); }
        Interlocked.Exchange(ref _presentation, null)?.Close();
        await _gate.WaitAsync(token).ConfigureAwait(false);
        Task watch;
        try
        {
            watch = _watch; await DrainReceiverAsync().ConfigureAwait(false); Volatile.Write(ref _surface, null);
            await DrainConnectionAsync().ConfigureAwait(false);
            await supervisor.StopForReplacementAsync(token).ConfigureAwait(false); CloseRings();
        }
        finally { _gate.Release(); }
        await watch.ConfigureAwait(false);
    }
    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completion;
        lock (_workGate)
        {
            _closing = true;
            if (_dispose is { IsCompletedSuccessfully: true } or { IsCompleted: false }) return new(_dispose);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously); _dispose = completion.Task;
        }
        _ = DisposeCoreAsync(); return new(completion.Task);
        async Task DisposeCoreAsync()
        {
            try { await DisposeOwnedAsync().ConfigureAwait(false); completion.TrySetResult(); }
            catch (Exception error) { completion.TrySetException(error); }
        }
    }
    private async Task DisposeOwnedAsync()
    {
        supervisor.Admission.Interrupt();
        await supervisor.Admission.DrainAsync().ConfigureAwait(false);
        await StopAsync().ConfigureAwait(false);
        if (!_transportReleased)
        {
            if (transport is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            else if (transport is IDisposable disposable) disposable.Dispose();
            _transportReleased = true;
        }
    }
}
