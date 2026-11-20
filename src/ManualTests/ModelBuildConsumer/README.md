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

## Play two revisions of an imported PNG offline

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/Verify.ps1 -ImportedTexture `
  -EvidenceDirectory 'TestResults/PlayerImportedTexture/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

Authoring places a two-slot glTF with external BIN and PNG and publishes Build A.
Only `textures/color.png` changes. Reimport keeps the texture ID and publishes
Build B with the same scene bytes, mesh revision and game assembly. Both builds
capture their exact Nom snapshots before the source files and Nom are moved aside.

The probe stages the scene and immutable artifacts in a separate playback
directory. Three standalone Player processes run A → B → A. Each checks the
presented scene stamp, exact Vulkan texture upload and balanced native cleanup.
The top-left texel changes from `D51006FF` to `2194D5FF`. The other three texels,
green slot and remaining pixels match. The two A frame hashes are identical.
Source Debug, published Release and isolated Release from a closed feed each
run on Cobalt and Microsoft DI.

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
editing its external BIN. Before a new build, it compares a fresh capture with
the published import for each model. Both imports start current. Editing the
red BIN at the same length and modification time marks only the red model stale.
Reimport makes both current again. The probe also checks a settings-only change,
a missing dependency, cancellation and rejection of a new build with stale content.
The model watcher proof uses a one-minute polling interval and observes the red
BIN edit, removal and restoration through native hints before the first poll.
The green watch remains at its initial observation. A polling-only watch confirms
the same edit. App Stop and disposal before Start both close live watches.
It checks canonical BuildId under reordered and
overlapping snapshots, rejection of conflicting revisions and missing snapshots,
and release of the snapshot leases. Player opens the old, new and old builds
after the source directories and closed Nom store have been moved. The red mask
changes and returns to its original pixels. The green mask matches across all
three launches. This runs in source Debug and fresh-package Release on both DI
providers.

## Selected model imports in Editor

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -ModelImports `
  -EvidenceDirectory 'TestResults/EditorModelImports/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

The probe opens a two-model scene, attaches both published imports and uses the
status popup and Project menu in a native Editor window. Editing or removing the
red BIN changes only its status and blocks Build before compilation. Selecting
the reimported generation restores both current statuses. A new Build carries the
updated canonical render keys. The probe also rejects a mismatched frozen payload
and verifies that closing the scene clears the selection. `-FreshPackages` runs
the same scenario with an independently published Editor from the closed feed.

## Reimport a selected model in Editor

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -ModelReimport `
  -EvidenceDirectory 'TestResults/EditorModelReimport/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

The native Editor probe clicks the selected model's reimport action. It verifies
failure on a missing dependency and cancels between the ledger commit and Nom
publication. A second click finishes publishing the same generation. Build
preserves the other model and the placed scene. Three GPU captures check the
original, updated and reopened original builds. `-FreshPackages` repeats the
scenario in a published consumer from the closed local feed.

## Restore the saved model selection

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -ModelSelectionPersistence `
  -EvidenceDirectory 'TestResults/EditorModelSelection/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

The reimport scenario also reopens the scene after the ledger advances and
the reimport is cancelled. It verifies read-only restoration of the retained
generation, then retries from the restored selection. Reopening after success
preserves the new exact resources. A competing write and malformed checkpoint
exercise both conflict boundaries before Build and the old/new GPU comparison.
`-FreshPackages` repeats the scenario in the independently published Editor.

## Select project models in Editor

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -ModelCatalog `
  -EvidenceDirectory 'TestResults/EditorModelCatalog/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

The fixture registers two published models in the project catalog and selects
them through checkboxes in the Editor status panel. Build rejects a placed model
with missing resources. The probe reopens the scene with both models and with
an empty selection, then verifies GPU output for the original and reimported
builds. `-FreshPackages` repeats the scenario in the independent published Editor.

## Place a catalog model in the scene

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -ModelPlacement `
  -EvidenceDirectory 'TestResults/EditorModelPlacement/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

The native Editor opens a scene containing the camera and the green model. It
adds the red model through the catalog row, undoes and redoes the placement
through the Edit menu, saves through the File menu, and reopens the scene.
The probe checks occurrence IDs, the saved revision and the selected generation.
It then builds and presents the scene through the GPU viewport and matches the
presented scene revision, BuildId and render manifest.

[Contract and ownership](../../docs/engine/MODEL_BUILD_DELIVERY.md)

