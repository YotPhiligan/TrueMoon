# Model source capture acceptance (ARCH1g.1a)

From `src`, PowerShell 7:

```powershell
./ManualTests/ModelCaptureConsumer/Verify.ps1 -EvidenceDirectory TestResults/MyFreshModelCapture
```

Uses the shared BuildProfile and persisted framework version. Source Debug/published
Release and fresh NuGet Debug/published Release each run Cobalt/Microsoft DI from an
unrelated working directory. A new private feed/cache contains exact framework
archives and pre-restored external archives; no download fallback is configured.
Loaded fresh DLL hashes, package graph, source hashes and cross-profile input revision
are checked. Each child is bounded and reaped; MSBuild logs use unique binlogs.

Five groups cover lazy factory/caller ownership, glTF+BIN closure and detached bytes,
revision invalidation/deletion, missing/unsafe/limit failures, embedded GLB, OBJ+MTL,
pre-cancel/Stop and unlocked source files. These generated fixtures exercise source
capture only: there is no decoded geometry, Assimp activation, ledger/Nom publication,
asset delivery to Runtime or GPU proof. Limits apply per capture; they are not a hard
process-wide memory/admission budget. Raw evidence remains under the new TestResults
and temp delivery directories; keep them with the versioned task manifest for replay.

The duplicate-material workload declares one 1 MiB MTL 1000 times. It must capture
two distinct files and allocate less than 16 MiB process-total managed bytes during
that operation. This is a regression budget for that exact workload, not a general
heap cap or latency SLA.
