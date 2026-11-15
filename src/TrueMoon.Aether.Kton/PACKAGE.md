# TrueMoon.Aether.Kton

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
