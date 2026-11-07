# Render resource ownership and preparation consumer

From src using the cached .NET10 dependencies:

```powershell
& ./ManualTests/RenderResources/Verify.ps1 -EvidenceDirectory ./TestResults/MyRenderResources
& ./ManualTests/RenderResources/VerifyNative.ps1 -ConsumerEvidenceDirectory ./TestResults/MyRenderResources -ValidationDirectory /path/to/extracted/validation
```

Verify.ps1 compiles the same Program.cs through source Debug, published Release and
a fresh isolated seven-package NuGet Release consumer with zero ProjectReferences.
The CPU mode injects a deterministic backend: its results prove public composition,
counted ownership and reload, not GPU work. Exact cached external nupkg and a new
local offline feed are used; each MSBuild operation retains a unique binlog.

VerifyNative.ps1 runs the frozen three artifacts with the actual standalone Vulkan
resource backend. It prepares shared geometry, verifies device buffer readback SHA256,
reloads both entities to new pinned revisions, rejects old/foreign handles, then checks
actual resource/context teardown. Each profile uses two fresh resource generations
and one zero-geometry-budget failure before native memory allocation. Core and
synchronization validation must be enabled with zero errors/warnings including cleanup.

No window, shaders, draw commands, external surface/frame transport, Game.Logic load,
driver settings or tooling installation is involved. Native readiness here means
prepared buffers; required-view Playing/presentation and owned mesh snapshots belong
to f.3/4. ValidationDirectory must contain the extracted Khronos validation JSON/DLL;
their hashes are retained and VK_LAYER_PATH is process-local.

Use fresh evidence/run directories. Failed artifacts are preserved. Checkout projects,
dependency versions and global NuGet configuration are unchanged.
