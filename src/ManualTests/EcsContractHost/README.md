# Real ECS contract acceptance — ARCH3.2

This host loads `Abstractions.Extension` into a private collectible acceptance
context, sharing the exact Abstractions/Contracts identities. The extension has
no Aether reference. The host supplies existing `UseAetherEcs`,
`UseAetherEcsSystems` and `UseAetherSceneLoading` through App with either Cobalt
or the Microsoft DI adapter.

The neutral schema/value factory materializes an extension struct into the real
typed pool. Its system changes that value through a neutral query and records
entity/component commands. An extraction system observes phase playback. Two
ticks must produce 2 then 3 entities and totals 44 then 88. Factories execute once
per system; the scheduler owns Stop/Dispose, the caller owns SceneWorld.
Reflection is confined to the acceptance composition root and scalar reports.
Component columns and callbacks remain generic typed storage.

Run `Verify.ps1 -EvidenceDirectory <fresh-directory>` using PowerShell 7. It
captures the common SDK/profile with `BuildProfile.ps1`, source Debug, published
Release and fresh offline NuGet consumers, each with both providers. The fresh
extension graph must contain only Abstractions and Contracts, with zero project
references. Exact package/output bytes and source drift are checked.

This helper requests unload after the owned systems/scene leave scope. It does
not implement production mod loading, prove ALC collection, replace live mods,
or activate a GPU/audio/UI backend. Those have separate acceptance gates.
