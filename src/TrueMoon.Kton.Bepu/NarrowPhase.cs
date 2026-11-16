using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;

namespace TrueMoon.Kton.Bepu;

internal struct NarrowPhase(BepuWorld world) : INarrowPhaseCallbacks
{
    public void Initialize(Simulation simulation) => world.Resources.Characters!.Initialize(simulation);
    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
    {
        if (world.Faults.FailContactCallback) { world.CallbackFailed = true; return false; }
        if (!world.ByBackend.TryGetValue(a.Packed, out var entryA) || !world.ByBackend.TryGetValue(b.Packed, out var entryB))
        { world.CallbackFailed = true; return false; }
        return (a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic)
            && entryA.Description.Filter.Allows(entryB.Description.Filter)
            && !world.DisabledPairs.ContainsKey(BepuWorld.Pair(entryA.Id.Slot, entryB.Id.Slot));
    }
    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) => true;
    public bool ConfigureContactManifold<T>(int workerIndex, CollidablePair pair, ref T manifold, out PairMaterialProperties material)
        where T : unmanaged, IContactManifold<T>
    {
        var profile = world.Profile;
        material = new PairMaterialProperties(0, profile.MaximumRecoveryVelocity,
            new SpringSettings(profile.ContactSpringFrequency, profile.ContactDampingRatio));
        if (!world.ByBackend.TryGetValue(pair.A.Packed, out var a) || !world.ByBackend.TryGetValue(pair.B.Packed, out var b))
        { world.CallbackFailed = true; return false; }
        material.FrictionCoefficient = a.Description.Material.CombineFriction(b.Description.Material);
        world.RecordContact(pair, ref manifold);
        if (!a.Description.IsSensor && !b.Description.IsSensor)
            world.Resources.Characters!.TryReportContacts(pair, ref manifold, workerIndex, ref material);
        return !a.Description.IsSensor && !b.Description.IsSensor;
    }
    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
    { world.RecordContact(pair, ref manifold, childIndexA, childIndexB); return true; }
    public void Dispose() { }
}

internal struct PoseIntegrator(Vector3 gravity) : IPoseIntegratorCallbacks
{
    private Vector3Wide _gravityDt;
    public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
    public readonly bool AllowSubstepsForUnconstrainedBodies => false;
    public readonly bool IntegrateVelocityForKinematics => false;
    public void Initialize(Simulation simulation) { }
    public void PrepareForIntegration(float dt) => _gravityDt = Vector3Wide.Broadcast(gravity * dt);
    public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation,
        BodyInertiaWide inertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
        => velocity.Linear += _gravityDt;
}
