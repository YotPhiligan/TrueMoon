# Rendering CPU contract consumer

From src with the .NET10 SDK and the repository's cached dependencies:

```powershell
& ./ManualTests/RenderingCpu/Verify.ps1 -EvidenceDirectory ./TestResults/MyRenderingCpu
```

The runner compiles the same Program.cs in fresh external projects: source Debug,
published Release and an isolated NuGet Release consumer with zero ProjectReferences.
It packs seven current framework packages and restores them and exact cached external
dependencies from a local offline feed. Run/evidence directories must be new; failed
artifacts and each MSBuild command's unique binlog are retained.

The consumer checks independent literal material bytes/SHA256, cube geometry,
manifest order/generation semantics, CPU bounds/projection, shared exact bindings
and isolated tint/reset. A three-entity scene is loaded through App/Cobalt and the
existing typed SceneWorld factory. GPU/native preparation, Runtime frames and UI
are not activated; their implementation belongs to ARCH1f.2–4.

The [CPU profile](../../docs/engine/RENDERING_CONTRACTS.md) specifies exact identities,
layouts, conversion and admission. The runner changes no project, package version,
NuGet settings or driver configuration in the checkout.
