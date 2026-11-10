# TrueMoon.Aether

`UseAetherEcs()` registers an App-local `IFactory<TrueMoon.Aether.Ecs.World>` without
runtime/window/GPU activation. Each returned World is caller-owned; dispose it in
the owning session, not through a second DI alias. The first profile provides
generational entity handles and bounded typed singleton component pools. Component
structs cannot contain managed references; reads return copies, values are never
disposed, and access must be serialized by the caller. Default limits are 65536 slots,
128 retained types and 64 initial slots; configuration is validated and copied.
Typed Query<T>/Query<T1,T2> supports scoped in/ref callbacks and up to eight excluded
types. Structural mutations are blocked during iteration. EntityCommandBuffer
preflights the whole bounded FIFO batch before commit; unexpected commit failure
faults World without pretending rollback. Deferred tokens are buffer/batch-local.

UseAetherEcsSystems registers IFactory<EcsScheduler,World> after UseAetherEcs.
Register each IFactory<TSystem,World> in the game module; describe Read/Write and
Before/After dependencies with EcsSystemDescription. Scheduler owns factory results,
borrows World/dependencies, and runs caller-driven positive fixed ticks in ordered
phases. Stop/cancel discards pending phase commands; failures are terminal and failed
cleanup remains owned for retry. Dispose scheduler before World. No timer or late
container resolution is introduced. Context command writers are invocation-fenced.

`TrueMoon.Aether.Ecs.Loading` adds frozen exact ComponentRegistry bindings and
SceneWorldLoader/IFactory<SceneWorld,SceneWorldLoadRequest>. Call UseAetherSceneLoading
after UseAetherEcs with a composition-root registry factory. Full immutable
SceneDocument data is resolved/validated before World creation; all entities are
reserved before import. Source defaults/null/references and exact schema versions
are preserved, singleton admission is explicit. Importers receive scoped data/handle
access and tracked resource acquisition, not a mutable World or service resolver.

SceneWorld owns World and prepared IDisposable leases. Dispose its scheduler first,
then SceneWorld; direct World disposal is rejected. Failure/cancel publishes no
partial scene, reverse cleanup retains failed resources via CleanupOwner for retry.
Loader/codec are stateless borrowed services; App does not own their factory results.

SceneWorldCodec adds a TMWL/version1/length/SHA256 envelope over the existing strict
authoring body, maximum4MiB+44bytes. It needs no registry/game assembly; malformed
UTF16 is rejected instead of silently replaced. Initial runtime activation negotiates
version1/registry generation in Hello and sends this envelope in contiguous32KiB
chunks within the existing64KiB control limit. One transfer per runtime generation;
checksums, source/operation/session/build identity, exact counts and atomic initial
publication are checked before readiness. SessionSupervisor.StartWorldAsync accepts
complete neutral data and an optional selected artifact; FrameSession.StartWorldAsync
adds existing first-presentation ownership. Neither loads game CLR types in the parent.

Ecs.Runtime.SceneWorldGameSession owns the active SceneWorld and retained failed
cleanup, using its injected registry-specific SceneWorldLoader. UseAetherRuntime
provides IFactory<SceneWorldLoader,ComponentRegistry> and preserves existing World
factory overrides. Failed Tick faults the World; cleanup starts a terminal operation
fence, retains failed owners and permits Dispose retry. GameBuildLoader retains the
game/ALC/artifact pin if cleanup fails. The protocol drains cooperative importers
before releasing staging; the runtime host drains admission/workers before the game.

`PrimitiveEcsComponents.Register(builder)` explicitly binds the existing three builtin
v1 schemas and optional parent-space VelocitySchema to value components. Existing
BuiltInComponents.Schemas is unchanged. LocalTransform uses a unit quaternion and
positive scale; iterative TransformSystem validates complete Parent/Local/World roles,
cycles/stale/foreign parents and numeric limits before cache publication, preserving shear.
MovementSystem integrates parent-coordinate velocity before Transform/Extraction.

UseAetherPrimitiveEcs configures copied count limits/fixed step/fixed-view camera and
lazy typed factories. UseAetherRuntime preserves existing factory overrides. The
caller owns PrimitiveSimulation, its scheduler and systems; its SceneWorld is borrowed
and must outlive the controller. Initial tick0 is prepared privately; detached immutable
PrimitiveRenderSnapshot values are published only after a successful complete tick.
PrimitiveGameSession implements IRenderGameSession for the existing cube renderer.
SceneWorldGameSession.PrepareSceneWorld may return an exclusively owned controller;
base disposes it before World and retains failed SceneWorldPreparationException.CleanupOwner.
Preflight errors preserve old caches/frames; whole simulation ticks are not rollback transactions.

Primitive extraction limits enabled drawables to64 independently of logical groups,
rejecting overflow without truncation. Native handles never enter these pools/snapshots.
Editor Play/preview, mesh/material resource preparation/rendering and ECS checkpoint/Replace
remain separate stages. Existing64-entity legacy SceneSnapshot and checkpoint limits
remain unchanged; complete scene capability has a separate bounded4096entity profile.

`TrueMoon.Aether.Rendering` provides CPU-only `RenderContentHash`/`RenderAssetKey`,
immutable `MeshDefinition`, `OpaqueUnlitMaterialDefinition`, `MaterialBindingSetDefinition`
and `RenderContentManifest`. Keys contain no managed/native references. Definitions
copy bounded input lists; the manifest checks exact project/kind/schema/revision,
duplicate identities, complete dependencies and ordered mesh slots. It is immutable
data, not a GPU cache or IDisposable service. BuiltinRenderContent.Create supplies
the eight-vertex cube, white material and exact default binding set for a project/build.

RenderContentCodec encodes/strictly decodes TMRA v1 canonical asset payloads: RFC4122
GUID bytes, little-endian scalars, normalized positive zero. SHA256 revisions cover
canonical data and dependencies. A manifest checks supplied revisions against content;
decode alone does not authenticate a caller's expected revision. EncodeManifest writes
a TMRM fingerprint of project/build and sorted exact keys, excluding process-local
generation. This is a CPU format, not an extension to the Runtime control protocol.

