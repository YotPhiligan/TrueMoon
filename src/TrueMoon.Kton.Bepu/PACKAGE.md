# TrueMoon.Kton.Bepu

BEPU provider for neutral TrueMoon.Kton contracts, on the repository .NET profile.
PHYS2 implements world-owned immutable shapes, static/dynamic/kinematic bodies,
generational identities, BallSocket, bounded command admission, fixed Step and
resource ownership. Exact dependencies: BepuPhysics/BepuUtilities 2.5.0-beta.33.
PHYS3 adds bounded raycast/convex sweep/overlap and Begin/Stay/End contact events.

Use the existing `IFactory<IPhysicsWorld, PhysicsWorldOptions>` contract from
TrueMoon.Contracts. `BepuPhysicsWorldFactory` is the low-level factory implementation;
worlds it returns are caller-owned. Its Dispose cleans only unpublished failed
creation resources and does not dispose successful caller-owned worlds. Retain
a failed cleanup owner for retry.

PHYS4 provides `UseBepuPhysics` and `UseConfiguredBepuPhysics` extensions on
`IAppConfigurationContext`. Register before physics consumer startup services:

```csharp
using TrueMoon.Kton.Bepu;

await using var app = App.Build(context => context.UseBepuPhysics(options =>
    options.DefaultWorld = options.DefaultWorld with { Gravity = new(0, -9.81f, 0) }));
await app.StartAsync();
// An injected IFactory<IPhysicsWorld> creates configured defaults via Create().
// IFactory<IPhysicsWorld, PhysicsWorldOptions>.Create(options) uses complete runtime arguments.
// The session joins its work and disposes its worlds before App.StopAsync().
await app.StopAsync();
```

Both aliases borrow a non-disposable adapter. The single App-owned
`Hosting.BepuPhysicsHost` opens admission at startup, without creating a world,
backend pool or worker. Stop closes admission permanently and cleans only failed
unpublished creations. Published worlds are exclusively caller-owned: App does
not track, tick, drain or dispose them. They retain no borrowed App resources;
their original owner can still dispose them after App closes. Do not abandon them.

`BepuPhysicsConfigurationSchema.Default` declares `Kton:DefaultWorld:*` for all
world settings. Register this exact instance in the shared configuration profile,
then call `UseConfiguredBepuPhysics`. Sources/priorities belong to App composition;
explicit callbacks run last and a validated snapshot is frozen before resources.
No-argument creation uses that snapshot. Explicit world options replace it entirely.
Runtime configuration reload does not change the module snapshot or existing worlds.

Stop performs synchronous, non-cancellable owned cleanup. A failure retains the
owner and closed admission. Retry `BepuPhysicsHost.StopAsync` or `Dispose` on the
same borrowed host. After an App/DI disposal failure, retain that host reference:
App and providers can cache their first disposal result and cannot be resolved again.
World cleanup failures remain the world owner's responsibility. Aliases never own
the host or the returned worlds a second time. Register once per App.

`Capabilities` includes RigidBodySimulation, Queries and ContactEvents. Queries
return a nearest sorted caller-owned prefix and explicit truncation. Static meshes
use BEPU BVH traversal with exact triangle identity. Scratch admission reserves the
hit buffer and conservative BVH traversal-stack payload before writing output.
World body enumeration remains bounded by MaxBodies; no all-scene frame-time
guarantee is implied. Results do not retain borrowed backend memory.

Contact snapshots publish only after Step joins and succeeds. Sleeping pairs retain
identity without Stay spam; wake resumes Stay. Delete/teleport queue End with the
old complete generational ID. Same-pair teleport orders End before the new Begin.
Raw callback, active-pair or event overflow faults the world with ContactOverflow,
without a partial tick/snapshot. CopyContactEvents requires enough room for the
whole snapshot and supports repeated copying. Only physical/touching contacts can
start a pair; speculative-only contacts do not produce Begin.

Baseline60 and FastLinear60 follow PHYS1 settings. FastLinear60 has limited sphere/
thin-wall evidence, not a general angular/no-tunnelling guarantee. Shapes use metres,
full box dimensions and +Y capsule CylinderLength. Mesh data is captured before
publication. Mutations validate whole inputs/batches before backend writes; unexpected
begun failures fault the world. No rollback is promised. Force commands queue one
F*dt impulse for the next Step; Sleep clears velocities and pending forces.

One external owner serializes all invocations; concurrent/reentrant calls fail Busy.
The factory/world diagnostics distinguish live owners and completed pool cleanup.
Pool sampling does not prove all process/GC leaks absent or impose a hard RAM cap.

See `docs/physics/PROVIDER.md`, `CONTRACTS.md`, `PLAN.md` and `STATUS.md` in the
repository for exact scope, commands and acceptance. Other platforms, AOT, parallel
workers and ECS require separate evidence. App/factory/lifecycle acceptance covers
Cobalt and the existing Microsoft DI adapter in source and fresh packages. See also
`docs/physics/QUERIES_CONTACTS.md` for exact budgets, mesh backface and event policy.
