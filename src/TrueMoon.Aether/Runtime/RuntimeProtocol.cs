using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Aether.Input;

namespace TrueMoon.Aether.Runtime;

/// <summary>Serializes same-build game commands, tick boundaries and startup checkpoint readiness.</summary>
/// <param name="identity">Expected runtime identity.</param><param name="game">Borrowed game session.</param>
/// <param name="processId">Owned runtime PID.</param><param name="configureSurface">Optional frame-producer configuration.</param>
/// <param name="droppedFrames">Optional producer diagnostics.</param><param name="requireRestore">Blocks readiness until startup restore succeeds.</param>
public sealed class RuntimeProtocol(SessionIdentity identity, IGameSession game, int processId, Action<FrameSurface>? configureSurface = null,
    Func<long>? droppedFrames = null, bool requireRestore = false) : IEngineControl
{
    private readonly object _gate = new();
    private ulong _sequence;
    private bool _hello, _loaded, _playing, _stopping;
    private ulong _surfaceGeneration;
    private SceneSnapshot? _scene;
    private bool _awaitingRestore = requireRestore;
    private readonly GameInputAccumulator _input = new();

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
            var checkpointOutcome = CheckpointOutcome.None; byte[]? checkpoint = null;
            var inputOutcome = GameInputOutcome.None;
            switch (kind)
            {
                case CommandKind.Hello:
                    if (_hello) throw new InvalidOperationException("Hello already accepted.");
                    _hello = true; break;
                case CommandKind.LoadScene:
                    if (_loaded) throw new InvalidOperationException("Scene already loaded.");
                    var scene = new SceneSnapshot(request.SceneId, request.Revision, request.Entities ?? throw new InvalidDataException("Missing scene entities."));
                    game.Load(scene); _scene = scene;
                    _loaded = true; break;
                case CommandKind.SetMode:
                    RequireReady(); _input.Clear(); _playing = request.Playing; break;
                case CommandKind.Step:
                    RequireReady();
                    if (_playing) throw new InvalidOperationException("Step requires Paused.");
                    TickGame(); break;
                case CommandKind.Stop: _input.Clear(); _stopping = true; _playing = false; break;
                case CommandKind.SetInput:
                    RequireReady();
                    var input = new GameInputState(request.InputCaptured, (GameButtons)request.InputButtons, request.InputYaw, request.InputPitch, request.InputInteract);
                    input.Validate();
                    if (game is IInputGameSession) { _input.Set(input); inputOutcome = GameInputOutcome.Accepted; }
                    else inputOutcome = GameInputOutcome.Unsupported;
                    break;
                case CommandKind.ConfigureSurface:
                    RequireReady();
                    var surface = (request.Surface ?? throw new InvalidDataException("Missing surface.")).Describe(identity);
                    surface.Validate();
                    if (surface.Generation <= _surfaceGeneration) throw new InvalidDataException("Stale surface generation.");
                    (configureSurface ?? throw new InvalidOperationException("Frame producer unavailable."))(surface);
                    _input.Clear();
                    _surfaceGeneration = surface.Generation; break;
                case CommandKind.CaptureCheckpoint:
                    RequireReady();
                    if (_playing) throw new InvalidOperationException("Checkpoint capture requires Paused.");
                    var beforeTick = game.TickNumber; var beforeRevision = game.Revision;
                    if (game is not ICheckpointGameSession captureGame) checkpointOutcome = CheckpointOutcome.Unsupported;
                    else
                    {
                        try
                        {
                            var state = captureGame.CaptureCheckpoint();
                            if (state == null || state.Tick != beforeTick) throw new InvalidDataException("Capture returned an incoherent tick.");
                            checkpoint = GameCheckpointCodec.Encode(new(identity.HostContractId, identity.GameBuildId, _scene!.Id, _scene.Revision, state));
                            checkpointOutcome = CheckpointOutcome.Captured;
                        }
                        catch (CheckpointRejectedException error) when (error.Outcome is CheckpointOutcome.CaptureRejected or CheckpointOutcome.Unsupported)
                        { checkpointOutcome = error.Outcome; }
                        catch (InvalidDataException) { checkpointOutcome = CheckpointOutcome.CaptureRejected; }
                    }
                    if (game.TickNumber != beforeTick || game.Revision != beforeRevision)
                        throw new InvalidDataException("Capture changed the paused runtime; outcome is unsafe.");
                    break;
                case CommandKind.RestoreCheckpoint:
                    RequireScene();
                    if (!_awaitingRestore || _playing || _surfaceGeneration != 0) throw new InvalidOperationException("Restore is only valid before fresh runtime readiness.");
                    if (game is not ICheckpointGameSession restoreGame) checkpointOutcome = CheckpointOutcome.Unsupported;
                    else
                    {
                        GameCheckpoint? restored = null;
                        try
                        {
                            restored = GameCheckpointCodec.Decode(request.Checkpoint ?? throw new InvalidDataException("Missing checkpoint."));
                            restored.ValidateFor(identity.HostContractId, _scene!);
                        }
                        catch (InvalidDataException) { checkpointOutcome = CheckpointOutcome.RestoreRejected; }
                        if (restored != null && checkpointOutcome == CheckpointOutcome.None)
                        {
                            try { restoreGame.RestoreCheckpoint(restored.State); }
                            catch (CheckpointRejectedException error) when (error.Outcome == CheckpointOutcome.RestoreRejected)
                            { checkpointOutcome = CheckpointOutcome.RestoreRejected; }
                            if (checkpointOutcome == CheckpointOutcome.None)
                            {
                                if (game.Revision != _scene!.Revision || game.TickNumber != restored.State.Tick || restoreGame.CaptureCheckpoint() != restored.State)
                                    throw new InvalidDataException("Restore did not apply the complete state.");
                                _input.Clear(); _awaitingRestore = false; checkpointOutcome = CheckpointOutcome.Restored;
                            }
                        }
                    }
                    break;
            }
            _sequence = unchecked((ulong)request.Sequence);
            return Task.FromResult(new ControlReply
            {
                Stamp = ControlRequest.Create(identity, _sequence, kind), ProcessId = processId,
                Revision = game.Revision, Tick = unchecked((long)game.TickNumber), SurfaceGeneration = unchecked((long)_surfaceGeneration),
                DroppedFrames = droppedFrames?.Invoke() ?? 0,
                CheckpointOutcome = (int)checkpointOutcome, Checkpoint = checkpoint, InputOutcome = (int)inputOutcome,
                Phase = _stopping ? "Stopping" : !_loaded ? "HelloAccepted" : _playing ? "Playing" : "Paused"
            });
        }
    }
    private void RequireScene() { if (!_loaded) throw new InvalidOperationException("Scene required."); }
    private void RequireReady() { RequireScene(); if (_awaitingRestore) throw new InvalidOperationException("Restore must complete before readiness/frames."); }
    private void TickGame()
    { if (game is IInputGameSession controlled) controlled.Tick(_input.Take()); else game.Tick(); }
    public void FixedTick() { lock (_gate) { if (_loaded && _playing && !_stopping && !_awaitingRestore) TickGame(); } }
    /// <summary>Clears transient input at a host focus/surface lifecycle boundary without changing game/checkpoint state.</summary>
    public void ResetInput() { lock (_gate) _input.Clear(); }
    public (ulong Tick, uint Color) CaptureFrame()
    { lock (_gate) { RequireReady(); if (_stopping) throw new InvalidOperationException("Runtime is stopping."); return (game.TickNumber, game.PatternColor); } }
    /// <summary>Captures optional renderer state and its exact simulation tick under the command/checkpoint lock.</summary>
    public (ulong Tick, Rendering.PrimitiveScene Scene) CaptureRenderFrame()
    {
        lock (_gate)
        {
            RequireReady(); if (_stopping) throw new InvalidOperationException("Runtime is stopping.");
            if (game is not Rendering.IRenderGameSession render) throw new NotSupportedException("Game does not provide a render scene.");
            var scene = render.CaptureRenderScene() ?? throw new InvalidDataException("Game returned a null render scene.");
            scene.Validate(); return (game.TickNumber, scene);
        }
    }
    /// <summary>Suppresses frame production until a required startup restore is fully applied.</summary>
    public bool TryCaptureFrame(out (ulong Tick, uint Color) frame)
    {
        lock (_gate)
        {
            if (_awaitingRestore) { frame = default; return false; }
            frame = (game.TickNumber, game.PatternColor); return true;
        }
    }
}
