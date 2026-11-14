# ARCH3.9 architecture acceptance

Run with PowerShell7.2+, the SDK from global.json and an existing extracted Vulkan
validation layer directory containing VkLayer_khronos_validation.json/.dll:

```powershell
pwsh -NoProfile -File ManualTests/ArchitectureDelivery/Verify.ps1 `
  -ValidationDirectory <existing-layer> -EvidenceDirectory <fresh-directory>
```

The default run builds the solution Debug/Release, runs all14test projects in both
configurations, then creates one immutable20-package delivery (including Player).
All layer consumers reuse those exact nupkg hashes in isolated caches/closed feeds.
It checks SDK-only, Runtime-only, headless Tooling, Runtime+Editor.UI without Tooling,
Player, full Editor and regular/stable/migrating module profiles. Runtime+UI/Player/
Editor tests create actual native windows and require Windows x64/GPU/validation.

`-Cycles 20` is the default/minimum. Each stable/migration consumer performs20
replacements per source/publish Debug/Release/fresh/SDK-free ×both-DI profile.
Retired ALCs collect while current World remains live. Guards/old contexts/disposed
Worlds/snapshots remain retained; state/tick/data/identity, stale commands, owner
counts and exact entry/system disposal are checked. The private-component profile
does one explicit v1→v2 schema migration followed by19 same-schema/new-CLR-identity
migrations. Stable systems alternate independently compiled module generations.

`Regression.ps1 -EvidenceDirectory <fresh-directory>` may run separately. A completed
manifest can be passed as `-RegressionVerification <path>` to repeat only delivery;
C#/project/build input hashes must still match. No other SDK command should run
concurrently with full Aether regression. Each child runner writes its own logs and
verification.json; read manifests after exit, never poll a live JSON file.

`-PackageVerification` on Abstractions.Host/RuntimeConsumer/ToolingConsumer/
EditorUiConsumer/PlayerProbe/ModConsumer reuses a completed ConfigurationDelivery
manifest; the default standalone behavior remains available. Shared archives are
verified before and after copy. Malformed/incomplete/version/profile/hash mismatch
inputs reject instead of repacking silently.

All writes stay local: no commit, push or public package publishing. TestResults and
temporary feeds are diagnostic artifacts without retention guarantee. Current
[architecture guide](../../docs/engine/ARCHITECTURE_GUIDE.md), [baseline](../../docs/engine/ARCH39_BASELINE.json).
