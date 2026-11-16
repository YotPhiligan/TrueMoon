using BepuPhysics;
using BepuUtilities;
using BepuUtilities.Memory;

namespace TrueMoon.Kton.Bepu;

internal sealed class WorldResources
{
    internal readonly BufferPool Pool = new();
    internal ThreadDispatcher? Dispatcher;
    internal Simulation? Simulation;
    internal Characters.CharacterControllers? Characters;
    private readonly BepuPhysicsWorldFactory _factory;
    private readonly FaultInjection _faults;
    private bool _simulationDisposed, _dispatcherDisposed, _released;
    private bool _budgetReported;
    private long _diagnosticBudget = long.MaxValue;
    internal WorldResources(BepuPhysicsWorldFactory factory, FaultInjection faults) { _factory = factory; _faults = faults; factory.Acquired(); }
    internal void Initialize(BepuWorld world)
    {
        _diagnosticBudget = world.Options.Limits.PoolDiagnosticBudgetBytes;
        _faults.Visit(Checkpoint.PoolCreated);
        Dispatcher = new ThreadDispatcher(world.Profile.Workers); _faults.Visit(Checkpoint.DispatcherCreated);
        Characters = new(Pool, Math.Min(16, world.Options.Limits.MaxBodies), world.Options.Limits.MaxBodies);
        Simulation = BepuPhysics.Simulation.Create(Pool, new NarrowPhase(world), new PoseIntegrator(world.Options.Gravity),
            new SolveDescription(world.Profile.SolverIterations, world.Profile.SolverSubsteps));
        _faults.Visit(Checkpoint.SimulationCreated); Sample();
    }
    internal long PoolBytes => checked((long)(Pool.GetTotalAllocatedByteCount() + (Dispatcher?.WorkerPools.GetTotalAllocatedByteCount() ?? 0)));
    internal void Sample()
    {
        var bytes = PoolBytes; _factory.Sample(bytes);
        if (!_budgetReported && bytes > _diagnosticBudget) { _budgetReported = true; _factory.PoolBudgetExceeded(); }
    }
    internal void Dispose()
    {
        if (_released) return;
        Sample();
        if (!_simulationDisposed) { _faults.Visit(Checkpoint.BeforeSimulationDispose); Characters?.Dispose(); Simulation?.Dispose(); _simulationDisposed = true; }
        if (!_dispatcherDisposed) { _faults.Visit(Checkpoint.BeforeDispatcherDispose); Dispatcher?.Dispose(); _dispatcherDisposed = true; }
        _faults.Visit(Checkpoint.BeforePoolClear); Pool.Clear();
        var remaining = PoolBytes;
        if (remaining != 0) throw new InvalidOperationException("BEPU pools remain allocated after cleanup.");
        _factory.Released(remaining); _released = true;
    }
}
