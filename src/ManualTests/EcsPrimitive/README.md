# ECS primitive profile acceptance

From `src` on Windows with the cached .NET10 dependencies:

```powershell
& ./ManualTests/EcsPrimitive/Verify.ps1 -EvidenceDirectory ./TestResults/MyEcsPrimitive
& ./ManualTests/EcsPrimitive/VerifyNative.ps1 -ConsumerEvidenceDirectory ./TestResults/MyEcsPrimitive -ValidationDirectory /path/to/extracted/validation
```

`Verify.ps1` builds source Debug, publishes parent/runtime Release, packs seven
current framework packages and creates independent parent/runtime/game consumers
with zero ProjectReferences in a fresh isolated NuGet cache. Exact cached external
nupkg are copied to an offline feed; checkout versions and global settings stay intact.
Every profile loads133logical entities/2drawables, inherited Transform/Appearance,
identity groups and velocity, then verifies CPU pixels encoding independently
known extracted matrices/colors before/after one tick. The parent loads no game
CLR types; repeated generations and actual process/frame/artifact release are checked.

`VerifyNative.ps1` uses those frozen source/published artifacts and the existing
Vulkan external-memory/GL readback path. It checks actual red/green geometry,
movement and stationary centroids, metadata0→1ticks, slot retirement, two runtime
generations, core/synchronization validation and final GPU/native/UI owners.
It opens only the probe window used to own the GL context; it changes no OS/driver
settings. ValidationDirectory must contain the existing VkLayer_khronos_validation
JSON/DLL; their hashes are retained and VK_LAYER_PATH is set only on the proof process.
The script does not install tooling. Native fresh-package GUI, physical input, K7 stress, device loss and
general release acceptance are outside this run.

Both commands require fresh evidence directories, preserve failed outputs, and
create only new scratch data. Each MSBuild command saves its own unique binlog.
The [contract](../../docs/engine/ECS_PRIMITIVES.md) separates logical transform
admission,64drawable rendering, copied snapshots and World/controller ownership.
