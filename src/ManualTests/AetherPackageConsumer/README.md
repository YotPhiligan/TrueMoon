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
fingerprints across both fresh compiles and confirms staging was released before Start.
The bounded C# capture profile copies source/generated/reference/config/compiler inputs
after trusted MSBuild preparation; installed dotnet runtime/environment and arbitrary
preparation task/generator IO remain trusted. Undo after Stop
restores the saved scene before the remaining crash/recovery workflow. Each full
workflow owns eleven children, with ten graceful validation reports including two
game runs; the intentionally killed preview has no final report. The workflow then
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
`-Scenario` for bounded subprofiles; source-only profiles record framework changes
since the package baseline, whereas package profiles require matching fresh inputs.
