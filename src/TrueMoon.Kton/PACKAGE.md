# TrueMoon.Kton

Backend-neutral 3D physics contracts and input validation for TrueMoon, on the
repository .NET profile. This package has no BEPU, Aether, UI or native dependency.
PHYS1 provides values, interfaces and pure validation; it does **not** instantiate
or simulate a physics world. The separate BEPU provider implements PHYS2–3;
App integration is provided separately by TrueMoon.Kton.Bepu's PHYS4
UseBepuPhysics/UseConfiguredBepuPhysics entry points. This neutral assembly remains
independent of App/DI implementation and BEPU.

Use `TrueMoon.Kton`, `.Bodies`, `.Shapes`, `.Joints`, `.Queries`, `.Contacts` and `.Characters`.
`PhysicsValidation` returns a `PhysicsError` before mutation; provider operations
throw `PhysicsException` with the same stable category. Runtime IDs contain a
world incarnation, slot and generation. Do not serialize them as authoring IDs.

Units are metres/kg/seconds, right-handed Y-up, unit quaternion XYZW. Shapes use
full box dimensions; a +Y capsule has height `CylinderLength + 2 * Radius`.
Mesh input is borrowed only during synchronous `CreateShape`; a provider must
capture its own immutable copy. Static triangle meshes cannot be dynamic bodies.

```csharp
using TrueMoon.Kton;

var arguments = new PhysicsWorldOptions {
    Quality = PhysicsQualityProfile.Baseline60,
    Limits = new PhysicsLimits { MaxBodies = 256 }
};
PhysicsValidation.Validate(arguments).ThrowIfFailure();
```

`Baseline60`: 1/60 s, one worker, 8 iterations/4 solver substeps, speculative
contacts, 120 Hz contact spring. `FastLinear60`: the same step/solver, swept
contacts, margin 0.01 m, sweep tolerance 1e-6 s and spring 600 Hz. Contact softness
cannot be overridden independently. The fast profile has evidence only for the
PHYS0 sphere/thin-wall fixture; arbitrary-scene, angular CCD and cross-platform
determinism are not guaranteed. PHYS0's soft 120 Hz swept fixture tunnels.

One caller serializes `IPhysicsWorld` operations; factory results will be
caller-owned through the existing `IFactory<IPhysicsWorld, PhysicsWorldOptions>`
from TrueMoon.Contracts. No new factory API or container dependency is introduced
here. Queries write bounded caller-owned spans and report truncation; gameplay
contact overflow faults the world. Disposal failures retain the owner for retry.

Before publishing a session, require its needed `IPhysicsWorld.Capabilities` flags.
The BEPU provider supplies RigidBodySimulation, Queries, ContactEvents and Characters.

PHYS6 adds the optional `IPhysicsCharacterWorld` capability, generation-safe
`PhysicsCharacterId`, immutable `PhysicsCharacterOptions`, world XZ velocity/jump
input and support snapshots. Controllers borrow upright dynamic nonsensor capsules.
Remove a controller before removing its body; removal restores inertia and leaves
the body alive. Input velocity persists, jump is consumed once at the next Step.
`PhysicsValidation.ValidateBody` also accepts an explicit current pose and linear/
angular velocities alongside captured immutable settings. This overload performs
the same complete admission without allocating a replacement descriptor per frame.
`MaximumStepHeight` (default 0, disabled) opts into the BEPU static box step
profile. `MinimumTreadDepth` (default 0.30 m) is measured along the input.
The respective admission bounds `[0, 1]` and `(0, 2]` metres are not geometry
guarantees; depth is validated even when disabled. This profile moves a supported
capsule onto a clear tread within the same Step and publishes normal solver
support/contact events, not a teleport. It does not add auto-stepping on mesh
or moving supports. No air steering, crouch or root motion is promised. The
neutral package has no provider, App, native or Aether dependency.
Other providers must explicitly reject operations for absent features. A zero
published event count alone does not prove that the scene had no physical collisions.

Default admission counts: 1024 bodies, 2048 shapes, 4096 joints/commands per tick,
8192 contact events, 256 hits/query. Query scratch is bounded separately; sampled
16 MiB backend pool and 1 ms diagnostic Step budgets are not hard total-RAM/FPS
guarantees. Provider enforcement, simulation/event semantics, App/DI, ECS and
platform delivery require their own acceptance.

Source documentation: `docs/physics/CONTRACTS.md`, `PLAN.md`, `STATUS.md` and
`ManualTests/KtonContracts/README.md` in the TrueMoon repository.
