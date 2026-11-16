# PHYS8.1 workloads / PHYS8.2–3 allocation attribution

```powershell
pwsh -NoProfile -File ManualTests/KtonPerformance/Verify.ps1 -EvidenceDirectory <new-directory>
```

The runner inherits SDK/TFM/language/features through `ManualTests/BuildProfile.ps1`
and explicit CPM isolation. The probe is not shipped in product packages and builds
the persisted repository inputs/version. Source and fresh package-only Debug run a
12-case smoke matrix. Published source Release and fresh published Release each
run28 workloads: both Cobalt and Microsoft DI, standalone and full Aether session,
100/1000 active boxes,100/1000 sleeping boxes,1/32/256 walking capsule controllers.
The exact14-package closed feed and fresh output/cache DLL hashes are verified.

Each fixture has its own App and caller-/scene-owned world. Rigids are separated,
gravity-free and moving at1m/s or explicitly asleep; they intentionally produce no
contacts. Characters move at2m/s on a single large flat static floor with gravity.
They validate support/contact event production. All worlds use Baseline60, one
worker,8iterations/4substeps and current default admission limits. No production
ceiling is raised to fit the benchmark. The256-character ECS fixture explicitly
selects the existing allowed256-event bridge projection capacity; other ECS fixtures
keep the default64. This per-fixture setting is recorded and does not alter production
defaults.10000bodies and2/4/8workers remain later profiles.

After120 warmup ticks,600 samples measure the public Kton Step and enclosing tick
with preallocated buffers. The inner Step includes validation, collision/solver,
event publication and worker join; full Aether time includes session/scheduler,
physics reconciliation and ECS pose/support writes. ECS entities have no drawable
components; renderer capacity/extraction is outside the physical workload. The
session still performs its normal empty render-snapshot bookkeeping. Standalone full time includes
the probe decorator/delegate. Timers have a small observer cost; no overhead is
subtracted. Percentiles use nearest rank. Setup, input, validation, sorting,
serialization and teardown are outside sample windows. No collections are forced.
Debug smoke timings and Release baselines must be considered separately.

The report records p50/p95/p99/max/mean, primary-thread and process-wide allocations,
GC collection counts and approximate managed heap before/after. Pool memory is the
factory's sampled high-water counter for main/dispatcher BEPU pools in one world;
it does not represent all native memory or process RSS. Contact totals count
published gameplay events, **not raw callback invocations**. World/scene teardown
is timed separately; App Stop must report zero published worlds, active owners
and remaining pool bytes. State/count/active partitions and sample/tick accounting
are checked before reporting success.

CPU model, runtime/architecture/SIMD, stopwatch frequency, solver/CCD settings and
source/package/report hashes accompany evidence. These are local-machine baseline
observations, not a performance SLA or cross-platform/release acceptance. A p99
above the configured diagnostic1ms budget is recorded without failing or changing
that budget. Raw callback/stall diagnostics, capacity/worker expansion, visual
pose/interpolation proofs and other platforms remain separate PHYS8 work.

PHYS8.2 additionally runs `after-attribution` from the fresh Release consumer.
This12-case matrix uses1000active/sleeping bodies and256controllers, both DI and
paths. Allocation-only marker systems bracket physics, builtin movement, movement
phase playback, transforms and extraction. Native Step has its own primary-thread
counter. Inclusive/exclusive values are reported explicitly; primary-thread stage
bytes are not confused with all-thread totals. No component access or mutation is
added by markers. Marker timings are not comparable to the uninstrumented baseline.

To compare exactly the same new probe with old production DLLs, pass
`-ComparisonReport <earlier-verification.json>` from an immutable local package
delivery, e.g. the PHYS8.1 verification report while its archives are retained.
The runner verifies the old archives' SHA, matches SDK/TFM/features, restores the
earlier exact cohort in a second fresh cache, validates its package-only graph and
output hashes, and runs `before-attribution`. Neither archive cohort is overwritten.
The versioned physics manifests preserve observations if temporary archives are
later cleaned. The extra allocation markers do not declare a timing SLA.

PHYS8.3 also captures the shared Runtime/Ecs source hashes because contact recording
and phase playback use the general command buffer. Workloads/marker boundaries are
unchanged; pass the completed PHYS8.2 verification report to compare immutable
`.3-alpha` packages with the current repository cohort using the same probe.
