# TrueMoon.Aether

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
example is `ManualTests/AetherRuntimeHost`, without Editor/UI dependencies.

`BuildIdentity.ForRuntime(runtimeDll)` fingerprints role-labelled host/runtime
managed entries plus exact shared Aether/Contracts/Enerit/Mithril binaries. It
checks the shared binaries in both entry directories; parent launch and child
bootstrap also verify their actual managed entry. Native counterpart identity
Version1 is carried in `RuntimeLaunchIdentity`; bootstrap JSON is bounded to
16384 characters and depth32. Control3/frame1/snapshot2 and session/nonce/generation
fencing are unchanged.

This is a trusted-local compatibility check, not an authentication boundary or a
complete native/private-dependency hash. Do not mutate either deployment in place
during a session. A different pair identity invalidates previous game artifacts
and checkpoints: rebuild games for the new pair. Scene/project formats do not
change. ARCH2d asset delivery and ARCH2e independent NuGet/published acceptance
remain open; source process checks do not establish that matrix.

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
