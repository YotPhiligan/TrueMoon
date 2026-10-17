using Engine.Probe.Contracts;

namespace EngineSessionProbe;

public sealed class RuntimeProtocol(SessionIdentity identity, IGameSession game, int processId, Action<FrameSurface>? configureSurface = null, Func<long>? droppedFrames = null) : IEngineControl
{
    private readonly object _gate = new();
    private ulong _sequence;
    private bool _hello, _loaded, _playing, _stopping;
    private ulong _surfaceGeneration;

    public Task<ControlReply> SendAsync(ControlRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            identity.Validate();
            if (request.Identity() != identity) throw new InvalidDataException("Session identity/nonce mismatch.");
            if (unchecked((ulong)request.Sequence) != checked(_sequence + 1)) throw new InvalidDataException("Out-of-order command sequence.");
            if (!Enum.IsDefined((CommandKind)request.Kind)) throw new InvalidDataException("Unknown control operation.");
            if (_stopping) throw new InvalidOperationException("Runtime is stopping.");
            var kind = (CommandKind)request.Kind;
            if (!_hello && kind != CommandKind.Hello) throw new InvalidOperationException("Hello required.");
            switch (kind)
            {
                case CommandKind.Hello:
                    if (_hello) throw new InvalidOperationException("Hello already accepted.");
                    _hello = true; break;
                case CommandKind.LoadScene:
                    if (_loaded) throw new InvalidOperationException("Scene already loaded.");
                    game.Load(new(request.SceneId, request.Revision, request.Entities ?? throw new InvalidDataException("Missing scene entities.")));
                    _loaded = true; break;
                case CommandKind.SetMode:
                    RequireScene(); _playing = request.Playing; break;
                case CommandKind.Step:
                    RequireScene();
                    if (_playing) throw new InvalidOperationException("Step requires Paused.");
                    game.Tick(); break;
                case CommandKind.Stop: _stopping = true; _playing = false; break;
                case CommandKind.ConfigureSurface:
                    RequireScene();
                    var surface = (request.Surface ?? throw new InvalidDataException("Missing surface.")).Describe(identity);
                    surface.Validate();
                    if (surface.Generation <= _surfaceGeneration) throw new InvalidDataException("Stale surface generation.");
                    (configureSurface ?? throw new InvalidOperationException("Frame producer unavailable."))(surface);
                    _surfaceGeneration = surface.Generation; break;
            }
            _sequence = unchecked((ulong)request.Sequence);
            return Task.FromResult(new ControlReply
            {
                Stamp = ControlRequest.Create(identity, _sequence, kind), ProcessId = processId,
                Revision = game.Revision, Tick = unchecked((long)game.TickNumber), SurfaceGeneration = unchecked((long)_surfaceGeneration),
                DroppedFrames = droppedFrames?.Invoke() ?? 0,
                Phase = _stopping ? "Stopping" : !_loaded ? "HelloAccepted" : _playing ? "Playing" : "Paused"
            });
        }
    }
    private void RequireScene() { if (!_loaded) throw new InvalidOperationException("Scene required."); }
    public void FixedTick() { lock (_gate) { if (_playing && !_stopping) game.Tick(); } }
    public (ulong Tick, uint Color) CaptureFrame() { lock (_gate) return (game.TickNumber, game.PatternColor); }
}