`Ecs.Rendering.MeshRenderingComponents.Register(builder,content)` registers only
the exact singleton MeshRenderer/PerspectiveCamera schemas, each requiring Transform.
It installs the builder's AssetValidator for that frozen manifest; consumers with
additional asset domains must explicitly compose their validator afterwards.
Register Transform separately, for example through PrimitiveEcsComponents.Register.
Use the existing UseAetherEcs/UseAetherSceneLoading composition path:

```csharp
var content = BuiltinRenderContent.Create(projectId, RenderContentHash.Parse(buildSha256), Guid.NewGuid());
context.UseAetherEcs();
context.UseAetherSceneLoading(_ => MeshRenderingComponents.Register(
    PrimitiveEcsComponents.Register(new ComponentRegistryBuilder()), content).Build());
```

Import validates all references/values before World creation, including disabled
renderers, zero layer masks and disabled tint. Appearance+MeshRenderer on one entity
is rejected. Shared material data stays immutable; instance tint is a linear RGB
multiplier and reset disables it. RenderColor explicitly decodes sRGB and encodes
linear results. PerspectiveCamera validates rigid explicit poses and finite Vulkan
view/projection matrices; import validates settings, not derived WorldTransform.
CPU types need no fake UseMesh service registration. Resource ownership/preparation
is opt-in below; owned mesh snapshots, new shader/renderer and Editor integration
are subsequent stages.

`UseAetherRenderResources(options => ...)` explicitly adds an App-owned
`Rendering.Resources.RenderResourceHost` and non-disposable typed factory wrappers
for RenderManifestLease, RenderDeviceOwner, RenderPreparation and RenderWorldBinding.
Startup stays CPU-only/lazy. Existing IFactory<IRenderResourceBackend,RenderDeviceRequest>
overrides are preserved; the default backend prepares Vulkan geometry on demand.
The host owns its CPU catalog and retains device/binding products until successful
cleanup; callers dispose products early, before SceneWorld and App. DI aliases do
not independently own the same disposable service. App cleanup with live external
CPU leases closes admission and fails without losing the owner; release and retry.

RenderContentCatalog admits complete frozen CPU scopes, deduplicates exact keys and
physical definition buffers, and returns exclusive counted leases. Aliasing a lease
does not acquire it. RenderResourceComponents.Register(builder,scope) adds the same
two rendering schemas and retains one CPU lease per imported mesh/camera component
through existing ComponentLoadContext.Acquire; SceneWorld owns these retains. Keep
the initial scope alive while its registry accepts new loads. Scene resource-count
admission remains separate and may require explicitly chosen load options.

RenderPreparation owns the entire ready closure until disposal; Completion is a
Task, not a second resource owner. The bounded device queue admits four active and
sixteen waiting operations by default. Exact mesh keys share one generation-fenced
entry. One cancelled waiter cannot destroy a resource used by another; last release
drains upload before destruction. Failed cleanup retains entries/slots/budgets for
retry. Unknown native submission/completion fences the device and retains ownership.
StopAsync cancellation stops the wait, never pretends resources were released.

RenderWorldBinding borrows World. InitializeAsync awaits resource readiness;
ReloadAsync prepares a complete candidate, rechecks render membership/values, then
changes pinned component keys and current manifest at a serialized boundary. The
caller serializes World access. Failed preflight/cancel preserves current binding;
successful publication with failed old cleanup returns Committed=true plus CleanupError.
This is resource readiness, not Runtime Playing, frame presentation or checkpoint support.

CPU limits include2048revisions/64MiB definition arrays/64scopes/16384lease wrappers.
Device budgets include64MiB actual geometry requirements,16MiB upload allocations
and16MiB CPU staging; old/candidate/retiring owners all count. Lower limits are copied
at configuration. Object/driver overhead and existing surface allocations are outside
these byte classes. Default Vulkan backend owns a standalone lazy graphics context,
uploads vertex/index buffers, confirms fences and optionally verifies GPU readback
hashes. It creates no shaders/pipelines/surfaces and exports no native handles.
Owned snapshots, surface/device matching and frame retirement integration remain f.3/4.

`Scenes.EntityDefinition`, `EntityInstance`, `EntityPatch` and `EntityResolver`
provide immutable in-memory authoring with single-base variants, property overrides,
component additions/removals/restorations and value provenance. Exact `ComponentSchema`
contracts expose defaults, nullability, multiplicity and dependencies without loading
game assemblies. Values support null, Boolean, Int64, finite double and bounded strings;
numeric kinds are distinct. Resolver errors expose stable codes and ID-based paths.
`SceneDocument` freezes a scene revision and its catalogs. `SceneResolver.Resolve`
validates entity identities, scene parents/cycles and all effective entities before
returning one immutable `ResolvedSceneSnapshot`, with deterministic parent-first
order and referenced definition/schema versions. `SceneResolutionException` retains
the scene/entity path and original authoring cause.
`SceneSnapshotAdapter.Project` explicitly maps the result to the existing
`SceneSnapshot`: the caller supplies world-space XYZ/interaction, while the adapter
preserves canonical IDs/revision and enforces finite values and the64-entity limit.
No partial result is returned on failure/cancellation. Full runtime snapshots,
live editing and MCP are later stages.

For headless scene files call `UseAetherAuthoring().UseAetherScenePersistence()`.
`TrueMoon.Aether.Scenes` now owns `SceneFileCodec`, `ISceneDocumentStore`,
`JsonSceneDocumentStore`, `SceneFile`, `SceneFileStamp` and `SceneFileConflictException`.
These alpha APIs moved from `TrueMoon.Aether.Editor.Documents`; update imports.
Version1 scene bytes/limits remain compatible. Persistence registration is lazy and
idempotent, preserving custom codec/store registrations; borrowed authoring is required.
The store uses bounded reads, SHA256 byte identities, create-only new writes and
cooperative atomic replacement. Editor project/document sessions and close UX remain
Editor responsibilities. The Aether package has no Editor reference.

For local editing call `UseAetherAuthoring(...)` on App configuration, independently
of `UseAether`. Inject `IFactory<ISceneAuthoringSession, SceneDocument>` from
`TrueMoon.Aether.Scenes.Authoring`. Create validates the initial scene and returns
a caller-owned IDisposable session; close it before its borrowed App dependencies.
App startup creates no sessions, windows, devices, IPC endpoints or game processes.
Options are copied at configuration/factory creation; repeated registration fails.

