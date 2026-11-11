# Configuration delivery acceptance — CORE7.6 / CORE8

From `src`, run `./ManualTests/ConfigurationDelivery/Verify.ps1` after building the
checkout in Debug and Release. Optional `-EvidenceDirectory` and `-RunDirectory`
must point to fresh directories; run files belong outside the checkout. The
runner does not build repository dependencies or use live package feeds.

`ManualTests/BuildProfile.ps1` evaluates the checkout's SDK, `TF`/`TestsTF`, package
version, language version and compiler features through MSBuild. Each independent
consumer receives a self-contained snapshot of that profile, an exact SDK pin,
and explicit CPM isolation. Optional `-PackageVersion` selects a unique prerelease derived from the current
framework version. Profile inputs and their hashes are included in the report.

The current profile uses SDK 11.0.100, net11.0 and runtime-async=on. It verifies configuration and child
bootstrap/lifecycle; it does not compile or activate a game, create a renderer,
or replace the separate full test/native acceptance stages.

One unique local version is packed for16TrueMoon packages. Exact existing external
nupkg are copied from restored assets into the local feed. Each consumer has its
own new cache. Source Debug, published Release and fresh-package Release run from
an unrelated cwd. Fresh graphs require0ProjectReference, one TrueMoon version,
local-feed provenance and installed/deployed runtime DLL hash equality.

| Consumer | Checks/profile | Actual scope |
| --- | --- | --- |
| Existing ConfigurationProfileConsumer |15 | Types, priorities, collisions, validation/copies and source errors |
| Existing ConfigurationAppConsumer |108 | Both DI, App stage/reader/reload/writer/cleanup,2real owned Mithril children |
| Existing ConfigurationModulesConsumer |76 | Both DI, actual World/Nom admission, Editor startup, factories and lazy window binding |
| New BootstrapConsumer |30 | Both DI,2actual child processes; selected pre-App env capture, protected Aether identity/report/delivery/flags, ordinary source spoof rejection, Core/Mithril unit identity and graceful stop/reap |
| Guide example, extracted unchanged from CONFIGURATION_GUIDE.md |1 | Actual App start/stop and configured name from canonical CLI, in all three delivery forms |

Each profile verifies230checks across the five programs; all15program/profile
combinations run. There are4owned children per source/published/fresh profile,
12total. App consumer reports its assertions and ready/stopped process artifacts;
bootstrap consumer additionally records PIDs/reaped counts. Consumer timeouts kill
the owned process tree and retain logs. App owners perform normal cleanup in finally.
No window, GPU, game or native runtime is activated. The bootstrap child builds
and disposes Aether's configured runtime graph without starting it, then runs a
separate Core/Mithril App to exercise its owned parent/control lifecycle.

`verification.json` preserves every command/exit, unique binlogs, source/package/
external dependency hashes, graph/cache provenance and all profile reports. Failed
attempts remain independent; a rerun uses fresh directories/version. Scratch feed,
cache and outputs are retained for diagnosis; versioned Core HISTORY preserves
scope and results if ignored/TEMP files are later cleaned. Package payload hashes
cover the current shared README where included (Nom explicitly excludes it), license/icon, module README, Cobalt/Enerit analyzer
DLLs/targets and bundled native Skia/targets. Source drift checks include these
shared files and build imports, in addition to runtime code and consumer sources.

`Export-Keys.ps1 -VerificationManifest <completed-verification.json> -OutputPath
<catalog.md>` reads only public frozen schema metadata from the verified fresh
module assemblies. It generates a17schema/139key catalog and JSON evidence without
App, provider IO or resource activation. Entry-dependent App:Name defaults are
represented symbolically; sensitive defaults are masked. The canonical reviewed
catalog is `docs/core/CONFIGURATION_KEYS.md`.

The API design example verifier supports `-OutputDirectory <fresh-directory>`.
It compiles the declarations/headless/Mithril/Editor blocks against actual
assemblies and does not execute them. Test suite runs and solution builds are
separate evidence; avoid any concurrent SDK captures during full Aether suites.
The legacy CLI-tool restore caveat, bounded test props override and current full
suite outcomes are recorded in `docs/core/HISTORY.md`, independently of this runner.

CORE8.5 combines this manifest with independent game and native viewport
consumers in `ManualTests/FrameworkDelivery/Verify.ps1`. The shared version/feed
is reused without repacking. Earlier CORE7 restore caveats are historical:
CORE8.1/3 removed the legacy CLI-tool/CPM/SDK-selector blockers, and CORE8.4
ran all 11 suites with ordinary restore/build and no test-only props override.
Fresh delivery and final runtime results have their own dated Core HISTORY entry.
