# Aether independent NuGet consumer

Windows x64, .NET 10 SDK, PowerShell 7. This is the headless ARCH2e.1/2 profile;
it opens no window and creates no graphics device.

```powershell
./ManualTests/AetherPackageConsumer/VerifyPackages.ps1
```

The runner packs fifteen fresh framework packages with a unique prerelease
version, then copies the `Consumer` sources into a new temporary solution outside
the checkout. Restore uses a private feed/cache and source mapping. Consumer
projects have no repository ProjectReference or linked source. The game and
font/OFL are copied explicitly at materialization; execution never searches
ManualTests or the source checkout. The combined solution is not added to the
framework solution.

The matrix covers Debug build and Release publish, Runtime DLL/apphost commands,
and Cobalt/Microsoft DI in the parent applications. The runtime child uses Cobalt.
It checks generated Cobalt constructors and a consumer-owned Enerit contract,
Editor registration, independent parent-owned Views with borrowed models, shared
scene save/read/conflict handling, and selected-game Build/Start/checkpoint/
Replace/Stop/restart. CPU frame pixels must come from the selected build. Child
diagnostics and stdout/stderr capture, bootstrap/fingerprint rejection and owned
process/resource drain are checked separately from native payload delivery.

The compiler builds against the installed Aether/Contracts DLLs supplied through
`AetherContractPath` / `TrueMoonContractsPath`. It does not rebuild framework
projects. A plain game build uses PackageReference instead.

Framework package metadata, analyzer/native payloads, restored graph/source,
loaded framework DLLs, embedded shaders, fonts and deployment hashes are checked.
Every SDK invocation saves a unique binlog. `verification.json` and command logs
are saved under `TestResults/ARCH2e-*`; it points to the external solution, feed,
cache and per-case reports. All directories are preserved, including failures.
Explicit `-RunDirectory` and `-EvidenceDirectory` must designate fresh paths.

GUI/font/native loading, Vulkan validation, Editor preview/document workflows,
device/driver loss and resource trends belong to ARCH2e.3 and the UI plan. No
NuGet publication is performed.

## Numeric native acceptance (ED4g.2a)

```powershell
./ManualTests/AetherPackageConsumer/VerifyNumeric.ps1
./ManualTests/AetherPackageConsumer/VerifyNumeric.ps1 -HostKind Embedded
./ManualTests/AetherPackageConsumer/VerifyNumeric.ps1 -InputMode WindowsPointer
```

Windows x64/OpenGL, source Debug and published Release, standard/Cobalt or
embedded/Microsoft DI. Each run creates a disposable project in a fresh evidence
directory. This focused source/publish runner requires no package proof or Vulkan
validation layer and launches no runtime child. It retains SDK binlogs, native
readback PNGs, input-source labels, assembly paths and final cleanup counters.

The default `Routed` mode supplies `UiSession.HandleInput` events to the native
host: arrows/Shift/Ctrl, invalid input, exact three-axis draft restoration during
a captured drag, release without auto Apply, explicit Apply and one Undo entry.
`WindowsPointer` waits for actual window input, reporting the next action in
`stage.json`: enable Scale link, drag X right from its prefix and release, Esc,
drag again, Ctrl+Enter, click Undo. Use current window geometry rather than fixed
screen coordinates. The runner verifies values/history and captures each stage;
it never supplies these Windows-phase actions itself. Close the clean window to
abort; incomplete stages fail rather than producing a passing report.

`WindowsArrows` additionally requires Position X: Up, Shift+Up, Ctrl+Down and Esc
before the linked-scale actions. It remains an acceptance mode for a physical
keyboard or a compatible input driver. The 2026-11-05 automation delivered arrows
as `UiKey.None`; this mode was not marked passed. Esc while a mouse button remains
held and pointer modifiers also require separate physical acceptance; the routed
and unit results do not imply those gates passed.

## Native Editor acceptance (ARCH2e.3)

```powershell
./ManualTests/AetherPackageConsumer/VerifyGui.ps1 `
  -PackageVerification ./TestResults/your-package-run/verification.json `
  -ValidationDirectory ./path/to/extracted/validation
```

Supply a completed fresh `VerifyPackages.ps1` manifest and the directory containing
`VkLayer_khronos_validation.json` / `.dll`. The runner verifies immutable package
hashes and current framework inputs, restores a new independent GUI/runtime
solution into a separate cache, and builds source Debug/published Release and
NuGet Debug/published Release profiles. It sets validation paths only in the
owned process environments, preserves live output and command/PID/source hashes,
and runs windows sequentially with deadlines.

