using System.Numerics;
using TrueMoon.Aether.Abstractions.Ecs;
using TrueMoon.Kton;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Characters;

namespace TrueMoon.Aether.Kton.Runtime;

public sealed partial class PhysicsSceneSubsystem
{
    private IPhysicsCharacterWorld CharacterWorld => _physics as IPhysicsCharacterWorld
        ?? throw new NotSupportedException("The selected Kton world does not implement capsule characters.");
    private void CaptureCharacter(Candidate candidate, bool initial)
    {
        var entity = candidate.Entity;
        if (!_world.TryGetComponent<PhysicsCharacter>(entity, out var descriptor)) return;
        if ((_physics!.Capabilities & PhysicsWorldCapabilities.Characters) == 0) throw new NotSupportedException("Kton characters are unavailable.");
        _ = CharacterWorld;
        var options = candidate.Previous?.CharacterDescriptor == descriptor
            ? candidate.Previous.CharacterOptions! : descriptor.CaptureOptions();
        options.Validate().ThrowIfFailure();
        if (candidate.Descriptor.Motion != PhysicsBodyMotion.Dynamic || candidate.Descriptor.IsSensor || candidate.Descriptor.Shape != PhysicsColliderShape.Capsule ||
            Vector3.DistanceSquared(Vector3.Transform(Vector3.UnitY, candidate.Transform.Rotation), Vector3.UnitY) >= 1e-10f || candidate.Velocity.Angular != Vector3.Zero)
            throw new InvalidDataException("A character requires an upright dynamic nonsensor capsule with zero angular velocity.");
        _world.TryGetComponent<PhysicsCharacterInput>(entity, out var input);
        options.ValidateInput(new(input.Velocity)).ThrowIfFailure();
        if (input.JumpSequence < (candidate.Previous?.LastJumpSequence ?? 0)) throw new InvalidDataException("Character jump sequence cannot decrease.");
        if (!initial)
        {
            if (!_world.TryGetComponent<PhysicsCharacterState>(entity, out var state) || !_world.HasComponent<PhysicsCharacterInput>(entity))
                throw new InvalidDataException("Use PhysicsEntityCommands.AddCharacter for complete character roles.");
            if (candidate.Previous?.CharacterDescriptor != null && state != candidate.Previous.LastCharacterState)
                throw new InvalidDataException("Physics owns character support state.");
        }
        if (candidate.Teleport is { } teleport && Vector3.DistanceSquared(Vector3.Transform(Vector3.UnitY, teleport.Pose.Orientation), Vector3.UnitY) >= 1e-10f)
            throw new InvalidDataException("Character teleports must remain upright.");
        candidate.Character = descriptor; candidate.Input = input; candidate.CharacterOptions = options;
    }
    private void ValidateCharacterRoles()
    {
        _world.Query<PhysicsCharacter>().ForEach((EntityHandle entity, in PhysicsCharacter _) =>
        { if (!_present.ContainsKey(entity)) throw new InvalidDataException("Character requires PhysicsBody."); });
        _world.Query<PhysicsCharacterInput>().ForEach((EntityHandle entity, in PhysicsCharacterInput _) =>
        { if (!_present.TryGetValue(entity, out var candidate) || candidate.Character is null) throw new InvalidDataException("Orphan character input."); });
        _world.Query<PhysicsCharacterState>().ForEach((EntityHandle entity, in PhysicsCharacterState _) =>
        { if (!_present.TryGetValue(entity, out var candidate) || candidate.Character is null) throw new InvalidDataException("Orphan character support state."); });
    }
    private void InitializeCharacterRoles(Candidate candidate)
    {
        if (candidate.Character is null) return;
        if (!_world.HasComponent<PhysicsCharacterInput>(candidate.Entity)) _world.AddComponent(candidate.Entity, candidate.Input);
        if (!_world.HasComponent<PhysicsCharacterState>(candidate.Entity)) _world.AddComponent(candidate.Entity, default(PhysicsCharacterState));
        else _world.SetComponent(candidate.Entity, default(PhysicsCharacterState));
    }
    private void RemoveController(Entry entry)
    {
        if (!entry.Character.IsValid) return;
        CharacterWorld.RemoveCharacter(entry.Character); entry.Character = default; entry.CharacterDescriptor = null; entry.CharacterOptions = null;
    }
    private void ReconcileCharacters()
    {
        foreach (var candidate in _candidates)
        {
            var entry = _entries[candidate.Entity];
            if (entry.CharacterDescriptor == candidate.Character) continue;
            RemoveController(entry);
            if (candidate.Character is not { } descriptor) continue;
            entry.Character = CharacterWorld.CreateCharacter(entry.Body, candidate.CharacterOptions!);
            entry.CharacterDescriptor = descriptor; entry.CharacterOptions = candidate.CharacterOptions;
        }
    }
    private void SubmitCharacterInputs()
    {
        foreach (var candidate in _candidates)
        {
            var entry = _entries[candidate.Entity]; if (!entry.Character.IsValid) continue;
            CharacterWorld.SetCharacterInput(entry.Character, new(candidate.Input.Velocity, candidate.Input.JumpSequence > entry.LastJumpSequence));
            entry.LastJumpSequence = candidate.Input.JumpSequence;
        }
    }
    private void PublishCharacter(EntityHandle entity, Entry entry)
    {
        if (!entry.Character.IsValid) return;
        var state = CharacterWorld.GetCharacterState(entry.Character);
        EntityHandle support = default;
        if (state.IsSupported && !_entities.TryGetValue(state.SupportBody, out support)) throw new InvalidDataException("Unmapped character support.");
        var projection = new PhysicsCharacterState(state.IsSupported, support, state.SupportNormal);
        _world.SetComponent(entity, projection); entry.LastCharacterState = projection;
    }
}
