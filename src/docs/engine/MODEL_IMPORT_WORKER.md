# ARCH1g.1b — isolated CPU model import

17.11.2026: текущий recipe/protocol v2 и persistent identity/publication API описаны
в [ARCH1g.1c](MODEL_SUBASSET_LEDGER.md). Ниже — общий worker contract с уточнениями v2;
[первоначальная приёмка g.1b](MODEL_IMPORT_WORKER_RESULTS.json) сохранена отдельно.

Windows x64, opt-in Tooling API. AssimpNetter **6.0.5** and its single-precision
Assimp **6.0.5** run in `TrueMoon.Aether.Importing.Assimp`, a separately published
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
- Existing `MeshDefinition`, one default material slot and `RenderContentCodec` validate
  topology, indices, finite positions, bounds and existing render budgets. There is no
  new render format or widened mesh limit.
- Opaque, single-sided, untextured material. glTF linear baseColorFactor RGB is encoded
  to stored sRGB through `RenderColor.Encode`; alpha must be 1. The existing renderer is
  unlit: metallic/roughness values are range-checked and deliberately not represented.
  Optional NORMAL, TANGENT and TEXCOORD_0 are deliberately omitted from the position-only
  output. Authored names/extras.truemoon.subasset provide bounded nullable semantic
  hints in v2; ambiguity requires explicit labels before ledger publication.
- Reject animations, skins/bones, morphs, cameras/lights, textures, nonempty extensions,
  transparency, double-sided/emissive materials, nontriangle modes and other attributes.
  Assimp validation or the CPU validator also rejects malformed input. Errors publish no
  partial candidate. This bounded recipe is not full glTF conformance.

Candidate-local mesh/material indices have no persistence meaning. `SourceId` remains
caller-supplied, `InputRevision` identifies captured bytes, and `Revision` hashes input
identity, recipe, semantic hints, applied scale and canonical CPU bytes. The
worker emits the v2 candidate packet. The ledger stores nondefault scale in a
v3 candidate packet and also reads existing v2 records with scale one.
CPU candidates are not RenderContentManifest/AssetKey values.
Persistent subasset IDs and atomic ledger publication are provided by [g.1c](MODEL_SUBASSET_LEDGER.md);
Nom/build/runtime integration remains **g.2**. In the child only, name fields are replaced
by checked transport tags in a derived captured JSON/GLB. Original InputRevision and BIN
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
`90984E886CF7394560E44FECD5A5666BECD2A93E98251974A562ECB79CFBA386`, version 6.0.5 and
single-precision compile flag before touching native structures. The deployment descriptor
pins reviewed files and ancestors using readonly Windows handles; closure limits are
128 files, 64MiB/file, 128MiB total and 64 directories/depth8.

This is process/resource isolation for trusted local tooling. A Job is not a filesystem,
ACL or hostile-code sandbox; the publish directory and dotnet runtime remain trusted.
Do not claim that arbitrary compromised native code cannot access the user's files.

## Build and verification

Use the SDK/profile from `global.json` and `Directory.Build.props`. Publish the optional
worker for `win-x64`, framework-dependent, `UseAppHost=false`; retain its full closure,
manifest and included licenses. `ManualTests/ModelImportConsumer/Verify.ps1` performs
source Debug, published source Release and fresh package Debug/published Release proofs
with Cobalt and Microsoft DI. Both source and fresh package paths use the same Release
worker; a separate Debug worker is also checked. The acceptance-only fake worker exercises
crash, damaged/oversized replies, blocked cancellation/deadline, Job commit denial,
admission, failed cleanup/retry and App Stop. It is never shipped as the production worker.

The original g.1b results and exact commands are recorded in [HISTORY](HISTORY.md#arch1g1b-2026-11-17)
and [delivery manifest](MODEL_IMPORT_WORKER_RESULTS.json). Current v2 verification is in
[g.1c](MODEL_SUBASSET_LEDGER.md). These do not establish GPU,
Editor preview, Nom publication, other OS/AOT or complete g.1/g.2 acceptance.

Primary ABI/policy sources: [Assimp 6.0.5 FileIO](https://github.com/assimp/assimp/blob/v6.0.5/include/assimp/cfileio.h),
[material keys](https://github.com/assimp/assimp/blob/v6.0.5/include/assimp/material.h),
[glTF coordinates/units](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#coordinate-system-and-units).
