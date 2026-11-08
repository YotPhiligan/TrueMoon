# Model importer candidate research

Bounded headless Windows x64/net10.0 comparison for ARCH1g.0. This is a research
consumer, not a production importer, public App module, or release acceptance suite.
The selected g.1 candidate is AssimpNetter6.0.5 low-level;
[decision and measured scope](../../docs/engine/MODEL_IMPORT_RESEARCH.md#decision-2026-11-07).

```powershell
& ./ManualTests/ModelImportResearch/Verify.ps1 -EvidenceDirectory TestResults/MyFreshModelImportRun
```

The runner first looks for the four official archives retained by the initial
investigation in TestResults/AssimpResearch-20261107, or the optional ArchiveDirectory.
Missing archives are fetched from the official NuGet flat-container into this run's
temporary feed. Versions and SHA256 are pinned in Verify.ps1 and
[the research document](../../docs/engine/MODEL_IMPORT_RESEARCH.md); mismatches fail.
It works without the initial ignored research directory, given network access and
the solution's cached third-party dependencies.

Each candidate gets a new project/cache under OS TEMP and runs in its own process.
The four variants are AssimpNetter6.0.5 high-level, the same package low-level,
Silk.NET.Assimp2.23.0+Ultz.Native.Assimp6.0.2, and SharpGLTF.Core1.0.7. Candidate
packages come from a new offline feed, cached third-party dependencies are read
through a fallback folder. Aether is a source ProjectReference for real CPU validation
and canonical encoding. No project/package references or versions in the checkout
are changed. This is not a fresh Aether NuGet package proof.

Source Debug and framework-dependent published Release each run:

- Literal generated GLB and glTF+external BIN with two material assignments,
  two nodes sharing both primitives, and nonzero parent/child translations.
- OBJ+MTL and a minimal ASCII FBX for both native providers. SharpGLTF explicitly
  skips these unsupported formats. The FBX fixture is created here, not an upstream asset.
- A grid with16384vertices/96774indices: complete triangle set/winding, bounds and
  cardinality are checked independently of vertex/index reordering.
- Broken JSON, missing BIN and truncated GLB must fail.
- Eleven imports of each supported positive fixture produce identical per-provider
  fingerprints. MeshDefinition/RenderContentCodec perform the actual CPU validation.
- Seven warmed import+CPU-projection observations of the grid, managed allocation
  deltas and whole-process peak working set. These include projection/canonical
  encoding; they are not parser-only timings, native allocation peaks or a performance SLA.

Version exports and compile flags are queried from the already activated native
module, using the unsigned-int/no-argument C ABI in assimp/version.h. An extra loader
reference is released after querying. The active native path must be output-local,
single precision, have the expected version and match the SHA of its archive entry.
Native scenes remain scoped until all detached data is copied; the low-level paths
release them in finally. The binding library is closed only after the final observation.
LiveImports counts our tracked ownership, not all allocator state inside a native DLL.

Trusted generated fixtures are used. Default candidate file I/O is intentionally
being compared. Material assignment is checked, full material/color conversion,
PBR/textures, animation and production stable subasset discovery are not implemented.
Every command retains logs and MSBuild operations get unique binlogs.
Old/failing evidence directories are preserved; use a fresh directory for each run.

The selected low-level candidate also has a separate captured-import mode:

```powershell
& ./ManualTests/ModelImportResearch/Verify.ps1 -Captured -EvidenceDirectory TestResults/MyFreshCapturedRun
```

This runs NetterLow source Debug and published Release only: immutable captured
GLB/glTF+BIN/OBJ+MTL/grid bytes, no disk fallback, bounded native file callbacks,
projection quotas, persistent semantic-ID ledger and atomic publication checks.
Native imports in the lifecycle proof run in an owned worker, admitted to a Windows
Job with512MiB committed-memory limit before parser activation. A blocked native
read callback proves cancel/deadline kill and actual exit before release. The Job
also denies an actual oversized memory commit. Concurrency is1active/0queued.

Eight named proof groups include paths/packet corruption, exact/over quotas,
identity/rename/tombstones/Unicode persistence, failed startup, repeated native
cancellation, competing admission, parser failure, multi-file recovery and stale
publication. [Contract, numeric limits and remaining production work](../../docs/engine/MODEL_IMPORT_CAPTURE.md).
The local capture helper is a per-file trusted-source capture, not an atomic or
race-hardened filesystem snapshot. Dependencies and semantic locators are explicit
inputs; discovery/App/IFactory/Editor/Runtime integration remain g.1/2 work.
