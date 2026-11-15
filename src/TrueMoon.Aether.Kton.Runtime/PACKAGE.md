# TrueMoon.Aether.Kton.Runtime

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
