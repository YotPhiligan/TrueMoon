using TrueMoon.Aether.Abstractions.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Motion;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Aether.Runtime.Ecs.Transforms;

namespace TrueMoon.Aether.Runtime.Ecs.Rendering;

/// <summary>Factory input for private initial scene preparation.</summary>
/// <param name="Scene">Borrowed private scene. Discard it if preparation fails.</param><param name="CancellationToken">Preparation cancellation.</param>
public sealed record PrimitiveSimulationRequest(SceneWorld Scene, CancellationToken CancellationToken = default);

/// <summary>Owns a scheduler and optional subsystems, borrows its scene, and publishes completed-tick detached frames.</summary>
public sealed class PrimitiveSimulation : IDisposable
{
    private readonly World _world;
    private readonly TimeSpan _step;
    private EcsScheduler? _scheduler;
    private IDisposable? _subsystemOwner;
    private PrimitiveRenderSnapshot? _snapshot;
    private bool _disposed, _busy, _disposing;
    internal TransformSystem? Transforms;
    internal MovementSystem? Movement;
    internal PrimitiveExtractionSystem? Extraction;
    internal SceneSubsystemSession? Subsystems;
    internal PrimitiveSimulation(World world, TimeSpan step) { _world = world; _step = step; }
    internal void Attach(EcsScheduler scheduler) => _scheduler = scheduler;
    internal void AttachSubsystems(IDisposable owner) => _subsystemOwner = owner;
    internal void Prime(SceneWorld scene, CancellationToken token)
    {
        if (!ReferenceEquals(Transforms?.Owner, _world) || !ReferenceEquals(Movement?.Owner, _world) || !ReferenceEquals(Extraction?.Owner, _world))
            throw new InvalidOperationException("A primitive system factory returned a system for another World.");
        token.ThrowIfCancellationRequested();
        Transforms.InitializeScene(scene, token); Transforms.Update(0, token);
        var snapshot = Extraction.Capture(0, token);
        if (snapshot.WorldId != _world.Id || snapshot.SceneId != scene.Scene.Id || snapshot.Revision != scene.Scene.Revision)
            throw new InvalidDataException("Primitive extraction factory changed the source scene identity/revision.");
        token.ThrowIfCancellationRequested(); _snapshot = snapshot;
    }
    /// <summary>Gets the borrowed World identity.</summary>
    public Guid WorldId => _world.Id;
    /// <summary>Gets completed scheduler ticks, including initial zero.</summary>
    public ulong TickNumber => _scheduler?.TickNumber ?? 0;
    /// <summary>Notifies optional real-time resources without advancing simulation.</summary><param name="playing">Actual runtime mode.</param>
    public void SetPlaying(bool playing)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_busy || _disposing) throw new InvalidOperationException("Primitive simulation is busy or cleaning up.");
        _busy = true;
        try { Subsystems?.SetPlaying(playing); }
        finally { _busy = false; }
    }
    /// <summary>Gets the last complete detached frame. Captured values remain valid after World/controller disposal.</summary>
    public PrimitiveRenderSnapshot Snapshot
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_disposing) throw new InvalidOperationException("Primitive simulation cleanup has started.");
            return _snapshot ?? throw new InvalidOperationException("No primitive frame is ready.");
        }
    }
    /// <summary>Runs movement, transform and extraction with the copied fixed step. Failed/stopped ticks publish no frame.</summary>
    /// <param name="cancellationToken">Cooperative tick cancellation.</param>
    public void Tick(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_busy || _disposing) throw new InvalidOperationException("Primitive simulation is busy or cleaning up.");
        var scheduler = _scheduler ?? throw new InvalidOperationException("Primitive simulation is not prepared.");
        _busy = true;
        try
        {
            if (!scheduler.Tick(_step, cancellationToken)) throw new InvalidOperationException("Primitive simulation was stopped before completing its tick.");
            var frame = Extraction!.Snapshot ?? throw new InvalidOperationException("Extraction produced no frame.");
            if (frame.Tick != scheduler.TickNumber || frame.WorldId != _world.Id) throw new InvalidDataException("Primitive frame tick/World mismatch.");
            _snapshot = frame;
        }
        catch (Exception error)
        {
            try { Subsystems?.SetPlaying(false); }
            catch (Exception pause) { throw new AggregateException(error, pause); }
            throw;
        }
        finally { _busy = false; }
    }
    /// <summary>Disposes the owned graph before the borrowed scene. Failed cleanup is retained for retry.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        if (_busy) throw new InvalidOperationException("Leave the primitive tick before disposal.");
        _busy = true; _disposing = true;
        try
        {
            _scheduler?.Dispose(); _scheduler = null;
            _subsystemOwner?.Dispose(); _subsystemOwner = null; Subsystems = null;
            Transforms = null; Movement = null; Extraction = null; _snapshot = null; _disposed = true;
        }
        finally { _busy = false; }
    }
}
