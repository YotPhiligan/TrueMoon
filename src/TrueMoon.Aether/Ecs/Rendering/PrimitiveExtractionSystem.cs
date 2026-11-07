using System.Collections.Immutable;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Ecs.Rendering;

/// <summary>Typed factory arguments; the system copies mutable options.</summary>
/// <param name="World">Borrowed serialized World.</param><param name="SceneId">Persistent scene identity.</param>
/// <param name="Revision">Initial nonnegative source revision.</param><param name="Options">Optional copied limits and camera.</param>
public sealed record PrimitiveExtractionArguments(World World, SceneId SceneId, long Revision, PrimitiveSimulationOptions? Options = null);

/// <summary>Validates all primitive roles before replacing its detached current snapshot.</summary>
public sealed class PrimitiveExtractionSystem : IEcsSystem
{
    private readonly World _world;
    private readonly SceneId _scene;
    private readonly long _revision;
    private readonly PrimitiveSimulationOptions _options;
    private readonly List<PrimitiveRenderItem> _items = [];
    private bool _disposed, _capturing;
    /// <summary>Creates an extractor without reading a World or publishing a frame.</summary>
    /// <param name="arguments">Borrowed World and copied view configuration.</param>
    public PrimitiveExtractionSystem(PrimitiveExtractionArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(arguments.World);
        if (arguments.SceneId.Value == Guid.Empty) throw new ArgumentException("A scene identity is required.", nameof(arguments));
        if (arguments.Revision < 0) throw new ArgumentOutOfRangeException(nameof(arguments));
        _world = arguments.World; _scene = arguments.SceneId; _revision = arguments.Revision; _options = (arguments.Options ?? new()).Snapshot();
    }
    internal World Owner => _world;
    /// <summary>Gets the last complete detached frame, or null before capture/after disposal.</summary>
    public PrimitiveRenderSnapshot? Snapshot { get; private set; }
    /// <summary>Creates fresh Extraction phase read metadata.</summary><returns>Composition-time description.</returns>
    public static EcsSystemDescription Describe() => new EcsSystemDescription("builtin.primitive-extraction", EcsPhase.Extraction)
        .Read<PrimitiveAppearance>().Read<WorldTransform>();
    /// <summary>Captures complete current-tick matrices and material values, with no partial result on failure/cancellation.</summary>
    /// <param name="tick">Transform/extraction stamp, strictly newer than the previous frame; first zero is allowed.</param>
    /// <param name="cancellationToken">Preparation cancellation.</param><returns>New detached frame.</returns>
    public PrimitiveRenderSnapshot Capture(ulong tick, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this); ObjectDisposedException.ThrowIf(_world.IsDisposed, _world);
        if (_capturing || _world.IsFaulted) throw new InvalidOperationException("Extraction is faulted or reentrant.");
        cancellationToken.ThrowIfCancellationRequested();
        if (Snapshot is { } previous && tick <= previous.Tick) throw new InvalidOperationException("Extraction tick must increase.");
        _capturing = true;
        try
        {
            _items.Clear();
            _world.Query<PrimitiveAppearance>().ForEach((EntityHandle entity, in PrimitiveAppearance appearance) =>
            {
                cancellationToken.ThrowIfCancellationRequested(); appearance.Validate();
                if (!_world.TryGetComponent<WorldTransform>(entity, out var transform)) throw new InvalidDataException("A drawable is missing its derived WorldTransform.");
                if (transform.Tick != tick) throw new InvalidDataException("Drawable transform and extraction ticks differ.");
                var primitive = new RenderPrimitive(transform.Matrix, appearance.Color); primitive.Validate();
                if (!appearance.Enabled) return;
                if (_items.Count == _options.MaximumPrimitives) throw new InvalidDataException("Primitive drawable limit exceeded; no frame was published.");
                _items.Add(new(entity, primitive));
            });
            _items.Sort(static (left, right) => left.Entity.Index.CompareTo(right.Entity.Index));
            var items = _items.ToImmutableArray(); var scene = _options.Scene(items.Select(item => item.Primitive)); scene.Validate();
            var snapshot = new PrimitiveRenderSnapshot(_world.Id, _scene, _revision, tick, items, scene);
            cancellationToken.ThrowIfCancellationRequested(); Snapshot = snapshot; return snapshot;
        }
        finally { _capturing = false; }
    }
    /// <inheritdoc />
    public void Execute(EcsSystemContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!ReferenceEquals(context.World, _world)) throw new InvalidOperationException("Extraction system World mismatch.");
        Capture(context.TickNumber, context.CancellationToken);
    }
    /// <summary>Releases scratch/current references. Previously returned snapshots remain valid.</summary>
    public void Dispose()
    {
        if (_capturing) throw new InvalidOperationException("Leave extraction before disposal.");
        if (_disposed) return; _disposed = true; _items.Clear(); Snapshot = null;
    }
}
