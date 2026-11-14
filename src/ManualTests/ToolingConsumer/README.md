# Independent headless Tooling consumer — ARCH3.4

`Verify.ps1 -EvidenceDirectory <fresh-path>` checks source Debug, published Release
and fresh package-only execution on Cobalt/Microsoft DI. SDK/TFM/language/features
come from BuildProfile.ps1. Fresh restore uses one offline feed/cache, explicit CPM
isolation, no ProjectReferences or legacy Aether/Editor/Alloy/Argentis packages.
Installed/output Tooling DLL bytes must match.

Each profile opens a real project and selected SDK, edits a scene, Undo/Redo,
saves/reopens stable IDs and custom schema values, builds a frozen neutral game,
binds the source baseline, checks failed/cancelled Build retains last-good and
actual compiler reap. Optimistic save conflict preserves external bytes and dirty
memory; cancelled Save preserves state. Pure DTO diagnostic export and App cleanup
complete without UI assemblies or Vulkan/Skia/GLFW/audio native modules.

Fixtures/reports remain in isolated temporary directories. This Windows x64
headless IO/SDK/package proof does not execute the game or claim other OS/AOT,
native graphics/device loss, mods/hot replacement or general release. SDK-free
execution is RuntimeConsumer's responsibility; Tooling Build deliberately uses SDK.
