# Cobalt baseline: graph v1

Permanent COB5 measurement suite for the current TrueMoon API, net10.0/Release.
It establishes a historical baseline before optimization. Provider comparison and
migration are COB7; this suite does not claim Cobalt is faster than Microsoft DI.

The graph has 12 caller registrations / 10 keys: one borrowed settings instance,
an owned factory singleton, open repository + exact closed string repository,
transient ticket/request, a Worker with IStartable/IStoppable Composite aliases,
and three ordered plugins (two singleton, one transient). App adds its standard
bootstrap/configuration/diagnostics/lifetime registrations. No network, files,
console logging, delays, scopes or actual external resources in the workload.
Transients are non-disposable so steady-state iterations do not grow the resolver's
owned-disposable collection. Constructor/property work belongs to this fixed graph.

| Case | Timed operation |
| --- | --- |
| ResolverLifecycle.BuildAndDisposeEmpty | Empty resolver build and synchronous disposal |
| ResolverLifecycle.BuildAndDisposeGraph | Register/build graph and synchronous disposal, no service activation |
| ResolverLifecycle.BuildResolveAndDisposeGraph | Register/build, first Request activation, then synchronous disposal |
| Resolution.Singleton | Warm concrete Worker singleton |
| Resolution.TransientRequest | New Request/ticket/plugin, shared warmed dependencies; materializes plugin array |
| Resolution.OpenGenericSingleton | Warm IRepository<int> from an open registration |
| Resolution.ClosedGenericSingleton | Warm exact IRepository<string> registration |
| Resolution.MaterializeMultiplePlugins | Resolve all plugins and copy to array |
| AppLifecycle.BuildAndDispose | App.Build and synchronous disposal |
| AppLifecycle.BuildStartStopDispose | App.Build, awaited Start/Stop and async disposal, plus one Worker lookup |

All times include the named cleanup. Do not subtract separate benchmark means to
claim isolated constructor or startup time. Input delegates/borrowed settings are
cached outside the timed operation. Each operation still executes registration
metadata validation, resolver selection, allocations and ownership as applicable.
Hot resolution uses one resolver per benchmark case, warmed in GlobalSetup and
disposed in GlobalCleanup. Results are returned to BenchmarkDotNet, collections
are fully materialized, and no manual amplification loop is used.

```powershell
pwsh -NoProfile -File Benchmarks/TrueMoon.Cobalt.Benchmarks/RunBaseline.ps1
```

The runner creates a fresh ignored `TestResults/CobaltBaseline/run-*`, builds
Release, verifies graph behavior (`--verify`), executes all 10 cases with Dry,
then measures a representative singleton with the default BDN job before the
remaining 9 cases. Dry timing is validation only. Final jobs use BDN's default
adaptive calibration/warmup/iterations and MemoryDiagnoser. BenchmarkDotNet is
pinned by Central Package Management after CLI resolution (initial version 0.15.8).
The existing user power plan is retained; no GC/JIT/affinity overrides are applied.
BDN may set the priority of its own child process and restores it on exit.

Finally, one fresh-process startup sample is discarded to prime executable/OS file
caches, followed by 12 measured fresh processes (configurable via `-ColdSamples`).
Each measures the first App.Build + Start from inside Main. This includes the
first use/JIT of the app path, but excludes process/CLR launch, module initializers,
Scenario/input creation, Stop and disposal. It is not machine-cold boot time.
Process-wide GC.GetTotalAllocatedBytes(precise: true) brackets the operation;
startup bytes may include runtime/background allocations. BDN's steady-state
allocation figures are reported separately. P95 uses nearest rank; with 12 samples
it equals the maximum and is exploratory, not an SLA estimate.

```powershell
dotnet run --project Benchmarks/TrueMoon.Cobalt.Benchmarks -c Release -- --verify
dotnet run --project Benchmarks/TrueMoon.Cobalt.Benchmarks -c Release --no-build -- --filter '*ResolutionBenchmarks.Singleton' --noOverwrite --artifacts FULL_RESULTS_PATH
```

Raw full JSON, CSV, GitHub Markdown, logs and a completed summary retain source/
binary/report SHA256, git HEAD, SDK/runtime/hardware, selected GC/JIT environment
settings, unchanged power plan, command exits and cold samples. The source checkout
may be dirty: file fingerprints identify the measured content. The runner fails
on any workload/build/benchmark error, missing case, source mutation or power-plan
change and terminates only its own process tree on timeout. Benchmark exceptions
also produce a nonzero exit from Program. Do not edit measured inputs or run builds/
tests/other benchmarks concurrently. Other desktop workloads remain uncontrolled.

Use the same graph, runtime, hardware, power plan and job for later comparisons;
keep confidence intervals and allocation columns. A result on this workstation
does not establish performance on other hardware/OS/SDK/RID/trimming/AOT modes.

## COB7 provider comparison

```powershell
pwsh -NoProfile -File Benchmarks/TrueMoon.Cobalt.Benchmarks/RunBaseline.ps1 -CompareProviders
pwsh -NoProfile -File Benchmarks/TrueMoon.Cobalt.Benchmarks/RunComparisonDelivery.ps1 -PackageRunDirectory FULL_PATH_TO_COMPLETED_CORE_PACKAGE_RUN
```

The comparison executes Scenario.Register unchanged through Cobalt or the existing
TrueMoon Microsoft DI adapter. Both App paths use App.Builder/Setup/Build (the COB5
helper used App.Build); do not interpret differences from the old report as speedups.
ProviderComparisonBenchmarks contains 10 operation pairs, grouped by category with
Microsoft as the baseline. Setup creates/warms both resolvers outside measurement.
Each pair measures the same graph/operation/cleanup; provider-specific alias disposal
cost is retained. No raw IServiceCollection benchmark replaces the TrueMoon adapter.
The ordinary runner still selects only its original 10 COB5 cases.

Comparison mode verifies 10 functional scenarios per provider, runs 20 Dry cases,
then 20 default calibrated measurements. Cold samples alternate provider order:
12 fresh processes per provider plus one discarded file-cache prime each. GC/JIT,
power plan and statistics follow the protocol above. Sources include both providers.

The delivery runner compiles the exact graph and verification sources into a fresh
PackageReference consumer, with its own cache and no framework ProjectReferences.
Generated mode runs both providers; analyzer opt-out mode runs Microsoft only.
Debug/Release build/publish produce 12 processes / 120 behavior scenarios. Actual
analyzer path/SHA, generated initializer and clean runtime payload are checked.
The provider verification intentionally asserts worker.Dispose=1 on Cobalt and=3
on Microsoft after resolving all Composite aliases. This is a documented semantic
difference, not a conformance failure. Error paths/cancellation/rollback/recovery
use additional registrations outside the timed graph.
