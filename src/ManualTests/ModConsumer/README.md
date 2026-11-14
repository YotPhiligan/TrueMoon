# ARCH3.6 managed Mod SDK proof

ARCH3.9: add `-Cycles 20` with `-Reload` or `-Migration` for repeated replacement
acceptance in every profile. Old ALCs must collect while the current World is live;
all stale contexts/callbacks/Worlds/snapshots remain retained. `-PackageVerification
<completed-ConfigurationDelivery-manifest>` consumes the exact shared archive
version/hashes. [Unified architecture runner](../ArchitectureDelivery/README.md).

ARCH3.8: `pwsh -NoProfile -File ManualTests/ModConsumer/Verify.ps1 -Migration -EvidenceDirectory TestResults/ModMigration-unique`
checks source/published Debug/Release, fresh closed-feed NuGet and SDK-free Runtime
on Cobalt/Microsoft DI. Choose either `-Migration` or `-Reload`. Private components,
exact migrator and entity-reference bindings preserve persistent IDs/relations/data/
system state/tick in a new World. Old handles/commands/callbacks become invalid.
Fresh ModV2 is independently compiled. Retired ALC must collect while the new World
is live, despite retained old World/context/callbacks. App owns all Worlds. SDK graph
is Abstractions+Contracts; Runtime receives no Tooling/UI. [Contract and limits](../../docs/engine/MOD_MIGRATION.md).
This is not the ARCH3.9 multi-cycle acceptance.

Run `Verify.ps1 -EvidenceDirectory <fresh path>` with PowerShell 7, after any exclusive Aether regression runner has exited. It captures the common SDK/TFM/language/runtime-async profile through `BuildProfile.ps1`, records binlogs and hashes, then checks:

- source and published Debug/Release hosts with Cobalt and Microsoft DI;
- a fresh package-only SDK mod (Abstractions + Contracts), private dependency, Tooling publisher and Runtime-only host in an isolated NuGet cache;
- execution from an unrelated working directory and a host containing only dotnet/fxr/shared runtime, without SDK;
- actual scalar component import, typed system ticks (7 then 8), stop/dispose order and weak-reference collection after roots are released, including retained disposed group/session handles.

The Runtime consumer receives no Tooling/Editor/UI. The publisher uses Tooling separately before runtime-only execution. Native code, GPU rendering, live replacement and World migration are outside this managed proof. Failure paths and admission/cleanup retry are checked in `TrueMoon.Aether.Tests/Mods`.

The fixture projects are build assets, not additional test runners. Runtime version and target framework come from the common profile; no independent SDK/TFM pins are introduced.

## ARCH3.7 stable-component system replacement

Add `-Reload` to run 12 replacement profiles instead of the original load/unload
proof. The Runtime-only host borrows an existing World containing shared
LinearVelocity, runs two old ticks, captures/restores system state, switches one
generation and runs the next tick. World/handle/data identity, tick/state sequence,
retired callbacks and expired command access are checked. The retired ALC must
collect while the new generation remains live, then the current ALC must collect
after App cleanup even with retained callback/context/disposed-session handles.

Fresh package mode compiles two different ModFixture DLLs with the same shared
SDK (the candidate uses MOD_V2); their hashes must differ. Source/published mode
selects the two fixture entry implementations in separate collectible contexts.
Both providers and the isolated SDK-free host are covered. Unit tests separately
exercise rejection, cancellation, rollback recovery, budgets and cleanup retry.
