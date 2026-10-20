using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Gpu.OpenGl;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Gpu.Surfaces;

/// <summary>Callback-scoped source texture plus the ticket to retain with the independent UI-owned copy.</summary>
public sealed record GpuViewportImage(GpuOpenGlTexture Texture, ViewportFrame Frame);

/// <summary>GPU delivery strategy for the ordinary Editor lifecycle, borrowing the App GPU owner and UI callbacks.</summary>
/// <remarks>Configure with AetherOptions.ViewportTransport. No UI/Skia dependency is required by Aether.
/// The consumer must synchronously copy/submit the source use and acknowledge FrameSession.Presented only after actual presentation.</remarks>
public sealed class GpuViewportTransport : IGameViewportTransport
{
    private readonly GpuResourceHost _owner;
    private readonly Func<(GpuAdapterId Adapter, GpuOpenGlContext Context)> _context;
    private readonly Action<GpuViewportImage> _consume;
    private readonly Func<IRuntimeLease, NativeRuntimeLease> _native;
    /// <summary>Creates a lazy strategy. Callbacks/context remain borrowed until all connections are disposed.</summary>
    /// <param name="owner">Borrowed App GPU resource owner.</param>
    /// <param name="context">Returns an eligible adapter and its already-created GL owner context.</param>
    /// <param name="consume">Synchronous GL-owner callback; retain only an independent image and its frame ticket.</param>
    /// <param name="nativeRuntime">Optional adapter for a decorating runtime lease; must return its actual borrowed owned-process lease.</param>
    public GpuViewportTransport(GpuResourceHost owner, Func<(GpuAdapterId Adapter, GpuOpenGlContext Context)> context,
        Action<GpuViewportImage> consume, Func<IRuntimeLease, NativeRuntimeLease>? nativeRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(owner); ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(consume);
        _owner = owner; _context = context; _consume = consume;
        _native = nativeRuntime ?? (runtime => runtime as NativeRuntimeLease ?? throw new NotSupportedException("GPU viewport requires an owned native runtime."));
    }
    /// <inheritdoc />
    public ViewportTransportKind Kind => ViewportTransportKind.Gpu;
    /// <inheritdoc />
    public async Task<IGameViewportConnection> ConnectAsync(IRuntimeLease runtime, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(runtime); token.ThrowIfCancellationRequested();
        var native = _native(runtime); var (adapter, context) = _context();
        var session = await _owner.ConnectSurfaceAsync(native, adapter, context, token).ConfigureAwait(false);
        return new Connection(session, native, _consume);
    }

    private sealed class Connection(GpuSurfaceSession session, NativeRuntimeLease runtime, Action<GpuViewportImage> consume) : IGameViewportConnection
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly CancellationTokenSource _end = new();
        private CancellationTokenSource _pumpStop = new();
        private Task _pump = Task.CompletedTask;
        private int _closed;
        public Task Completion => _pump;
        public async Task ConfigureAsync(ViewportPresentation presentation, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(presentation); presentation.Surface.Validate();
            if (presentation.Surface.Identity != runtime.Identity || presentation.Surface.Transport != ViewportTransportKind.Gpu)
                throw new InvalidDataException("Viewport does not belong to this GPU runtime.");
            await _gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
                _pumpStop.Cancel(); await _pump.WaitAsync(token).ConfigureAwait(false); _pumpStop.Dispose(); _pumpStop = new();
                await session.ConfigureAsync(presentation.Surface.Width, presentation.Surface.Height, token).ConfigureAwait(false);
                if (!presentation.Surface.Suspended) _pump = PumpAsync(presentation, _pumpStop.Token);
            }
            finally { _gate.Release(); }
        }
        private async Task PumpAsync(ViewportPresentation presentation, CancellationToken stop)
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1d / 30)); var slot = 0;
                while (!stop.IsCancellationRequested && !_end.IsCancellationRequested)
                {
                    var produced = await session.ProduceFrameAsync(slot, _end.Token).ConfigureAwait(false);
                    var metadata = produced.Metadata ?? throw new InvalidDataException("Runtime omitted GPU simulation metadata.");
                    var completion = await session.UseTextureAsync(produced.Completion, texture =>
                    {
                        // Resize may close the old presentation while an accepted frame is still in flight.
                        if (!stop.IsCancellationRequested && presentation.TryIssue(metadata) is { } frame) consume(new(texture, frame));
                    }, _end.Token).ConfigureAwait(false);
                    await session.ReturnAsync(completion, _end.Token).ConfigureAwait(false);
                    await presentation.FirstPresented.WaitAsync(stop).ConfigureAwait(false);
                    slot = (slot + 1) % 3;
                    if (!await timer.WaitForNextTickAsync(stop).ConfigureAwait(false)) break;
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested || _end.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (_end.IsCancellationRequested) { }
            catch (Exception error) { presentation.Close(error); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _closed, 1); _end.Cancel(); _pumpStop.Cancel();
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await session.DisposeAsync().ConfigureAwait(false);
                try { await _pump.ConfigureAwait(false); } catch (Exception) { /* FrameSession observes the worker error. */ }
            }
            finally { _gate.Release(); }
        }
    }
}