## Change MeshRenderer assets in the inspector

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -RenderInspector `
  -EvidenceDirectory 'TestResults/EditorRenderInspector/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

The fixture selects two published imports with different shapes and base
colors. In the real inspector it chooses a different mesh for one occurrence
and assigns a material from the other import. It applies both refs together
and checks that the sibling sharing the original definition keeps its refs.
The probe builds and displays the original, edited and undone scene revisions
on Vulkan. It checks the selected material in the edited build manifest and
compares the GPU masks, including exact recovery after one Undo.
`-FreshPackages` runs the same scenario in an independent published Editor.

## Restore renderer choices after restarting Editor

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -RenderInspectorRestart `
  -EvidenceDirectory 'TestResults/EditorRenderInspectorRestart/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

The first Editor applies the mesh and material in the inspector and saves the
scene. It builds the saved revision and captures the Vulkan viewport before
exiting. The next Editor opens the same file and restores the pinned imports
from disk. It checks the displayed mesh and material, the binding ID and the
exact resource keys, then builds in an empty output directory. The receipts
record both process IDs, scene and import file hashes, and identical RGBA
pixels in the unobstructed viewport area containing the visible model pixels.
`-FreshPackages` repeats the two-process check in an independent Editor.

## Restore the mesh's imported material

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -RenderMaterialReset `
  -EvidenceDirectory 'TestResults/EditorRenderMaterialReset/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

The Editor replaces one occurrence's mesh and assigns a different material.
It clicks “Вернуть материал меша”, cancels the draft, then stages the action
again and applies it. The probe builds and displays the override, restored
material, Undo and Redo states on Vulkan. Every state keeps the exact mesh
key and slot, and the sibling keeps its references. The viewport's blue
channel stays identical across the fixture's red and green materials. RGBA
pixels in the unobstructed viewport match on override and Undo, and on
restore and Redo.
`-FreshPackages` repeats the scenario in an independent published Editor.

## Edit one slot of an imported two-slot mesh

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -RenderMaterialSlots `
  -EvidenceDirectory 'TestResults/EditorRenderMaterialSlots/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

Set `meshes[].extras.truemoon.combineMaterialSlots` to `true` on a uniquely
named glTF mesh with two triangle primitives. Give each primitive a distinct
`extras.truemoon.subasset` label. The import ledger assigns each slot a
persistent ID from the mesh and primitive labels. Primitive order may change
on reimport. The inspector offers both slots and allows one material override
per mesh instance. Revert the override to edit the other slot.

The probe reorders the primitives and reimports geometry, then checks both
slot IDs through the reopened ledger, Nom and Build. It changes the left
material in the inspector and compares two Vulkan frames. The red and white
masks coincide and the entire right region matches pixel for pixel.
`-FreshPackages` repeats the scenario in an independent published Editor.

## Import the first model from Editor

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -InitialModelImport `
  -EvidenceDirectory 'TestResults/EditorInitialModelImport/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

The project starts with a camera scene and an empty model catalog. The probe
enters the path to a two-slot glTF with an external BIN through the Editor's
project menu. It exercises dismissal, missing BIN and retry after a blocked
catalog write. It then selects, places and saves the imported mesh, checks the
actual Nom resource heads and displays a new Build on Vulkan. A separate Editor
process reopens the project and scene, then compares the catalog and ledger
bytes, the exact mesh and material keys, both slot IDs and the viewport pixels.
`-FreshPackages` repeats the complete two-process scenario from the closed feed.

## Restore an imported PNG texture after restarting Editor

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -ImportedTextureRestart `
  -EvidenceDirectory 'TestResults/EditorImportedTexture/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

The Editor imports a glTF with an external BIN and a 2×2 PNG, places it from
the catalog, and saves the scene. A second process reopens the saved scene and
runs a new Build. Both Builds carry the exact mesh, material, binding and texture
revisions from Nom. The probe compares the four colored texture regions, the plain
green slot, and the unobstructed RGBA viewport. It also checks the exact Vulkan
upload and native cleanup. Use `-FreshPackages` with a fresh evidence directory
to repeat the scenario using the closed feed.

## Reimport a changed PNG in the open Editor

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -ImportedTextureReimport `
  -EvidenceDirectory 'TestResults/EditorImportedTextureReimport/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

One Editor imports, places and saves the textured glTF. The probe presents a
baseline Build, changes only the external PNG and observes staleness in the
status popup before attempting another Build. It checks the Build rejection,
activates the model's Reimport action with Enter and observes the current status. A new Build
on Vulkan must have the same texture ID and a different revision. The top-left
texel changes and the other three texels and the green slot still match.
`-FreshPackages` runs the same open-project scenario from the closed feed.

## Hot Replace a playing scene after PNG reimport

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -ImportedTextureReplace `
  -EvidenceDirectory 'TestResults/EditorImportedTextureReplace/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

