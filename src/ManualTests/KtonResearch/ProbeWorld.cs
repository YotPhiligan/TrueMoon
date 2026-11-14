using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities;
using BepuUtilities.Memory;

namespace KtonResearch;

// Research-only owner/factory. No claim that this is PHYS1 API or App/DI integration.
internal static class ResearchWorldFactory
{
    internal static ProbeWorld Create(Vector3 gravity, int substeps = 4, int collisionSteps = 1, float contactFrequency = 120) => new(gravity, substeps, collisionSteps, contactFrequency);
}

internal sealed class ProbeWorld : IDisposable
{
    internal const float Delta = 1f / 60f;
    internal const int MaximumBodies = 1024;
    internal const ulong PoolBudgetBytes = 16 * 1024 * 1024;
    internal static int LiveOwners { get; private set; }
    internal static ulong MaximumPoolBytes { get; private set; }
    internal static ulong MaximumBytesAfterCleanup { get; private set; }
    internal readonly BufferPool Pool = new();
    internal readonly ContactBuffer ContactBuffer = new();
    private readonly ThreadDispatcher _dispatcher;
    private readonly int _collisionSteps;
    internal readonly Simulation Simulation;
    private bool _disposed;
    private int _bodies;
    private int _shapes;

    internal ProbeWorld(Vector3 gravity, int substeps, int collisionSteps, float contactFrequency)
    {
        if (!Finite(gravity) || substeps < 1 || collisionSteps < 1 || !float.IsFinite(contactFrequency) || contactFrequency <= 0)
            throw new ArgumentOutOfRangeException(nameof(gravity), "Invalid research world preset.");
        ThreadDispatcher? dispatcher = null;
        Simulation? simulation = null;
        try
        {
            dispatcher = new ThreadDispatcher(1);
            simulation = BepuPhysics.Simulation.Create(Pool, new Contacts(ContactBuffer, contactFrequency), new Integrator(gravity), new BepuPhysics.SolveDescription(8, substeps));
            _dispatcher = dispatcher;
            _collisionSteps = collisionSteps;
            Simulation = simulation;
            LiveOwners++;
        }
        catch { simulation?.Dispose(); dispatcher?.Dispose(); Pool.Clear(); throw; }
    }

    private void Check() => ObjectDisposedException.ThrowIf(_disposed, this);
    internal TypedIndex Shape<T>(T shape) where T : unmanaged, IShape
    {
        Check();
        if (_shapes == 2048) throw new InvalidOperationException("Research shape limit reached.");
        var result = Simulation.Shapes.Add(shape); _shapes++; return result;
    }
    internal BodyHandle Dynamic<T>(T shape, Vector3 position, float mass = 1, Vector3 velocity = default, bool ccd = false, float ccdTimeTolerance = 1e-6f, float maximumSpeculativeMargin = float.MaxValue)
        where T : unmanaged, IConvexShape
    {
        Check();
        if (_bodies == MaximumBodies) throw new InvalidOperationException("Research body limit reached.");
        if (!float.IsFinite(mass) || mass <= 0 || !Finite(position) || !Finite(velocity)) throw new ArgumentOutOfRangeException(nameof(mass));
        var description = BodyDescription.CreateDynamic(new RigidPose(position), new BodyVelocity(velocity),
            shape.ComputeInertia(mass), new CollidableDescription(Shape(shape), maximumSpeculativeMargin,
                ccd ? ContinuousDetection.Continuous(ccdTimeTolerance, ccdTimeTolerance) : ContinuousDetection.Passive), new BodyActivityDescription(0.01f));
        var body = Simulation.Bodies.Add(description); _bodies++; return body;
    }
    internal BodyHandle SharedDynamic(TypedIndex shape, BodyInertia inertia, Vector3 position)
    {
        Check(); if (_bodies == MaximumBodies) throw new InvalidOperationException("Research body limit reached.");
        var result = Simulation.Bodies.Add(BodyDescription.CreateDynamic(position, inertia, shape, 0.01f)); _bodies++; return result;
    }
    internal BodyHandle Kinematic(Vector3 position, Vector3 velocity)
    {
        Check(); if (_bodies == MaximumBodies) throw new InvalidOperationException("Research body limit reached.");
        var result = Simulation.Bodies.Add(BodyDescription.CreateKinematic(new RigidPose(position), new BodyVelocity(velocity), Shape(new Box(1, 1, 1)), 0.01f));
        _bodies++; return result;
    }
    internal StaticHandle StaticBox(Vector3 position, Vector3 dimensions)
    {
        Check(); return Simulation.Statics.Add(new StaticDescription(position, Shape(new Box(dimensions.X, dimensions.Y, dimensions.Z))));
    }
    internal void MeshFloor()
    {
        Check();
        Pool.Take<Triangle>(2, out var triangles);
        triangles[0] = new Triangle(new(-10, 0, -10), new(10, 0, -10), new(-10, 0, 10));
        triangles[1] = new Triangle(new(10, 0, -10), new(10, 0, 10), new(-10, 0, 10));
        var mesh = new Mesh(triangles, Vector3.One, Pool);
        Simulation.Statics.Add(new StaticDescription(Vector3.Zero, Shape(mesh)));
    }
    internal Vector3 Position(BodyHandle body) { Check(); return Simulation.Bodies[body].Pose.Position; }
    internal Vector3 Velocity(BodyHandle body) { Check(); return Simulation.Bodies[body].Velocity.Linear; }
    internal void Impulse(BodyHandle body, Vector3 impulse)
    {
        Check(); var reference = Simulation.Bodies[body]; reference.Awake = true; reference.ApplyLinearImpulse(impulse);
    }
    internal void ForceForNextStep(BodyHandle body, Vector3 force) => Impulse(body, force * Delta);
    internal void Remove(BodyHandle body) { Check(); Simulation.Bodies.Remove(body); _bodies--; }
    internal void Step(int count = 1)
    {
        Check();
        for (var i = 0; i < count; i++)
        {
            ContactBuffer.Clear();
            for (var collision = 0; collision < _collisionSteps; collision++)
                Simulation.Timestep(Delta / _collisionSteps, _dispatcher);
            if (ContactBuffer.Overflow) throw new InvalidOperationException("Research callback capacity exceeded.");
        }
        MaximumPoolBytes = Math.Max(MaximumPoolBytes, Pool.GetTotalAllocatedByteCount() + _dispatcher.WorkerPools.GetTotalAllocatedByteCount());
    }
    internal static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    public void Dispose()
    {
        if (_disposed) return;
        Simulation.Dispose();
        _dispatcher.Dispose();
        Pool.Clear();
        MaximumBytesAfterCleanup = Math.Max(MaximumBytesAfterCleanup, Pool.GetTotalAllocatedByteCount() + _dispatcher.WorkerPools.GetTotalAllocatedByteCount());
        _disposed = true; LiveOwners--;
    }
}
