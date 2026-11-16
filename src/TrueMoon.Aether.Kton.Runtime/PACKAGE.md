# TrueMoon.Aether.Kton.Runtime

PHYS6 capsule controllers share the existing scene-owned Kton world and fixed
Movement step. Registry registration includes the optional Character v1 schema.
Complete input/support roles are added during private authored-scene preparation;
runtime changes use the SDK command helpers. Full preflight validates upright
capsules, settings, velocities, jump sequences and output ownership before native
mutation. Controller cleanup precedes body removal; scene disposal retains the
same world retry contract. The bridge depends only on neutral Kton contracts,
and requires `Characters` capability plus `IPhysicsCharacterWorld` when used.

PHYS8.2 reuses bounded per-world capture scratch and immutable body/controller
settings. Every tick still validates the complete current candidate set before
native mutation, including motion, roles, mass envelope and command sequences.
Descriptor changes, detach/delete and generation reuse are reconciled normally;
scratch references are reset before capture and released at subsystem disposal.

Opt-in Kton integration with Aether's shared scene-subsystem layer. Select the
provider (for example UseBepuPhysics), register UseAetherKton, add exact physics
bindings with PhysicsComponentRegistry.Register, and select AddKtonPhysics in the
single UseAetherSceneSubsystems callback. No Tooling/UI/BEPU reference is added here.

Each scene owns an independent physics world obtained through IFactory<IPhysicsWorld>.
The exact60Hz scheduler reconciles complete body roles, steps physics before builtin
Movement, writes poses/velocities before Transform, and publishes bounded transient
PhysicsContact entities for the next Gameplay tick. Root/unit-scale primitive bodies
only; explicit sequenced teleport, no implicit pose edits. Runtime spawn/detach uses
PhysicsEntityCommands. Contact projection overflow is explicit, never silent truncation.

Dispose scheduler systems before subsystem/native resources, then borrowed SceneWorld
and App dependencies. Cleanup failure retains the owner for retry. Pause advances no
native time; paused Step advances one tick. No native-state checkpoint/mod migration,
character controller, ragdoll, mesh asset import or platform guarantee is added.
