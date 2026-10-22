using System.Collections.Immutable;
using System.Numerics;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Input;
using TrueMoon.Aether.Interactions;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;

namespace Game.Logic;

/// <summary>Authored room with fixed-tick camera, occluded reach queries and checkpointed per-object switches.</summary>
public sealed class RoomGame : ICheckpointGameSession, IRenderGameSession, IInputGameSession, TrueMoon.Aether.Runtime.IGameSessionEntry
{
    public static global::TrueMoon.Aether.Sessions.IGameSession Create(global::TrueMoon.Services.IServiceResolver? services) => new RoomGame();
    private SceneSnapshot? _scene;
    private ImmutableArray<InteractionBody> _bodies = [];
    private GameObjectStates _objects = GameObjectStates.Empty;
    private CameraTransform _camera = new(0, 1.6f, 6, 0, 0, 0);
    private bool _disposed;
    /// <summary>Maximum surface reach in world units for the demo switch.</summary>
    public const float InteractionReach = 4;
    /// <summary>Visible stored-sRGB material while a target's gameplay switch is active.</summary>
    public static Vector3 ActiveColor => new(1, .8f, .1f);
    public ulong TickNumber { get; private set; }
    public long Revision => _scene?.Revision ?? -1;
    public uint PatternColor => 0xff2080c0;
    public void Load(SceneSnapshot scene)
    {
        ObjectDisposedException.ThrowIf(_disposed, this); ArgumentNullException.ThrowIfNull(scene);
        var entities = scene.CopyEntities();
        var bodies = entities.Where(e => e.Primitive != null).Select(e => new InteractionBody(e.Id, e.Primitive!.ToPrimitive(), e.Interactive)).ToImmutableArray();
        var objects = new GameObjectStates(bodies.Where(b => b.Interactive).Select(b => new GameObjectState(b.EntityId, false)));
        _scene = scene; _bodies = bodies; _objects = objects;
        _camera = new(0, 1.6f, 6, 0, 0, 0); TickNumber = 0;
    }
    public void Tick() => Tick(default);
    public void Tick(GameInputTick input)
    {
        RequireScene(); input.Validate();
        var tick = checked(TickNumber + 1); var camera = FirstPersonCamera.Step(_camera, input);
        var objects = _objects;
        if (input.InteractPressed && PrimitiveRaycast.Cast(_bodies, camera, InteractionReach) is { Interactive: true } hit)
            objects = new(objects.Entries.Select(entry => entry.EntityId == hit.EntityId ? entry with { Active = !entry.Active } : entry));
        _camera = camera; TickNumber = tick; _objects = objects;
    }
    public GameCheckpointState CaptureCheckpoint()
    { RequireScene(); return new(TickNumber, _camera, _objects.Entries.Any(entry => entry.Active)) { Objects = _objects }; }
    public void RestoreCheckpoint(GameCheckpointState state)
    {
        RequireScene(); ArgumentNullException.ThrowIfNull(state); state.Validate();
        if (!state.Objects.Entries.Select(entry => entry.EntityId).SequenceEqual(_objects.Entries.Select(entry => entry.EntityId)) ||
            state.Interaction != state.Objects.Entries.Any(entry => entry.Active))
            throw new CheckpointRejectedException(CheckpointOutcome.RestoreRejected);
        TickNumber = state.Tick; _camera = state.Camera; _objects = state.Objects;
    }
    public PrimitiveScene CaptureRenderScene()
    {
        RequireScene(); var active = _objects.Entries.Where(entry => entry.Active).Select(entry => entry.EntityId).ToHashSet(StringComparer.Ordinal);
        return new(_camera, _bodies.Select(body => active.Contains(body.EntityId) ? body.Primitive with { Color = ActiveColor } : body.Primitive));
    }
    private void RequireScene() { ObjectDisposedException.ThrowIf(_disposed, this); if (_scene == null) throw new InvalidOperationException("No scene loaded."); }
    public void Dispose() { _disposed = true; _scene = null; _bodies = []; _objects = GameObjectStates.Empty; }
}
