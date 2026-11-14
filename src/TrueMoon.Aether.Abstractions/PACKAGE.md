# TrueMoon.Aether.Abstractions

Neutral managed game/ECS contracts, stable IDs and immutable scene/render values.
The only framework dependency is TrueMoon.Contracts. No World, scheduler, compiler,
filesystem service, native handle, GPU/backend, IPC implementation or UI is exposed.

ARCH3.3 makes these the canonical game/ECS/scene/render values used directly by
TrueMoon.Aether.Runtime. Duplicate legacy value models were removed; Runtime World,
typed pools and scheduler implement these contracts. Host-specific legacy system
overloads remain in Runtime.Ecs. No type-forwarding facade is supplied.
Game artifacts now use schema 2 with exact shared identities/hashes; rebuild old
games and hosts. Default game ABI is Abstractions + Contracts; concrete Runtime
sharing is an explicit tooling capability. Compiler/authoring workflows now live in
TrueMoon.Aether.Tooling after ARCH3.4. ARCH3.6 adds the managed Mod SDK in Mods:
versioned manifests, IModEntryPoint/IModEntry, local factory registration,
composition-only stable host services and cooperative service lifecycle. The SDK
still has no Runtime implementation dependency. Runtime owns collectible loading;
Tooling provides explicit immutable artifact publication. Live replacement and
component migration remain ARCH3.7/3.8.

IEcsSystem keeps synchronous Execute, cooperative cancellation, Stop before Dispose
and exclusive scheduler ownership. EcsSystemContext contains invocation-bound data,
queries and recording commands, never a concrete World or container. The host access
implementation must validate invocation, owner thread, declared read/write types,
World identity and terminal state on every operation. Retained contexts/queries/
writers remain invalid after their original invocation, including during a later one.

IComponent is optional metadata for reference-free value components. Existing
struct components remain usable; generic APIs do not require the marker and never
store component values through object/IComponent. CLR Type is live metadata only;
persistence and migrations use ComponentTypeId plus exact positive schema version.

No UseAbstractions registration is needed: this package owns no App services or
resources. App/DI/factory and lifecycle bindings belong to the runtime implementation.
