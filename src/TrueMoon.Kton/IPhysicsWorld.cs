using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Contacts;
using TrueMoon.Kton.Joints;
using TrueMoon.Kton.Queries;
using TrueMoon.Kton.Shapes;

namespace TrueMoon.Kton;

/// <summary>Observable world lifetime.</summary>
public enum PhysicsWorldState
{
    /// <summary>Owner may invoke commands, queries or Step.</summary>
    Ready,
    /// <summary>Synchronous Step is in progress; concurrent/reentrant calls fail Busy.</summary>
    Stepping,
    /// <summary>Partial step/command failure. Only metadata and disposal are permitted.</summary>
    Faulted,
    /// <summary>Cleanup is in progress or failed; retain owner and retry Dispose.</summary>
    Disposing,
    /// <summary>Cleanup completed. Dispose remains idempotent.</summary>
    Disposed
}

/// <summary>Published result of a fully joined successful fixed step.</summary>
/// <param name="Tick">Completed tick, starting at one.</param><param name="ContactEventCount">Full snapshot size for CopyContactEvents.</param>
public readonly record struct PhysicsStepResult(ulong Tick, int ContactEventCount);

/// <summary>Caller-owned synchronous world; one external owner serializes all operations and disposal.</summary>
/// <remarks>Provider implementation belongs to PHYS2–3. No backend, App resolver, timer or ECS reference enters this API.</remarks>
public interface IPhysicsWorld : IDisposable
{
    /// <summary>Unique world incarnation. Remains readable after disposal.</summary>
    PhysicsWorldId Id { get; }
    /// <summary>Implemented features; unavailable operations throw UnsupportedProfile instead of pretending to return empty results.</summary>
    PhysicsWorldCapabilities Capabilities { get; }
    /// <summary>Validated immutable arguments snapshot, readable after disposal.</summary>
    PhysicsWorldOptions Options { get; }
    /// <summary>Observable state; cleanup failure never reports Disposed.</summary>
    PhysicsWorldState State { get; }
    /// <summary>Last fully published tick. Partial failures do not advance this value.</summary>
    ulong LastCompletedTick { get; }

    /// <summary>Validates/captures input synchronously before publication; returns a world-owned immutable shape.</summary>
    /// <param name="definition">Borrowed input for the duration of this invocation.</param><returns>New live shape ID.</returns>
    PhysicsShapeId CreateShape(PhysicsShapeDefinition definition);
    /// <summary>Removes an unused shape. Any body reference causes ResourceInUse, without mutation.</summary>
    /// <param name="shape">Live identity in this world.</param>
    void RemoveShape(PhysicsShapeId shape);
    /// <summary>Creates a body after validation and capacity checks; dynamic triangle meshes are rejected.</summary>
    /// <param name="description">Neutral body input.</param><returns>New live body ID.</returns>
    PhysicsBodyId CreateBody(PhysicsBodyDescription description);
    /// <summary>Removes a body without slot-generation reuse hazards. Existing joints cause ResourceInUse.</summary>
    /// <param name="body">Live body; removal queues End events for the next successful tick.</param>
    void RemoveBody(PhysicsBodyId body);
    /// <summary>Creates a ball socket; both bodies must be non-static and at least one Dynamic.</summary>
    /// <param name="description">Distinct live IDs and anchors.</param><returns>New joint ID.</returns>
    PhysicsJointId CreateBallSocket(PhysicsBallSocket description);
    /// <summary>Removes a live joint.</summary><param name="joint">Identity in this world.</param>
    void RemoveJoint(PhysicsJointId joint);
    /// <summary>Returns copied state after identity validation.</summary><param name="body">Live ID.</param><returns>Value snapshot.</returns>
    PhysicsBodyState GetBodyState(PhysicsBodyId body);
    /// <summary>Prevalidates the whole ordered batch, including state-dependent IDs/modes/capacity, before applying it.</summary>
    /// <param name="commands">Borrowed synchronous input; repeated batches consume the same per-tick admission budget.</param>
    /// <remarks>Force is one pending F*dt impulse for the next Step. Apply failure after mutation faults the world; no rollback is promised.</remarks>
    void ApplyCommands(ReadOnlySpan<PhysicsBodyCommand> commands);
    /// <summary>Advances exactly one fixed tick, joins workers, then publishes events. Cancellation is checked only before mutation.</summary>
    /// <param name="tick">Exactly LastCompletedTick+1, starting at one; never wraps.</param>
    /// <param name="cancellationToken">Pre-step cancellation throws OperationCanceledException and leaves the world Ready.</param>
    /// <returns>Successful tick/event count. Begun failures fault the world and retain ownership.</returns>
    PhysicsStepResult Step(ulong tick, CancellationToken cancellationToken = default);
    /// <summary>Copies the entire last successful event snapshot. Too-small output fails CapacityExceeded without any writes.</summary>
    /// <param name="output">Caller-owned output, reusable and independent after return.</param><returns>Copied count.</returns>
    int CopyContactEvents(Span<PhysicsContactEvent> output);
    /// <summary>Returns a nearest hit, breaking equal-distance ties by body slot/generation/subshape.</summary>
    /// <param name="query">Validated ray.</param><param name="hit">Copied hit or default on miss.</param><returns>Whether a hit exists.</returns>
    bool RaycastNearest(PhysicsRay query, out PhysicsHit hit);
    /// <summary>Returns the nearest bounded prefix, sorted by distance then body slot/generation/subshape. Zero output is valid.</summary>
    /// <param name="query">Ray input.</param><param name="output">Only the initialized prefix is overwritten.</param><returns>Written count and explicit truncation.</returns>
    PhysicsQueryResult Raycast(PhysicsRay query, Span<PhysicsHit> output);
    /// <summary>Convex translational sweep; initial overlap returns zero distance without a normal.</summary>
    /// <param name="query">Sweep input.</param><param name="output">Nearest sorted bounded prefix.</param><returns>Count and truncation.</returns>
    PhysicsQueryResult Sweep(PhysicsSweep query, Span<PhysicsHit> output);
    /// <summary>Exact convex overlap, sorted by body slot/generation/subshape.</summary>
    /// <param name="query">Overlap input.</param><param name="output">Bounded caller-owned prefix.</param><returns>Count and truncation.</returns>
    PhysicsQueryResult Overlap(PhysicsOverlap query, Span<PhysicsOverlapHit> output);
}