Each profile tests the standard Editor window on Cobalt and a parent-owned
embedded Editor View on the Microsoft DI adapter. The embedded consumer supplies
its own public GPU transport factory, initializes its borrowed GL context before
preview commands, defines first-presentation readiness, and detaches/disposes/
recreates the View without disposing its shared model.

The actual window workflow opens a scene, checks invalid drafts and retained theme
updates, edits Transform through UI input, verifies GPU pixels for preview/Undo/
Redo, saves/reopens, vetoes dirty close with Cancel and an injected save failure,
kicks off inline F2 name editing and Ctrl+Down sibling reordering through the retained
hierarchy, checks their shared Undo/Redo and persistent metadata after another Save/reopen,
creates a cube and child from New, duplicates/deletes/restores the branch through context
actions and keyboard, searches by ID and reopens the created scene with clean history,
opens the project path prompt, cancels a dirty switch, saves an untitled scene before
switching to another manifest/game path, verifies context/selection reset, closes and
reopens the project and persisted scene (the embedded host uses the same model actions),
and reaps an active preview before project publication,
then builds consumer-owned Game.Logic from an unsaved scene and runs two fresh
game children through Play/Pause/Step/Stop. It checks actual scene GPU pixels,
preview reap before game startup, distinct selected artifact IDs, child-only game
assembly paths and unchanged authoring dirty/history/selection. It records the
verified input fingerprints and requires identical inputs across both fresh builds;
the compile tickets and artifact IDs remain distinct. The tracked scene profile is
one net10.0 SDK project with HintPath references, without PackageReference/ProjectReference.
It also records captured compiler fingerprints/file counts/bytes, requires stable
fingerprints across both tickets and confirms staging was released before Start.
The first Scene Start compiles; the second verifies and reuses its managed payload.
Each job records reuse.json; the reused job has no prepare.json or build stdout,
while before/after/prestart observations and a distinct revision/manifest remain required.
The bounded C# capture profile copies source/generated/reference/config/compiler inputs.
Scene Start additionally freezes the supported declarative graph before preparation,
pins installed Windows SDK/runtime files with read leases and uses a private process
environment. Custom tasks/functions/SDKs, external analyzers/globs and package/project
graphs fail closed; installed toolchain directories/native OS remain trusted.
The workflow requires CapturePreparation, matching before/after/prestart preparation
fingerprints across both fresh builds and absence of preparation staging before Start.
It records six freeze samples with source/toolchain durations and byte/file counts,
two complete Start latencies and parent handle counts. The runner samples process
wall/CPU time, working set and handle peaks; timings do not enter build fingerprints.
Build/Start waits for its admitted operation under compiler and case deadlines;
the general 30-second UI-state polling deadline remains unchanged. Undo after Stop
restores the saved scene before the remaining crash/recovery workflow. Each full
workflow owns fourteen children, with thirteen graceful validation reports including
  five game runs. Its version3 project declares Game.Shared.dll from a sibling
  ProjectReference library and Game.Packaged.dll from that library's locked local
  unsigned PackageReference. The fixture emits IL into a local nupkg without loading
  it into the parent. Every graceful Game report must load both private DLLs from
  the selected artifact; the parent Editor must never load them. Root capture
  metadata must contain the child compilation and package DLL, and before/after/
  prestart preparation must include the lock/archive hashes. The second Start reuses
  the whole verified payload; the same package participates in Replace/rollback.
  Document-aware Replace checks a paused code swap with exact tick
preservation, a compiler failure leaving the old world alive, and candidate restore
rejection rolling back old code/state/Playing mode. It captures replaced/recovered
paused frames and preserves the same authoring history. The intentionally killed
preview has no final report. The workflow then
kills only its own preview child, preserves dirty document/history/selection,
and recovers preview before saving/closing. Separate native startup failure and
first-frame cancellation cases check rollback and final App disposal. Original
startup errors may remain observable from Stop/Dispose; only the exact injected
failure is accepted, and success is recorded after disposal.

