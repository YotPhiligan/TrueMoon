# PHYS5 independent Aether/Kton consumer

```powershell
pwsh -NoProfile -File ManualTests/KtonEcs/Verify.ps1 -EvidenceDirectory <fresh-directory>
```

Builds outside the checkout with the shared BuildProfile.ps1 SDK/TFM/language/features
and explicit CPM isolation. Source Debug/published Release and fresh package-only
Debug/published Release each execute both Cobalt and Microsoft DI. A closed local
feed supplies14coherent TrueMoon packages plus exact cached external dependencies;
fresh output DLL hashes are checked against the isolated cache.

Actual BEPU worlds run through the public App/scene-subsystem/PrimitiveGameSession
path: independent scenes, falling/settled dynamic box, static floor, kinematic
velocity, same-tick CPU render pose, next-tick Gameplay contacts, phase spawn/delete,
new generations, sequenced teleport, invalid candidate preserving the old session,
Stop/replacement and final native owners/pools zero. The SDK and Runtime bridge
reference closures are checked separately from the selected BEPU provider.

This is headless physics plus CPU snapshot acceptance. It does not claim matching
collider/render geometry, actual GPU/window pixels, character controller, native
mod-state migration, device loss or expanded platform/release readiness.
