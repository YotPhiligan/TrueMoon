using System.Numerics;
using BepuPhysics;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Characters;
using TrueMoon.Kton.Shapes;

namespace TrueMoon.Kton.Bepu;

internal sealed class CharacterEntry(PhysicsCharacterId id, BodyEntry body, PhysicsCharacterOptions options, BodyInertia inertia)
{
    internal readonly PhysicsCharacterId Id = id;
    internal readonly BodyEntry Body = body;
    internal readonly PhysicsCharacterOptions Options = options;
    internal readonly BodyInertia Inertia = inertia;
}

internal sealed partial class BepuWorld
{
    internal readonly GenerationalSlots<CharacterEntry> Characters;
    private CharacterEntry Character(PhysicsCharacterId id)
    {
        if (!id.IsValid) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.InvalidIdentifier, "Character.Id");
        if (id.World != Id) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.ForeignWorld, "Character.Id");
        var entry = Characters[id.Slot];
        if (entry?.Id != id) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.StaleIdentifier, "Character.Id");
        return entry;
    }
    internal static bool Upright(PhysicsPose pose) => Vector3.DistanceSquared(Vector3.Transform(Vector3.UnitY, pose.Orientation), Vector3.UnitY) < 1e-10f;
    public PhysicsCharacterId CreateCharacter(PhysicsBodyId body, PhysicsCharacterOptions options)
    {
        Enter();
        try
        {
            if (options is null) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.InvalidArgument, "Character.Options");
            options.Validate().ThrowIfFailure(); var entry = Body(body); var state = Read(entry);
            if (entry.Character != null || entry.JointReferences != 0) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.ResourceInUse, "Character.Body");
            if (entry.Description.Motion != PhysicsBodyMotion.Dynamic || entry.Description.IsSensor || entry.Shape.Definition is not PhysicsCapsule capsule ||
                !Upright(state.Pose) || state.AngularVelocity != Vector3.Zero)
                throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.InvalidArgument, "Character.Body");
            var slot = Characters.NextSlot("Characters"); var id = new PhysicsCharacterId(Id, slot, Characters.Generation(slot));
            try
            {
                Faults.Visit(Checkpoint.BeforeMutation);
                var reference = Simulation.Bodies[entry.Handle]; reference.Awake = true;
                var character = new CharacterEntry(id, entry, options, reference.LocalInertia);
                reference.SetLocalInertia(new BodyInertia { InverseMass = 1 / entry.Description.Mass });
                ref var controller = ref Resources.Characters!.AllocateCharacter(entry.Handle);
                controller.LocalUp = Vector3.UnitY; controller.ViewDirection = -Vector3.UnitZ;
                controller.CosMaximumSlope = MathF.Cos(options.MaximumSlopeRadians);
                controller.JumpVelocity = options.JumpSpeed; controller.MaximumHorizontalForce = options.MaximumHorizontalForce;
                controller.MaximumVerticalForce = options.MaximumVerticalForce;
                controller.MinimumSupportDepth = -capsule.Radius * .01f;
                controller.MinimumSupportContinuationDepth = -MathF.Min(.1f, capsule.Radius * .1f);
                Characters.Publish(slot, character); entry.Character = character; Resources.Sample(); return id;
            }
            catch (Exception error) { throw Fault("CreateCharacter", error); }
        }
        finally { Exit(); }
    }
    public void RemoveCharacter(PhysicsCharacterId character)
    {
        Enter();
        try
        {
            var entry = Character(character);
            try
            {
                Faults.Visit(Checkpoint.BeforeMutation); ResetCharacterSupport(entry);
                Resources.Characters!.RemoveCharacterByBodyHandle(entry.Body.Handle);
                var reference = Simulation.Bodies[entry.Body.Handle]; reference.Awake = true; reference.SetLocalInertia(entry.Inertia);
                entry.Body.Character = null; Characters.Remove(character.Slot);
            }
            catch (Exception error) { throw Fault("RemoveCharacter", error); }
        }
        finally { Exit(); }
    }
    public void SetCharacterInput(PhysicsCharacterId character, PhysicsCharacterInput input)
    {
        Enter();
        try
        {
            var entry = Character(character); entry.Options.ValidateInput(input).ThrowIfFailure();
            ref var controller = ref Resources.Characters!.GetCharacterByBodyHandle(entry.Body.Handle);
            // With fixed -Z view, BEPU's X/right and Y/forward map to world X and -Z.
            var target = new Vector2(input.Velocity.X, -input.Velocity.Y);
            try
            {
                if (target != controller.TargetVelocity || input.Jump) Simulation.Awakener.AwakenBody(entry.Body.Handle);
                controller.TargetVelocity = target; controller.TryJump = input.Jump;
            }
            catch (Exception error) { throw Fault("SetCharacterInput", error); }
        }
        finally { Exit(); }
    }
    public PhysicsCharacterState GetCharacterState(PhysicsCharacterId character)
    {
        Enter();
        try
        {
            var entry = Character(character); var state = Read(entry.Body);
            ref var controller = ref Resources.Characters!.GetCharacterByBodyHandle(entry.Body.Handle);
            var supported = controller.Supported && ByBackend.TryGetValue(controller.Support.Packed, out _);
            var support = supported ? ByBackend[controller.Support.Packed].Id : default;
            return new(character, entry.Body.Id, state.Pose, state.LinearVelocity, supported, support, supported ? controller.SupportNormal : default);
        }
        finally { Exit(); }
    }
    private void ResetCharacterSupport(CharacterEntry entry)
    {
        ref var controller = ref Resources.Characters!.GetCharacterByBodyHandle(entry.Body.Handle);
        if (controller.Supported && Simulation.Solver.ConstraintExists(controller.MotionConstraintHandle)) Simulation.Solver.Remove(controller.MotionConstraintHandle);
        controller.Supported = false; controller.Support = default; controller.SupportNormal = default; controller.TryJump = false;
        Simulation.Awakener.AwakenBody(entry.Body.Handle);
    }
    private void InvalidateCharacterSupports(BodyEntry changed)
    {
        for (var i = 0; i < Characters.Capacity; i++)
        {
            if (Characters[i] is not { } entry) continue;
            ref var controller = ref Resources.Characters!.GetCharacterByBodyHandle(entry.Body.Handle);
            if (entry.Body == changed || controller.Supported && controller.Support.Packed == changed.Collider.Packed) ResetCharacterSupport(entry);
        }
    }
}
