using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Rendering.Resources;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;

namespace TrueMoon.Aether.Runtime.Gpu.Vulkan;

// One slot owns its framebuffer/depth/pipeline. The surface serializes the shared device queue.
internal sealed unsafe class VulkanPrimitiveRenderer(Vk vk, PhysicalDevice physical, Device device, Image color, int width, int height) : IDisposable
{
    private Image _depth;
    private DeviceMemory _depthMemory;
    private ImageView _colorView, _depthView;
    private RenderPass _pass;
    private Framebuffer _framebuffer;
    private PipelineLayout _layout;
    private Pipeline _pipeline;
    private Pipeline _overlayPipeline;
    private DescriptorSetLayout _texturedSetLayout;
    private PipelineLayout _texturedLayout;
    private Pipeline _texturedPipeline;
    internal bool MeshProfile { get; init; }

    internal void Initialize()
    {
        vk.GetPhysicalDeviceFormatProperties(physical, Format.D32Sfloat, out var format);
        if ((format.OptimalTilingFeatures & FormatFeatureFlags.DepthStencilAttachmentBit) == 0)
            throw new NotSupportedException("D32 depth attachment is unavailable.");
        var image = new ImageCreateInfo { SType = StructureType.ImageCreateInfo, ImageType = ImageType.Type2D, Format = Format.D32Sfloat,
            Extent = new((uint)width, (uint)height, 1), MipLevels = 1, ArrayLayers = 1, Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal, Usage = ImageUsageFlags.DepthStencilAttachmentBit, SharingMode = SharingMode.Exclusive };
        Check(vk.CreateImage(device, in image, null, out _depth), "CreateDepth");
        vk.GetImageMemoryRequirements(device, _depth, out var requirements);
        vk.GetPhysicalDeviceMemoryProperties(physical, out var memory);
        uint? type = null;
        for (var i = 0; i < memory.MemoryTypeCount; i++)
            if ((requirements.MemoryTypeBits & (1u << i)) != 0 && (memory.MemoryTypes[i].PropertyFlags & MemoryPropertyFlags.DeviceLocalBit) != 0) { type = (uint)i; break; }
        var allocation = new MemoryAllocateInfo { SType = StructureType.MemoryAllocateInfo, AllocationSize = requirements.Size,
            MemoryTypeIndex = type ?? throw new NotSupportedException("No device-local depth memory.") };
        Check(vk.AllocateMemory(device, in allocation, null, out _depthMemory), "AllocateDepth");
        Check(vk.BindImageMemory(device, _depth, _depthMemory, 0), "BindDepth");
        _colorView = View(color, Format.R8G8B8A8Unorm, ImageAspectFlags.ColorBit);
        _depthView = View(_depth, Format.D32Sfloat, ImageAspectFlags.DepthBit);
        var attachments = stackalloc AttachmentDescription[2];
        attachments[0] = new() { Format = Format.R8G8B8A8Unorm, Samples = SampleCountFlags.Count1Bit, LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store, StencilLoadOp = AttachmentLoadOp.DontCare, StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.ColorAttachmentOptimal, FinalLayout = ImageLayout.ColorAttachmentOptimal };
        attachments[1] = new() { Format = Format.D32Sfloat, Samples = SampleCountFlags.Count1Bit, LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare, StencilLoadOp = AttachmentLoadOp.DontCare, StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined, FinalLayout = ImageLayout.DepthStencilAttachmentOptimal };
        var colorRef = new AttachmentReference(0, ImageLayout.ColorAttachmentOptimal);
        var depthRef = new AttachmentReference(1, ImageLayout.DepthStencilAttachmentOptimal);
        var subpass = new SubpassDescription { PipelineBindPoint = PipelineBindPoint.Graphics, ColorAttachmentCount = 1,
            PColorAttachments = &colorRef, PDepthStencilAttachment = &depthRef };
        // Also serializes reuse of the slot-local depth attachment after its preceding draw.
        var dependency = new SubpassDependency { SrcSubpass = Vk.SubpassExternal, DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.LateFragmentTestsBit,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
            SrcAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit };
        var pass = new RenderPassCreateInfo { SType = StructureType.RenderPassCreateInfo, AttachmentCount = 2, PAttachments = attachments,
            SubpassCount = 1, PSubpasses = &subpass, DependencyCount = 1, PDependencies = &dependency };
        Check(vk.CreateRenderPass(device, in pass, null, out _pass), "CreatePrimitivePass");
        var views = stackalloc ImageView[2] { _colorView, _depthView };
        var framebuffer = new FramebufferCreateInfo { SType = StructureType.FramebufferCreateInfo, RenderPass = _pass,
            AttachmentCount = 2, PAttachments = views, Width = (uint)width, Height = (uint)height, Layers = 1 };
        Check(vk.CreateFramebuffer(device, in framebuffer, null, out _framebuffer), "CreatePrimitiveFramebuffer");
        CreateLayout(default, out _layout);
        CreatePipeline(MeshProfile ? "mesh" : "cube", MeshProfile, false, _layout, ref _pipeline);
    }

