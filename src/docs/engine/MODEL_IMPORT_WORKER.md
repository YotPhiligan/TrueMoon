# ARCH1g.1b — isolated CPU model import

17.11.2026: текущий recipe/protocol v2 и persistent identity/publication API описаны
в [ARCH1g.1c](MODEL_SUBASSET_LEDGER.md). Ниже — общий worker contract с уточнениями v2;
[первоначальная приёмка g.1b](MODEL_IMPORT_WORKER_RESULTS.json) сохранена отдельно.

Windows x64, opt-in Tooling API. AssimpNetter **6.0.6** and its single-precision
Assimp **6.0.6** run in `TrueMoon.Aether.Importing.Assimp`, a separately published
executable. Tooling/Runtime packages have no Assimp dependency. The worker is not
a NuGet package and is not started by registration or an empty App.

## Composition and ownership

```csharp
context.UseAetherModelSourceCapture();
context.UseAetherModelImport();
// Composition root: obtain the registered factories and reviewed deployment manifest.
// captureFactory: IFactory<ModelSourceCapture, ModelSourceCaptureArguments>
// importFactory: IFactory<ModelImportOperation, ModelImportArguments>
await using var capture = captureFactory.Create(new(sourceId, sourceDirectory, "scene.gltf"));
var source = await capture.CaptureAsync(token);
var deployment = new ModelImportWorkerDeployment(workerDirectory, absoluteDotnetHost, publishedFileHashes);
await using var operation = importFactory.Create(new(source, deployment, scratchDirectory));
var candidate = await operation.ImportAsync(token);
```

`publishedFileHashes` is a reviewed publish manifest with **all** canonical
relative filenames and SHA256 values. Retain the immutable published directory;
do not generate a new trusted manifest from an arbitrary directory on every import.
The .NET host/runtime is explicitly trusted by the composition root. Its selection
is not an environment setting invented by this module.

Factory products, captured input and returned CPU candidates belong to the caller.
The App-owned `ModelImportHost` holds only the active operation: **1 active / 0 queued**.
Stop closes admission, cancels, kills if necessary, waits for actual process exit,
drains both pipes and removes the operation's unique scratch child. Dispose an idle
product too. No alias disposes the host a second time.

An operation runs once. Pre-cancellation starts no process. Default deadline is
10 seconds, explicit maximum 30 seconds, including deployment preparation.
Cancellation/timeout completion waits for cleanup; the deadline is not a guarantee
that OS cleanup finishes within that same interval. Reap/drain waits have bounded
10-second stages. If cleanup fails, the owner retains admission and remaining
resources; `DisposeAsync` retries. A killed task alone is not a reaped worker.
`WorkerProcessId`, `WorkerReaped` and host admission expose the corresponding state.

## Format and projection recipe

Recipe `aether.assimp605.gltf2-static-opaque-unlit/2` accepts **glTF 2.0/GLB 2.0**,
using the [captured source](MODEL_SOURCE_CAPTURE.md), including external BIN bytes.
The native FileIO callbacks have no disk fallback, basename search or write mode.
Source paths may disappear after capture. OBJ/MTL capture is available, but decoding
OBJ, FBX, embedded data URIs and other formats is outside this recipe.
The `assimp605` wire recipe IDs remain fixed for existing worker packets and
ledger sidecars. The deployed worker checks the native version independently.

- Coordinates: right-handed, +Y up, meters. No winding, handedness or unit conversion.
  Assimp column matrices are transposed to System.Numerics row matrices; world = local × parent.
  The parent can scale local mesh positions through `ModelImportArguments.GeometryScale`.
  The default is one. The setting must be finite and positive. Bounds scale with
  positions, instance matrices retain their authored coordinates, and the result
  passes the mesh validators.
- Only static triangle primitives. No explicit post-process triangulation, vertex joining,
  normal generation, mesh optimization or transform baking. Assimp may normalize its
  imported vertex/index representation. The CPU mesh retains local positions/indices;
  each node occurrence carries its world matrix.
- By default, each primitive projects to a mesh with one material slot.
  `meshes[].extras.truemoon.combineMaterialSlots: true` opts a uniquely named mesh
  with exactly two triangle primitives into a combined mesh. Each primitive supplies a
  distinct `extras.truemoon.subasset` label. The worker orders the ranges by label and
  keeps the material of each primitive. The ledger keys the slots by mesh and primitive
  labels. `MeshDefinition` and `RenderContentCodec` apply the existing geometry and
  render budgets to the combined mesh.
- Opaque, single-sided material. glTF linear baseColorFactor RGB is encoded
  to stored sRGB through `RenderColor.Encode`; alpha must be 1. The existing renderer is
  unlit: metallic/roughness values are range-checked and deliberately not represented.
  The worker retains TEXCOORD_0 for textured primitives and restores glTF's V
  orientation after Assimp. Optional NORMAL and TANGENT are omitted.
  Authored names/extras.truemoon.subasset provide bounded nullable semantic
  hints in v2; ambiguity requires explicit labels before ledger publication.
