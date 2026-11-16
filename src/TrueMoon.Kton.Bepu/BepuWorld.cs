using BepuPhysics;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Contacts;
using TrueMoon.Kton.Joints;
using TrueMoon.Kton.Queries;
using TrueMoon.Kton.Shapes;

namespace TrueMoon.Kton.Bepu;

internal sealed partial class BepuWorld : IPhysicsWorld, TrueMoon.Kton.Characters.IPhysicsCharacterWorld
{
    private readonly BepuPhysicsWorldFactory _factory;
    internal readonly WorldResources Resources;
    internal readonly FaultInjection Faults;
    internal readonly PhysicsProfileSettings Profile;
    internal readonly GenerationalSlots<BodyEntry> Bodies;
    internal readonly GenerationalSlots<ShapeEntry> Shapes;
    internal readonly GenerationalSlots<JointEntry> Joints;
    internal readonly Dictionary<uint, BodyEntry> ByBackend;
    internal readonly Dictionary<ulong, int> DisabledPairs;
    private readonly PreparedBody[] _prepared;
    private readonly int[] _touched;
    private readonly PhysicsHit[] _queryHits;
    internal readonly ContactState Contacts;
    private int _gate, _state, _commandsAdmitted;
    private ulong _tick;
    internal bool CallbackFailed;
    private Simulation Simulation => Resources.Simulation!;
    public PhysicsWorldId Id { get; } = new(Guid.NewGuid());
    public PhysicsWorldCapabilities Capabilities => PhysicsWorldCapabilities.RigidBodySimulation | PhysicsWorldCapabilities.Queries | PhysicsWorldCapabilities.ContactEvents | PhysicsWorldCapabilities.Characters;
    public PhysicsWorldOptions Options { get; }
    public PhysicsWorldState State => (PhysicsWorldState)Volatile.Read(ref _state);
    public ulong LastCompletedTick => Volatile.Read(ref _tick);
    internal BepuWorld(BepuPhysicsWorldFactory factory, WorldResources resources, PhysicsWorldOptions options, FaultInjection faults)
    {
        _factory = factory; Resources = resources; Options = options; Faults = faults; Profile = PhysicsProfileSettings.For(options.Quality);
        Bodies = new(options.Limits.MaxBodies); Shapes = new(options.Limits.MaxShapes); Joints = new(options.Limits.MaxJoints); Characters = new(options.Limits.MaxBodies);
        ByBackend = new(options.Limits.MaxBodies); DisabledPairs = new(options.Limits.MaxJoints);
        _prepared = new PreparedBody[options.Limits.MaxBodies]; _touched = new int[options.Limits.MaxBodies];
        _queryHits = new PhysicsHit[Math.Min(options.Limits.MaxQueryHits, options.Limits.MaxQueryScratchBytes / System.Runtime.CompilerServices.Unsafe.SizeOf<PhysicsHit>())];
        Contacts = new ContactState(options.Limits.MaxContactEvents);
    }
    internal void Initialize() => Resources.Initialize(this);
    private void Enter(bool disposing = false)
    {
        if (Interlocked.CompareExchange(ref _gate, 1, 0) != 0) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.Busy, "World");
        var state = State;
        if (disposing || state == PhysicsWorldState.Ready) return;
        Volatile.Write(ref _gate, 0);
        throw BepuPhysicsWorldFactory.Error(state == PhysicsWorldState.Disposed ? PhysicsErrorCode.Disposed : PhysicsErrorCode.Faulted, "World.State");
    }
    private void Exit() => Volatile.Write(ref _gate, 0);
    private PhysicsException Fault(string field, Exception error)
    {
        Volatile.Write(ref _state, (int)PhysicsWorldState.Faulted);
        if (error is PhysicsException { Error.Code: PhysicsErrorCode.ContactOverflow } overflow) return overflow;
        return new(new(PhysicsErrorCode.Faulted, field), error);
    }
    private BodyEntry Body(PhysicsBodyId id)
    {
        var current = Bodies[id.Slot]; PhysicsIdentity.Validate(Id, id, current?.Id ?? default).ThrowIfFailure(); return current!;
    }
    private ShapeEntry Shape(PhysicsShapeId id)
    {
        var current = Shapes[id.Slot]; PhysicsIdentity.Validate(Id, id, current?.Id ?? default).ThrowIfFailure(); return current!;
    }
    private JointEntry Joint(PhysicsJointId id)
    {
        var current = Joints[id.Slot]; PhysicsIdentity.Validate(Id, id, current?.Id ?? default).ThrowIfFailure(); return current!;
    }
    internal static ulong Pair(int a, int b) => ((ulong)(uint)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
    public PhysicsBodyState GetBodyState(PhysicsBodyId body)
    {
        Enter(); try { return Read(Body(body)); } finally { Exit(); }
    }
    private PhysicsBodyState Read(BodyEntry body)
    {
        if (body.Description.Motion == PhysicsBodyMotion.Static)
        {
            var pose = Simulation.Statics[body.StaticHandle].Pose;
            return new(body.Id, PhysicsBodyMotion.Static, new(pose.Position, pose.Orientation), default, default, false);
        }
        var reference = Simulation.Bodies[body.Handle]; var p = reference.Pose; var v = reference.Velocity;
        return new(body.Id, body.Description.Motion, new(p.Position, p.Orientation), v.Linear, v.Angular, reference.Awake);
    }
    public void Dispose()
    {
        Enter(disposing: true);
        try
        {
            if (State == PhysicsWorldState.Disposed) return;
            Volatile.Write(ref _state, (int)PhysicsWorldState.Disposing);
            // Each resource stage retains its completion bit, so a boundary failure can be retried without double-disposal.
            Resources.Dispose(); Bodies.Clear(); Shapes.Clear(); Joints.Clear(); Characters.Clear(); ByBackend.Clear(); DisabledPairs.Clear(); Array.Clear(_prepared); Contacts.Clear();
            _factory.WorldReleased(); Volatile.Write(ref _state, (int)PhysicsWorldState.Disposed);
        }
        catch (Exception error) { throw new PhysicsException(new(PhysicsErrorCode.Faulted, "World.Cleanup"), error); }
        finally { Exit(); }
    }
}
