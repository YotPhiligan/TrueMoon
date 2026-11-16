# ENG7b.3 launch acceptance

Windows x64, PowerShell 7.2+, SDK from `global.json`, and an existing Khronos
validation layer are required. Run from `src` with a fresh evidence directory:

```powershell
pwsh -NoProfile -File ManualTests/LaunchProfiles/Verify.ps1 `
  -ValidationDirectory TestResults/EditorWindow-20261104/tools/validation `
  -EvidenceDirectory TestResults/ENG7B3-new-run
```

`-SourceOnly` selects a development pass. The normal run executes source Debug,
published Release and fresh package-only Release profiles. All packages use the
exact persisted `Directory.Build.props` version. It never publishes to a remote feed.
Independent consumers use a closed local feed, an initially empty cache, shared
SDK/TFM/language/features snapshot, explicit CPM isolation and no ProjectReference.

The combined `LaunchProfiles` executable uses the public launch parser. It exercises
default/explicit Editor, local Player and validated internal Runtime child. On both
Cobalt and Microsoft DI it verifies:

- Editor with self-child, the actual combined example as child, and the standalone
  Runtime example. A real disposable project/document has one Undo and one Redo.
  Three Play/Pause/Step/Stop cycles include an owned-child crash and a fresh recovery.
  Each cycle checks actual presentation, reaping, zero resource/handle owners,
  retained window/selection, exact document bytes and unchanged authoring history.
  Normal child shutdown checks native validation and balanced surface/device counts.
- Forged host fingerprint and claimed runtime-entry identity sent directly to the
  actual child, bypassing the parent precheck; rejection must precede App resources.
- Standalone Player and Player inside the combined consumer, using explicitly
  selected artifacts and scene bytes. The shared Player probe checks five actual
  GPU readbacks, input, resize/suspend/resume, immutable scene capture, no loaded
  Editor/Tooling, and zero resources after close.

The unmodified entry points of the actual `AetherHost` and `AetherPlayer` examples
also run: default/explicit Editor and combined/separate Player create their native
windows and exit cleanly after a close directed to their owned process. These are
startup/close checks; the instrumented consumers above provide behavioral evidence.
`PlayerProbe/VerifyLaunch.ps1` additionally checks CLI outcomes and rejection causes.

Finally, the fresh Players run on a copied .NET runtime with no SDK and a restricted
PATH. The Editor profile intentionally opens a source project and therefore keeps
the SDK available. Artifact preparation occurs in its own process before normal
launch; no game assembly is loaded into the Editor CLR, and no game compilation
occurs during these selected-artifact cycles.

Every process has a deadline, captured output and owned-tree cleanup. Evidence is
read after child exit; immutable package hashes, native/font payloads, shared binary
closure and source/build-profile hashes are checked. Preserve failed run directories.
Do not run SDK commands concurrently with the full Aether regression suite.

This is bounded Windows x64 launch/delivery acceptance. Device loss, physical input/
DPI, other OS/RID, performance, subsystem checkpoint/native-state migration and general
release readiness remain separate. Kton/Kul are not enabled by this fixture.
