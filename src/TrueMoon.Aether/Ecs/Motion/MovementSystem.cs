using System.Numerics;
using TrueMoon.Aether.Ecs.Transforms;

namespace TrueMoon.Aether.Ecs.Motion;

/// <summary>Preflights finite parent-space displacement, then updates local positions before transform evaluation.</summary>
public sealed class MovementSystem : IEcsSystem
{
    private readonly World _world;
    private readonly int _maximumEntities;
    private readonly List<(EntityHandle Entity, LocalTransform Value)> _prepared = [];
    private bool _disposed, _updating;
    /// <summary>Creates a movement system without mutating a World.</summary>
    /// <param name="world">Borrowed externally serialized World.</param><param name="maximumEntities">Velocity admission count,1..1048576.</param>
    public MovementSystem(World world, int maximumEntities = 65536)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (maximumEntities is < 1 or > 1048576) throw new ArgumentOutOfRangeException(nameof(maximumEntities));
        _world = world; _maximumEntities = maximumEntities;
    }
    internal World Owner => _world;
    /// <summary>Creates fresh Movement phase metadata.</summary><returns>Read velocity/write local pose description.</returns>
    public static EcsSystemDescription Describe() => new EcsSystemDescription("builtin.movement", EcsPhase.Movement).Read<LinearVelocity>().Write<LocalTransform>();
    /// <summary>Applies one positive step. Preflight failure/cancellation leaves every position unchanged.</summary>
    /// <param name="deltaTime">Positive duration.</param><param name="cancellationToken">Preflight cancellation.</param>
    public void Advance(TimeSpan deltaTime, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this); ObjectDisposedException.ThrowIf(_world.IsDisposed, _world);
        if (_world.IsFaulted || _updating) throw new InvalidOperationException("Movement is faulted or reentrant.");
        if (deltaTime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(deltaTime));
        cancellationToken.ThrowIfCancellationRequested(); _updating = true;
        try
        {
            _prepared.Clear(); var seconds = deltaTime.TotalSeconds;
            _world.Query<LinearVelocity>().ForEach((EntityHandle entity, in LinearVelocity velocity) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_prepared.Count == _maximumEntities) throw new TransformEvaluationException(TransformEvaluationError.LimitExceeded, entity);
                if (!_world.HasComponent<LocalTransform>(entity)) throw new TransformEvaluationException(TransformEvaluationError.IncompleteNode, entity);
                var local = _world.GetComponent<LocalTransform>(entity);
                try { local.CreateMatrix(); }
                catch (InvalidDataException error) { throw new TransformEvaluationException(TransformEvaluationError.InvalidLocalTransform, entity, inner: error); }
                try
                {
                    velocity.Validate();
                    var position = new Vector3(TransformMath.Displace(local.Position.X, velocity.Value.X, seconds),
                        TransformMath.Displace(local.Position.Y, velocity.Value.Y, seconds), TransformMath.Displace(local.Position.Z, velocity.Value.Z, seconds));
                    _prepared.Add((entity, local with { Position = position }));
                }
                catch (InvalidDataException error) { throw new TransformEvaluationException(TransformEvaluationError.InvalidVelocity, entity, inner: error); }
            });
            cancellationToken.ThrowIfCancellationRequested();
            try { foreach (var value in _prepared) _world.SetComponent(value.Entity, value.Value); }
            catch (Exception error) { _world.MarkFaulted(error); throw; }
        }
        finally { _updating = false; }
    }
    /// <inheritdoc />
    public void Execute(EcsSystemContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!ReferenceEquals(context.World, _world)) throw new InvalidOperationException("Movement system World mismatch.");
        Advance(context.DeltaTime, context.CancellationToken);
    }
    /// <summary>Releases prepared scratch values, never the borrowed World.</summary>
    public void Dispose()
    {
        if (_updating) throw new InvalidOperationException("Leave movement before disposal.");
        if (_disposed) return; _disposed = true; _prepared.Clear();
    }
}