`Read()` returns the committed document, resolved scene and history availability.
`Apply(new TransformEditBatch(new AuthoringOperation(id, expectedRevision, actor),
edits))` atomically replaces all nine local built-in v1 Transform properties by
stable entity/component occurrence IDs, preserving other data. Schema resolution,
finite float/positive-scale and parent-composed nonsingular transforms are checked
before publication. Authoring does not impose the preview's64-entity/material limits.
`Undo(operation)`/`Redo(operation)` restore exact content with increasing document
Revision; ContentRevision restores the session-local content checkpoint. Fresh
Apply clears Redo. MaxHistoryEntries defaults100 (0..1024, combined Undo/Redo),
MaxBatchEdits64 (1..4096), MaxOperations4096 (1..65536).

Commands return typed error/revision/path receipts. Exact duplicate operation IDs
return the original receipt; a different intent/actor/revision/payload conflicts.
Terminal receipts, including expected rejection, are retained until Dispose without
retaining old documents. At MaxOperations new IDs are rejected instead of evicting
and risking replay. Cancellation/unexpected failure before the single commit
publishes neither content/history nor receipt; cancellation after commit cannot
hide success. Receipt identity is session-local and does not survive restart.
Create/delete/reparent, generic property edits, persistence, UI and Edit preview
remain subsequent steps; runtime World and saved authoring are separate.

An App-owned `AetherHost.Preview` provides the first independent Edit preview.
Configure the existing explicit GPU viewport strategy, then call
`ShowAsync(resolvedScene, width, height, token)`. Projection is validated before
stopping an existing child; accepted updates drain/reap and start a fresh built-in
runtime generation. No game project, compiler, Play or Game.Logic DLL is used.
The child calls `UseAetherPreviewRuntime` with `RuntimeRole.EditPreview`,
`BuildIdentity.PreviewId`, no build and no restore. Its static scene consumer
uses a fixed camera(0,1,6) and tick0; gameplay/Step/input are rejected.

Keep each `ViewportFrame` with the displayed image and call
`host.Preview.Presented(frame)` only after actual presentation, including while
ShowAsync awaits its first frame. Document/LastPresented/Describe bind the exact
authoring revision to the runtime generation and surface. Stale/foreign/closed
tickets cannot acknowledge new content. Resize supports0x0 suspend and positive
16..1920 by16..1080 extents; Show requires a positive extent. The initial profile
requires GPU; CPU pattern frames are not presented as scene preview.

One update/resize is admitted at a time. Stop fences pending work and retains
cleanup ownership until drain; failed cleanup can be retried. Dispose closes
preview admission without separately disposing borrowed Frames. Explicitly stop
the current mode before switching between game and preview. Transform/Undo/Redo
remain local authoring operations; the caller forwards their resolved snapshot.
First profile uses full child replacement per update, not incremental/live World
editing. Main Editor UI integration, project/storage and camera/tools remain later
steps. Existing control/frame wire versions and same-build fencing are preserved.

`BuiltInComponents.Schemas` supplies stable v1 Transform/Appearance/Interaction roles.
`PrimitiveSceneAdapter.Project` maps them to control-v3 scene data: up to64entities,
optional opaque cubes with immutable world matrices and stored-sRGB colors. Local
row-vector transforms are Scale * yaw/pitch/roll Rotation * Translation;
world = local * parentWorld, retaining shear. Positive scales and exact built-in
schemas are required. Missing Appearance means no draw; groups without Transform
use identity. Interaction is eligibility, not reach/raycast behavior.
`new Rendering.PrimitiveScene(camera, primitives)` freezes up to64draws; empty draws
clear the background. The original single-cube constructor remains available.
Control v1/v2 identities are rejected: rebuild matching Editor/Runtime/Aether/game
DLLs together. Frame1 remains current; SnapshotVersion is2 (TMCK2), with v1 rejected.
Static geometry reloads from the same scene revision; checkpoint retains tick/camera/
interaction and a bounded stable-ID object switch set. Any authoring data
change must advance the scene revision. Full world restore/live edits remain planned.

`Interactions.PrimitiveRaycast.Cast` finds the closest opaque affine cube along
the camera's forward ray, preserving shear and world-space distances. Up to64distinct
bodies, finite reach(0,1000], inclusive boundary; ineligible/disabled bodies occlude.
Distance ties prefer blockers then ordinal IDs. Inside/on-surface distance is zero.
The repository RoomGame uses reach4 and one E pulse to toggle the selected cube's
material, preserving independent per-object state through Replace.
`GameCheckpointState.Objects` contains immutable `GameObjectStates`: up to64sorted
unique valid entity IDs and Boolean Active flags, with value equality. TMCK2 carries
the full set with strict UTF8/count/order/flag/length/checksum validation. RoomGame
validates the exact loaded eligible-ID set before atomic camera/tick/state commit;
unsupported object states are rejected by ProbeGame/CameraGame. Editor authoring
data remain unchanged. This does not provide full World snapshots or live editing.

Experimental TrueMoon game-engine module: session lifecycle, control contracts,
owned game processes, checkpoint replacement, bounded CPU/GPU frame transport and
an explicit Vulkan/OpenGL viewport with a first depth-tested primitive renderer. Current prototype scope:
Windows x64 and .NET 10.

The repository project is packable. The API remains experimental/alpha;
local package validation does not imply publication or production readiness.

Connect the module through `UseAether(...)` on `IAppConfigurationContext` inside
`App.Build`, `App.Builder(...).Setup(...)` or `App.RunAsync`. Configure host/game
build identities and a report directory; an optional runtime-factory delegate
can use application services. Options are snapshotted during registration.

Resolve `AetherHost` from App services. App owns the host and runtime factory;
the host owns `Supervisor`, `Frames`, `Builds`, `Replacement`, `Editor` and `Gpu`. App startup does not launch a game or allocate a GPU device.
Start game sessions explicitly; App Stop permanently closes the host, fences
new starts, drains frame workers and stops/reaps the child. Use session Stop for
repeatable Play/Stop while App remains running. Do not separately register or
dispose the borrowed supervisor/frame session. Cleanup failures remain observable
and an explicit host Dispose retry is supported.

