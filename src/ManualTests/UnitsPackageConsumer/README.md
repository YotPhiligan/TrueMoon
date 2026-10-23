# Independent Enerit / Mithril package consumer

The runner packages the nine-project App/DI/IPC/units dependency graph into eight
packages in a unique
local prerelease feed, then creates fresh consumer directories using only
PackageReference. It copies the agreed Program.cs/Contract.cs from AppUnitsPipesProbe
as application source, not as Compile links or ProjectReference. All app builds and
process runs subsequently use only the copied files and restored packages.

From src with PowerShell7/.NET10 SDK:

```powershell
pwsh -NoProfile -File ManualTests/UnitsPackageConsumer/RunPackageSmoke.ps1
```

Use a fresh absolute RunDirectory under TestResults/UnitsPackages if specified.
Version defaults to a unique0.2.2.7-alpha.units timestamp; explicit versions must
be prerelease. Outputs are ignored, previous evidence preserved. Packages remain
local and are not published. Every launched build/process has a deadline and only
its own process tree is killed/reaped on failure.

Expected matrix:11scenarios ×Debug/Release ×build/publish ×(Cobalt and Microsoft DI
with both generators, plus Microsoft DI without Cobalt analyzer) =132cases.
Scenarios:payload,32concurrent,local cancellation,remote error,child crash/reconnect,
restart budget exhaustion,Stop with pending RPC,startup rollback,parent abrupt exit,
saturated-stop and saturated-kill through the independent process control endpoint.
The final summary requires all132cases and a missing Enerit analyzer compile failure.

Apphost runs parent and selected own child from the delivered output. Framework-
dependent publish has no RID. The complete output includes apphost,assembly,deps,
runtimeconfig and dependencies. Both endpoints use the same packaged runtime/generator
and service sources. Child generations/exit codes/typed checks, App lifetime, pending
map/workers/controller cleanup are checked; killed parent client cleanup remains
unobservable, reported false/Pending=-1. The Windows x64/net10.0 profile includes
cooperative drain and owned kill/reap of an uncooperative child. Kill does not
prove managed finally execution. No cross-version/MMF/long resource trend or
general in-process uncooperative shutdown guarantee follows.

The consumer has its own Directory.Build.props/targets/Directory.Packages.props
boundary and NuGet.Config source mapping. TrueMoon packages must come from the local
feed in an isolated cache and all have the same version. Existing global cache is
only a fallback for third-party dependencies. No project libraries/UI dependencies
are accepted. Analyzer paths and SHA256 must match the extracted packages; generated
client/handler/DTO/initializer sources and optional Cobalt registrations are recorded.
Analyzer-only packages have no lib payload; runtime outputs exclude analyzers/Roslyn.

AnalyzerEvidence.cs requires concrete generated types at compile time without
constructing adapters. Omitting TrueMoon.Enerit.Generator must give CS0234/CS0246.
An explicit PrivateAssets="all" reference is required only for Enerit.Generator.
Cobalt's generator is bundled in TrueMoon.Cobalt and attaches through the
transitive TrueMoon package dependency. There is no separate Cobalt generator
package. Microsoft DI is also checked with EnableCobaltGenerator=false.
UseDiagnostics and explicit local
Composite registrations are retained from the source fixture.

Source fingerprints,package hashes/metadata,library provenance,compiler analyzer
paths/generated-source hashes,commands and132result records are saved in summary.json.
[Acceptance owner](../../docs/mithril/PLAN.md), [status](../../docs/mithril/STATUS.md).
ENE5 performance baseline and MIT5 operational diagnostics remain separate tasks;
this runner does not measure latency/throughput/allocations.
