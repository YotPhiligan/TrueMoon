using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Ecs.Runtime;

/// <summary>Initial ECS game profile using the existing cube renderer with a fixed-view camera.</summary>
/// <remarks>The session owns its prepared simulation through the base class. Loader/factory dependencies are borrowed.</remarks>
public class PrimitiveGameSession : SceneWorldGameSession, IRenderGameSession
{
    private readonly IFactory<PrimitiveSimulation, PrimitiveSimulationRequest> _simulations;
    private PrimitiveSimulation? _simulation;
    /// <summary>Creates a game without loading data, starting systems or allocating native resources.</summary>
    /// <param name="loader">Registry-specific borrowed loader.</param><param name="simulations">Borrowed typed controller factory.</param>
    public PrimitiveGameSession(SceneWorldLoader loader, IFactory<PrimitiveSimulation, PrimitiveSimulationRequest> simulations) : base(loader)
    { ArgumentNullException.ThrowIfNull(simulations); _simulations = simulations; }
    /// <summary>CPU compatibility frame color; the GPU consumer uses CaptureRenderScene.</summary>
    public override uint PatternColor => 0xff2080c0;
    /// <inheritdoc />
    protected override IDisposable PrepareSceneWorld(SceneWorld scene, CancellationToken cancellationToken)
    {
        _simulation = _simulations.Create(new(scene, cancellationToken)) ?? throw new InvalidOperationException("Primitive simulation factory returned null.");
        return _simulation;
    }
    /// <inheritdoc />
    protected override void TickWorld(SceneWorld scene, ulong nextTick)
    {
        var simulation = _simulation ?? throw new InvalidOperationException("Primitive simulation is not ready.");
        if (simulation.WorldId != scene.World.Id || simulation.TickNumber != nextTick - 1) throw new InvalidDataException("Primitive simulation tick/World mismatch.");
        simulation.Tick();
        if (simulation.TickNumber != nextTick) throw new InvalidDataException("Primitive simulation did not complete exactly one tick.");
    }
    /// <summary>Returns detached values from the last completed tick without reading/mutating component pools.</summary>
    /// <returns>Immutable frame accepted by the existing primitive renderer.</returns>
    public PrimitiveScene CaptureRenderScene()
    {
        var scene = ActiveScene ?? throw new InvalidOperationException("No primitive scene is active.");
        if (!scene.IsReady || scene.World.IsDisposed || scene.World.IsFaulted) throw new InvalidOperationException("Primitive scene is not ready.");
        var snapshot = (_simulation ?? throw new InvalidOperationException("Primitive simulation is not ready.")).Snapshot;
        if (snapshot.WorldId != scene.World.Id || snapshot.Tick != TickNumber || snapshot.Revision != Revision)
            throw new InvalidDataException("Primitive render snapshot is stale.");
        return snapshot.Scene;
    }
}
