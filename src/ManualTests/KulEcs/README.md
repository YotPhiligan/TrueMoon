# Kul ECS independent acceptance

Run with PowerShell7 from the workspace:

```powershell
pwsh -NoProfile -File ManualTests/KulEcs/Verify.ps1 -EvidenceDirectory TestResults/KUL_ECS/new-offline
pwsh -NoProfile -File ManualTests/KulEcs/Verify.ps1 -EvidenceDirectory TestResults/KUL_ECS/new-device -Device
```

Each run requires a fresh evidence directory. BuildProfile.ps1 captures the shared
SDK/TFM/LangVersion/Features and emits isolated consumer profiles; no copied SDK pins.
The runner copies sources/known OGG fixture to temp, generates deterministic PCM WAV
and immutable audio capture, and checks both Cobalt and Microsoft DI through actual
PrimitiveGameSession. Source Debug, published Release, fresh package-only Debug and
published Release execute against a closed exact-version feed and isolated caches.
Managed DLL and patched miniaudioex.dll hashes are checked against the closed cache.

Offline checks known PCM/gain and silence; `-Device` opens actual WASAPI with only this
App stream muted, checks cursor advancement/pause/seek and cleanup, and never falls
back to Offline. No global volume changes. These are not listening/device-loss/soak
proofs. Logs/binlogs, source hashes, graphs and JSON reports remain in evidence.

Tests cover EOF/replays, capacities, invalid input and retained cleanup separately.
See [bridge contract](../../docs/audio/ECS_BRIDGE.md).
