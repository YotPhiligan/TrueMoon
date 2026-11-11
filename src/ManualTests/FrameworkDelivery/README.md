# Framework delivery — CORE8.5

Run from `src` with PowerShell 7.2+ on Windows x64 and the SDK selected by
`global.json`. Supply an existing Khronos validation layer directory; the runner
does not install a layer or change driver/OS settings.

```powershell
./ManualTests/FrameworkDelivery/Verify.ps1 -ValidationDirectory <existing-layer-directory>
```

Optional `-RunDirectory` and `-EvidenceDirectory` must be new directories.
RunDirectory must be outside the checkout. The defaults retain scratch files in
TEMP and reports in `TestResults/FrameworkDelivery-*`.

The runner builds the solution in Debug and Release, then creates one unique
local version derived from the shared framework version. It packs all 16 runtime,
adapter, UI, Editor, Nom and generator packages, verifies matching dependency
versions and payload hashes, and copies exact restored external nupkg into the
local feed. It does not publish packages to a remote service.

The same package bytes are used by these sequential stages:

| Stage | Scope |
| --- | --- |
| Example hosts | Source Debug / published Release, DLL / apphost, actual runtime children, bootstrap rejection, isolation, fault/cleanup and resource checks |
| Configuration delivery | Five consumers in source Debug, published Release and fresh-package Release; 230 checks and four owned children per delivery form; Cobalt and Microsoft DI |
| Independent Aether package consumer | Debug build / Release publish, DLL / apphost, Cobalt / Microsoft DI: eight cases with actual game compilation, child loading, replacement, stop/reap and parent isolation |
| Fresh native viewport | Independent package-only host, Debug build / Release publish, actual GPU pixels and validation, state and child/resource cleanup |
| Final runtime regression | All 11 tracked test projects in both Debug and Release, ordinary restore/build, xUnit 2 / VSTest, sequential process-heavy fixtures |

Each independent consumer has explicit SDK/TFM/language/features snapshots,
CPM isolation, a new cache, no ProjectReference and local TrueMoon package
provenance. Aether reuses the completed configuration delivery manifest through
`-PackageVerification`; it checks and copies packages rather than repacking them.
Viewport uses its own fresh cache and the same feed. Framework assemblies and
native/analyzer/buildTransitive payloads are checked against package contents.

`verification.json` records commands, exits, source/package hashes, referenced
stage manifests and TRX paths. Every MSBuild command saves a unique binlog.
Changed source inputs, missing tests, failed/skipped tests and incomplete stages
reject completion. Failed runs are retained; retry with new directories/version.
Versioned Core HISTORY records the actual outcome and limitations independently
of TEMP/ignored artifact retention.

This is bounded Windows x64 framework-dependent acceptance. Physical input,
DPI/multimonitor, device loss/K7/VRAM, other OS/RID, AOT/trimming, performance,
storage power-loss and the causes of earlier reliability timeouts remain separate.
See [the migration guide](../../docs/core/DOTNET11_MIGRATION.md).
