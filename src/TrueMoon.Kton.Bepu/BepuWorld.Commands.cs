using System.Numerics;
using BepuPhysics;
using TrueMoon.Kton.Bodies;

namespace TrueMoon.Kton.Bepu;

internal sealed partial class BepuWorld
{
    public void ApplyCommands(ReadOnlySpan<PhysicsBodyCommand> commands)
    {
        Enter(); var touched = 0;
        try
        {
            PhysicsValidation.ValidateCommands(Id, Options, commands, _commandsAdmitted).ThrowIfFailure();
            foreach (var command in commands)
            {
                var body = Body(command.Body); // All live/state-dependent admission happens before any backend write.
                ref var prepared = ref _prepared[body.Id.Slot];
                if (prepared.Entry is null)
                {
                    var state = Read(body);
                    prepared = new PreparedBody { Entry = body, Pose = state.Pose, Linear = state.LinearVelocity, Angular = state.AngularVelocity,
                        Awake = state.IsAwake, PendingImpulse = body.PendingImpulse };
                    _touched[touched++] = body.Id.Slot;
                }
                var motion = body.Description.Motion;
                switch (command.Kind)
                {
                    case PhysicsCommandKind.SetVelocity:
                        if (motion == PhysicsBodyMotion.Static) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.InvalidArgument, "Command.BodyMode");
                        prepared.Linear = command.Linear; prepared.Angular = command.Angular; prepared.Awake = true; break;
                    case PhysicsCommandKind.ApplyLinearImpulse:
                    case PhysicsCommandKind.ApplyLinearForce:
                        RequireDynamic(motion);
                        if (command.Kind == PhysicsCommandKind.ApplyLinearImpulse) prepared.Linear += command.Linear / body.Description.Mass;
                        else prepared.PendingImpulse += command.Linear * Profile.FixedDeltaSeconds;
                        prepared.Awake = true; break;
                    case PhysicsCommandKind.Teleport: prepared.Pose = command.Pose; prepared.Teleported = true; prepared.Awake = true; break;
                    case PhysicsCommandKind.Wake: RequireDynamic(motion); prepared.Awake = true; break;
                    case PhysicsCommandKind.Sleep:
                        RequireDynamic(motion); prepared.Linear = prepared.Angular = prepared.PendingImpulse = Vector3.Zero; prepared.Awake = false; break;
                }
                ValidateMotion(prepared.Pose, prepared.Linear, prepared.Angular);
                if (motion == PhysicsBodyMotion.Dynamic)
                    ValidateMotion(prepared.Pose, prepared.Linear + prepared.PendingImpulse / body.Description.Mass, prepared.Angular);
            }
            Contacts.PreflightTeleports(_prepared);
            try
            {
                if (touched != 0) Faults.Visit(Checkpoint.BeforeMutation);
                for (var i = 0; i < touched; i++)
                {
                    ref var prepared = ref _prepared[_touched[i]]; var entry = prepared.Entry!;
                    if (prepared.Teleported) Contacts.Invalidate(entry.Id, TrueMoon.Kton.Contacts.PhysicsContactEndReason.Teleported);
                    if (entry.Description.Motion == PhysicsBodyMotion.Static)
                    {
                        var reference = Simulation.Statics[entry.StaticHandle];
                        reference.ApplyDescription(new StaticDescription(new RigidPose(prepared.Pose.Position, prepared.Pose.Orientation), entry.Shape.Backend));
                    }
                    else
                    {
                        var reference = Simulation.Bodies[entry.Handle];
                        if (prepared.Awake) reference.Awake = true;
                        if (prepared.Teleported) { reference.Pose = new RigidPose(prepared.Pose.Position, prepared.Pose.Orientation); reference.UpdateBounds(); }
                        reference.Velocity = new BodyVelocity(prepared.Linear, prepared.Angular);
                        if (!prepared.Awake) reference.Awake = false;
                    }
                    entry.PendingImpulse = prepared.PendingImpulse;
                    Faults.Visit(Checkpoint.AfterCommandCommit);
                }
                _commandsAdmitted += commands.Length;
            }
            catch (Exception error) { throw Fault("ApplyCommands", error); }
        }
        finally
        {
            for (var i = 0; i < touched; i++) _prepared[_touched[i]] = default;
            Exit();
        }
    }
    private static void RequireDynamic(PhysicsBodyMotion motion)
    {
        if (motion != PhysicsBodyMotion.Dynamic) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.InvalidArgument, "Command.BodyMode");
    }
    private void ValidateMotion(PhysicsPose pose, Vector3 linear, Vector3 angular)
    {
        PhysicsValidation.Validate(pose).ThrowIfFailure();
        if (!FiniteSpeed(linear, Profile.MaximumLinearSpeed) || !FiniteSpeed(angular, PhysicsValidation.MaximumAngularSpeed))
            throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.InvalidArgument, "Body.MotionEnvelope");
    }
    private static bool FiniteSpeed(Vector3 value, float maximum)
        => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) && value.LengthSquared() <= maximum * maximum;
}
