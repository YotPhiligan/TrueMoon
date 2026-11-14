# Audio module acceptance

Uses the public `UseConfiguredAudio`, constructor injection and `IFactory<AudioSession>`.
Build/install the corrected native package as described in [audio build instructions](../../docs/audio/README.md).

From src, PowerShell 7:

```powershell
pwsh -NoProfile -File ManualTests/KulModuleProbe/Verify.ps1 `
  -EvidenceDirectory TestResults/AudioModule-acceptance -Device
```

Creates source Debug, published Release and an independent package-only published
consumer, using common SDK/TFM/LangVersion/Features snapshots and private feed/cache.
Packs four framework packages plus the exact native dependency. Each profile proves
App/DI, sound playback, music streaming, malformed OGG rejection and cleanup.
`-Device` adds actual WASAPI, two streams/crossfade and pause/resume, always with
this stream muted. It does not change system volume or capture other applications.
Offline output proves known PCM. JSON, SHA, stdout/stderr and per-command binlogs
are retained in the fresh evidence directory. Processes have a timeout and are reaped.

The generated source program also accepts common `--Audio:Key=value` settings after
its two command artifact paths. Never request audible output without coordinating
the listening moment. These profiles make no device-loss, OS/RID/AOT or RT latency claim.
