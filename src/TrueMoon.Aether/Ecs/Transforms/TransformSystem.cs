using System.Numerics;
using TrueMoon.Aether.Ecs.Loading;

namespace TrueMoon.Aether.Ecs.Transforms;

/// <summary>Bounded iterative hierarchy evaluator. Borrows one externally serialized World.</summary>
/// <remarks>Preflight failures/cancellation preserve all previous caches. Commit is not interrupted after its final check.</remarks>
public sealed class TransformSystem : IEcsSystem
{
    private readonly World _world;
    private readonly int _maximumEntities;
    private readonly List<Node> _nodes = [];
    private readonly Dictionary<EntityHandle, int> _indices = [];
    private readonly List<int> _stack = [];
    private bool _disposed, _updating, _initialized;
    /// <summary>Creates an evaluator without mutating or reserving a World.</summary>
    /// <param name="world">Borrowed World.</param><param name="maximumEntities">Admission count,1..1048576; not a byte quota.</param>
    public TransformSystem(World world, int maximumEntities = 65536)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (maximumEntities is < 1 or > 1048576) throw new ArgumentOutOfRangeException(nameof(maximumEntities));
        _world = world; _maximumEntities = maximumEntities;
    }
    /// <summary>Gets the last fully published evaluation, or null before the first update.</summary>
    public ulong? LastTick { get; private set; }
    internal World Owner => _world;
    /// <summary>Creates fresh access metadata for the Transform phase.</summary><returns>Composition-time description.</returns>
    public static EcsSystemDescription Describe() => new EcsSystemDescription("builtin.transform", EcsPhase.Transform)
        .Read<LocalTransform>().Read<TransformParent>().Write<WorldTransform>();

    /// <summary>Seeds identity groups, scene parent edges and derived caches in a private initial scene.</summary>
    /// <remarks>The caller must discard the private scene on failure; initialization is not a general World transaction.</remarks>
    /// <param name="scene">Fresh loaded scene borrowing this World, before game activation.</param><param name="cancellationToken">Preparation cancellation.</param>
    public void InitializeScene(SceneWorld scene, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene); Enter();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_initialized || LastTick != null || !scene.IsReady || !ReferenceEquals(scene.World, _world) || scene.Entities.Count != _world.EntityCount)
                throw new InvalidOperationException("Transform initialization requires this fresh private scene.");
            if (scene.Entities.Count > _maximumEntities) throw Failure(TransformEvaluationError.LimitExceeded, default);
            foreach (var entity in scene.Entities.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_world.IsAlive(entity) || _world.HasComponent<TransformParent>(entity) || _world.HasComponent<WorldTransform>(entity))
                    throw new InvalidOperationException("Scene already has derived hierarchy roles or stale identities.");
                if (_world.TryGetComponent<LocalTransform>(entity, out var local)) local.CreateMatrix();
            }
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var id in scene.Scene.HierarchyOrder)
            {
                var entity = scene.Entities[id];
                if (!_world.HasComponent<LocalTransform>(entity)) _world.AddComponent(entity, LocalTransform.Identity);
                var parent = scene.Scene.Entities[id].ParentId is { } parentId ? scene.Entities[parentId] : default;
                _world.AddComponent(entity, new TransformParent(parent)); _world.AddComponent(entity, default(WorldTransform));
            }
            _initialized = true;
        }
        finally { _updating = false; }
    }
    /// <summary>Prepares all matrices and publishes them together at a strictly increasing tick; first zero is allowed.</summary>
    /// <param name="tick">Evaluation stamp.</param><param name="cancellationToken">Preflight cancellation.</param>
    public void Update(ulong tick, CancellationToken cancellationToken = default)
    {
        Enter();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (LastTick is { } previous && tick <= previous) throw new InvalidOperationException("Transform tick must increase.");
            _nodes.Clear(); _indices.Clear(); _stack.Clear();
            _world.Query<LocalTransform>().ForEach((EntityHandle entity, in LocalTransform local) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_nodes.Count == _maximumEntities) throw Failure(TransformEvaluationError.LimitExceeded, entity);
                if (!_world.HasComponent<TransformParent>(entity) || !_world.HasComponent<WorldTransform>(entity)) throw Failure(TransformEvaluationError.IncompleteNode, entity);
                var parent = _world.GetComponent<TransformParent>(entity).Parent;
                Matrix4x4 matrix;
                try { matrix = local.CreateMatrix(); }
                catch (InvalidDataException error) { throw Failure(TransformEvaluationError.InvalidLocalTransform, entity, parent, error); }
                _nodes.Add(new(entity, parent, matrix));
            });
            _world.Query<TransformParent>().ForEach((EntityHandle entity, in TransformParent parent) =>
            { cancellationToken.ThrowIfCancellationRequested(); if (!_world.HasComponent<LocalTransform>(entity)) throw Failure(TransformEvaluationError.IncompleteNode, entity, parent.Parent); });
            _world.Query<WorldTransform>().ForEach((EntityHandle entity, in WorldTransform cache) =>
            { cancellationToken.ThrowIfCancellationRequested(); if (!_world.HasComponent<LocalTransform>(entity)) throw Failure(TransformEvaluationError.IncompleteNode, entity); });
            _nodes.Sort(static (left, right) => left.Entity.Index.CompareTo(right.Entity.Index));
            for (var i = 0; i < _nodes.Count; i++) _indices.Add(_nodes[i].Entity, i);
            for (var start = 0; start < _nodes.Count; start++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_nodes[start].State == 2) continue;
                var current = start;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested(); var node = _nodes[current];
                    if (node.State == 2) break;
                    if (node.State == 1) throw Failure(TransformEvaluationError.Cycle, node.Entity, node.Parent);
                    node.State = 1; _nodes[current] = node; _stack.Add(current);
                    if (node.Parent == default) break;
                    if (!_world.IsAlive(node.Parent) || !_indices.TryGetValue(node.Parent, out current))
                        throw Failure(TransformEvaluationError.InvalidParent, node.Entity, node.Parent);
                }
                while (_stack.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var index = _stack[^1]; _stack.RemoveAt(_stack.Count - 1); var node = _nodes[index];
                    node.World = node.Parent == default ? node.Local : node.Local * _nodes[_indices[node.Parent]].World;
                    try { TransformMath.ValidateMatrix(node.World); }
                    catch (InvalidDataException error) { throw Failure(TransformEvaluationError.InvalidWorldTransform, node.Entity, node.Parent, error); }
                    node.State = 2; _nodes[index] = node;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                foreach (var node in _nodes) _world.SetComponent(node.Entity, new WorldTransform(node.World, tick));
                LastTick = tick;
            }
            catch (Exception error) { _world.MarkFaulted(error); throw; }
        }
        finally { _updating = false; }
    }
    /// <inheritdoc />
    public void Execute(EcsSystemContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!ReferenceEquals(context.World, _world)) throw new InvalidOperationException("Transform system World mismatch.");
        Update(context.TickNumber, context.CancellationToken);
    }
    /// <summary>Releases scratch collections. The World and published caches are borrowed.</summary>
    public void Dispose()
    {
        if (_updating) throw new InvalidOperationException("Leave transform evaluation before disposal.");
        if (_disposed) return;
        _disposed = true; _nodes.Clear(); _indices.Clear(); _stack.Clear();
    }
    private void Enter()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ObjectDisposedException.ThrowIf(_world.IsDisposed, _world);
        if (_world.IsFaulted || _updating) throw new InvalidOperationException("Transform evaluator is faulted or reentrant.");
        _updating = true;
    }
    private static TransformEvaluationException Failure(TransformEvaluationError error, EntityHandle entity, EntityHandle parent = default, Exception? inner = null)
        => new(error, entity, parent, inner);
    private struct Node(EntityHandle entity, EntityHandle parent, Matrix4x4 local)
    {
        internal EntityHandle Entity = entity, Parent = parent;
        internal Matrix4x4 Local = local, World;
        internal byte State;
    }
}
