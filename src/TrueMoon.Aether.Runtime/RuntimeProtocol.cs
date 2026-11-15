using TrueMoon.Aether.Abstractions.Checkpoints;
using TrueMoon.Aether.Abstractions.Input;
using TrueMoon.Aether.Abstractions.Preview;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Abstractions.Sessions;
using TrueMoon.Aether.Runtime.Checkpoints;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Gpu.Surfaces;
using TrueMoon.Aether.Runtime.Input;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Runtime.Transport;

namespace TrueMoon.Aether.Runtime;

/// <summary>Serializes same-build game commands, tick boundaries and startup checkpoint readiness.</summary>
/// <param name="identity">Expected runtime identity.</param><param name="game">Borrowed game session.</param>
/// <param name="processId">Owned runtime PID.</param><param name="configureSurface">Optional frame-producer configuration.</param>
/// <param name="droppedFrames">Optional producer diagnostics.</param><param name="requireRestore">Blocks readiness until startup restore succeeds.</param>
/// <param name="role">Edit preview disallows gameplay mode, Step and input commands.</param>
/// <param name="sceneWorldCodec">Borrowed neutral codec; enables full-scene transfer only for an ECS game session.</param>
public sealed partial class RuntimeProtocol(SessionIdentity identity, IGameSession game, int processId, Action<FrameSurface>? configureSurface = null,
    Func<long>? droppedFrames = null, bool requireRestore = false, RuntimeRole role = RuntimeRole.Game,
    SceneWorldCodec? sceneWorldCodec = null) : IEngineControl, IDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _worldSerial = new(1, 1);
    private readonly RenderViewReadiness _renderView = new();
    private bool _resumeAfterPresentation;
    /// <summary>Whether this game explicitly requires the GPU mesh-view/1 profile.</summary>
    public bool RequiresMeshView => game is IRenderSnapshotGameSession;
    private ulong _sequence;
    private bool _hello, _loaded, _playing, _stopping, _playbackFaulted;
    private ulong _surfaceGeneration;
    private SceneSnapshot? _scene;
    private bool _awaitingRestore = requireRestore;
    private readonly GameInputAccumulator _input = new();

    public async Task<ControlReply> SendAsync(ControlRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _worldSerial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            identity.Validate();
            if (request.Identity() != identity) throw new InvalidDataException("Session identity/nonce mismatch.");
            if (unchecked((ulong)request.Sequence) != checked(_sequence + 1)) throw new InvalidDataException("Out-of-order command sequence.");
            if (!Enum.IsDefined((CommandKind)request.Kind)) throw new InvalidDataException("Unknown control operation.");
            if (_stopping) throw new InvalidOperationException("Runtime is stopping.");
            var kind = (CommandKind)request.Kind;
            if (request.PreviewView != null && kind is not (CommandKind.LoadScene or CommandKind.SetPreviewView))
                throw new InvalidDataException("Preview payload is not valid for this operation.");
            if (request.RenderViewStamp != null && kind != CommandKind.PresentRenderView)
                throw new InvalidDataException("Mesh presentation payload is not valid for this operation.");
            if (role == RuntimeRole.EditPreview && (kind == CommandKind.Step || kind == CommandKind.SetInput || kind == CommandKind.SetMode && request.Playing))
                throw new InvalidOperationException("Edit preview has no gameplay simulation or input.");
            if (!_hello && kind != CommandKind.Hello) throw new InvalidOperationException("Hello required.");
            var checkpointOutcome = CheckpointOutcome.None; byte[]? checkpoint = null;
            var inputOutcome = GameInputOutcome.None;
            var meshPresentationAccepted = false;
            switch (kind)
            {
                case CommandKind.Hello:
                    if (_hello) throw new InvalidOperationException("Hello already accepted.");
                    _hello = true; break;
                case CommandKind.LoadScene:
                    if (_loaded) throw new InvalidOperationException("Scene already loaded.");
                    if (_sceneTransferUsed) throw new InvalidOperationException("Full-scene transfer already admitted on this runtime.");
                    var scene = new SceneSnapshot(request.SceneId, request.Revision, request.Entities ?? throw new InvalidDataException("Missing scene entities."));
                    if (request.PreviewView != null)
                    {
                        if (role != RuntimeRole.EditPreview || game is not Preview.PreviewGameSession preview) throw new InvalidOperationException("Preview view capability unavailable.");
                        preview.Load(scene, PreviewViewCodec.Decode(request.PreviewView));
                    }
                    else game.Load(scene);
                    _scene = scene;
                    _loaded = true; break;
                case CommandKind.SetMode:
                    RequireReady();
                    RefreshRequiredView();
                    if (request.Playing && RequiresMeshView && !_renderView.IsReady) throw new InvalidOperationException("Required mesh view has not been prepared and presented.");
                    _input.Clear(); _resumeAfterPresentation = false; SetPlayback(request.Playing); break;
                case CommandKind.PresentRenderView:
                    RequireReady();
                    if (!RequiresMeshView) throw new NotSupportedException("Mesh-view presentation capability unavailable.");
                    meshPresentationAccepted = _renderView.TryPresented(RenderSnapshotStamp.Decode(request.RenderViewStamp ?? throw new InvalidDataException("Missing mesh presentation stamp.")),
                        ((IRenderSnapshotGameSession)game).ReadRenderScene());
                    if (meshPresentationAccepted && _resumeAfterPresentation) { SetPlayback(true); _resumeAfterPresentation = false; }
                    break;
                case CommandKind.SetPreviewView:
                    RequireReady();
                    if (role != RuntimeRole.EditPreview || game is not Preview.PreviewGameSession viewGame) throw new InvalidOperationException("Preview view capability unavailable.");
                    var view = PreviewViewCodec.Decode(request.PreviewView ?? throw new InvalidDataException("Missing preview view."));
                    viewGame.SetView(view); break;
                case CommandKind.Step:
                    RequireReady();
                    if (_playing) throw new InvalidOperationException("Step requires Paused.");
                    TickGame(); break;
                case CommandKind.Stop: SetPlayback(false); _input.Clear(); _sceneStage = null; _stopping = true; break;
                case CommandKind.BeginSceneWorld:
                case CommandKind.AppendSceneWorld:
                case CommandKind.ActivateSceneWorld:
                case CommandKind.AbortSceneWorld:
                    ExecuteSceneWorld(kind, request, cancellationToken); break;
                case CommandKind.SetInput:
                    RequireReady();
                    var input = new GameInputState(request.InputCaptured, (GameButtons)request.InputButtons, request.InputYaw, request.InputPitch, request.InputInteract);
                    input.Validate();
                    if (game is IInputGameSession) { _input.Set(input); inputOutcome = GameInputOutcome.Accepted; }
                    else inputOutcome = GameInputOutcome.Unsupported;
                    break;
                case CommandKind.ConfigureSurface:
                    RequireReady();
                    if (RequiresMeshView) throw new NotSupportedException("Required mesh views use the explicit GPU surface transport.");
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
                    if (_scene == null || game is not ICheckpointGameSession captureGame) checkpointOutcome = CheckpointOutcome.Unsupported;
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
            var reply = new ControlReply
            {
                Stamp = ControlRequest.Create(identity, _sequence, kind), ProcessId = processId,
                Revision = game.Revision, Tick = unchecked((long)game.TickNumber), SurfaceGeneration = unchecked((long)_surfaceGeneration),
                DroppedFrames = droppedFrames?.Invoke() ?? 0,
                RenderViewVersion = RequiresMeshView ? 1 : 0, RenderViewReady = RequiresMeshView && _renderView.IsReady,
                RenderViewPresentationAccepted = meshPresentationAccepted,
                CheckpointOutcome = (int)checkpointOutcome, Checkpoint = checkpoint, InputOutcome = (int)inputOutcome,
                Phase = _stopping ? "Stopping" : _playbackFaulted || SceneGame?.IsFaulted == true ? "Faulted" : !_loaded ? "HelloAccepted" : _playing ? "Playing" : "Paused"
            };
            if (role == RuntimeRole.EditPreview && game is Preview.PreviewGameSession previewGame)
            {
                reply.PreviewViewVersion = PreviewViewCodec.Version;
                if (previewGame.View is { } currentView)
                { reply.PreviewViewStamp = new byte[PreviewViewCodec.StampBytes]; PreviewViewCodec.WriteStamp(reply.PreviewViewStamp, currentView.Stamp); }
            }
            DescribeSceneWorld(reply); return reply;
        }
        }
        finally { _worldSerial.Release(); }
    }
    private void RequireScene() { if (!_loaded) throw new InvalidOperationException("Scene required."); }
    private void RequireReady()
    {
        ObjectDisposedException.ThrowIf(_disposed, this); RequireScene();
        if (_playbackFaulted || SceneGame?.IsFaulted == true) throw new InvalidOperationException("The game session is faulted.");
        if (_awaitingRestore) throw new InvalidOperationException("Restore must complete before readiness/frames.");
    }
    private void TickGame()
    {
        try { if (game is IInputGameSession controlled) controlled.Tick(_input.Take()); else game.Tick(); }
        catch (Exception error)
        {
            _playbackFaulted = true;
            try { SetPlayback(false); } catch (Exception pause) { throw new AggregateException(error, pause); }
            throw;
        }
        RefreshRequiredView();
    }
    private void SetPlayback(bool playing)
    {
        if (_playing == playing && !_playbackFaulted) return;
        if (playing && _playbackFaulted) throw new InvalidOperationException("A faulted session cannot resume playback.");
        _playing = false;
        try { if (_loaded && game is IGamePlaybackSession playback) playback.SetPlaying(playing); _playing = playing; }
        catch (Exception error)
        {
            _playbackFaulted = true;
            if (playing && _loaded && game is IGamePlaybackSession playback)
                try { playback.SetPlaying(false); } catch (Exception pause) { throw new AggregateException(error, pause); }
            throw;
        }
    }
    private void RefreshRequiredView()
    {
        if (_renderView.IsReady && game is IRenderSnapshotGameSession mesh && !_renderView.MatchesSource(mesh.ReadRenderScene()))
        { _resumeAfterPresentation |= _playing; SetPlayback(false); _input.Clear(); _renderView.Invalidate(); }
    }
    public void FixedTick()
    {
        if (!_worldSerial.Wait(0)) return;
        try { lock (_gate) { if (!_disposed && _loaded && _playing && !_stopping && !_playbackFaulted && !_awaitingRestore && SceneGame?.IsFaulted != true &&
            (!RequiresMeshView || _renderView.IsReady)) TickGame(); } }
        finally { _worldSerial.Release(); }
    }
    /// <summary>Clears transient input at a host focus/surface lifecycle boundary without changing game/checkpoint state.</summary>
    public void ResetInput() { lock (_gate) _input.Clear(); }
    public (ulong Tick, uint Color) CaptureFrame()
    { lock (_gate) { RequireReady(); if (_stopping) throw new InvalidOperationException("Runtime is stopping."); return (game.TickNumber, game.PatternColor); } }
    /// <summary>Captures optional renderer state and its exact simulation tick under the command/checkpoint lock.</summary>
    public (ulong Tick, PrimitiveScene Scene) CaptureRenderFrame()
    {
        lock (_gate)
        {
            RequireReady(); if (_stopping) throw new InvalidOperationException("Runtime is stopping.");
            if (game is not IRenderGameSession render) throw new NotSupportedException("Game does not provide a render scene.");
            var scene = render.CaptureRenderScene() ?? throw new InvalidDataException("Game returned a null render scene.");
            scene.Validate(); return (game.TickNumber, scene);
        }
    }
    /// <summary>Suppresses frame production until a required startup restore is fully applied.</summary>
    public bool TryCaptureFrame(out (ulong Tick, uint Color) frame)
    {
        lock (_gate)
        {
            if (_disposed || !_loaded || _stopping || _playbackFaulted || _awaitingRestore || SceneGame?.IsFaulted == true || RequiresMeshView) { frame = default; return false; }
            frame = (game.TickNumber, game.PatternColor); return true;
        }
    }

    internal async Task<GpuFrameData> CaptureSurfaceAsync(GpuSurfaceRenderRequest request, RuntimeMeshRenderHost? meshes, CancellationToken token)
    {
        await _worldSerial.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (game is IRenderSnapshotGameSession source)
            {
                RenderSceneSource scene; ulong tick;
                lock (_gate) { RequireReady(); if (_stopping) throw new InvalidOperationException("Runtime is stopping."); scene = source.ReadRenderScene(); tick = game.TickNumber; }
                return await (meshes ?? throw new InvalidOperationException("Call UseAetherSurfaceRendering in the runtime composition root."))
                    .CaptureAsync(scene, tick, request, token).ConfigureAwait(false);
            }
            var frame = CaptureRenderFrame(); return GpuFrameData.FromScene(request.Stamp, frame.Tick, frame.Scene);
        }
        finally { _worldSerial.Release(); }
    }
    internal void SurfaceProduced(GpuFrameData data)
    {
        if (data.Metadata.RenderView is not { } stamp) return;
        lock (_gate)
        {
            if (_disposed || _stopping) return;
            _renderView.Produced(stamp);
            if (_playing && !_renderView.IsReady) { _resumeAfterPresentation = true; SetPlayback(false); _input.Clear(); }
        }
    }
    internal void DetachRenderSurface()
    {
        if (!RequiresMeshView) return;
        lock (_gate) { _resumeAfterPresentation |= _playing; SetPlayback(false); _input.Clear(); _renderView.Reset(); }
    }
}
