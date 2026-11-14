# TrueMoon.Aether.Runtime

Standalone .NET 11 Aether execution, using the repository's common SDK/TFM profile.
Use matching TrueMoon package versions. No project compiler, authoring history,
source preparation, Editor or UI is required to execute prepared FDD game assets.
References Abstractions/Contracts plus IPC/process and Vulkan providers.

```csharp
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Hosting;

context.UseAetherRuntime(options =>
{
    options.HostContractId = exactHostOrPairId;
    options.GameBuildId = selectedGameId;
    options.ReportDirectory = absoluteReportDirectory;
    options.RuntimeLaunch = explicitChildCommand;
});
```

Resolve RuntimeEngine for Supervisor/Frames/Replacement/lazy Gpu. Idle App starts
no game or GPU. RuntimeOptions excludes compiler/job settings. RuntimeConfigurationSchemas
and UseConfiguredAetherRuntime use the common App profile/providers; identity and
factory callbacks remain explicit. Child RuntimeBootstrap is captured before App;
UseConfiguredAetherRuntime(bootstrap) preserves protected parent/session flags.
UseAetherLocalRuntime(PlayerStart) is the separate local Player composition.

UseAetherEcs/UseAetherEcsSystems/UseAetherSceneLoading register injected factories.
World, scheduler, SceneWorld and game-loader results are caller-owned. Scheduler
owns its systems and borrows World; dispose scheduler before scene/World, and
retire render resources before game/ALC. Failed cleanup retains ownership for retry.
App owns singleton RuntimeEngine; workflow hosts borrow it and request Stop.

Game/ECS/scene/input/checkpoint/render/preview values are canonical Abstractions
types. Host-specific World/context/legacy-system APIs reside in Runtime.Ecs;
neutral game/mod systems use Abstractions.Ecs without a World/container capability.
SceneDocumentCodec reads/writes strict scene JSON v1/v2 without authoring services.
SceneWorldCodec retains its TMWL v1 envelope and existing limits. SPIR-V is embedded
in this assembly. Native GPU activation remains optional and bounded to Windows x64.

GameBuildArtifact only Open/validates/Pins schema 2 prepared managed artifacts.
Exact shared descriptors contain assembly identity and SHA256; the default game
ABI is Abstractions + Contracts. Concrete Runtime sharing requires explicit
RuntimeAccess in the tooling request/publisher. Private framework/shared copies
are rejected. Schema 1 needs rebuild; no retag/fallback facade is supplied.
GameArtifactPublisher, compiler and GameBuildResult source witnesses belong to
TrueMoon.Aether.Tooling after ARCH3.4. Runtime itself has no Tooling reference.

This is a breaking namespace/artifact transition. Rebuild game and host together.
ARCH3.6 adds optional UseAetherMods after UseAetherEcs: one App-owned ModHost,
strict version/hash/dependency validation, immutable managed byte snapshots and
one collectible ALC per WholeGroup closure. Local entry/service/system factories
never enter App DI; private Worlds belong to the group. Failed startup/cleanup
retains explicit ownership for retry. Weak unload tickets distinguish requested
unload from actual collection. No native mod libraries or forced thread abort.
ModArtifactPublisher lives separately in Tooling; GameBuildLoader is unchanged.

ARCH3.7 adds ModHost.CreateSystemSession(World): an App-owned system graph borrowing
a caller-owned stable-component World. ReplaceAsync privately prepares a new ALC,
quiesces/captures/restores explicit system state and commits at a tick boundary.
Guarded callbacks stage/drain/clear; failed pre-commit replacement resumes the old
graph, while post-commit retirement failure is separate and retryable. No per-tick
resolution, World reset or implicit system-state drop. Direct session factory
products remain caller-owned and must finish cleanup before borrowed dependencies.

ARCH3.8 adds ModHost.CreateWorldMigrationSession(): App owns changing Worlds and
groups. Neutral snapshots, bidirectional local bindings and explicit exact migrators
prepare a private World and preserve system state/tick before a single publication.
Unknown data rejects; field removal/reset require host admission. Candidate rejection
resumes the old World; retired cleanup remains separate/retryable. Entity refs remap
through persistent IDs; old handles are stale. Asset/native/GPU migration, universal
production reload, other OS/RID, AOT/trimming, device-loss/performance and general
release require separate acceptance.
