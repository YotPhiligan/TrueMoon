using System.Diagnostics;
using System.IO.Pipes;
using TrueMoon.Aether.Gpu.OpenGl;
using TrueMoon.Aether.Hosting;

namespace TrueMoon.Aether.Gpu.Surfaces;

/// <summary>Diagnostic readback plus the matching CPU-observed GL completion; return it explicitly to retire the slot.</summary>
public sealed record GpuSurfaceReadback(byte[] Pixels, GpuFrameCompletion Completion);

/// <summary>App-owned three-slot GPU session on a borrowed GL owner. Stop this session before stopping/replacing its runtime.</summary>
public sealed class GpuSurfaceSession : IGpuResource
{
    private readonly NativeRuntimeLease _runtime;
    private readonly GpuAdapterId _adapter;
    private readonly GpuOpenGlContext _context;
    private readonly NamedPipeClientStream _pipe;
    private readonly CancellationTokenSource _end;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _state = new();
    private readonly Task _watch;
    private Task? _dispose;
    private GpuSurfaceImports? _imports;
    private GpuSurfaceOffer? _surface;
    private GpuSlotLedger? _ledger;
    private ulong _generation, _sequence;
    private int _closed;
    private bool _configuring;
    internal GpuSurfaceSession(NativeRuntimeLease runtime, GpuAdapterId adapter, GpuOpenGlContext context, CancellationToken hostEnd)
    {
        ArgumentNullException.ThrowIfNull(runtime); ArgumentNullException.ThrowIfNull(adapter); ArgumentNullException.ThrowIfNull(context);
        _runtime = runtime; _adapter = adapter; _context = context;
        _pipe = new(".", runtime.GpuEndpoint + "-surface", PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        _end = CancellationTokenSource.CreateLinkedTokenSource(hostEnd); _watch = WatchExitAsync();
    }
    /// <summary>Current immutable allocation set; null before configure, during suspend or after cleanup.</summary>
    public GpuSurfaceOffer? Surface => Volatile.Read(ref _surface);
    /// <summary>Last acknowledged generation; suspended configurations also advance it.</summary>
    public ulong Generation => Volatile.Read(ref _generation);
    /// <summary>Whether the session remains open; runtime death fences it immediately.</summary>
    public bool IsAvailable => Volatile.Read(ref _closed) == 0 && !_runtime.Exited.IsCompleted && !_end.IsCancellationRequested;
    /// <summary>Current immutable slot snapshots, or an empty list when no surface is allocated.</summary>
    public IReadOnlyList<GpuSlotSnapshot> Slots => _ledger?.Slots ?? [];
    /// <summary>True after every local import, command and exit watcher has drained.</summary>
    public bool IsReleased { get; private set; }
    /// <summary>Retained cleanup error; cleared by an explicit successful Dispose retry.</summary>
    public Exception? CleanupError { get; private set; }
    internal async Task ConnectAsync(CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try { Guard(); using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _end.Token); await _pipe.ConnectAsync(3000, linked.Token).ConfigureAwait(false); Guard(); }
        finally { _gate.Release(); }
    }
    /// <summary>Drains old slots/imports and acknowledged exporter detach before a fresh generation. 0x0 suspends.</summary>
    public async Task ConfigureAsync(int width, int height, CancellationToken token = default)
    {
        GpuSurfaceOffer.ValidateExtent(width, height);
        lock (_state) { Guard(); if (_configuring) throw new InvalidOperationException("GPU configuration is already pending."); _configuring = true; }
        var entered = false;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _end.Token);
            await _gate.WaitAsync(linked.Token).ConfigureAwait(false); entered = true; Guard();
            if (_surface != null)
            {
                foreach (var slot in _ledger!.Slots)
                {
                    if (slot.Phase == "Ready") await UseCoreAsync(new(slot.Stamp!, true), _ => { }, linked.Token).ConfigureAwait(false);
                    var current = _ledger.Slots[slot.Slot];
                    if (current.Phase == "Complete") await ReturnCoreAsync(new(current.Stamp!, true), linked.Token).ConfigureAwait(false);
                }
                if (!_ledger.IsIdle) throw new InvalidOperationException("Old GPU surface has unconfirmed use.");
                await _imports!.DisposeAsync().ConfigureAwait(false); _imports = null;
                await ExchangeAsync(new(0, "Detach", _runtime.Identity, _generation), "Detached", linked.Token).ConfigureAwait(false);
                _surface = null; _ledger = null;
            }
            var generation = checked(_generation + 1);
            var reply = await ExchangeAsync(new(0, "Configure", _runtime.Identity, generation, width, height, _adapter), width == 0 ? "Suspended" : "Surface", linked.Token).ConfigureAwait(false);
            _generation = generation;
            if (width == 0) return;
            var surface = reply.Offer ?? throw new InvalidDataException("Missing reusable GPU allocation set.");
            surface.Validate(_runtime.Identity, _adapter, generation, width, height);
            _imports = new(surface, _context); await _context.Invoke(() => _imports.Initialize(_runtime)).ConfigureAwait(false);
            await ExchangeAsync(new(0, "Imported", _runtime.Identity, generation), "Imported", linked.Token).ConfigureAwait(false);
            _ledger = new(_runtime.Identity, generation); _ledger.Imported(); _surface = surface; Guard();
        }
        catch { if (entered) Fence(); throw; }
        finally { if (entered) _gate.Release(); lock (_state) _configuring = false; }
    }
    /// <summary>Produces the next use of an idle slot. Busy slots/concurrent commands are rejected without queueing.</summary>
    public async Task<GpuFrameCompletion> ProduceAsync(int slot, CancellationToken token = default)
        => (await ProduceFrameAsync(slot, token).ConfigureAwait(false)).Completion;
    /// <summary>Produces an idle slot and returns simulation metadata supplied by the runtime, if any.</summary>
    public async Task<GpuProducedFrame> ProduceFrameAsync(int slot, CancellationToken token = default)
    {
        await EnterAsync(token).ConfigureAwait(false);
        try
        {
            var ledger = RequireSurface(); var stamp = ledger.Begin(slot);
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _end.Token);
                var reply = await ExchangeAsync(new(0, "Produce", _runtime.Identity, _generation, Completion: new(stamp, false)), "Ready", linked.Token).ConfigureAwait(false);
                var ready = reply.Completion ?? throw new InvalidDataException("Missing surface ready completion."); ledger.Produced(ready);
                if (reply.Metadata is { } metadata && (metadata.Slot != slot || metadata.FrameId != GpuFrameData.FrameNumber(stamp) || metadata.ProducedTimestamp <= 0))
                    throw new InvalidDataException("GPU runtime frame metadata mismatch.");
                Guard(); return new(ready, reply.Metadata);
            }
            catch { Fence(); throw; }
        }
        finally { _gate.Release(); }
    }
    /// <summary>Reads the exact ready frame and polls its local GL fence without blocking the owner on a fence wait.</summary>
    public async Task<GpuSurfaceReadback> ReadAsync(GpuFrameCompletion ready, CancellationToken token = default)
    {
        await EnterAsync(token).ConfigureAwait(false);
        try { return await ReadCoreAsync(ready, token).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    /// <summary>Loans the ready texture on its GL owner, then observes the fence after the callback's last submitted use.</summary>
    /// <param name="ready">Matching CPU-observed producer completion.</param>
    /// <param name="consume">Synchronous callback. Submit all native/renderer work before returning or throwing; do not retain the texture.</param>
    /// <param name="token">Cancellation fences accepted work and retains imports until safe cleanup.</param>
    /// <returns>Observed GL completion; explicitly ReturnAsync to retire the producer slot. This is not a presentation acknowledgement.</returns>
    public async Task<GpuFrameCompletion> UseTextureAsync(GpuFrameCompletion ready, Action<GpuOpenGlTexture> consume, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(consume);
        await EnterAsync(token).ConfigureAwait(false);
        try { return await UseCoreAsync(ready, consume, token).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    private async Task<GpuFrameCompletion> UseCoreAsync(GpuFrameCompletion ready, Action<GpuOpenGlTexture> consume, CancellationToken token)
    {
        var ledger = RequireSurface(); ledger.BeginRead(ready);
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _end.Token); linked.Token.ThrowIfCancellationRequested();
            await _context.Invoke(() => { Guard(); linked.Token.ThrowIfCancellationRequested(); _imports!.Use(ready, consume, Guard); }).ConfigureAwait(false);
            var completion = await PollAsync(ready.Stamp.Slot, linked.Token).ConfigureAwait(false);
            ledger.ReadCompleted(completion); Guard(); return completion;
        }
        catch { Fence(); throw; }
    }
    private async Task<GpuFrameCompletion> PollAsync(int slot, CancellationToken token)
    {
        GpuFrameCompletion? completion = null; var clock = Stopwatch.StartNew();
        while (completion == null)
        {
            token.ThrowIfCancellationRequested(); await _context.Invoke(() => completion = _imports!.Poll(slot)).ConfigureAwait(false);
            if (clock.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("GPU surface GL completion remains unconfirmed.");
            if (completion == null) await Task.Delay(2, token).ConfigureAwait(false);
        }
        return completion;
    }
    private async Task<GpuSurfaceReadback> ReadCoreAsync(GpuFrameCompletion ready, CancellationToken token)
    {
        var ledger = RequireSurface(); ledger.BeginRead(ready);
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _end.Token); linked.Token.ThrowIfCancellationRequested();
            byte[] pixels = null!; await _context.Invoke(() => { Guard(); pixels = _imports!.Read(ready); }).ConfigureAwait(false);
            var completion = await PollAsync(ready.Stamp.Slot, linked.Token).ConfigureAwait(false);
            ledger.ReadCompleted(completion); Guard(); return new(pixels, completion);
        }
        catch { Fence(); throw; }
    }
    /// <summary>Retires the exact read-complete slot after a matching observed Vulkan return fence.</summary>
    public async Task<GpuFrameCompletion> ReturnAsync(GpuFrameCompletion completion, CancellationToken token = default)
    {
        await EnterAsync(token).ConfigureAwait(false);
        try { return await ReturnCoreAsync(completion, token).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    private async Task<GpuFrameCompletion> ReturnCoreAsync(GpuFrameCompletion completion, CancellationToken token)
    {
        var ledger = RequireSurface(); ledger.Return(completion);
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _end.Token);
            var reply = await ExchangeAsync(new(0, "Return", _runtime.Identity, _generation, Completion: completion), "Retired", linked.Token).ConfigureAwait(false);
            var retired = reply.Completion ?? throw new InvalidDataException("Missing surface retirement completion."); ledger.Retired(retired); Guard(); return retired;
        }
        catch { Fence(); throw; }
    }
    private async Task EnterAsync(CancellationToken token)
    {
        lock (_state) { Guard(); if (_configuring) throw new InvalidOperationException("GPU surface is reconfiguring."); }
        if (!await _gate.WaitAsync(0, token).ConfigureAwait(false)) throw new InvalidOperationException("GPU surface operation already pending.");
        try { Guard(); lock (_state) if (_configuring) throw new InvalidOperationException("GPU surface is reconfiguring."); }
        catch { _gate.Release(); throw; }
    }
    private GpuSlotLedger RequireSurface() { Guard(); return _ledger ?? throw new InvalidOperationException("GPU surface is suspended/not configured."); }
    private void Guard() => ObjectDisposedException.ThrowIf(!IsAvailable, this);
    private async Task<GpuSurfaceMessage> ExchangeAsync(GpuSurfaceMessage request, string expected, CancellationToken token)
    {
        Guard(); using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(8));
        request = request with { Sequence = checked(++_sequence) };
        await GpuWire.WriteMessageAsync(_pipe, request, deadline.Token).ConfigureAwait(false);
        var reply = await GpuWire.ReadMessageAsync<GpuSurfaceMessage>(_pipe, deadline.Token).ConfigureAwait(false);
        if (reply.Identity != _runtime.Identity || reply.Sequence != request.Sequence || reply.Generation != request.Generation || reply.Kind != expected)
            throw new InvalidDataException("GPU surface reply mismatch/error: " + reply.Error);
        return reply;
    }
    private async Task WatchExitAsync()
    {
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = _end.Token.Register(() => ended.TrySetResult());
        if (await Task.WhenAny(_runtime.Exited, ended.Task).ConfigureAwait(false) == _runtime.Exited) Fence();
    }
    private void Fence()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0) return;
        _ledger?.Fault(); _end.Cancel(); _pipe.Dispose();
    }
    /// <summary>Fences admission immediately, drains commands/local GPU use and retains failed cleanup for retry. Does not own runtime Stop/reap.</summary>
    public ValueTask DisposeAsync()
    {
        Fence(); TaskCompletionSource completion;
        lock (_state)
        {
            if (_dispose is { IsCompletedSuccessfully: true } or { IsCompleted: false }) return new(_dispose);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously); _dispose = completion.Task;
        }
        _ = CleanupAsync(); return new(completion.Task);
        async Task CleanupAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_imports != null) await _imports.DisposeAsync().ConfigureAwait(false);
                await _watch.ConfigureAwait(false); _imports = null; _ledger = null; _surface = null; _end.Dispose();
                IsReleased = true; CleanupError = null; completion.TrySetResult();
            }
            catch (Exception error) { CleanupError = error; completion.TrySetException(error); }
            finally { _gate.Release(); }
        }
    }
}