**Explicit native runtime launch (ARCH2c).** The default native factory never
selects the current executable or adds `--runtime` implicitly. In the parent App:

```csharp
using TrueMoon.Aether;
using TrueMoon.Aether.Hosting;

context.UseAether(engine =>
{
    engine.RuntimeLaunch = new NativeRuntimeLaunch(absRuntimeExe, absRuntimeDll);
    engine.HostContractId = BuildIdentity.ForRuntime(absRuntimeDll);
    engine.GameBuildId = new string('0', 64); // Build/select a game artifact before game Start.
    engine.ReportDirectory = reportDirectory;
});
```

Both paths must be absolute: the executable is the apphost and the DLL is its
managed runtime entry. For dotnet, explicitly include the DLL and any runtime
arguments: `new NativeRuntimeLaunch(absDotnetExe, absRuntimeDll,
[absRuntimeDll, .. runtimeArguments])`. Arguments are copied as individual values,
without shell parsing; the immutable descriptor defaults its working directory to
the runtime DLL directory. No process is started during configuration.

`AetherOptions.RuntimeLaunch` is nullable. Without a descriptor, registration,
App startup and headless/common services still work; default native Start fails
with a clear configuration error asking for `AetherOptions.RuntimeLaunch` or an
explicit native runtime factory. Custom `RuntimeFactory` remains supported without
a descriptor unless that factory itself needs one. Standalone native creation is
`new NativeRuntimeFactory(runtimeLaunch)`.

A host implementing its own child branch may explicitly use
`NativeRuntimeLaunch.CurrentProcess("--runtime")` with `BuildIdentity.HostId`.
This compatible same-executable profile preserves the previous HostId; it does
not make an arbitrary consuming executable a runtime. The separate repository
example is `Examples/AetherRuntimeHost`, without Editor/UI dependencies.

`BuildIdentity.ForRuntime(runtimeDll)` fingerprints role-labelled host/runtime
managed entries plus exact shared Aether/Contracts/Enerit/Mithril binaries. It
checks the shared binaries in both entry directories; parent launch and child
bootstrap also verify their actual managed entry. Native counterpart identity
Version1 is carried in `RuntimeLaunchIdentity`; bootstrap JSON is bounded to
16384 characters and depth32. Control4/frame2/snapshot2 and session/nonce/generation
fencing are unchanged.

This is a trusted-local compatibility check, not an authentication boundary or a
complete native/private-dependency hash. Do not mutate either deployment in place
during a session. A different pair identity invalidates previous game artifacts
and checkpoints: rebuild games for the new pair. Scene/project formats do not
change. Repository verification on 2026-10-29 covers independent NuGet build/
publish/headless consumers and standard/embedded native Editor consumers on
Windows x64/net10.0 with OpenGL/Vulkan interop. The [consumer instructions](../ManualTests/AetherPackageConsumer/README.md)
describe separate source/package profiles, ownership, native validation and
failure/cleanup checks. Other OS/RID/AOT, physical input/DPI/monitors, device loss,
K7 stress and VRAM/general release remain separate acceptance work.

Services are created through injected `IFactory<T>` / `IFactory<T, TArguments>`.
Register overrides before `UseAether`; the host owns its factory-created children.
In the child executable call `RuntimeBootstrap.ReadEnvironment()` before App
construction. Configure `UseAether` from its `.Launch` identities/report directory
and `DiagnosticDelivery`, then call `UseAetherRuntime(launch, bootstrap.ParentProcessId)`
or `UseAetherPreviewRuntime` for `RuntimeRole.EditPreview`. The parser requires a
selected game artifact or valid built-in preview; only the explicit legacy probe
profile uses `allowLegacyGame: true`. No RuntimeLaunch is needed in a child-only App.
Start the App and run the resolved `Runtime.AetherRuntimeHost`. It owns the game,
parent listener, workers and IPC server, borrowing App/GPU services. Main/CLI,
App construction, diagnostics and reporting remain consumer responsibilities;
the library supplies no concrete Main.
When both Apps enable the bounded Core diagnostics pipeline, the default native
factory and runtime host also own a separate child-log receiver/forwarder pair.
`AetherOptions.ChildDiagnostics` controls copied record/byte/rate/deadline limits;
`SessionLaunch.DiagnosticDelivery` carries the per-generation settings. The lane
preserves origin/PID/trace and exposes loss/unconfirmed/cleanup health without
sharing control admission. Local JSONL and continuous stdout/stderr remain fallback.
See [delivery configuration and ownership](../docs/engine/DIAGNOSTICS_DELIVERY.md).

`UseAether` also registers a non-disposable `Diagnostics.IProcessDiagnosticsSource`
over borrowed runtime/compiler owners. `Read(maximumCount, maximumOutputCharacters)`
returns at most64 recent memory snapshots with public session/generation/role/build/
job/revision correlation, raw stdout/stderr, read/file completion and delivery health.
No endpoint, nonce or environment is exposed and no files/IPC/SDK operations are
performed by a read. A custom IRuntimeFactory can implement IRuntimeDiagnostics;
otherwise RuntimeAvailable is false, independently of compiler availability.
NativeRuntimeFactory retains64 recent lease references with thread-safe reads;
GameBuildCompiler retains16 completed job snapshots plus its active job, including
failed builds across a successful retry. This memory history does not replace disk
retention. Limits and UTF16-safe tail windows do not reclassify raw stderr as errors.
Consumers borrow the source and never dispose/reap its underlying process owners.

Inject `Scenes.SceneResolutionService` for authoring resolution through its
document-specific `EntityResolver` factory.

The single Aether assembly contains `TrueMoon.Aether` and
`TrueMoon.Aether.Sessions`, `.Scenes`, `.Transport`, `.Frames`, `.Checkpoints`, `.Rendering`, `.Input` and `.Diagnostics`.
It uses TrueMoon Contracts/Enerit/Mithril without
depending on UI, Skia or a particular game DLL. Silk.NET.Vulkan 2.23.0 supports
the opt-in Windows x64 GPU path; native GPU startup remains explicit. The application supplies
Editor/Runtime entry points and its `IGameSession` implementation.

