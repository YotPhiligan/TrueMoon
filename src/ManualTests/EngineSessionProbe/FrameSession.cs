using Engine.Probe.Contracts;

namespace EngineSessionProbe;

public sealed record FrameDisplay(FrameSurface Surface, FrameReceiver Receiver, TaskCompletionSource Presented);

// Owns local readers/mappings; SessionSupervisor continues to own the child/RPC.
public sealed class FrameSession(SessionSupervisor supervisor) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<SharedFrameRing> _rings = [];
    private readonly LatencySamples _copies = new();
    private readonly object _workGate = new();
    private int _fence;
    private bool _closing;
    private CancellationTokenSource _work = new();
    private FrameDisplay? _display;
    private FrameSurface? _surface;
    private ulong _surfaceGeneration, _lastPresented, _lastReceived;
    private Task _watch = Task.CompletedTask;
    public FrameDisplay? Display
    {
        get
        {
            var display = Volatile.Read(ref _display); var status = supervisor.Status;
            return status.State is SessionState.Paused or SessionState.Playing && display?.Surface.Identity == status.Identity ? display : null;
        }
    }
    public SessionSupervisor Supervisor => supervisor;
    public FrameSurface? Surface => supervisor.Status.State is SessionState.Paused or SessionState.Playing ? Volatile.Read(ref _surface) : null;
    public int MappingCount => _rings.Count;
    public int PeakMappings { get; private set; }
    public long PeakMappingBytes { get; private set; }
    public LatencyReport CopyLatency => _copies.Report();
    public long Received { get; private set; }
    public long Replaced { get; private set; }
    public async Task StartAsync(SceneSnapshot scene, int width, int height, string behavior = "normal", CancellationToken token = default)
    {
        var fence = Volatile.Read(ref _fence);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            lock (_workGate)
            {
                ObjectDisposedException.ThrowIf(_closing, this);
                if (fence != _fence) throw new OperationCanceledException("Frame start superseded by Stop.");
                if (_rings.Count != 0 || Display != null || supervisor.Status.State is not (SessionState.Stopped or SessionState.Faulted))
                    throw new InvalidOperationException("Stop/reap the previous surface first.");
                _work.Dispose(); _work = new();
            }
            _lastPresented = 0; _lastReceived = 0;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token, _work.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                await supervisor.StartAsync(scene, behavior, deadline.Token).ConfigureAwait(false);
                await ConfigureCoreAsync(width, height, deadline.Token).ConfigureAwait(false);
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
    private async Task ConfigureCoreAsync(int width, int height, CancellationToken token)
    {
        var identity = supervisor.Status.Identity ?? throw new InvalidOperationException("No runtime.");
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
        await supervisor.ConfigureSurfaceAsync(surface, token).ConfigureAwait(false);
        Volatile.Write(ref _surface, surface);
        // Ack proves old producer detached; the local receiver already drained.
        foreach (var old in _rings.Where(r => r != ring).ToArray()) { old.Dispose(); _rings.Remove(old); }
        if (ring == null) return;
        var receiver = new FrameReceiver(ring, Math.Max(Volatile.Read(ref _lastPresented), _lastReceived), _copies);
        var display = new FrameDisplay(surface, receiver, new(TaskCreationOptions.RunContinuationsAsynchronously));
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
        _work.Cancel(); await DrainReceiverAsync().ConfigureAwait(false);
        Volatile.Write(ref _surface, null);
        if (supervisor.Status.Identity is { } identity) await supervisor.ReportFrameFaultAsync(identity, phase, error).ConfigureAwait(false);
        // Retain mappings if ownership/reap remains uncertain; retry Stop must finish it.
        if (supervisor.Status.State != SessionState.CleanupFailed) CloseRings();
    }
    private void CloseRings() { foreach (var ring in _rings) ring.Dispose(); _rings.Clear(); }
    public async Task StopAsync()
    {
        lock (_workGate) { Interlocked.Increment(ref _fence); _work.Cancel(); }
        await _gate.WaitAsync().ConfigureAwait(false);
        Task watch;
        try
        {
            watch = _watch; await DrainReceiverAsync().ConfigureAwait(false); Volatile.Write(ref _surface, null);
            await supervisor.StopAsync().ConfigureAwait(false); CloseRings();
        }
        finally { _gate.Release(); }
        await watch.ConfigureAwait(false);
    }
    public async ValueTask DisposeAsync()
    {
        lock (_workGate) _closing = true;
        await StopAsync().ConfigureAwait(false);
    }
}
