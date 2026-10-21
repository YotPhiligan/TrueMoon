# TrueMoon.Aether

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

The default native runtime factory expects an executable with the matching child
role/bootstrap handler, as provided by the repository's EngineSessionProbe example.
The registration extension does not generate an executable entry point.

The single Aether assembly contains `TrueMoon.Aether` and
`TrueMoon.Aether.Sessions`, `.Scenes`, `.Transport`, `.Frames`, `.Checkpoints`, `.Rendering` and `.Input`.
It uses TrueMoon Contracts/Enerit/Mithril without
depending on UI, Skia or a particular game DLL. Silk.NET.Vulkan 2.23.0 supports
the opt-in Windows x64 GPU path; native GPU startup remains explicit. The application supplies
Editor/Runtime entry points and its `IGameSession` implementation.

`Rendering.IRenderGameSession` is an optional observational capability. Capture it
through `RuntimeProtocol.CaptureRenderFrame` at a serialized simulation boundary,
then create `Gpu.Surfaces.GpuFrameData.FromScene` in the runtime frame provider.
The scene currently describes one cube, a perspective camera and an opaque RGB
material. Vulkan draws directly into the exported image with a slot-local depth
buffer; GLSL/SPIR-V are embedded, with no runtime shader compiler dependency.
The default reference pattern and CPU viewport remain available. Room/HUD,
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

Control wire is version2 (input fields/outcome); frame and TMCK checkpoint schemas
remain version1. Rebuild matching host/game artifacts; version1 control peers
are rejected, and old host fingerprints remain incompatible. The engine has no
UI dependency. The example owns a bounded30Hz sender, focus/pointer routing and
its drain before App/UI teardown; raw/locked OS mouse input is not supplied.

Game logic remains a separate assembly. CPU frame transport and session control
and explicit immutable game-build compilation/selection are implemented. Use
`host.Builds.BuildAsync(new GameBuildRequest(project, entryDll, entryType, revision))`
to compile a trusted local net10.0 library consuming the non-copying MSBuild
`AetherContractPath` reference. Then explicitly stop/reap the old session and start
the returned artifact through Supervisor or Frames. Build completion never replaces
the active game. Host options include BuildDirectory and BuildTimeout (maximum120s).
Private managed dependencies must be declared; native game dependencies are outside
this scope. Build failures retain diagnostics; App shutdown cancels/reaps the owned
compiler. Only the child loads game code, with a shared Aether contract assembly.

Optional `ICheckpointGameSession` supplies a minimal v1 tick/camera/interaction
snapshot. Pause then call `Supervisor.CaptureCheckpointAsync`; preflight
`checkpoint.ValidateFor(hostId, scene)` before stopping the old game. Start a fresh
session with `StartAsync(scene, artifact, checkpoint)` on Supervisor, or
`Frames.StartAsync(scene, artifact, checkpoint, width, height)` for first-present
readiness. The child handler must pass `SessionLaunch.RequireRestore` to
RuntimeProtocol; restore completes before surface/frame publication. Start without
a checkpoint explicitly resets from the document. Source build may differ; host,
scene and revision must match. TMCK v1 codec validates integrity and bounded data.
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
