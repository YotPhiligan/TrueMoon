# TrueMoon.Aether.Kton

PHYS6: add the optional `PhysicsComponentSchemas.Character` v1 schema to a scene
with an upright dynamic `PhysicsBody` capsule. `PhysicsCharacter` holds settings;
`PhysicsCharacterInput` holds persistent world XZ velocity and a monotonically
increasing `JumpSequence`. Increase the sequence for each jump; retaining it never
repeats the jump. `PhysicsCharacterState` is physics-owned support output, updated
in the same Movement tick as the pose. Input/state and native handles are not
persisted. Runtime attachment/detachment uses `PhysicsEntityCommands.AddCharacter`
and `RemoveCharacter`; `RemoveBody` clears character roles too.

To opt into automatic stepping with a supporting provider, use the exact
`PhysicsComponentSchemas.CharacterV2` schema (the same type ID, version 2).
It adds `maximumStepHeight` (metres, default 0/off) and `minimumTreadDepth`
(metres of remaining tread in the movement direction, default 0.30). Legacy
v1 documents stay unchanged and stepping stays off. Runtime callers set the
same `PhysicsCharacter` properties. BEPU supports steps between adjoining
static boxes, not arbitrary stairs; crouch, air steering and root motion are
outside this profile.

Optional reference-free physics components, exact scene schemas and phase-command
helpers for Aether games/mods. References Aether.Abstractions and backend-neutral
TrueMoon.Kton; no Runtime, Tooling, UI or BEPU dependency. Use matching package versions.

PhysicsBody supports box/sphere/capsule and static/dynamic/kinematic motion. Root
unit-scale LocalTransform is physics output; PhysicsVelocity commands velocities,
PhysicsTeleport supplies explicit sequenced pose changes. PhysicsContact is a
transient next-tick event component with original generational identities.
PhysicsEntityCommands records complete runtime spawn/attach/detach roles.

Runtime hosts separately opt into TrueMoon.Aether.Kton.Runtime and select a provider
in their composition root. This package owns no services/resources and needs no UseX.

Collectible Mod loader hosts explicitly share this assembly and TrueMoon.Kton
through ModLoaderOptions.AdditionalSharedAssemblies with exact identity/hash.
This package does not automatically extend the fixed GameBuildArtifact shared ABI;
copying DLLs does not establish CLR component identity with a host registry.