The Editor starts the saved scene on Vulkan. While it plays, the probe
changes only the external PNG, reimports through the open document API and
publishes the new candidate through the game compiler. Before Replace, the original child
advances its tick and presents the exact original pixels and manifest digest.
The published-build picker performs Replace. The next child plays from the
checkpoint, presents the new texel and keeps the other texels and the green
slot. The probe checks both uploaded revisions and retirement of resources.
It issues Stop only after capturing the replacement frame.
`-FreshPackages` runs the same scenario from the closed feed.

## Reimport, Build and Replace from the Editor during play

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -PlayingTextureWorkflow `
  -EvidenceDirectory 'TestResults/EditorPlayingTextureWorkflow/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

While the scene plays, change the external PNG. The Build menu rejects the
stale import and the playing viewport still matches the original. Invoke
Reimport in the status popup, then use the Build menu to publish a candidate.
The original Runtime and pixels stay active until the published-build picker
confirms Replace. The picker offers the prepared BuildId. The new Runtime
continues from the checkpoint and shows the new exact texture revision on
Vulkan. The probe records both upload revisions and checks native cleanup.

## Restore an edited material slot after restarting Editor

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -RenderMaterialSlotsRestart `
  -EvidenceDirectory 'TestResults/EditorRenderMaterialSlotsRestart/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

The first Editor imports and places the two-slot mesh, replaces the left slot's
red material with white in the inspector and saves the scene. A new Editor opens
the saved scene and checks both named material controls. Each process builds from
its own output directory. The probe compares the two slot IDs, the mesh, binding
and material keys, the saved scene and import hashes, and the exact viewport
pixels with the white left slot and green right slot.
`-FreshPackages` repeats the check from the closed feed.

## Persist independent overrides for two material slots

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -RenderMaterialSlotOverrides `
  -EvidenceDirectory 'TestResults/EditorRenderMaterialSlotOverrides/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

Apply white to the left slot, then apply red to the right slot. Undo once
and compare the Vulkan captures. The left half stays pixel-identical and
the right slot returns to green. Redo and save both overrides. A new Editor
opens the scene, checks both inspector values, builds it again and compares
the exact mesh, slot, binding and material keys and viewport pixels with the
saved scene. `-FreshPackages` repeats the check from the closed feed.
`-SaveAfterSlotUndo` saves immediately after Undo and checks that the
restarted Editor shows the white left slot and the original green right slot.

## Transfer material overrides to another mesh

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -RenderMaterialTransfer `
  -EvidenceDirectory 'TestResults/EditorRenderMaterialTransfer/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

Import three two-slot meshes. An ordinary choice previews the destination
defaults. The explicit transfer previews both overrides on the compatible
mesh. A destination with one matching slot offers transfer once only that
slot is overridden. Its matching slot has a different index. The probe
compares exact mesh, slot and material keys and Vulkan pixels for the mesh
with both overrides, then checks Undo and Redo. `-FreshPackages` repeats the
scenario from the closed feed.

## Edit and restore the camera projection

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -CameraProjection `
  -EvidenceDirectory 'TestResults/EditorCameraProjection/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

Select the scene camera and edit its vertical angle and clipping distances.
An invalid near/far pair blocks Apply. A valid edit changes the angle from
60° to 80° and both distances in one transaction. The probe checks the smaller
projected shapes in the Vulkan viewport, exact pixel restoration after Undo,
and the inspector values and viewport pixels after saving the Redo state and
restarting Editor. `-FreshPackages` uses the closed feed.

## Diagnose a missing mesh or material binding

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/VerifyEditor.ps1 -RenderReferenceDiagnostics `
  -EvidenceDirectory 'TestResults/EditorRenderReferenceDiagnostics/<fresh-run>' `
  -ValidationDirectory 'C:/VulkanSDK/<version>/Bin'
```

One instance uses a material from another selected import. The probe removes
the mesh import and then the material import through scene catalog selection.
The inspector displays the exact missing reference. Build rejects each case
before changing the previous candidate. Selecting the imports again restores
the same asset binding and the same viewport pixels on Vulkan.
`-FreshPackages` repeats the scenario in an independent published Editor.
