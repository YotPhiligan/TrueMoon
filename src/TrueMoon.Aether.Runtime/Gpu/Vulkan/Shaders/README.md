# Vulkan surface shaders

`cube.vert` emits a unit cube (36 triangle vertices, no vertex buffer), centered at
world (0,1,0) by `PrimitiveScene.CreateTransform`. The camera uses a right-handed
world, +Y up, -Z forward, Vulkan depth [0,1]. The CPU row-vector matrix is uploaded
as the equivalent column-major GLSL matrix. The projection flips Y for top-left
stored image coordinates. The shared target remains opaque RGBA8 stored-sRGB.

The simple material is RGB times a fixed face intensity; it is not a PBR or
linear-light pipeline. Front faces are submitted before the darker back faces,
so the native proof's center-color assertion also detects missing depth testing.
Push constants contain one mat4 and vec4 (80 bytes, vertex and fragment stages).

`mesh.vert` consumes packed position float3 vertices and UInt32 indices from the
surface resource backend. Its push matrix is world * viewProjection. `mesh.frag`
encodes the already linear material-times-instance tint to sRGB exactly once;
the shared attachment is RGBA8 UNORM, not an automatic sRGB target. The opaque
profile uses depth Less/write, no blending and no culling. It does not alter the
legacy primitive pipeline above.

All four checked-in `.spv` files are embedded in TrueMoon.Aether. Build, pack and
publish require no shader compiler or new runtime dependency. After editing GLSL,
compile and validate all modules from `src`:

```powershell
./TrueMoon.Aether/Gpu/Vulkan/Shaders/CompileShaders.ps1 `
  -GlslangValidator (Resolve-Path TestResults/VulkanInterop/tools/shaders-1.4.363.0/Bin/glslangValidator.exe) `
  -SpirvValidator (Resolve-Path TestResults/VulkanInterop/tools/shaders-1.4.363.0/Bin/spirv-val.exe)
```

Alternatively pass the tools from a local Vulkan SDK; target environment is
Vulkan1.1. Source and SPIR-V fingerprints are included in RunProbe.ps1 evidence.
Native rules: [attachment layouts](https://docs.vulkan.org/refpages/latest/refpages/source/VkAttachmentDescription.html),
[push constants](https://docs.vulkan.org/refpages/latest/refpages/source/vkCmdPushConstants.html),
[image barriers / external queue ownership](https://docs.vulkan.org/refpages/latest/refpages/source/VkImageMemoryBarrier.html).