Reports include loaded extended Skia/font hashes, screenshots, graceful producer
validation and surface/slot/device/handle teardown, plus actual child reap and
UI session counts. Deliberately killed children cannot supply a final validation
report. These directed inputs do not replace physical OS input/DPI/multi-monitor,
K7 stress, device loss or VRAM acceptance. Use `-Profile`, `-HostKind` and
  `-Scenario` for bounded subprofiles. `-Scenario ReplaceInterrupt` runs nine cases
  per host: Cancel/Stop/exit during the dependency SDK query, graph input verification,
  and native checkpoint restore after old-child reap. The query log's creation time
  must belong to the observed live compiler PID, with its output still open. Verification
  additionally compares staged Game.Shared.dll with the selected candidate payload.
  Startup capture must prove the private project graph; all compiler staging must
  be removed before admission reopens. Restore uses a bounded barrier inside the consumer
  game, with real RPC, runtime and viewport. Cancel retains the old game before
  the transaction or recovers old code/state/Playing mode after candidate admission.
  Stop/exit forbid recovery; document admission stays closed until actual drain.
  All children must supply graceful validation reports and release owned resources.
  This lane is separate from the full Workflow and startup Failure/Cancel cases.
  Source-only profiles record framework changes
since the package baseline, whereas package profiles require matching fresh inputs.

`-Scenario PackageAcceptance` combines the nine ReplaceInterrupt cases with four
`PackageInputs` cases per host (Start/Replace × stale/corrupt archive). Interrupts
also prove the locked archive SHA512 and selected Game.Packaged.dll SHA256, including
the captured copies used by the live dependency/root SDK query. Restore observes
the candidate's published package after preparation staging has drained.

PackageInputs uses UI scene capture/Build, then the public GameEditor StartBuildAsync
or ReplaceBuildAsync API, without a production hook. It changes only package inputs
after candidate selection: stale regenerates a valid DLL with the same behavior and
updates its lock; corrupt changes the archive while retaining the old lock. Rejection
must preserve the original stopped/playing runtime and child count, document dirty/
history/selection, and leave no checkpoint marker. Corruption must fail before SDK.
The marker is inside the consumer game; successful fresh Replace must activate it,
so its absence during failed preflight is meaningful. A new Build consumes the
changed DLL, rejects stale payload reuse, and must successfully start/replace a real
native Game. The final child reports prove both private DLLs came from its selected
artifact. These cases test the engine preflight API; document-operation admission
is exercised separately by the ScenePlayback interrupt lane. No claim about direct
document gates is inferred from a raw GameEditor call.

## ED4f.3 Window root

`VerifyGui.ps1 -Scenario WindowRoot` сохраняет strict runtime validation и прежний
full document/preview/game workflow. Standard/Cobalt использует настоящий
EditorMainWindow, copied custom Title/1360x840 и chained header callback с EditorView;
проверяет native/header clean+dirty captions, одну root/view/header identity,
retained source owners и три component close requests (Cancel, failed Save, clean).
Embedded/MS DI сохраняет старый EditorView/parent path без Window products.
SourceDebug/SourceRelease и свежий PackageRelease запускаются отдельно; metadata
failure теперь ожидается до native allocation, cancellation — после него.

## ED5a.4 fresh-package viewport

`VerifyViewport.ps1` consumes a completed fresh `VerifyPackages.ps1` manifest. It
checks package/framework source SHA, creates an independent external application
host with PackageReference only, private feed/cache and copied font/license, then
builds Debug and publishes Release. No framework ProjectReference/source fallback.
It creates disposable two-cube/Other/Bad fixtures, runs13group native GPU lifecycle
in both configurations, checks17PNG/profile,9children=reaped,8GPU validation reports+
1explicit pre-GPU failure, zero owned resources, and installed/output DLL hashes.

```powershell
./ManualTests/AetherPackageConsumer/VerifyViewport.ps1 `
  -PackageVerification TestResults/fresh-packages/verification.json `
  -ValidationDirectory E:/tools/validation `
  -EvidenceDirectory TestResults/fresh-viewport
```

All run/evidence paths must be fresh; ValidationDirectory must contain the Vulkan
layer manifest. The application-only sources are copied from ManualTests; framework
code comes from the fresh packages. This is directed UiSession/native GPU input,
with separate Windows input evidence in the source/published host. Current15package,
8headless and2GPU profile results/limits are in
[HISTORY](../../docs/engine/HISTORY.md#ed5a4-acceptance-2026-11-08).

## ED5b fresh-package translation

Use `VerifyViewport.ps1 -Profile translation` with the same fresh-package manifest,
validation and fresh evidence arguments. It builds Debug/publishes Release in an
independent PackageReference-only host, runs --smoke-translation in both, checks
five translation captures/four reaped children per profile and zero resources/
validation, and verifies installed/output DLL hashes. The default profile remains
viewport. Optional `-DependencyFeed path` substitutes an offline hierarchical feed
for third-party dependencies; TrueMoon IDs remain mapped only to the fresh private
feed. [Current commands and scope](../../docs/engine/HISTORY.md#ed5b-translation-2026-11-09).
