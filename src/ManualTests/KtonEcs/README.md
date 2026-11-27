# Independent Aether/Kton consumer: PHYS5, PHYS6 and T3

```powershell
pwsh -NoProfile -File ManualTests/KtonEcs/Verify.ps1 -EvidenceDirectory <fresh-directory>
```

Builds outside the checkout with the shared BuildProfile.ps1 SDK/TFM/language/features
and explicit CPM isolation. Source Debug/published Release and fresh package-only
Debug/published Release each execute both Cobalt and Microsoft DI. The verifier
seeds exact external archives with the repository's `NuGet.Config`, then builds
from a closed feed under `<fresh-directory>/run/feed`. It packs 14 TrueMoon
packages at the persisted repository version and restores the independent probe
into an empty cache against that feed alone. For seven physics/runtime DLLs it
matches the loaded file to the exact runtime asset in both the cache and archive.
Controller license, provenance and template entries are checked against source
SHA256. `verification.json`, the individual probe reports and binlogs stay in
the evidence directory.

Actual BEPU worlds run through the public App/scene-subsystem/PrimitiveGameSession
path: independent scenes, falling/settled dynamic box, static floor, kinematic
velocity, same-tick CPU render pose, next-tick Gameplay contacts, phase spawn/delete,
new generations, sequenced teleport, invalid candidate preserving the old session,
Stop/replacement and final native owners/pools zero. The SDK and Runtime bridge
reference closures are checked separately from the selected BEPU provider.

PHYS6 adds an authored capsule character through the Character v1 schema: support
entity/normal, target velocity and same-tick pose, sequenced single jump, landing
without replay, and teleport support invalidation. On `Baseline60` and
`FastLinear60`, v1 and default v2 block at 0.25 m, opt-in v2 takes 0.25 m
with upper support in the same tick, and opt-in v2 blocks at 0.30 m. Each of
the four build/publish outputs runs 104 checks across both DI providers,
including cleanup of all six App owners.

The same fresh Release publish output also runs a separate `--t3` process:

```powershell
dotnet <published>/KtonEcsProbe.dll --t3 <absolute-T3Fixture.json> <absolute-new-report.json>
```

[T3Fixture.json](T3Fixture.json) captures only the archived route inputs, including
the two-triangle ramp and its source archive SHA256. The probe has no Tales project
or DLL reference. It imports the ramp through Body v2 and supplies the geometry
through `PhysicsBridgeOptions.AddStaticMesh`. Each of 88 rows (two DI adapters,
two profiles, 11 cases, auto-step off/on) compares each ECS sample exactly with a
fresh ECS repeat and a direct-provider control from the same loaded DLLs. The
verifier checks those DLLs against the closed feed independently of the legacy
process. `t3-fresh-Release.json` records the traces, route landmarks and cleanup;
an existing report is never overwritten.

This is headless ECS physics plus CPU snapshot acceptance. Aether's session
protocol (Pause/Step/Replace), native Player, actual GPU/window pixels,
mod-state migration, device loss and expanded platform readiness are separate.
