using System.Numerics;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Input;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;

namespace Game.Logic;

/// <summary>ENG5b game: input-driven camera and rising-edge demo interaction, independent of the reference pattern session.</summary>
public sealed class CameraGame : ICheckpointGameSession, IRenderGameSession, IInputGameSession, TrueMoon.Aether.Runtime.IGameSessionEntry
{
    public static global::TrueMoon.Aether.Sessions.IGameSession Create(global::TrueMoon.Services.IServiceResolver? services) => new CameraGame();
    private SceneSnapshot? _scene;
    private CameraTransform _camera = new(0, 1, 3, 0, 0, 0);
    private bool _interaction, _interactive, _disposed;
    public ulong TickNumber { get; private set; }
    public long Revision => _scene?.Revision ?? -1;
    public uint PatternColor => _interaction ? 0xffc06020 : 0xff2080c0;
    public void Load(SceneSnapshot scene)
    {
        ObjectDisposedException.ThrowIf(_disposed, this); ArgumentNullException.ThrowIfNull(scene);
        _scene = scene; _camera = new(0, 1, 3, 0, 0, 0); _interaction = false; TickNumber = 0;
        _interactive = scene.CopyEntities().Any(e => e.Interactive);
    }
    public void Tick() => Tick(default);
    public void Tick(GameInputTick input)
    {
        RequireScene(); input.Validate();
        var tick = checked(TickNumber + 1); var camera = FirstPersonCamera.Step(_camera, input);
        _camera = camera; TickNumber = tick; if (_interactive && input.InteractPressed) _interaction = !_interaction;
    }
    public GameCheckpointState CaptureCheckpoint() { RequireScene(); return new(TickNumber, _camera, _interaction); }
    public void RestoreCheckpoint(GameCheckpointState state)
    {
        RequireScene(); ArgumentNullException.ThrowIfNull(state); state.Validate();
        if (!state.Objects.Entries.IsEmpty) throw new CheckpointRejectedException(CheckpointOutcome.RestoreRejected);
        TickNumber = state.Tick; _camera = state.Camera; _interaction = state.Interaction;
    }
    public PrimitiveScene CaptureRenderScene()
    {
        RequireScene(); var color = PatternColor;
        return new(_camera, .45f, new Vector3((color >> 16 & 255) / 255f, (color >> 8 & 255) / 255f, (color & 255) / 255f));
    }
    private void RequireScene() { ObjectDisposedException.ThrowIf(_disposed, this); if (_scene == null) throw new InvalidOperationException("No scene loaded."); }
    public void Dispose() { _disposed = true; _scene = null; }
}
