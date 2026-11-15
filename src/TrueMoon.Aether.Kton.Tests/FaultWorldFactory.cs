using System.Numerics;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Contacts;
using TrueMoon.Kton.Joints;
using TrueMoon.Kton.Queries;
using TrueMoon.Kton.Shapes;

namespace TrueMoon.Aether.Kton.Tests;

internal sealed class FaultWorldFactory : IFactory<IPhysicsWorld>, IDisposable
{
    private readonly BepuPhysicsWorldFactory _native = new();
    internal readonly List<WorldProxy> Worlds = [];
    internal CancellationTokenSource? CancelOnCreate;
    internal bool FailCreate, ReturnNull, FailShapeCreation, FailNativeStep, FailAllDisposals;
    internal BepuResourceSnapshot Resources => _native.Resources;
    public IPhysicsWorld Create()
    {
        if (FailCreate) throw new IOException("world create");
        if (ReturnNull) return null!;
        var world = new WorldProxy(_native.Create(new() { Gravity = Vector3.Zero }))
        { FailShapeCreation = FailShapeCreation, FailNativeStep = FailNativeStep, FailAllDisposals = FailAllDisposals };
        Worlds.Add(world); CancelOnCreate?.Cancel(); return world;
    }
    public IPhysicsWorld Create<TData>(TData? data = default) => throw new ArgumentException("No runtime arguments.", nameof(data));
    public void Dispose() => _native.Dispose();
    internal sealed class WorldProxy(IPhysicsWorld inner) : IPhysicsWorld
    {
        internal readonly List<PhysicsBodyDescription> CreatedBodies = [];
        internal int DisposeFailures, DisposeAttempts, StepAttempts;
        internal bool FailAllDisposals, FailShapeCreation, FailNativeStep;
        public PhysicsWorldId Id => inner.Id;
        public PhysicsWorldCapabilities Capabilities => inner.Capabilities;
        public PhysicsWorldOptions Options => inner.Options;
        public PhysicsWorldState State => inner.State;
        public ulong LastCompletedTick => inner.LastCompletedTick;
        public PhysicsShapeId CreateShape(PhysicsShapeDefinition definition) => FailShapeCreation ? throw new IOException("shape create") : inner.CreateShape(definition);
        public void RemoveShape(PhysicsShapeId shape) => inner.RemoveShape(shape);
        public PhysicsBodyId CreateBody(PhysicsBodyDescription description) { CreatedBodies.Add(description); return inner.CreateBody(description); }
        public void RemoveBody(PhysicsBodyId body) => inner.RemoveBody(body);
        public PhysicsJointId CreateBallSocket(PhysicsBallSocket description) => inner.CreateBallSocket(description);
        public void RemoveJoint(PhysicsJointId joint) => inner.RemoveJoint(joint);
        public PhysicsBodyState GetBodyState(PhysicsBodyId body) => inner.GetBodyState(body);
        public void ApplyCommands(ReadOnlySpan<PhysicsBodyCommand> commands) => inner.ApplyCommands(commands);
        public PhysicsStepResult Step(ulong tick, CancellationToken cancellationToken = default)
        { StepAttempts++; if (FailNativeStep) throw new IOException("native step"); return inner.Step(tick, cancellationToken); }
        public int CopyContactEvents(Span<PhysicsContactEvent> output) => inner.CopyContactEvents(output);
        public bool RaycastNearest(PhysicsRay query, out PhysicsHit hit) => inner.RaycastNearest(query, out hit);
        public PhysicsQueryResult Raycast(PhysicsRay query, Span<PhysicsHit> output) => inner.Raycast(query, output);
        public PhysicsQueryResult Sweep(PhysicsSweep query, Span<PhysicsHit> output) => inner.Sweep(query, output);
        public PhysicsQueryResult Overlap(PhysicsOverlap query, Span<PhysicsOverlapHit> output) => inner.Overlap(query, output);
        public void Dispose()
        {
            DisposeAttempts++;
            if (FailAllDisposals || DisposeFailures-- > 0) throw new IOException("native cleanup");
            inner.Dispose();
        }
    }
}
