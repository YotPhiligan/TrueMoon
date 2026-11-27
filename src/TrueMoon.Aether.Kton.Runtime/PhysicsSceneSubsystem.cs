using System.Numerics;
using System.Runtime.InteropServices;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Motion;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Kton;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Contacts;
using TrueMoon.Kton.Shapes;
using A = TrueMoon.Aether.Ecs;
using EcsSystemContext = TrueMoon.Aether.Ecs.EcsSystemContext;
using EcsSystemDescription = TrueMoon.Aether.Ecs.EcsSystemDescription;
using ISystem = TrueMoon.Aether.Ecs.ISystem;

namespace TrueMoon.Aether.Kton.Runtime;

/// <summary>Owns one independent Kton world for a borrowed scene; scheduler systems borrow this owner.</summary>
/// <remarks>Calls are externally serialized. Dispose after scheduler cleanup and before the scene/App dependencies.
/// Native cleanup failure retains the world for retry. Physics owns root unit-scale poses; use PhysicsTeleport for
/// discontinuities and PhysicsEntityCommands for complete phase-boundary attachment/detachment.</remarks>
public sealed partial class PhysicsSceneSubsystem : ISceneSubsystem
{
    /// <summary>Exact version-one scene duration. Both current Kton quality profiles simulate60Hz.</summary>
    public static TimeSpan FixedStep { get; } = TimeSpan.FromSeconds(1d / 60);
    private readonly SceneWorld _scene;
    private readonly World _world;
    private readonly PhysicsBridgeOptions _options;
    private readonly int _maximumEvents;
    private IPhysicsWorld? _physics;
    private readonly Dictionary<EntityHandle, Entry> _entries = [];
    private readonly Dictionary<PhysicsBodyId, EntityHandle> _entities = [], _retired = [];
    private readonly List<Candidate> _candidates = [];
    private readonly List<Candidate> _candidatePool = [];
    private readonly List<EntityHandle> _removals = [];
    private readonly Dictionary<EntityHandle, Candidate> _present = [];
    private readonly List<PhysicsBodyCommand> _commands = [];
    private readonly List<EntityHandle> _oldEvents = [];
    private readonly List<PhysicsContact> _contacts = [];
    private PhysicsContactEvent[] _raw;
    private bool _busy, _disposing, _disposed;
    internal PhysicsSceneSubsystem(SceneSubsystemArguments arguments, PhysicsBridgeOptions options)
    {
        _scene = arguments.Scene; _world = _scene.World; _maximumEvents = options.MaximumContactEvents;
        _options = options;
        _raw = new PhysicsContactEvent[_maximumEvents];
    }
    /// <summary>Gets the native world's incarnation, retained as diagnostic identity after disposal.</summary>
    public PhysicsWorldId PhysicsWorldId { get; private set; }
    /// <summary>Gets the last fully joined native step. A later ECS publication failure remains terminal.</summary>
    public ulong LastCompletedTick { get; private set; }
    /// <summary>Gets one immutable system registration; its instance belongs to the session scheduler.</summary>
    public IReadOnlyList<EcsSystemRegistration> Systems { get; private set; } = [];
    /// <summary>Gets whether native cleanup has completed.</summary>
    public bool IsDisposed => _disposed;
    /// <summary>Returns the current attachment only for a live entity still carrying PhysicsBody.</summary>
    /// <param name="entity">Exact ECS generation.</param><param name="body">Exact physical generation or default.</param><returns>Whether mapped and live.</returns>
    public bool TryGetBody(EntityHandle entity, out PhysicsBodyId body)
    {
        body = default;
        if (_disposed || _disposing || _world.IsDisposed || !_world.IsAlive(entity) || !_world.HasComponent<PhysicsBody>(entity) || !_entries.TryGetValue(entity, out var entry)) return false;
        body = entry.Body; return true;
    }
    /// <summary>Maps only current physical and ECS generations; retired contact identities never alias new entities.</summary>
    /// <param name="body">Exact physical generation.</param><param name="entity">Exact ECS generation or default.</param><returns>Whether currently attached.</returns>
    public bool TryGetEntity(PhysicsBodyId body, out EntityHandle entity)
    {
        entity = default;
        if (_disposed || _disposing || _world.IsDisposed || !_entities.TryGetValue(body, out var found) || !_world.IsAlive(found) || !_world.HasComponent<PhysicsBody>(found)) return false;
        entity = found; return true;
    }
    internal void Prepare(IFactory<IPhysicsWorld> worlds, IFactory<PhysicsBridgeSystem, PhysicsSceneSubsystem> systems, CancellationToken token)
    {
        if (!_scene.IsReady || _world.IsDisposed || _world.IsFaulted) throw new InvalidOperationException("A healthy private scene is required.");
        token.ThrowIfCancellationRequested();
        _physics = worlds.Create() ?? throw new InvalidOperationException("Physics world factory returned null.");
        PhysicsWorldId = _physics.Id;
        if (_physics.State != PhysicsWorldState.Ready || _physics.LastCompletedTick != 0) throw new InvalidOperationException("A fresh caller-owned Kton world is required.");
        PhysicsValidation.Validate(_physics.Options).ThrowIfFailure();
        Capture(initial: true, token);
        token.ThrowIfCancellationRequested(); Reconcile();
        foreach (var candidate in _candidates)
        {
            if (!_world.HasComponent<PhysicsVelocity>(candidate.Entity)) _world.AddComponent(candidate.Entity, candidate.Velocity);
            _world.AddComponent(candidate.Entity, new ExternalMotion());
            InitializeCharacterRoles(candidate);
        }
        token.ThrowIfCancellationRequested();
        var description = new EcsSystemDescription("kton.physics", EcsPhase.Movement).Before("builtin.movement")
            .Read<PhysicsBody>().Read<PhysicsTeleport>().Read<ExternalMotion>().Read<TransformParent>().Read<WorldTransform>()
            .Write<LocalTransform>().Write<PhysicsVelocity>().Write<PhysicsContact>();
        description.Read<PhysicsCharacter>().Read<PhysicsCharacterInput>().Write<PhysicsCharacterState>();
        Systems = [new(description, new Factory<ISystem, World>(world =>
        {
            if (!ReferenceEquals(world, _world)) throw new InvalidOperationException("Physics system World mismatch.");
            return systems.Create(this) ?? throw new InvalidOperationException("Physics system factory returned null.");
        }))];
    }
    private void Capture(bool initial, CancellationToken token)
    {
        var physics = _physics!; _candidates.Clear(); _present.Clear();
        foreach (var candidate in _candidatePool) candidate.Reset();
        _world.Query<PhysicsBody>().ForEach((EntityHandle entity, in PhysicsBody descriptor) =>
        {
            token.ThrowIfCancellationRequested();
            if (_candidates.Count == Math.Min(physics.Options.Limits.MaxBodies, physics.Options.Limits.MaxShapes))
                throw new PhysicsException(new(PhysicsErrorCode.CapacityExceeded, "Bridge.BodiesOrShapes"));
            if (!_world.TryGetComponent<LocalTransform>(entity, out var transform)) throw new InvalidDataException("Physics requires LocalTransform.");
            transform.Validate();
            if (transform.Scale != Vector3.One || _world.TryGetComponent<TransformParent>(entity, out var parent) && parent.Parent != default)
                throw new NotSupportedException("Physics PHYS5 requires a root entity with unit scale.");
            if (initial)
            {
                ValidateAuthoredRoot(entity);
                if (_world.HasComponent<ExternalMotion>(entity)) throw new InvalidOperationException("Physics cannot take another motion owner's marker.");
            }
            else if (!_world.HasComponent<TransformParent>(entity) || !_world.HasComponent<WorldTransform>(entity) || !_world.HasComponent<ExternalMotion>(entity) || !_world.HasComponent<PhysicsVelocity>(entity))
                throw new InvalidDataException("Runtime physics attachment requires complete roles; use PhysicsEntityCommands.");
            _world.TryGetComponent<PhysicsVelocity>(entity, out var velocity);
            _entries.TryGetValue(entity, out var previous);
            if (previous != null && transform != previous.LastTransform) throw new InvalidDataException("Physics owns LocalTransform; use PhysicsTeleport.");
            PhysicsTeleport? teleport = _world.TryGetComponent<PhysicsTeleport>(entity, out var request) ? request : null;
            if (teleport is { } command)
            {
                if (command.Sequence == 0 || command.Sequence < (previous?.LastTeleport.Sequence ?? 0) ||
                    command.Sequence == (previous?.LastTeleport.Sequence ?? 0) && command != previous!.LastTeleport)
                    throw new InvalidDataException("Teleport sequence must increase; replays require identical payload.");
                PhysicsValidation.Validate(command.Pose).ThrowIfFailure();
            }
            PhysicsShapeDefinition shape;
            var isMesh = descriptor.Shape == PhysicsColliderShape.StaticMesh;
            if (!initial && (isMesh || previous?.Descriptor.Shape == PhysicsColliderShape.StaticMesh))
            {
                if (previous is null || previous.Descriptor != descriptor)
                    throw new InvalidDataException("Static mesh colliders can only be attached during authored scene preparation and cannot change descriptor at runtime.");
                if (velocity != default)
                    throw new PhysicsException(new(PhysicsErrorCode.InvalidArgument, "Body.StaticVelocity"));
                // The unchanged attachment already passed full geometry and live-limit validation.
                shape = previous.ShapeDefinition;
            }
            else
            {
                var unchanged = previous?.Descriptor == descriptor;
                if (isMesh)
                {
                    if (!_options.TryGetStaticMesh(descriptor.MeshAssetId, out var mesh))
                        throw new InvalidDataException($"Kton static mesh asset '{descriptor.MeshAssetId:D}' is not registered.");
                    shape = mesh;
                }
                else
                    shape = unchanged ? previous!.ShapeDefinition : descriptor.CaptureShape();

                // Live provider lookup is performed by CreateBody. This preflight uses a structurally valid shape identity.
                var id = previous?.Shape ?? new PhysicsShapeId(physics.Id, 0, 1);
                var checkedVelocity = unchanged && previous!.LastVelocity == velocity ? default : velocity;
                var description = unchanged ? previous!.BodyDescription : Description(id, descriptor, transform, velocity);
                PhysicsValidation.ValidateBody(physics.Options, description, shape, new(transform.Position, transform.Rotation), checkedVelocity.Linear, checkedVelocity.Angular).ThrowIfFailure();
            }
            var index = _candidates.Count;
            if (index == _candidatePool.Count) _candidatePool.Add(new());
            var candidate = _candidatePool[index];
            candidate.Entity = entity; candidate.Descriptor = descriptor; candidate.Transform = transform; candidate.Velocity = velocity;
            candidate.Teleport = teleport; candidate.Shape = shape; candidate.Previous = previous;
            CaptureCharacter(candidate, initial);
            _present.Add(entity, candidate); _candidates.Add(candidate);
        });
        foreach (var entity in _entries.Keys)
            if (!_present.ContainsKey(entity) && _world.IsAlive(entity) &&
                (_world.HasComponent<ExternalMotion>(entity) || _world.HasComponent<PhysicsVelocity>(entity) || _world.HasComponent<PhysicsTeleport>(entity)))
                throw new InvalidDataException("Physics detachment must remove all owned roles; use PhysicsEntityCommands.RemoveBody.");
        ValidateCharacterRoles();
        var minimum = float.PositiveInfinity; var maximum = float.NegativeInfinity;
        foreach (var candidate in _candidates)
        {
            if (candidate.Descriptor.Motion != PhysicsBodyMotion.Dynamic) continue;
            minimum = MathF.Min(minimum, candidate.Descriptor.Mass); maximum = MathF.Max(maximum, candidate.Descriptor.Mass);
        }
        if (minimum != float.PositiveInfinity) PhysicsValidation.ValidateMassRatio(minimum, maximum).ThrowIfFailure();
        _candidates.Sort(static (a,b) => a.Entity.Index.CompareTo(b.Entity.Index));
    }
    private void Reconcile()
    {
        var physics = _physics!; _retired.Clear(); _removals.Clear();
        foreach (var pair in _entries)
        {
            if (_present.TryGetValue(pair.Key, out var candidate) && candidate.Descriptor == pair.Value.Descriptor) continue;
            _removals.Add(pair.Key);
        }
        foreach (var entity in _removals)
        {
            var entry = _entries[entity]; RemoveController(entry); physics.RemoveBody(entry.Body); physics.RemoveShape(entry.Shape);
            _retired.Add(entry.Body, entity); _entities.Remove(entry.Body); _entries.Remove(entity);
        }
        foreach (var candidate in _candidates)
        {
            if (_entries.ContainsKey(candidate.Entity)) continue;
            var shape = physics.CreateShape(candidate.Shape);
            var description = Description(shape, candidate.Descriptor, candidate.Transform, candidate.Velocity);
            var body = physics.CreateBody(description);
            var entry = new Entry(candidate.Descriptor, shape, candidate.Shape, body, candidate.Transform, candidate.Velocity, description)
            { LastTeleport = candidate.Previous?.LastTeleport ?? default, LastJumpSequence = candidate.Previous?.LastJumpSequence ?? 0,
              LastCharacterState = candidate.Previous?.LastCharacterState ?? default };
            _entries.Add(candidate.Entity, entry); _entities.Add(body, candidate.Entity);
        }
        ReconcileCharacters();
        _removals.Clear();
    }
    internal void Advance(EcsSystemContext context)
    {
        Ready();
        if (context.WorldId != _world.Id || context.DeltaTime != FixedStep || context.TickNumber != _physics!.LastCompletedTick + 1)
            throw new InvalidOperationException("Physics scene step/World/tick mismatch.");
        _busy = true;
        try
        {
            Capture(initial: false, context.CancellationToken);
            _oldEvents.Clear();
            _world.Query<PhysicsContact>().ForEach((EntityHandle entity, in PhysicsContact contact) =>
            { if (contact.Contact.BodyA.World == PhysicsWorldId) { if (_oldEvents.Count == _maximumEvents) throw new InvalidDataException("Foreign contact projection mutation."); _oldEvents.Add(entity); } });
            context.CancellationToken.ThrowIfCancellationRequested();
            // Commit has begun: finish native mutation/Step and worker join. Scheduler observes later cancellation at its boundary.
            Reconcile(); _commands.Clear();
            foreach (var candidate in _candidates)
            {
                var entry = _entries[candidate.Entity]; var velocity = candidate.Velocity;
                var teleport = candidate.Teleport is { } request && request.Sequence > entry.LastTeleport.Sequence;
                if (teleport)
                {
                    _commands.Add(new(PhysicsCommandKind.Teleport, entry.Body, Pose: candidate.Teleport!.Value.Pose));
                    if (candidate.Teleport.Value.ClearVelocity) velocity = default;
                }
                if (entry.Descriptor.Motion != PhysicsBodyMotion.Static && velocity != entry.LastVelocity)
                    _commands.Add(new(PhysicsCommandKind.SetVelocity, entry.Body, velocity.Linear, velocity.Angular));
            }
            if (_commands.Count != 0) _physics.ApplyCommands(CollectionsMarshal.AsSpan(_commands));
            SubmitCharacterInputs();
            var result = _physics.Step(context.TickNumber); LastCompletedTick = result.Tick;
            if (result.ContactEventCount > _maximumEvents) throw new PhysicsException(new(PhysicsErrorCode.ContactOverflow, "Bridge.ContactProjection"));
            var count = _physics.CopyContactEvents(_raw); _contacts.Clear();
            for (var index = 0; index < count; index++)
            {
                var contact = _raw[index];
                if (!Map(contact.BodyA, out var a) || !Map(contact.BodyB, out var b)) throw new InvalidDataException("Unmapped physics contact generation.");
                _contacts.Add(new(contact, a, b));
            }
            foreach (var candidate in _candidates)
            {
                var entry = _entries[candidate.Entity]; var state = _physics.GetBodyState(entry.Body);
                PhysicsValidation.Validate(state.Pose).ThrowIfFailure();
                var transform = new LocalTransform(state.Pose.Position, state.Pose.Orientation, Vector3.One);
                var velocity = new PhysicsVelocity(state.LinearVelocity, state.AngularVelocity);
                _world.SetComponent(candidate.Entity, transform); _world.SetComponent(candidate.Entity, velocity);
                PublishCharacter(candidate.Entity, entry);
                entry.LastTransform = transform; entry.LastVelocity = velocity;
                if (candidate.Teleport is { } request) entry.LastTeleport = request;
            }
            foreach (var entity in _oldEvents) context.Commands.DestroyEntity(entity);
            foreach (var contact in _contacts) { var entity = context.Commands.CreateEntity(); context.Commands.AddComponent(entity, contact); }
            _retired.Clear();
        }
        finally { _busy = false; }
    }
    private bool Map(PhysicsBodyId body, out EntityHandle entity) => _entities.TryGetValue(body, out entity) || _retired.TryGetValue(body, out entity);
    private static PhysicsBodyDescription Description(PhysicsShapeId shape, PhysicsBody body, LocalTransform transform, PhysicsVelocity velocity) =>
        new(shape, body.Motion, new(transform.Position, transform.Rotation))
        { Mass = body.Mass, Material = body.Material, Filter = body.Filter, IsSensor = body.IsSensor, LinearVelocity = velocity.Linear, AngularVelocity = velocity.Angular };
    /// <summary>Records actual playback mode without advancing native time. Paused Step remains caller-driven.</summary>
    /// <param name="playing">Actual runtime mode.</param>
    public void SetPlaying(bool playing)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_busy || playing && (_disposing || _physics?.State != PhysicsWorldState.Ready)) throw new InvalidOperationException("Physics subsystem cannot change playback.");
    }
    /// <summary>Disposes only the owned native world. Failed cleanup is retained; borrowed ECS World/App dependencies remain alive.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        if (_busy) throw new InvalidOperationException("Leave the physics step before cleanup.");
        _disposing = true; _busy = true;
        try
        {
            _physics?.Dispose(); _physics = null;
            _entries.Clear(); _entities.Clear(); _retired.Clear(); _candidates.Clear(); _candidatePool.Clear(); _removals.Clear(); _present.Clear(); _commands.Clear(); _oldEvents.Clear(); _contacts.Clear(); _raw = [];
            Systems = []; _disposed = true;
        }
        finally { _busy = false; }
    }
    private void Ready()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_busy || _disposing || _physics?.State != PhysicsWorldState.Ready) throw new InvalidOperationException("Physics subsystem is not ready.");
    }
    private void ValidateAuthoredRoot(EntityHandle entity)
    {
        foreach (var pair in _scene.Entities)
        {
            if (pair.Value != entity) continue;
            if (_scene.Scene.Entities[pair.Key].ParentId != null) throw new NotSupportedException("Physics PHYS5 requires root scene entities.");
            return;
        }
        throw new InvalidDataException("Physics entity is absent from the private authored scene.");
    }
    // Per-world bounded scratch, reset before every full capture. Never a published entity/descriptor cache.
    private sealed class Candidate
    {
        internal EntityHandle Entity;
        internal PhysicsBody Descriptor;
        internal LocalTransform Transform;
        internal PhysicsVelocity Velocity;
        internal PhysicsTeleport? Teleport;
        internal PhysicsShapeDefinition Shape = null!;
        internal Entry? Previous;
        internal PhysicsCharacter? Character;
        internal PhysicsCharacterInput Input;
        internal TrueMoon.Kton.Characters.PhysicsCharacterOptions? CharacterOptions;
        internal void Reset()
        {
            Entity = default; Descriptor = default; Transform = default; Velocity = default; Teleport = null;
            Shape = null!; Previous = null; Character = null; Input = default; CharacterOptions = null;
        }
    }
    private sealed class Entry(PhysicsBody descriptor, PhysicsShapeId shape, PhysicsShapeDefinition definition, PhysicsBodyId body, LocalTransform transform, PhysicsVelocity velocity, PhysicsBodyDescription description)
    {
        internal PhysicsBody Descriptor = descriptor;
        internal readonly PhysicsBodyDescription BodyDescription = description;
        internal PhysicsShapeId Shape = shape;
        internal PhysicsShapeDefinition ShapeDefinition = definition;
        internal PhysicsBodyId Body = body;
        internal LocalTransform LastTransform = transform;
        internal PhysicsVelocity LastVelocity = velocity;
        internal PhysicsTeleport LastTeleport;
        internal TrueMoon.Kton.Characters.PhysicsCharacterId Character;
        internal PhysicsCharacter? CharacterDescriptor;
        internal TrueMoon.Kton.Characters.PhysicsCharacterOptions? CharacterOptions;
        internal ulong LastJumpSequence;
        internal PhysicsCharacterState LastCharacterState;
    }
}
