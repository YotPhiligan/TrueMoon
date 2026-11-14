using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;

namespace KtonResearch;

// Protocol signatures follow the self-contained demo at the exact package repository commit.
// This probe implements its own policies; it does not copy the demo infrastructure.
internal readonly record struct ContactSample(uint A, uint B, int ContactCount);

internal sealed class ContactBuffer
{
    internal readonly ContactSample[] Samples = new ContactSample[8192];
    internal int Count;
    internal bool Overflow;
    internal int? ExcludedBody;
    internal int? SensorBody;
    internal bool IsBody(CollidableReference c, int? body) => body.HasValue && c.Mobility != CollidableMobility.Static && c.BodyHandle.Value == body.Value;
    internal void Clear() { Count = 0; Overflow = false; }
    internal void Record(CollidablePair pair, int count)
    {
        // The research profile uses one worker. No ECS, IO, exceptions, or unbounded allocation here.
        if (Count == Samples.Length) { Overflow = true; return; }
        Samples[Count++] = new(pair.A.Packed, pair.B.Packed, count);
    }
}

internal struct Contacts(ContactBuffer buffer, float contactFrequency) : INarrowPhaseCallbacks
{
    public void Initialize(Simulation simulation) { }
    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
        => !buffer.IsBody(a, buffer.ExcludedBody) && !buffer.IsBody(b, buffer.ExcludedBody)
           && (a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic);
    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) => true;
    public bool ConfigureContactManifold<T>(int workerIndex, CollidablePair pair, ref T manifold, out PairMaterialProperties material)
        where T : unmanaged, IContactManifold<T>
    {
        material = new PairMaterialProperties(0.8f, 2f, new SpringSettings(contactFrequency, 1));
        if (manifold.Count > 0) buffer.Record(pair, manifold.Count);
        return !buffer.IsBody(pair.A, buffer.SensorBody) && !buffer.IsBody(pair.B, buffer.SensorBody);
    }
    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold) => true;
    public void Dispose() { }
}

internal struct Integrator(Vector3 gravity) : IPoseIntegratorCallbacks
{
    private Vector3Wide _gravityDt;
    public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
    public readonly bool AllowSubstepsForUnconstrainedBodies => false;
    public readonly bool IntegrateVelocityForKinematics => false;
    public void Initialize(Simulation simulation) { }
    public void PrepareForIntegration(float dt) => _gravityDt = Vector3Wide.Broadcast(gravity * dt);
    public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation,
        BodyInertiaWide inertia, Vector<int> mask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
        => velocity.Linear += _gravityDt;
}
