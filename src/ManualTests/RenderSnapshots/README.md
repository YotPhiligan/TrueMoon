# Owned render snapshot consumer — ARCH1f.3

`Verify.ps1` creates an independent application outside the checkout. It uses
public App/Cobalt/IFactory APIs, source Debug, published Release and seven fresh
local packages with a private feed/cache. It requires the repository's dependencies
already restored and current framework outputs built in Debug and Release; exact third-party archives are copied from those caches, without
network lookup or dependency upgrades. All run/evidence paths must be fresh.

```powershell
./ManualTests/RenderSnapshots/Verify.ps1 -EvidenceDirectory TestResults/RenderSnapshots/delivery
```

Program checks explicit camera, stable draw order, shear bounds/linear per-instance
tint, shared current/pending data, recording transfer and pre-factory submission
ledger, reload with old mesh resources alive until completion, suspend/stale recording
rejection/paused resume, detached inspection and zero final snapshot/lease/geometry
counters. The backend is deterministic memory; no Vulkan/window/native pixel or
Runtime Playing acceptance is claimed. Every command has a unique binlog, source
and package hashes are recorded, installed/output Aether DLLs match and fresh graph
contains no framework ProjectReferences. Raw artifacts remain under the chosen
evidence path until explicit cleanup; versioned results belong to engine HISTORY.
Source consumer restore uses --no-dependencies and compilation/publish use
BuildProjectReferences=false against those verified framework outputs, keeping
legacy repository CLI-tool restore separate. Fresh-package restore is complete.

[Contract](../../docs/engine/RENDER_SNAPSHOTS.md), [single roadmap](../../docs/engine/PLAN.md#arch1f-rendering).
