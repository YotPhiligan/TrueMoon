using System.Diagnostics;
using TrueMoon.Aether.Contracts;
using TrueMoon.Aether.Runtime;

namespace TrueMoon.Aether.Frames;

public sealed class FrameProducer : IDisposable
{
    private readonly object _gate = new();
    private SharedFrameRing? _ring;
    private byte[] _row = [];
    private ulong _generation, _frame;
    private long _dropped;
    public long Dropped { get { lock (_gate) return _dropped + (_ring?.Dropped ?? 0); } }
    public void Configure(FrameSurface surface)
    {
        surface.Validate();
        lock (_gate)
        {
            if (surface.Generation <= _generation) throw new InvalidDataException("Surface generation must increase.");
            var next = surface.Suspended ? null : SharedFrameRing.Open(surface);
            try { _ring?.End(); } catch { next?.Dispose(); throw; }
            _dropped += _ring?.Dropped ?? 0;
            _ring?.Dispose(); _ring = next; _generation = surface.Generation;
            _row = surface.Suspended ? [] : new byte[surface.Stride];
        }
    }
    public async Task RunAsync(RuntimeProtocol protocol, CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1d / 30));
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            // Never take the protocol gate while holding the producer gate.
            if (!protocol.TryCaptureFrame(out var frame)) continue;
            var (tick, color) = frame;
            lock (_gate) _ring?.WritePattern(checked(++_frame), tick, color, _row);
        }
    }
    public void Dispose() { lock (_gate) { try { _ring?.End(); } finally { _ring?.Dispose(); _ring = null; _row = []; } } }
}

// Exactly two reusable private pixel buffers; at most one pending latest frame.
// The UI holds a lease only during its owner-thread image copy.
public sealed class FrameReceiver : IAsyncDisposable
{
    private readonly SharedFrameRing _ring;
    private readonly CancellationTokenSource _end = new();
    private readonly object _gate = new();
    private readonly byte[][] _buffers;
    private readonly int[] _states = new int[2]; // 0 free, 1 worker, 2 pending, 3 UI
    private readonly FrameMetadata?[] _metadata = new FrameMetadata?[2];
    private readonly TaskCompletionSource _first = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly LatencySamples _copies = new();
    private readonly LatencySamples? _aggregateCopies;
    private int _pending = -1;
    private ulong _last;
    private Task? _dispose;
    public Task Completion { get; }
    public Task FirstFrame => _first.Task;
    private long _replaced, _received;
    public long Replaced { get { lock (_gate) return _replaced; } }
    public long Received { get { lock (_gate) return _received; } }
    public LatencyReport CopyLatency => _copies.Report();
    public ulong LastFrameId { get { lock (_gate) return _last; } }
    public FrameReceiver(SharedFrameRing ring, ulong after = 0, LatencySamples? aggregateCopies = null)
    {
        _ring = ring; _last = after; _aggregateCopies = aggregateCopies;
        _buffers = [new byte[ring.Surface.PixelBytes], new byte[ring.Surface.PixelBytes]];
        Completion = Task.Run(Receive);
        _ = Completion.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }
    private void Receive()
    {
        try
        {
            WaitHandle[] waits = [_ring.Ready, _end.Token.WaitHandle];
            while (!_end.IsCancellationRequested)
            {
                int index;
                lock (_gate)
                {
                    index = Array.IndexOf(_states, 0);
                    if (index < 0 && _pending >= 0) { index = _pending; _pending = -1; _replaced++; }
                    if (index >= 0) _states[index] = 1;
                }
                if (index >= 0)
                {
                    var start = Stopwatch.GetTimestamp(); var frame = _ring.CopyLatest(_buffers[index], _last);
                    lock (_gate)
                    {
                        if (frame == null) _states[index] = 0;
                        else
                        {
                            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                            _copies.Add(elapsed); _aggregateCopies?.Add(elapsed);
                            if (_pending >= 0) { _states[_pending] = 0; _replaced++; }
                            _last = frame.FrameId; _received++; _metadata[index] = frame;
                            _states[index] = 2; _pending = index; _first.TrySetResult();
                        }
                    }
                }
                WaitHandle.WaitAny(waits, 33); // notification is only a hint
            }
        }
        catch (Exception e) { _first.TrySetException(e); throw; }
        finally { _first.TrySetCanceled(); }
    }
    public FrameLease? TakeLatest()
    {
        lock (_gate)
        {
            if (_end.IsCancellationRequested || _pending < 0) return null;
            var index = _pending; _pending = -1; _states[index] = 3;
            return new(_buffers[index], _metadata[index]!, () => { lock (_gate) { _states[index] = 0; Monitor.PulseAll(_gate); } });
        }
    }
    public ValueTask DisposeAsync()
    {
        lock (_gate) return new(_dispose ??= DisposeCoreAsync());
    }
    private async Task DisposeCoreAsync()
    {
        _end.Cancel();
        try { await Completion.ConfigureAwait(false); } catch { /* Failure remains observable on Completion. */ }
        var drain = Stopwatch.StartNew();
        while (true)
        {
            lock (_gate)
            {
                if (!_states.Contains(3)) { _pending = -1; _buffers[0] = []; _buffers[1] = []; break; }
            }
            if (drain.Elapsed > TimeSpan.FromSeconds(2)) throw new TimeoutException("UI frame lease did not drain.");
            await Task.Delay(10).ConfigureAwait(false);
        }
        _end.Dispose();
    }
}
public sealed class FrameLease(byte[] pixels, FrameMetadata metadata, Action release) : IDisposable
{
    private Action? _release = release;
    private byte[]? _pixels = pixels;
    public ReadOnlySpan<byte> Pixels => _pixels ?? throw new ObjectDisposedException(nameof(FrameLease));
    public FrameMetadata Metadata => metadata;
    public void Dispose()
    {
        var release = Interlocked.Exchange(ref _release, null);
        if (release == null) return;
        _pixels = null; release();
    }
}
public sealed record LatencyReport(int Samples, double P50Ms, double P95Ms);
public sealed class LatencySamples
{
    private readonly object _gate = new();
    private readonly double[] _values = new double[256];
    private long _count;
    public void Add(double milliseconds) { lock (_gate) _values[_count++ % _values.Length] = milliseconds; }
    public LatencyReport Report()
    {
        lock (_gate)
        {
            var values = _values.Take((int)Math.Min(_count, _values.Length)).Order().ToArray();
            return values.Length == 0 ? new(0, 0, 0) : new(values.Length, values[(values.Length - 1) / 2], values[(int)((values.Length - 1) * .95)]);
        }
    }
}