- ARCH1f.5 accepts one external 8-bit PNG in a `.gltf` file with one base-color
  material reference and one explicit sampler. Minification and magnification
  are nearest (9728), both wrap axes are clamp-to-edge (33071), and the material
  uses TEXCOORD_0. The isolated worker decodes captured PNG bytes with SkiaSharp
  into opaque RGBA8 sRGB at up to 512×512. A source-relative PNG path identifies
  the texture in the ledger. The canonical texels determine its render revision.
  [glTF texture and sampler specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#textures)
- Reject animations, skins/bones, morphs, cameras/lights, other textures, nonempty extensions,
  transparency, double-sided/emissive materials, nontriangle modes and other attributes.
  Assimp validation or the CPU validator also rejects malformed input. Errors publish no
  partial candidate. This bounded recipe is not full glTF conformance.

Candidate-local mesh/material indices have no persistence meaning. `SourceId` remains
caller-supplied, `InputRevision` identifies captured bytes, and `Revision` hashes input
identity, recipe, semantic hints, applied scale and canonical CPU bytes. The
worker emits v2 for ordinary candidates and v4 for combined meshes with explicit slot
labels and material indices. The ledger uses v3 and v5 for the respective nondefault
scale variants. Textured candidates use v6 and their nondefault scale variants
use v7. The ledger reads all six versions. Existing v2–v5 bytes and revisions
are preserved.
CPU candidates are not RenderContentManifest/AssetKey values.
Persistent subasset IDs and atomic ledger publication are provided by [g.1c](MODEL_SUBASSET_LEDGER.md);
Nom/build/runtime integration is described in [ARCH1g.2](PLAN.md#arch1g-model-assets)
and [ARCH1f.5](RENDER_SURFACE.md#imported-base-color-texture). In the child, name
fields are replaced by checked transport tags in a derived captured JSON/GLB.
The derived Assimp input omits texture declarations. Original InputRevision and BIN
bytes remain authoritative; there is no disk fallback or index-derived persistent ID.

## Bounds and native boundary

Input: 32 files, 16MiB/file, 32MiB total. Native IO: 512 opens, 16 concurrent cursors,
128MiB cumulative reads; size_t overflow and out-of-bounds seek fail without allocating
from native request sizes. Callback exceptions are retained and checked after native
scene release; no managed exception crosses the C ABI. Cursors/GCHandles/native FileIO
are freed after `ReleaseImport`, before library unload.

Projection: 64 meshes, 128 materials, 65536 vertices and triangle faces per mesh,
4096 nodes/instances, depth64, 8MiB encoded candidate. Existing per-mesh 2MiB limit
still applies. Counts and aggregate budgets are checked before detached arrays.
The binary packets reject unsupported recipes, excessive counts/lengths, foreign
input identities, truncation and trailing bytes. Each diagnostic pipe retains at most
8192 characters and drains excess before reporting failure.

The parent attaches a Windows Job **before GO/native activation**, then queries installed
limits: 512MiB committed process memory, one process, kill on Job close. Native import
errors/crashes remain in the child. The worker verifies the output-local native DLL hash
`4EC73281A4FDDB735A0A59180EBFCEAB122EC8A8EF136814B2FCCCFB7B78E944`, version 6.0.6 and
single-precision compile flag before touching native structures. The deployment descriptor
pins reviewed files and ancestors using readonly Windows handles; closure limits are
128 files, 64MiB/file, 128MiB total and 64 directories/depth8.

This is process/resource isolation for trusted local tooling. A Job is not a filesystem,
ACL or hostile-code sandbox; the publish directory and dotnet runtime remain trusted.
Do not claim that arbitrary compromised native code cannot access the user's files.

## Build and verification

Use the SDK/profile from `global.json` and `Directory.Build.props`. Publish the optional
worker for `win-x64`, framework-dependent, `UseAppHost=false`; retain its full closure,
manifest and included licenses. The native Skia PDB is excluded from worker
publish to keep the per-file size limit. The decoder DLL is pinned by the
worker closure manifest, which also covers the Skia license and third-party
notices copied from the restored package. `ManualTests/ModelImportConsumer/Verify.ps1` performs
source Debug, published source Release and fresh package Debug/published Release proofs
with Cobalt and Microsoft DI. Both source and fresh package paths use the same Release
worker; a separate Debug worker is also checked. The acceptance-only fake worker exercises
crash, damaged/oversized replies, blocked cancellation/deadline, Job commit denial,
admission, failed cleanup/retry and App Stop. It is never shipped as the production worker.

The original g.1b results and exact commands are recorded in [HISTORY](HISTORY.md#arch1g1b-2026-11-17)
and [delivery manifest](MODEL_IMPORT_WORKER_RESULTS.json). Current v2 verification is in
[g.1c](MODEL_SUBASSET_LEDGER.md). These do not establish GPU,
Editor preview, Nom publication, other OS/AOT or complete g.1/g.2 acceptance.

Primary ABI/policy sources: [Assimp 6.0.6 FileIO](https://github.com/assimp/assimp/blob/v6.0.6/include/assimp/cfileio.h),
[material keys](https://github.com/assimp/assimp/blob/v6.0.6/include/assimp/material.h),
[glTF coordinates/units](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#coordinate-system-and-units).