`Rendering.IRenderGameSession` is an optional observational capability. Capture it
through `RuntimeProtocol.CaptureRenderFrame` at a serialized simulation boundary,
then create `Gpu.Surfaces.GpuFrameData.FromScene` in the runtime frame provider.
The scene describes up to64world-space cubes, a perspective camera and opaque RGB
material. Vulkan draws directly into the exported image with a slot-local depth
buffer; GLSL/SPIR-V are embedded, with no runtime shader compiler dependency.
The default reference pattern and CPU viewport remain available. A static authored
room and reach/raycast interaction are available in the repository's `--room` sample. Collisions/HUD,
asset import and general scene rendering are later stages.

`Input.IInputGameSession` optionally receives `GameInputTick` at each fixed
1/60-second tick/Step. Send a validated `GameInputState` through the App-owned
`host.Supervisor.SendInputAsync(expectedIdentity, state)`. An Accepted outcome
stages input; it does not advance simulation. Unsupported is explicit and keeps
legacy sessions working. Held movement has a12-tick lease; look/action pulses
are consumed once. `FirstPersonCamera.Step` provides normalized XZ movement and
bounded pitch/yaw. Pause/Stop/restore clear transient input. Runtime adapters
call `RuntimeProtocol.ResetInput` on GPU surface/focus changes; the example wires
Configure/Detach automatically. Input itself is never checkpoint state.

Control wire is version3 (input and primitive payload); frame schema is1, TMCK schema2.
Rebuild matching host/game artifacts; version1/version2 control and snapshot1 peers
are rejected, and old host fingerprints remain incompatible. The engine has no
UI dependency. The example owns a bounded30Hz sender, focus/pointer routing and
its drain before App/UI teardown; raw/locked OS mouse input is not supplied.

Game logic remains a separate assembly. CPU frame transport and session control
and explicit immutable game-build compilation/selection are implemented. Use
`host.Builds.BuildAsync(new GameBuildRequest(project, entryDll, entryType, revision))`
to compile a trusted local net10.0 library consuming the non-copying MSBuild
`AetherContractPath` and `TrueMoonContractsPath` references. Then explicitly stop/reap the old session and start
the returned artifact through Supervisor or Frames. Build completion never replaces
the active game. Host options include BuildDirectory and BuildTimeout (maximum120s).
Private managed dependencies must be declared; native game dependencies are outside
this scope. Build failures retain diagnostics; App shutdown cancels/reaps the owned
compiler. Only the child loads game code, sharing Aether and TrueMoon.Contracts.
The selected type implements `IGameSession` and `Runtime.IGameSessionEntry`, whose
static `Create(IServiceResolver? services)` explicitly creates a caller-owned game
with constructor dependencies from runtime DI. No constructor reflection fallback
is used. Standalone loading passes null or accepts an explicit game factory.
Migration2026-10-22: add this entry factory and the Contracts reference; rebuild
host/runtime/game together. HostId and artifact ContractHash cover both shared
assemblies, so previous artifacts must be rebuilt.

Optional `ICheckpointGameSession` supplies a minimal v1 tick/camera/interaction
snapshot. Pause then call `Supervisor.CaptureCheckpointAsync`; preflight
`checkpoint.ValidateFor(hostId, scene)` before stopping the old game. Start a fresh
session with `StartAsync(scene, artifact, checkpoint)` on Supervisor, or
`Frames.StartAsync(scene, artifact, checkpoint, width, height)` for first-present
readiness. The child handler must pass `SessionLaunch.RequireRestore` to
RuntimeProtocol; restore completes before surface/frame publication. Start without
a checkpoint explicitly resets from the document. Source build may differ; host,
scene and revision must match. TMCK v2 codec validates integrity and bounded data.
Capture must be observational and game restore atomic. Confirmed capture rejection
keeps the paused session usable; unknown transport/callback outcomes fault and
clean up.

`host.Replacement.ReplaceAsync(new GameReplacementRequest(candidate, expectedIdentity,
scene, currentSourceRevision), token)` combines pause/capture, old-child reap,
selected-build restore/first-present and previous mode. A shared reservation rejects
competing commands; Stop/App close fence late candidate and recovery starts. One
old-build rollback is allowed. GameReplacementException preserves the original
InnerException and separate RecoveryError, with rollback outcome flags. Caller
cancellation after Stop can recover the old game within the remaining budget;
explicit Stop/close prevent recovery. App snapshots ReplacementDeadlines: total45s,
forward18s, cleanup10s; limits may be reduced. Unconfirmed cleanup retains the task
and lease in CleanupFailed until an explicit successful Stop retry. Headless and
visible CPU viewport are supported; resume a suspended surface before Replace.
Opt-in `CaptureCompilation` (requiring `TrackInputs`) prepares SDK Csc arguments,
copies source/generated/reference/config/SDK analyzer and compiler bytes into the
owned job, then executes captured Csc without rerunning MSBuild. Process-local
`GameBuildArtifact.Compilation` records their hashes; schema1 reopening loses this
provenance. Source paths/diagnostics are mapped back, staging is removed before the
result returns, and private DLL outputs use captured copies. Unknown/search/response
file switches, custom compiler/environment/external analyzers, resource/ruleset/signing
inputs and absolute analyzer-config sections fail closed. This is a C# library
compilation profile, not general MSBuild/postbuild semantics. Without CapturePreparation,
preparation, installed dotnet runtime and ambient environment remain trusted. The existing
file/byte limits include compiler and reference-pack files; response/config documents
are limited to8MiB. Legacy requests keep capture disabled.