    private void EnsureTexturedPipeline()
    {
        if (_texturedPipeline.Handle != 0) return;
        _texturedSetLayout = VulkanTextureUpload.CreateLayout(vk, device);
        try
        {
            CreateLayout(_texturedSetLayout, out _texturedLayout);
            CreatePipeline("mesh-textured", true, true, _texturedLayout, ref _texturedPipeline);
        }
        catch
        {
            ReleaseTexturedPipeline();
            throw;
        }
    }

    private void CreateLayout(DescriptorSetLayout setLayout, out PipelineLayout target)
    {
        var push = new PushConstantRange(ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0, 80);
        var layout = new PipelineLayoutCreateInfo { SType = StructureType.PipelineLayoutCreateInfo, PushConstantRangeCount = 1, PPushConstantRanges = &push,
            SetLayoutCount = setLayout.Handle == 0 ? 0u : 1u, PSetLayouts = &setLayout };
        Check(vk.CreatePipelineLayout(device, in layout, null, out target), "CreatePrimitiveLayout");
    }

    private void CreatePipeline(string shader, bool vertexInput, bool textured, PipelineLayout layout, ref Pipeline target)
    {
        ShaderModule vertex = default, fragment = default;
        try
        {
            vertex = Shader(shader + ".vert.spv"); fragment = Shader(shader + ".frag.spv");
            var name = stackalloc byte[5] { (byte)'m', (byte)'a', (byte)'i', (byte)'n', 0 };
            var stages = stackalloc PipelineShaderStageCreateInfo[2];
            stages[0] = new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.VertexBit, Module = vertex, PName = name };
            stages[1] = new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.FragmentBit, Module = fragment, PName = name };
            var input = new PipelineVertexInputStateCreateInfo { SType = StructureType.PipelineVertexInputStateCreateInfo };
            var bindings = stackalloc VertexInputBindingDescription[2]
                { new(0, 12, VertexInputRate.Vertex), new(1, 8, VertexInputRate.Vertex) };
            var attributes = stackalloc VertexInputAttributeDescription[2]
                { new(0, 0, Format.R32G32B32Sfloat, 0), new(1, 1, Format.R32G32Sfloat, 0) };
            if (vertexInput)
            {
                input.VertexBindingDescriptionCount = textured ? 2u : 1u; input.PVertexBindingDescriptions = bindings;
                input.VertexAttributeDescriptionCount = textured ? 2u : 1u; input.PVertexAttributeDescriptions = attributes;
            }
            var assembly = new PipelineInputAssemblyStateCreateInfo { SType = StructureType.PipelineInputAssemblyStateCreateInfo, Topology = PrimitiveTopology.TriangleList };
            var viewport = new Viewport(0, 0, width, height, 0, 1); var scissor = new Rect2D(default, new((uint)width, (uint)height));
            var viewportState = new PipelineViewportStateCreateInfo { SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1, PViewports = &viewport, ScissorCount = 1, PScissors = &scissor };
            var raster = new PipelineRasterizationStateCreateInfo { SType = StructureType.PipelineRasterizationStateCreateInfo,
                PolygonMode = PolygonMode.Fill, CullMode = CullModeFlags.None, FrontFace = FrontFace.CounterClockwise, LineWidth = 1 };
            var multisample = new PipelineMultisampleStateCreateInfo { SType = StructureType.PipelineMultisampleStateCreateInfo, RasterizationSamples = SampleCountFlags.Count1Bit };
            var depth = new PipelineDepthStencilStateCreateInfo { SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = true, DepthWriteEnable = true, DepthCompareOp = CompareOp.Less };
            var colorBlend = new PipelineColorBlendAttachmentState { ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit };
            var blend = new PipelineColorBlendStateCreateInfo { SType = StructureType.PipelineColorBlendStateCreateInfo, AttachmentCount = 1, PAttachments = &colorBlend };
            var pipeline = new GraphicsPipelineCreateInfo { SType = StructureType.GraphicsPipelineCreateInfo, StageCount = 2, PStages = stages,
                PVertexInputState = &input, PInputAssemblyState = &assembly, PViewportState = &viewportState, PRasterizationState = &raster,
                PMultisampleState = &multisample, PDepthStencilState = &depth, PColorBlendState = &blend, Layout = layout, RenderPass = _pass, BasePipelineIndex = -1 };
            // Even a failed create may return a handle; retain it for owner cleanup.
            fixed (Pipeline* output = &target) Check(vk.CreateGraphicsPipelines(device, default, 1, &pipeline, null, output), "CreatePrimitivePipeline");
        }
        finally
        {
            if (fragment.Handle != 0) vk.DestroyShaderModule(device, fragment, null);
            if (vertex.Handle != 0) vk.DestroyShaderModule(device, vertex, null);
        }
    }

    internal void Draw(CommandBuffer command, PrimitiveScene scene)
    {
        var primitives = scene.Primitives;
        Span<Constants> draws = stackalloc Constants[primitives.Length];
        PrepareCubes(primitives.AsSpan(), scene.CreateViewProjection(width, height), draws);
        var clears = stackalloc ClearValue[2];
        clears[0].Color = new ClearColorValue(0.04f, 0.06f, 0.10f, 1);
        clears[1].DepthStencil = new ClearDepthStencilValue(1, 0);
        var begin = new RenderPassBeginInfo { SType = StructureType.RenderPassBeginInfo, RenderPass = _pass, Framebuffer = _framebuffer,
            RenderArea = new(default, new((uint)width, (uint)height)), ClearValueCount = 2, PClearValues = clears };
        vk.CmdBeginRenderPass(command, in begin, SubpassContents.Inline);
        DrawCubes(command, _pipeline, draws);
        vk.CmdEndRenderPass(command);
    }

    private static void PrepareCubes(ReadOnlySpan<RenderPrimitive> primitives, Matrix4x4 viewProjection, Span<Constants> draws)
    {
        for (var i = 0; i < primitives.Length; i++)
        {
            var transform = primitives[i].World * viewProjection;
            RenderPrimitive.ValidateMatrix(transform);
            draws[i] = new(transform, new(primitives[i].Color, 1));
        }
    }

    private void DrawCubes(CommandBuffer command, Pipeline pipeline, ReadOnlySpan<Constants> draws)
    {
        vk.CmdBindPipeline(command, PipelineBindPoint.Graphics, pipeline);
        foreach (var draw in draws)
        {
            var constants = draw;
            vk.CmdPushConstants(command, _layout, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0, 80, &constants);
            vk.CmdDraw(command, 36, 1, 0, 0);
        }
    }

    internal void DrawSnapshot(CommandBuffer command, RenderSnapshotData snapshot, RenderSnapshotResources resources,
        ImmutableArray<RenderPrimitive> overlay)
    {
        var owner = resources.Device;
        if (!MeshProfile || snapshot.DeviceGeneration != owner.Generation || snapshot.Identity != owner.Identity ||
            snapshot.BindingRevision != resources.BindingRevision || snapshot.ManifestDigest != resources.Manifest.Digest ||
            snapshot.Configuration.Width != width || snapshot.Configuration.Height != height)
            throw new InvalidDataException("Mesh snapshot/device/target mismatch.");
        Span<Constants> cubes = stackalloc Constants[overlay.IsDefaultOrEmpty ? 0 : overlay.Length];
        if (!cubes.IsEmpty)
        {
            PrepareCubes(overlay.AsSpan(), snapshot.ViewProjection, cubes);
            if (_overlayPipeline.Handle == 0)
                CreatePipeline("cube", false, false, _layout, ref _overlayPipeline);
        }
        // Resolve every non-owning handle before recording; the accepted snapshot
        // submission owns the retained uploads until its actual GPU fence completes.
        foreach (var draw in snapshot.Draws)
        {
            var mesh = ResolveMesh(draw);
            if (ResolveTexture(draw) != null)
            {
                if (mesh.IndexOffset == mesh.Uv0Offset)
                    throw new InvalidDataException("Textured mesh has no UV0 stream.");
                EnsureTexturedPipeline();
            }
        }
        var clears = stackalloc ClearValue[2];
        clears[0].Color = new ClearColorValue(snapshot.ClearColorSrgb.X, snapshot.ClearColorSrgb.Y, snapshot.ClearColorSrgb.Z, 1);
        clears[1].DepthStencil = new ClearDepthStencilValue(1, 0);
        var begin = new RenderPassBeginInfo { SType = StructureType.RenderPassBeginInfo, RenderPass = _pass, Framebuffer = _framebuffer,
            RenderArea = new(default, new((uint)width, (uint)height)), ClearValueCount = 2, PClearValues = clears };
        vk.CmdBeginRenderPass(command, in begin, SubpassContents.Inline);
        if (!cubes.IsEmpty) DrawCubes(command, _overlayPipeline, cubes);
        Pipeline boundPipeline = default;
        var buffers = stackalloc Silk.NET.Vulkan.Buffer[2];
        var offsets = stackalloc ulong[2];
        offsets[0] = 0;
        foreach (var draw in snapshot.Draws)
        {
            var upload = ResolveMesh(draw);
            var texture = ResolveTexture(draw);
            var textured = texture != null;
            var pipeline = textured ? _texturedPipeline : _pipeline;
            var layout = textured ? _texturedLayout : _layout;
            if (pipeline.Handle != boundPipeline.Handle)
            {
                vk.CmdBindPipeline(command, PipelineBindPoint.Graphics, pipeline);
                boundPipeline = pipeline;
            }
            buffers[0] = upload.Geometry;
            if (texture != null)
            {
                buffers[1] = upload.Geometry;
                offsets[1] = upload.Uv0Offset;
                var descriptor = texture.Descriptor;
                vk.CmdBindDescriptorSets(command, PipelineBindPoint.Graphics, layout, 0, 1, &descriptor, 0, null);
            }
            vk.CmdBindVertexBuffers(command, 0, textured ? 2u : 1u, buffers, offsets);
            vk.CmdBindIndexBuffer(command, upload.Geometry, upload.IndexOffset, IndexType.Uint32);
            var constants = new Constants(draw.World * snapshot.ViewProjection, new(draw.ColorLinear, 1));
            RenderPrimitive.ValidateMatrix(constants.Transform);
            vk.CmdPushConstants(command, layout, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0, 80, &constants);
            vk.CmdDrawIndexed(command, checked((uint)draw.Slot.IndexCount), 1, checked((uint)draw.Slot.FirstIndex), 0, 0);
        }
        vk.CmdEndRenderPass(command);

        VulkanMeshUpload ResolveMesh(RenderDrawRange draw)
        {
            if (owner.ValidateHandle(draw.Handle) != draw.Mesh || owner.BorrowUpload(draw.Handle) is not VulkanMeshUpload upload ||
                upload.Backend.Device.Handle != device.Handle || !ReferenceEquals(upload.Backend.Api, vk))
                throw new InvalidDataException("Mesh is not retained on this surface device.");
            return upload;
        }

        VulkanTextureUpload? ResolveTexture(RenderDrawRange draw)
        {
            if (!resources.HasBaseColorTextures ||
                ((OpaqueUnlitMaterialDefinition)resources.Manifest.Resolve(draw.Material).Definition).BaseColorTexture is not { } key)
                return null;
            if (owner.BorrowTextureUpload(resources.Handle(key)) is not VulkanTextureUpload upload ||
                upload.Backend.Device.Handle != device.Handle || !ReferenceEquals(upload.Backend.Api, vk))
                throw new InvalidDataException("Texture is not retained on this surface device.");
            return upload;
        }
    }

    private ImageView View(Image image, Format format, ImageAspectFlags aspect)
    {
        var info = new ImageViewCreateInfo { SType = StructureType.ImageViewCreateInfo, Image = image, ViewType = ImageViewType.Type2D,
            Format = format, SubresourceRange = new(aspect, 0, 1, 0, 1) };
        Check(vk.CreateImageView(device, in info, null, out var view), "CreatePrimitiveView"); return view;
    }
    private ShaderModule Shader(string name)
    {
        using var stream = typeof(VulkanPrimitiveRenderer).Assembly.GetManifestResourceStream("TrueMoon.Aether.Runtime.Gpu.Vulkan.Shaders." + name)
            ?? throw new InvalidDataException("Missing embedded shader: " + name);
        var code = new byte[checked((int)stream.Length)]; stream.ReadExactly(code);
        fixed (byte* data = code)
        {
            var info = new ShaderModuleCreateInfo { SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)code.Length, PCode = (uint*)data };
            Check(vk.CreateShaderModule(device, in info, null, out var shader), "CreatePrimitiveShader"); return shader;
        }
    }
    public void Dispose()
    {
        ReleaseTexturedPipeline();
        if (_overlayPipeline.Handle != 0) vk.DestroyPipeline(device, _overlayPipeline, null); _overlayPipeline = default;
        if (_pipeline.Handle != 0) vk.DestroyPipeline(device, _pipeline, null); _pipeline = default;
        if (_layout.Handle != 0) vk.DestroyPipelineLayout(device, _layout, null); _layout = default;
        if (_framebuffer.Handle != 0) vk.DestroyFramebuffer(device, _framebuffer, null); _framebuffer = default;
        if (_pass.Handle != 0) vk.DestroyRenderPass(device, _pass, null); _pass = default;
        if (_depthView.Handle != 0) vk.DestroyImageView(device, _depthView, null); _depthView = default;
        if (_colorView.Handle != 0) vk.DestroyImageView(device, _colorView, null); _colorView = default;
        if (_depth.Handle != 0) vk.DestroyImage(device, _depth, null); _depth = default;
        if (_depthMemory.Handle != 0) vk.FreeMemory(device, _depthMemory, null); _depthMemory = default;
    }

    private void ReleaseTexturedPipeline()
    {
        if (_texturedPipeline.Handle != 0) vk.DestroyPipeline(device, _texturedPipeline, null); _texturedPipeline = default;
        if (_texturedLayout.Handle != 0) vk.DestroyPipelineLayout(device, _texturedLayout, null); _texturedLayout = default;
        if (_texturedSetLayout.Handle != 0) vk.DestroyDescriptorSetLayout(device, _texturedSetLayout, null); _texturedSetLayout = default;
    }
    private static void Check(Result result, string operation) { if (result != Result.Success) throw new IOException(operation + ": " + result); }
    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Constants(Matrix4x4 Transform, Vector4 Color);
}
