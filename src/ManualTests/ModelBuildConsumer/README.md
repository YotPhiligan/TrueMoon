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

## Editor hot replacement

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 `
  -EvidenceDirectory 'TestResults/ModelBuildEditor/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

The Editor probe reuses the imported scene and builds. It opens the Project
menu and confirms both a BuildId and an absolute artifact directory through the
retained path prompt. One Editor window starts the old build and replaces it
with the new build while playing.
It then submits a damaged copy of the old artifact and verifies that the
current child and surface survive preflight. A second attempt cancels after
the candidate has activated its full scene. The probe checks one recovery
of the previous build and its checkpoint, then replaces it successfully once
more. It compares the active BuildId in the caption, scene and manifest stamps,
ticks and GPU pixel masks throughout. After the final Stop it checks that the children
and GPU resources were released. The sequence runs in source Debug and
published Release with Cobalt and Microsoft DI in the Editor host.

## Independent Editor from packages

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -FreshPackages `
  -EvidenceDirectory 'TestResults/ModelBuildEditorPackages/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

This profile packs the framework closure at the repository version. The
authoring tool and Editor probe restore into a fresh cache from a closed
local feed and publish from outside the checkout. The report records
the resolved graphs, archive hashes, matching installed and published
DLL hashes, runtime child reports, and GPU captures from Cobalt and
Microsoft DI. The importer runs in the separately published Assimp worker
with its pinned native library.

## Dependency, import setting and deletion

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/Verify.ps1 -RevisionMatrix `
  -EvidenceDirectory 'TestResults/ModelBuildRevisions/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

The probe builds three revisions of one placed glTF model. The second
revision changes a vertex in the external BIN. The third applies
`GeometryScale=1.5` to the same captured bytes. It checks the scene and
subasset identities, reopens the ledger and deletes all four Nom heads.
Retained snapshots must still reproduce the original BuildIds.
After removing those snapshots and pruning the operation records, the
probe collects the original CAS objects and verifies all three GPU shapes
in Player, including reopening the first build. Both DI providers run
from source Debug and fresh-package published Release.

## Two models in one build

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/Verify.ps1 -MultiModel `
  -EvidenceDirectory 'TestResults/ModelBuildMulti/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

Two independent imports place red and green models in one scene. The Authoring
probe combines their exact Nom snapshots, then reimports the red model after
editing its external BIN. It checks canonical BuildId under reordered and
overlapping snapshots, rejection of conflicting revisions and missing snapshots,
and release of the snapshot leases. Player opens the old, new and old builds
after the source directories and closed Nom store have been moved. The red mask
changes and returns to its original pixels. The green mask matches across all
three launches. This runs in source Debug and fresh-package Release on both DI
providers.

[Contract and ownership](../../docs/engine/MODEL_BUILD_DELIVERY.md)