`CapturePreparation` additionally requires both flags and freezes the supported
declarative project/import/source/reference closure before SDK evaluation. Scene
Start enables it by default. The profile is Windows/stable net10.0 Microsoft.NET.Sdk
library with literal imports/HintPath/linked files and project-local globs. Custom
tasks/targets, MSBuild functions, custom SDKs/analyzers, external globs and package/
project graphs fail closed before any SDK process. Explicit installed SDK/runtime
files are read-leased; processes use an explicit private environment/home/temp and
offline NuGet configuration. Installed toolchain directories and Windows/native OS
remain trusted; arbitrary MSBuild code is not sandboxed. Original paths and diagnostics
are preserved, including generated analyzer-config ProjectDir. `Inputs.Preparation`
records process-local source/toolchain/environment identity; reopening schema1 loses
provenance. ElapsedMilliseconds/SourceMilliseconds/ToolchainMilliseconds and
ToolchainFiles/ToolchainBytes report freeze cost without changing content identity;
SDK restore/evaluation/compilation and total consumer Start latency are separate.
ToolchainEnumerationMilliseconds/ToolchainOpenMilliseconds/ToolchainHashMilliseconds
split census/path validation, fresh path checks/read leases and byte reads/SHA256.
Every phase still enumerates and hashes the entire toolchain. Ancestor validation
is reused only within one pass over trusted/stable installed directories, and
leases disable per-stream managed buffering in favor of one 64KiB capture buffer.
After-build/prestart re-freeze live inputs before accepting them.
Requests may opt into ReuseArtifact with CapturePreparation. The compiler retains
one successful process-local result without live resources. Matching before/after
input observations, original compiler inputs and pinned artifact hashes allow reusing
its managed DLLs; a new source ticket republishes the manifest. Missing/corrupt
payloads or changed inputs rebuild; failed/cancelled jobs and failed prestart checks
discard reuse. Reopening schema1 never seeds it. Explicit builds remain fresh by default.
GameEditor.ReplaceBuildAsync(expectedContextVersion) requires the exact selected
context/candidate; both Replace entry points verify tracked inputs before reserving
the checkpoint transaction or pausing the old game. Reopened tracked provenance is
rejected. Existing compatible-scene and bounded recovery/Stop fences remain unchanged.
Source quota is4096files/64MiB per file/256MiB; toolchain8192files/1GiB, depth≤64. Copies and
leases remain compiler-owned and are released with the job before result completion.
Extended graph profiles, disk cache/document-aware Replace and independent native/
package acceptance remain separate work.

Opt-in `GameBuildRequest.TrackInputs` observes evaluated SDK files/options and
preprocessed imports before/after compilation. `GameBuildArtifact.Inputs` retains an
immutable process-local content witness; GameEditor rechecks it before Start. Persistent
changes reject publication/startup with `GameBuildInputsChangedException`. The bounded
profile is one net10.0 SDK project without PackageReference/ProjectReference, using
managed HintPath references. Reopening an artifact does not recreate its witness;
schema1 is unchanged. Verification uses compiler-owned cancellation/deadline/reap/drain
and diagnostic retention. Transient edit-and-revert, arbitrary target/generator/environment
IO, hermetic capture, artifact caching and document-aware Replace remain later work.

`host.Editor` provides UI-independent serialized Build/Select/Start/Replace/Play/
Pause/Step/Cancel/Stop commands. SetContext snapshots build/scene/selection inputs,
cancels pending work and invalidates candidates; completion checks originating
context/runtime identity and Stop version. Build never applies the candidate
automatically. SelectBuildAsync explicitly reuses a compatible artifact. Cancel
retains transaction recovery; Stop/close fence recovery and drain compiler/commands.
Status retains complete command/recovery errors. App owns disposal through the host.
The native example verifies 20 A/B replacements in one App/window with actual
loaded hashes, OpenGL pixels, checkpoint/first tick and selected next-step behavior.
`TrueMoon.Aether.Gpu.GpuTransportProbe` validates immutable producer/consumer
capabilities bound to a full session identity. Schema/profile one checks Windows
x64, device/driver UUIDs, exact Vulkan RGBA8 image and opaque Win32 memory/binary
semaphore export, and required GL extensions/procedures/tiling/fence polling.
Eligibility permits only a subsequent explicit import experiment; it never
enables GPU transport. This stateless utility needs no lifecycle registration
without creating GPU resources. The native example queries an owned runtime and
the actual Editor GL context; its separate ENG4b mode additionally imports one
dedicated RGBA8 image and binary semaphores through owned-process duplicated NT
handles. `AetherHost.Gpu` owns the channel/producer/imports, drains before UI cleanup,
and retains failed cleanup for explicit retry. Keep the borrowed GL context alive
until cleanup succeeds; do not add disposable aliases. Exact ready and return
CPU-observed fences precede GPU wait/retirement. Whole-image readback is a proof
operation, not regular viewport presentation. The ordinary viewport still uses
CPU transport. ENG4c additionally exposes `GpuResourceHost.ConnectSurfaceAsync`
and `GpuSurfaceSession` in `TrueMoon.Aether.Gpu.Surfaces`. Configure explicitly;
three persistent allocations share one Vulkan device/queue. Produce, Read and
Return use an exact version-two session/surface/slot/frame stamp. A slot is reusable
only after observed GL completion and Vulkan retirement. Configure drains ready
slots and acknowledged detach before the next generation; 0x0 suspends. Dispose
fences/drains accepted operations and local GPU use; runtime Stop/reap remains
owned by Supervisor. A failed cleanup retains the session and blocks another
reusable surface session until explicit retry succeeds. Keep the borrowed GL owner alive.
ENG4d adds `GpuSurfaceSession.UseTextureAsync(ready, consume)`: a synchronous
owner-thread texture loan after the ready wait, followed by return signal and
observed GL completion. Submit all renderer commands before returning or throwing;
do not retain the native name. Descriptor access outside the callback or owner is
rejected. ReturnAsync still explicitly retires the slot. Configure drains unused
Ready slots without CPU readback. Callback failure/cancellation fences the session.
Use the optional Rendering.Skia `SkiaOpenGLSurface.CopyTexture` to retain an
independent GPU image across slot reuse, resize and runtime Stop. Aether has no UI
dependency. The image requires its original Skia surface/context owner for drawing
and disposal, before UI teardown. Profile: RGBA8 premultiplied stored sRGB/top-left.
GPU completion does not acknowledge first presentation; observe the actual UI
FramePresented event and current stamp. The explicit reference GPU viewport uses
one GPU copy per frame. ENG4d2 provides explicit ordinary GameEditor GPU selection
through `AetherOptions.ViewportTransport`, returning `GpuViewportTransport` from
the App-owned factory. Its context provider and synchronous copy consumer remain
borrowed. Keep `GpuViewportImage.Frame` with the retained image and acknowledge
`host.Frames.Presented(frame)` after actual UI presentation; stale tickets return
false. `FrameSession.Presentation`/ViewportSurface are transport-independent,
while Display/Surface preserve the legacy CPU API. The shared lifecycle covers
Start/Resize/Stop/selected-build replacement/checkpoint/rollback. Connection cleanup
precedes process Stop; failed cleanup retains ownership and blocks a fresh Start
until retry. Disposable strategies are owned once by FrameSession, not DI aliases.
CPU is the default; no automatic transport switch occurs after a fault.