internal sealed class GpuSurfaceImports(GpuSurfaceOffer surface, GpuOpenGlContext context) : IGpuResource
{
    private readonly List<OpenGlSharedFrame> _slots = [];
    public bool IsReleased { get; private set; }
    internal void Initialize(NativeRuntimeLease runtime)
    {
        foreach (var offer in surface.Slots) { var frame = new OpenGlSharedFrame(offer, context); _slots.Add(frame); frame.Initialize(runtime); }
    }
    internal byte[] Read(GpuFrameCompletion ready) => _slots[ready.Stamp.Slot].ReadSlot(ready);
    internal void Use(GpuFrameCompletion ready, Action<GpuOpenGlTexture> consume, Action verify) => _slots[ready.Stamp.Slot].UseSlot(ready, consume, verify);
    internal GpuFrameCompletion? Poll(int slot) => _slots[slot].PollCompletion();
    public async ValueTask DisposeAsync()
    {
        if (IsReleased) return; List<Exception> errors = [];
        foreach (var frame in _slots.AsEnumerable().Reverse())
        {
            var clock = Stopwatch.StartNew();
            while (true)
            {
                try { await frame.DisposeAsync().ConfigureAwait(false); break; }
                catch (IOException) when (clock.Elapsed < TimeSpan.FromSeconds(5)) { await Task.Delay(2).ConfigureAwait(false); }
                catch (Exception error) { errors.Add(error); break; }
            }
        }
        if (errors.Count != 0) throw new AggregateException("GL surface imports retained for retry.", errors);
        IsReleased = true;
    }
}
