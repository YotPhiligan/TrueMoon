using BepuPhysics;
using BepuPhysics.Collidables;
using TrueMoon.Kton.Bodies;

namespace TrueMoon.Kton.Bepu;

internal sealed partial class BepuWorld
{
    public PhysicsBodyId CreateBody(PhysicsBodyDescription description)
    {
        Enter();
        try
        {
            if (description is null) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.InvalidArgument, "Body");
            var shape = Shape(description.Shape);
            PhysicsValidation.ValidateBody(Options, description, shape.Definition).ThrowIfFailure();
            var slot = Bodies.NextSlot("Bodies");
            if (description.Motion == PhysicsBodyMotion.Dynamic)
            {
                var min = description.Mass; var max = min;
                for (var i = 0; i < Bodies.Capacity; i++)
                    if (Bodies[i] is { Description.Motion: PhysicsBodyMotion.Dynamic } b) { min = MathF.Min(min, b.Description.Mass); max = MathF.Max(max, b.Description.Mass); }
                PhysicsValidation.ValidateMassRatio(min, max).ThrowIfFailure();
            }
            var id = new PhysicsBodyId(Id, slot, Bodies.Generation(slot)); var entry = new BodyEntry(id, description, shape);
            try
            {
                Faults.Visit(Checkpoint.BeforeMutation);
                var pose = new RigidPose(description.Pose.Position, description.Pose.Orientation);
                if (description.Motion == PhysicsBodyMotion.Static)
                {
                    entry.StaticHandle = Simulation.Statics.Add(new StaticDescription(pose, shape.Backend));
                    entry.Collider = Simulation.Statics[entry.StaticHandle].CollidableReference;
                }
                else
                {
                    var velocity = new BodyVelocity(description.LinearVelocity, description.AngularVelocity);
                    var continuity = Profile.CcdMode == PhysicsCcdMode.Swept
                        ? ContinuousDetection.Continuous(Profile.MinimumSweepTimestep, Profile.SweepConvergenceThreshold) : ContinuousDetection.Passive;
                    var collidable = new CollidableDescription(shape.Backend, Profile.MaximumSpeculativeMargin, continuity);
                    var activity = new BodyActivityDescription(0.01f);
                    var backend = description.Motion == PhysicsBodyMotion.Dynamic
                        ? BodyDescription.CreateDynamic(pose, velocity, shape.Inertia(description.Mass), collidable, activity)
                        : BodyDescription.CreateKinematic(pose, velocity, collidable, activity);
                    entry.Handle = Simulation.Bodies.Add(backend); entry.Collider = Simulation.Bodies[entry.Handle].CollidableReference;
                }
                ByBackend.Add(entry.Collider.Packed, entry); Bodies.Publish(slot, entry); shape.BodyReferences++; Resources.Sample(); return id;
            }
            catch (Exception error) { throw Fault("CreateBody", error); }
        }
        finally { Exit(); }
    }
    public void RemoveBody(PhysicsBodyId body)
    {
        Enter();
        try
        {
            var entry = Body(body);
            if (entry.Character != null) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.ResourceInUse, "Body.Character");
            if (entry.JointReferences != 0) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.ResourceInUse, "Body.Joints");
            Contacts.PreflightInvalidation(body);
            try
            {
                Faults.Visit(Checkpoint.BeforeMutation);
                InvalidateCharacterSupports(entry);
                Contacts.Invalidate(body, TrueMoon.Kton.Contacts.PhysicsContactEndReason.BodyRemoved);
                if (entry.Description.Motion == PhysicsBodyMotion.Static) Simulation.Statics.Remove(entry.StaticHandle); else Simulation.Bodies.Remove(entry.Handle);
                ByBackend.Remove(entry.Collider.Packed); entry.Shape.BodyReferences--; Bodies.Remove(body.Slot);
            }
            catch (Exception error) { throw Fault("RemoveBody", error); }
        }
        finally { Exit(); }
    }
}