The runtime hosts RunSurfaceProducerAsync with an optional frameProvider that
returns matching RGBA pixels and FrameMetadata from a loaded/restored game capture.
GpuFrameData.GamePattern is a CPU-generated reference uploaded through Vulkan
staging; pixel data stays out of the pipe. The original diagnostic reference
producer is still available when no provider is supplied. A 3D renderer and
performance validation remain later milestones. Same-build protocol identity,
owned-process cleanup and frame/thread ownership remain required.

Source, integration example and roadmap:
[TrueMoon repository](https://github.com/YotPhiligan/TrueMoon).

With `CaptureProjectReferences = true` (requires CapturePreparation), the compiler
captures a DAG of at most four net10.0 C# library projects including the entry.
Project directories must not overlap. References must be unconditional literal ProjectReference items without metadata;
dependency AssemblyName is the project filename or an unconditional literal.
Every private DLL must be declared in Dependencies. Cycles, output-name collisions,
mixed SDKs, package references without PackageDirectory and context-dependent shared imports fail closed.
All source/import/config/glob/reference inputs are frozen before SDK execution.
Each node compiles once from captured Csc inputs in dependency order, with isolated
intermediates and one job's aggregate4096file/64MiB per file/256MiB input budget.
Compilation.Projects retains child provenance; VerifyInputsAsync(inputs, compilation)
and reuse recheck the entire closure and stage dependency bytes from the selected,
pinned artifact. Open never restores provenance. Graph staging drains with the job;
SDK BuildProjectReferences remains false. This opt-in extends the default
single-project profile; artifact/checkpoint/control schemas remain unchanged.

`GameBuildRequest.PackageDirectory` opts into local unsigned managed packages and
requires CapturePreparation. Supply lowercase `id.version.nupkg` archives and a
schema1 `packages.lock.json` beside each package-consuming project (only net10.0,
no RID). PackageReference must have unconditional literal Include/Version without
metadata; stable normalized three-part versions only, with resolved equal to the
literal version. Direct/transitive edges and ranges must agree with the nuspec;
archive SHA512 must equal the lock contentHash before SDK execution. No network,
ambient NuGet source/cache or package resolution participates.
RestorePackagesWithLockFile/RestoreLockedMode declarations are supported; after our
package admission, SDK restore uses rewritten references and its own lock under job work.

Archives must contain one managed lib target (net10.0/net9.0/net8.0/netstandard2.1/
netstandard2.0), flat IL-only DLLs, optional XML/PDB and ordinary package metadata.
One matching dependency group or an ungrouped list is supported. Signatures,
multiple lib targets, ref/runtime variants, build/analyzer/native/content/framework
assets, meta-packages and nuspec references/developmentDependency are rejected.
At most16 packages share the entire graph;
versions/hashes must agree and DLL names must be unique. Every private DLL belongs
to Dependencies (the flat artifact still admits at most15 plus entry). Archives
and expanded entries share the source4096file/64MiB per file/256MiB budget.
PackageReference becomes Reference only in owned mirrors; packages propagate through
the project graph. Full capture before/after compilation, prestart and reuse checks
include lock/archive hashes; captured DLLs drain with preparation. This bounded
profile does not implement general NuGet asset selection or signature validation.

### Process-local source watch descriptor

A successful frozen Build exposes `artifact.Inputs.SourceWatch`, an immutable
`GameBuildSourceWatchDescriptor`. Files retain original path/length/SHA256 for
source/import/config/HintPath/lock/nupkg inputs; SourceTrees contain only original
project directories; TreeFiles retain actual enumerated membership separately from
explicit inputs; Probes retain exact present and absent import/config/response
paths. The shared Policy.ExcludesEntry predicate and limits match recursive capture,
including case-sensitive excluded directory names. Explicit inputs under bin/obj
remain known files. Ancestors are exact probes, never recursive watch roots.

SourceFingerprint covers source bytes, file membership, probe presence and policy,
excluding tickets, staging, toolchain census and environment. Full input identity
and artifact schemas are unchanged. The descriptor owns no live resources and its
snapshot properties are excluded from diagnostic serialization. Open does not
restore it. A watch-only probe limit or unrepresentable probe leaves the Build
valid with a null descriptor and Inputs.Preparation.SourceWatchUnavailableReason.
This supplies observer data; compiler preflight remains required.

## External preview camera geometry

`TrueMoon.Aether.Preview.PreviewCamera` is an immutable external camera, separate
from authoring, World and game checkpoints. Default pose is (0,1,6), identity
orientation, pivot(0,1,0), distance6, vertical FOV pi/3, near0.1/far100, revision1.
WithView returns the same instance for an exact no-op; any changed camera/navigation
value advances the exact ulong revision. Invalid candidates and overflow do not
mutate the previous instance. Context epochs and publication remain caller-owned.

`TrueMoon.Aether.Rendering.PerspectiveProjection` shares rigid-pose validation and
RH row-vector Vulkan projection math with Ecs.Rendering.PerspectiveCamera.
`camera.CreateProjection(width,height)` freezes a CameraProjection with a usable
inverse and physical extent16..1920 by16..1080. TryProject accepts finite points in
front of the camera; TryUnproject uses NDC depth[0,1]. TryCreateRay uses a pixel
center, near origin, normalized world direction and bounded near-to-far distance.
Invalid points/inverses/segments are rejected, not clamped.

ViewportImageBounds maps already-normalized logical coordinates to physical pixels
using half-open image bounds, excluding letterbox bars and without another DPI factor.
Ray3.TryIntersectUnitCube preserves world distance with full affine scale/shear;
true+null means a valid miss, false means unavailable math. A near origin inside
the cube uses its exit surface. These are pure values/helpers, with no DI lifetime.

PreviewPickSnapshot.Capture(projectedScene, PreviewPickSnapshot.CubeGeometryProfile)
freezes the exact render payload, persistent GUID IDs, draw order and validated
inverses, up to64 entities. It does not reproject authoring or filter by Interaction.
Nearest Hit wins; exact ties retain draw order. PreviewPickResult retains its frozen
source and distinguishes Hit, Miss and Unavailable. Unknown complete geometry
profiles and unusable inverses disable the whole query, preserving occlusion policy.

The geometry result does not authorize Select/Clear. Explicit preview integration
now uses Control4/Frame2/Snapshot2, capability PreviewViewVersion1 and dedicated
PreviewViewCodec bytes. Both native counterparts must use the new matching binaries;
older session/frame versions are rejected. GPU synchronization stamp version2 stays
separate from the new required preview stamp Version1. CPU reference patterns stay
untagged/non-pickable; Frame2 can encode an explicit capture stamp without reusing tick.

ScenePreview.ShowViewAsync(scene, viewState, width, height, token) installs the initial
PreviewViewContext+PreviewCamera with the projected scene before frame readiness.
The context has caller-owned DocumentEpoch/ViewId/ContentId/SceneId/SceneRevision;
same-ID reopen needs a fresh opening epoch. QueueCamera(context,camera) retains one
in-flight control/presentation wait and one latest waiting value. CameraUpdates
awaits actual presentation; moves preserve the child, surface and tick. Show/Resize/
Stop cancel presentation readiness before draining accepted control. Suspension
retains latest desired until resume; errors retain cleanup ownership for Stop retry.

Presented(frame,imageBounds,clipBounds,layoutRevision) creates PreviewPresentedView
only after actual presentation ack, freezing its original camera/projection/extent,
pick source and UI geometry. IsCurrent checks desired camera/context/runtime/surface
without requiring latest FrameId or acknowledging again. The Editor UI owner uses
EditorSession.ReadPresentedView(view) to additionally check drawn layout/clip/visibility.
Retain only the immutable receipt, not borrowed GPU handles.

Use a single explicit preview owner alongside the shared FrameSession; do not race
game/legacy document commands against it. Legacy ShowAsync stays fixed/untagged.
The default GPU Editor now owns explicit opening/view/content context, retained
orbit/pan/dolly/F and shared selection intent admission. A custom external transport
can retain the legacy consumer. UI consumers may call SetPresentationSuspended(true)
when hidden/minimized: presentation waits are cancelled, receipts invalidated, latest
desired camera retained and actual ack denied without replacing the child/surface.
SetPresentationSuspended(false) resumes the bounded latest camera lane. The consumer
must also fence UI copies and drawn geometry/DPI; physical ResizeAsync(0,0) remains
the separate render-surface suspension API. Resume preserves a pending camera wait queued while hidden until actual presentation. Bounded Windows x64 source/published/fresh-package acceptance is complete; physical held-RMB/MMB and multi-monitor DPI remain separate hardware gates.

## Owned mesh render snapshots — ARCH1f.3

`UseAetherRenderResources` also registers a non-owning
`IFactory<RenderSnapshotView, RenderSnapshotViewRequest>` in
`TrueMoon.Aether.Rendering.Snapshots`. A session receives this factory through DI,
creates one caller-owned view for its resource binding, and disposes the view before
the binding/device/World. The App host retains failed products for cleanup retry.

After the binding is ready and Transform evaluation completes, call CaptureAsync
at an externally serialized World boundary. Configuration explicitly selects a
live entity camera or external rigid pose, physical extent and monotonic camera/
surface revisions. Hidden renderers are still validated. Frozen draw values contain
exact content keys, affine matrices/world bounds, linear per-instance color and
complete source/tick/resource stamps. Current/pending share one immutable allocation;
inspection data remain readable after release and grant no resource ownership.

TakeLatestAsync transfers the latest pending frame to the sole recording reservation.
SubmitAsync transfers it to an injected `IFactory<IRenderSnapshotSubmission,
RenderSnapshotData>`. The backend owner is tracked before native work, must be
exclusive, and must prove completion and safe cleanup. Coalescing/cancellation cannot
release accepted work early; unknown completion fences the view and retains owners
for retry. A zero-by-zero extent suspends publication; changed extent/camera requires
an advanced generation. Admission is bounded by4096draw ranges,2MiB per payload,
16MiB retained buffers,7envelopes and3accepted submissions.

The default Vulkan resource backend still provides mesh upload/readback. A built-in
mesh surface submission backend and Runtime Playing presentation fence follow in
ARCH1f.4. This API does not change the primitive renderer or existing runtime wire
formats. Source/published/fresh-package snapshot proofs use a deterministic memory
backend and do not claim new native pixels/device-loss acceptance.

## Unified startup settings

Register `AetherConfigurationSchemas.Engine` with a unified profile and call
`UseConfiguredAether(new AetherComposition { HostContractId = hostId,
GameBuildId = buildId, RuntimeFactory = runtimeFactory })`. Sources bind data-only
`AetherSettings`; identity, service factories and viewport ownership stay explicit.
Schemas and matching `UseConfiguredAether*` helpers also cover ECS World, Scheduler,
SceneLoading, Primitives, RenderResources and Authoring. All these settings are
startup-only; a changed reload requires a new App. Existing typed `UseAether*`
callbacks remain available.

The child entry captures `RuntimeBootstrap.ReadEnvironment()` before App and passes
it to `UseConfiguredAetherRuntime`. The protected launch envelope and parent/control
identity are separate from public sources. Public GPU validation uses frozen
`RuntimeStartupSettings`: canonical `TRUEMOON_AETHER_RUNTIME__GPUVALIDATION` overrides
legacy `TRUEMOON_GPU_VALIDATION` (including 1/0). `NativeRuntimeLaunch.WithStartupSettings`
passes flags in the owned child's environment without changing the parent process.
