using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Scenes;

namespace Game.Logic;

// Minimal ENG1/ENG3b demo state; no generic scene migration or replacement transaction.
public sealed class ProbeGame : ICheckpointGameSession, TrueMoon.Aether.Rendering.IRenderGameSession
{
    private SceneSnapshot? _scene;
    private bool _disposed;
    private CameraTransform _camera = new(0, 1, 3, 0, 0, 0);
    private bool _interaction, _hasInteractiveEntity;
    public ulong TickNumber { get; private set; }
    public long Revision => _scene?.Revision ?? -1;
    public uint PatternColor => 0xff2080c0;
    public void Load(SceneSnapshot scene)
    {
        ObjectDisposedException.ThrowIf(_disposed, this); ArgumentNullException.ThrowIfNull(scene);
        _scene = scene; TickNumber = 0;
        _camera = new(0, 1, 3, 0, 0, 0); _interaction = false;
        _hasInteractiveEntity = scene.CopyEntities().Any(e => e.Interactive);
    }
    public void Tick()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_scene == null) throw new InvalidOperationException("No scene loaded.");
        var next = checked(TickNumber + 1);
        var camera = _camera with { X = _camera.X + 0.125f, Yaw = _camera.Yaw + 0.01f }; camera.Validate();
        TickNumber = next; _camera = camera; _interaction |= _hasInteractiveEntity && next >= 3;
    }
    public GameCheckpointState CaptureCheckpoint()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_scene == null) throw new InvalidOperationException("No scene loaded.");
        return new(TickNumber, _camera, _interaction);
    }
    public TrueMoon.Aether.Rendering.PrimitiveScene CaptureRenderScene()
    {
        var state = CaptureCheckpoint();
        return new(state.Camera, 0.45f, new System.Numerics.Vector3((PatternColor >> 16 & 255) / 255f,
            (PatternColor >> 8 & 255) / 255f, (PatternColor & 255) / 255f));
    }
    public void RestoreCheckpoint(GameCheckpointState state)
    {
        ObjectDisposedException.ThrowIf(_disposed, this); ArgumentNullException.ThrowIfNull(state); state.Validate();
        if (!state.Objects.Entries.IsEmpty) throw new CheckpointRejectedException(CheckpointOutcome.RestoreRejected);
        if (_scene == null) throw new InvalidOperationException("No scene loaded.");
        // All validation precedes this non-throwing primitive-field commit.
        TickNumber = state.Tick; _camera = state.Camera; _interaction = state.Interaction;
    }
    public void Dispose() { _disposed = true; _scene = null; }
}
