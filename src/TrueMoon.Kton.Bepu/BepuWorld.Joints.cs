using BepuPhysics.Constraints;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Joints;

namespace TrueMoon.Kton.Bepu;

internal sealed partial class BepuWorld
{
    public PhysicsJointId CreateBallSocket(PhysicsBallSocket description)
    {
        Enter();
        try
        {
            PhysicsValidation.ValidateJoint(Id, description).ThrowIfFailure();
            var a = Body(description.BodyA); var b = Body(description.BodyB);
            if (a.Character != null || b.Character != null) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.ResourceInUse, "Joint.Character");
            if (a.Description.Motion == PhysicsBodyMotion.Static || b.Description.Motion == PhysicsBodyMotion.Static
                || (a.Description.Motion != PhysicsBodyMotion.Dynamic && b.Description.Motion != PhysicsBodyMotion.Dynamic))
                throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.InvalidArgument, "Joint.BodyModes");
            var slot = Joints.NextSlot("Joints"); var id = new PhysicsJointId(Id, slot, Joints.Generation(slot));
            try
            {
                Faults.Visit(Checkpoint.BeforeMutation);
                var backend = Simulation.Solver.Add(a.Handle, b.Handle, new BallSocket { LocalOffsetA = description.LocalAnchorA, LocalOffsetB = description.LocalAnchorB,
                    SpringSettings = new SpringSettings(description.SpringFrequency, description.DampingRatio) });
                Joints.Publish(slot, new JointEntry(id, description, backend)); a.JointReferences++; b.JointReferences++;
                var pair = Pair(a.Id.Slot, b.Id.Slot); DisabledPairs.TryGetValue(pair, out var count); DisabledPairs[pair] = count + 1;
                Resources.Sample(); return id;
            }
            catch (Exception error) { throw Fault("CreateJoint", error); }
        }
        finally { Exit(); }
    }
    public void RemoveJoint(PhysicsJointId joint)
    {
        Enter();
        try
        {
            var entry = Joint(joint); var a = Body(entry.Description.BodyA); var b = Body(entry.Description.BodyB);
            try
            {
                Faults.Visit(Checkpoint.BeforeMutation); Simulation.Solver.Remove(entry.Backend);
                a.JointReferences--; b.JointReferences--; Joints.Remove(joint.Slot);
                var pair = Pair(a.Id.Slot, b.Id.Slot); var count = DisabledPairs[pair];
                if (count == 1) DisabledPairs.Remove(pair); else DisabledPairs[pair] = count - 1;
            }
            catch (Exception error) { throw Fault("RemoveJoint", error); }
        }
        finally { Exit(); }
    }
}
