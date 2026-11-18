# Imported model delivery and GPU acceptance

Run from the repository with its pinned SDK on Windows x64. Use an interactive
desktop, Vulkan and OpenGL interop support, and a directory containing
`VkLayer_khronos_validation.json` and `VkLayer_khronos_validation.dll`.

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/Verify.ps1 `
  -EvidenceDirectory 'TestResults/ModelBuild/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

The runner publishes the Assimp worker and exercises the importer, ledger,
Nom and game compiler. It places two occurrences of a shared mesh next to
a scene camera. It checks that a geometry-only reimport changes the selected
build and preserves the game DLL, scene bytes and persistent IDs. Source Debug
and published Release from a closed local NuGet feed both run on Cobalt and
Microsoft DI.

With Nom and the source files moved aside, three Player processes open the
same `placed.aetherscene` with the old build, the new build and the old build
again. They check the presented GPU frame stamp, capture PNGs from the Skia
OpenGL surface and measure both visible occurrences. The original and reopened
red pixel masks must match. The new geometry must extend right and cover more
pixels while keeping its left edge. A fourth process rejects a copy with an
altered render payload. The evidence directory retains the PNGs, measurements,
build/snapshot IDs, validation counters, package hashes and logs.

[Contract and ownership](../../docs/engine/MODEL_BUILD_DELIVERY.md)
