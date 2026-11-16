using System.Diagnostics;
using TrueMoon.Kton;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Characters;
using TrueMoon.Kton.Contacts;
using TrueMoon.Kton.Joints;
using TrueMoon.Kton.Queries;
using TrueMoon.Kton.Shapes;

namespace TrueMoon.ManualTests.KtonPerformance;

// Probe-only decorator. It owns exactly one caller-owned world and adds no production instrumentation.
internal sealed class MeasuredWorld(IPhysicsWorld inner) : IPhysicsWorld, IPhysicsCharacterWorld
{
    internal readonly List<PhysicsBodyId> DynamicBodies = [];
    internal readonly List<PhysicsCharacterId> Characters = [];
    internal long[]? StepTicks;
    internal int Samples;
    internal long PublishedEvents;
    internal bool MeasureAllocations;
    internal long StepAllocatedBytes;
    public PhysicsWorldId Id => inner.Id;
    public PhysicsWorldCapabilities Capabilities => inner.Capabilities;
    public PhysicsWorldOptions Options => inner.Options;
    public PhysicsWorldState State => inner.State;
    public ulong LastCompletedTick => inner.LastCompletedTick;
    private IPhysicsCharacterWorld Controllers => (IPhysicsCharacterWorld)inner;
    public PhysicsShapeId CreateShape(PhysicsShapeDefinition definition) => inner.CreateShape(definition);
    public void RemoveShape(PhysicsShapeId shape) => inner.RemoveShape(shape);
    public PhysicsBodyId CreateBody(PhysicsBodyDescription description)
    {
        var id = inner.CreateBody(description);
        if (description.Motion == PhysicsBodyMotion.Dynamic) DynamicBodies.Add(id);
        return id;
    }
    public void RemoveBody(PhysicsBodyId body) { inner.RemoveBody(body); DynamicBodies.Remove(body); }
    public PhysicsJointId CreateBallSocket(PhysicsBallSocket description) => inner.CreateBallSocket(description);
    public void RemoveJoint(PhysicsJointId joint) => inner.RemoveJoint(joint);
    public PhysicsBodyState GetBodyState(PhysicsBodyId body) => inner.GetBodyState(body);
    public void ApplyCommands(ReadOnlySpan<PhysicsBodyCommand> commands) => inner.ApplyCommands(commands);
    public PhysicsStepResult Step(ulong tick, CancellationToken cancellationToken = default)
    {
        if (StepTicks is not { } samples) return inner.Step(tick, cancellationToken);
        if (Samples == samples.Length) throw new InvalidOperationException("Unexpected extra native step.");
        var allocated = MeasureAllocations ? GC.GetAllocatedBytesForCurrentThread() : 0;
        var start = Stopwatch.GetTimestamp();
        var result = inner.Step(tick, cancellationToken);
        samples[Samples++] = Stopwatch.GetTimestamp() - start;
        if (MeasureAllocations) StepAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
        PublishedEvents += result.ContactEventCount;
        return result;
    }
    public int CopyContactEvents(Span<PhysicsContactEvent> output) => inner.CopyContactEvents(output);
    public bool RaycastNearest(PhysicsRay query, out PhysicsHit hit) => inner.RaycastNearest(query, out hit);
    public PhysicsQueryResult Raycast(PhysicsRay query, Span<PhysicsHit> output) => inner.Raycast(query, output);
    public PhysicsQueryResult Sweep(PhysicsSweep query, Span<PhysicsHit> output) => inner.Sweep(query, output);
    public PhysicsQueryResult Overlap(PhysicsOverlap query, Span<PhysicsOverlapHit> output) => inner.Overlap(query, output);
    public PhysicsCharacterId CreateCharacter(PhysicsBodyId body, PhysicsCharacterOptions options)
    { var id = Controllers.CreateCharacter(body, options); Characters.Add(id); return id; }
    public void RemoveCharacter(PhysicsCharacterId character) { Controllers.RemoveCharacter(character); Characters.Remove(character); }
    public void SetCharacterInput(PhysicsCharacterId character, PhysicsCharacterInput input) => Controllers.SetCharacterInput(character, input);
    public PhysicsCharacterState GetCharacterState(PhysicsCharacterId character) => Controllers.GetCharacterState(character);
    public void Dispose() => inner.Dispose();
}

internal sealed class MeasuredWorldFactory(IFactory<IPhysicsWorld, PhysicsWorldOptions> inner) : IFactory<IPhysicsWorld>
{
    // Borrowed witness only. The consumer/scene owns the world, never this factory.
    internal MeasuredWorld? Created;
    public IPhysicsWorld Create()
    {
        if (Created != null) throw new InvalidOperationException("One world per measured fixture.");
        return Created = new(inner.Create(Arguments));
    }
    public IPhysicsWorld Create<TData>(TData? data = default) => throw new ArgumentException("This probe factory has fixed fixture arguments.", nameof(data));
    internal PhysicsWorldOptions Arguments = new();
}
