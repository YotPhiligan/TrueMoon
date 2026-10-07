using TrueMoon.Alloy.Rendering.Skia;
using Silk.NET.Vulkan;
using SkiaSharp;
using TrueMoon.Alloy.Platform.Silk;

namespace AlloyVulkanTest;

// Independent Vulkan renderer: neither scene drawing nor HUD blending calls Skia.
// The host owns all resources below; the input VkImage is borrowed for one completed submission.
internal sealed unsafe class VulkanHostCompositor : IDisposable
{
    private readonly VulkanDevice _device;
    private readonly uint _width;
    private readonly uint _height;
    private Image _target;
    private DeviceMemory _memory;
    private ImageView _targetView;
    private RenderPass _pass;
    private Framebuffer _framebuffer;
    private Sampler _sampler;
    private DescriptorSetLayout _setLayout;
    private DescriptorPool _descriptorPool;
    private DescriptorSet _set;
    private PipelineLayout _layout;
    private Pipeline _scenePipeline;
    private Pipeline _hudPipeline;
    private CommandPool _commandPool;
    private CommandBuffer _command;
    private bool _disposed;
    private ImageLayout _outputLayout = ImageLayout.Undefined;

    // Borrowed scene output; host operations remain sequential on the device queue.
    internal VulkanTextureInfo Output => !_disposed && _outputLayout != ImageLayout.Undefined
        ? new VulkanTextureInfo
        {
            Image = _target.Handle, Width = _width, Height = _height, Format = (uint)Format.R8G8B8A8Unorm,
            Layout = (uint)_outputLayout, QueueFamily = _device.QueueFamily,
            Usage = (uint)(ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit),
            SampleCount = 1, LevelCount = 1
        }
        : throw new InvalidOperationException("Compose a live scene before borrowing its output.");
    internal void OutputStateChanged(ImageLayout layout) => _outputLayout = layout;

    internal VulkanHostCompositor(VulkanDevice device, int width, int height)
    {
        _device = device;
        _width = checked((uint)width);
        _height = checked((uint)height);
        try { Initialize(); }
        catch { Dispose(); throw; }
    }

    internal static (SKColor Left, SKColor Right) SceneColors(int variant) => variant == 0
        ? (new SKColor(20, 40, 100), new SKColor(120, 30, 10))
        : (new SKColor(90, 150, 30), new SKColor(15, 55, 170));

    private void Initialize()
    {
        var vk = _device.Api;
        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo, ImageType = ImageType.Type2D,
            Format = Format.R8G8B8A8Unorm, Extent = new Extent3D(_width, _height, 1),
            MipLevels = 1, ArrayLayers = 1, Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal, Usage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit,
            SharingMode = SharingMode.Exclusive, InitialLayout = ImageLayout.Undefined
        };
        VulkanDevice.Check(vk.CreateImage(_device.Device, in imageInfo, null, out _target), "CreateSceneImage");
        vk.GetImageMemoryRequirements(_device.Device, _target, out var requirements);
        vk.GetPhysicalDeviceMemoryProperties(_device.PhysicalDevice, out var properties);
        uint? type = null;
        for (uint i = 0; i < properties.MemoryTypeCount; i++)
            if ((requirements.MemoryTypeBits & (1u << (int)i)) != 0 &&
                (properties.MemoryTypes[(int)i].PropertyFlags & MemoryPropertyFlags.DeviceLocalBit) != 0)
            { type = i; break; }
        var allocation = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo, AllocationSize = requirements.Size,
            MemoryTypeIndex = type ?? throw new NotSupportedException("No device-local scene memory.")
        };
        VulkanDevice.Check(vk.AllocateMemory(_device.Device, in allocation, null, out _memory), "AllocateSceneMemory");
        VulkanDevice.Check(vk.BindImageMemory(_device.Device, _target, _memory, 0), "BindSceneMemory");
        _targetView = CreateView(_target);
        var attachment = new AttachmentDescription
        {
            Format = Format.R8G8B8A8Unorm, Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.DontCare, StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare, StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined, FinalLayout = ImageLayout.ColorAttachmentOptimal
        };
        var reference = new AttachmentReference(0, ImageLayout.ColorAttachmentOptimal);
        var subpass = new SubpassDescription
        { PipelineBindPoint = PipelineBindPoint.Graphics, ColorAttachmentCount = 1, PColorAttachments = &reference };
        var dependency = new SubpassDependency
        {
            SrcSubpass = Vk.SubpassExternal, DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.AllCommandsBit, DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = AccessFlags.MemoryReadBit | AccessFlags.MemoryWriteBit,
            DstAccessMask = AccessFlags.ColorAttachmentReadBit | AccessFlags.ColorAttachmentWriteBit
        };
        var passInfo = new RenderPassCreateInfo
        {
            SType = StructureType.RenderPassCreateInfo, AttachmentCount = 1, PAttachments = &attachment,
            SubpassCount = 1, PSubpasses = &subpass, DependencyCount = 1, PDependencies = &dependency
        };
        VulkanDevice.Check(vk.CreateRenderPass(_device.Device, in passInfo, null, out _pass), "CreateSceneRenderPass");
        var view = _targetView;
        var framebufferInfo = new FramebufferCreateInfo
        {
            SType = StructureType.FramebufferCreateInfo, RenderPass = _pass,
            AttachmentCount = 1, PAttachments = &view, Width = _width, Height = _height, Layers = 1
        };
        VulkanDevice.Check(vk.CreateFramebuffer(_device.Device, in framebufferInfo, null, out _framebuffer), "CreateSceneFramebuffer");
        var samplerInfo = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo, MagFilter = Filter.Nearest, MinFilter = Filter.Nearest,
            MipmapMode = SamplerMipmapMode.Nearest, AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge, AddressModeW = SamplerAddressMode.ClampToEdge
        };
        VulkanDevice.Check(vk.CreateSampler(_device.Device, in samplerInfo, null, out _sampler), "CreateHudSampler");
        var binding = new DescriptorSetLayoutBinding
        { Binding = 0, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit };
        var setInfo = new DescriptorSetLayoutCreateInfo
        { SType = StructureType.DescriptorSetLayoutCreateInfo, BindingCount = 1, PBindings = &binding };
        VulkanDevice.Check(vk.CreateDescriptorSetLayout(_device.Device, in setInfo, null, out _setLayout), "CreateHudSetLayout");
        var poolSize = new DescriptorPoolSize(DescriptorType.CombinedImageSampler, 1);
        var descriptorInfo = new DescriptorPoolCreateInfo
        { SType = StructureType.DescriptorPoolCreateInfo, MaxSets = 1, PoolSizeCount = 1, PPoolSizes = &poolSize };
        VulkanDevice.Check(vk.CreateDescriptorPool(_device.Device, in descriptorInfo, null, out _descriptorPool), "CreateHudDescriptorPool");
        var setLayout = _setLayout;
        var allocateSet = new DescriptorSetAllocateInfo
        { SType = StructureType.DescriptorSetAllocateInfo, DescriptorPool = _descriptorPool, DescriptorSetCount = 1, PSetLayouts = &setLayout };
        VulkanDevice.Check(vk.AllocateDescriptorSets(_device.Device, in allocateSet, out _set), "AllocateHudSet");
        var push = new PushConstantRange(ShaderStageFlags.FragmentBit, 0, 32);
        var layoutInfo = new PipelineLayoutCreateInfo
        { SType = StructureType.PipelineLayoutCreateInfo, SetLayoutCount = 1, PSetLayouts = &setLayout, PushConstantRangeCount = 1, PPushConstantRanges = &push };
        VulkanDevice.Check(vk.CreatePipelineLayout(_device.Device, in layoutInfo, null, out _layout), "CreateScenePipelineLayout");
        ShaderModule vertex = default, scene = default, hud = default;
        try
        {
            vertex = LoadShader("fullscreen.vert.spv");
            scene = LoadShader("scene.frag.spv");
            hud = LoadShader("hud.frag.spv");
            _scenePipeline = CreatePipeline(vertex, scene, false);
            _hudPipeline = CreatePipeline(vertex, hud, true);
        }
        finally
        {
            if (hud.Handle != 0) vk.DestroyShaderModule(_device.Device, hud, null);
            if (scene.Handle != 0) vk.DestroyShaderModule(_device.Device, scene, null);
            if (vertex.Handle != 0) vk.DestroyShaderModule(_device.Device, vertex, null);
        }
        var commandInfo = new CommandPoolCreateInfo
        { SType = StructureType.CommandPoolCreateInfo, QueueFamilyIndex = _device.QueueFamily, Flags = CommandPoolCreateFlags.TransientBit };
        VulkanDevice.Check(vk.CreateCommandPool(_device.Device, in commandInfo, null, out _commandPool), "CreateCompositionCommandPool");
        var commandAllocation = new CommandBufferAllocateInfo
        { SType = StructureType.CommandBufferAllocateInfo, CommandPool = _commandPool, Level = CommandBufferLevel.Primary, CommandBufferCount = 1 };
        VulkanDevice.Check(vk.AllocateCommandBuffers(_device.Device, in commandAllocation, out _command), "AllocateCompositionCommands");
    }

    private ImageView CreateView(Image image)
    {
        var info = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo, Image = image, ViewType = ImageViewType.Type2D,
            Format = Format.R8G8B8A8Unorm, SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };
        VulkanDevice.Check(_device.Api.CreateImageView(_device.Device, in info, null, out var view), "CreateCompositionImageView");
        return view;
    }

    private ShaderModule LoadShader(string name)
    {
        using var stream = typeof(VulkanHostCompositor).Assembly.GetManifestResourceStream($"AlloyVulkanTest.Shaders.{name}")
            ?? throw new InvalidOperationException($"Missing embedded shader: {name}");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        var code = bytes.ToArray();
        if (code.Length == 0 || code.Length % 4 != 0) throw new InvalidOperationException("Invalid SPIR-V size.");
        fixed (byte* pointer = code)
        {
            var info = new ShaderModuleCreateInfo
            { SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)code.Length, PCode = (uint*)pointer };
            VulkanDevice.Check(_device.Api.CreateShaderModule(_device.Device, in info, null, out var module), "CreateCompositionShader");
            return module;
        }
    }

    private Pipeline CreatePipeline(ShaderModule vertex, ShaderModule fragment, bool blend)
    {
        ReadOnlySpan<byte> main = "main\0"u8;
        fixed (byte* entry = main)
        {
            var stages = stackalloc PipelineShaderStageCreateInfo[2];
            stages[0] = new PipelineShaderStageCreateInfo
            { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.VertexBit, Module = vertex, PName = entry };
            stages[1] = new PipelineShaderStageCreateInfo
            { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.FragmentBit, Module = fragment, PName = entry };
            var vertexInput = new PipelineVertexInputStateCreateInfo { SType = StructureType.PipelineVertexInputStateCreateInfo };
            var assembly = new PipelineInputAssemblyStateCreateInfo
            { SType = StructureType.PipelineInputAssemblyStateCreateInfo, Topology = PrimitiveTopology.TriangleList };
            var viewport = new Viewport(0, 0, _width, _height, 0, 1);
            var scissor = new Rect2D(new Offset2D(0, 0), new Extent2D(_width, _height));
            var viewportInfo = new PipelineViewportStateCreateInfo
            { SType = StructureType.PipelineViewportStateCreateInfo, ViewportCount = 1, PViewports = &viewport, ScissorCount = 1, PScissors = &scissor };
            var raster = new PipelineRasterizationStateCreateInfo
            { SType = StructureType.PipelineRasterizationStateCreateInfo, PolygonMode = PolygonMode.Fill, CullMode = CullModeFlags.None, FrontFace = FrontFace.CounterClockwise, LineWidth = 1 };
            var multisample = new PipelineMultisampleStateCreateInfo
            { SType = StructureType.PipelineMultisampleStateCreateInfo, RasterizationSamples = SampleCountFlags.Count1Bit };
            // Skia supplies premultiplied RGB: src*ONE + dst*(1-srcAlpha), including alpha.
            var colorAttachment = new PipelineColorBlendAttachmentState
            {
                BlendEnable = blend, SrcColorBlendFactor = BlendFactor.One, DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                ColorBlendOp = BlendOp.Add, SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha, AlphaBlendOp = BlendOp.Add,
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit
            };
            var colorBlend = new PipelineColorBlendStateCreateInfo
            { SType = StructureType.PipelineColorBlendStateCreateInfo, AttachmentCount = 1, PAttachments = &colorAttachment };
            var info = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo, StageCount = 2, PStages = stages,
                PVertexInputState = &vertexInput, PInputAssemblyState = &assembly, PViewportState = &viewportInfo,
                PRasterizationState = &raster, PMultisampleState = &multisample, PColorBlendState = &colorBlend,
                Layout = _layout, RenderPass = _pass, Subpass = 0, BasePipelineIndex = -1
            };
            Pipeline pipeline = default;
            var result = _device.Api.CreateGraphicsPipelines(_device.Device, default, 1, &info, null, &pipeline);
            if (result != Result.Success && pipeline.Handle != 0) _device.Api.DestroyPipeline(_device.Device, pipeline, null);
            VulkanDevice.Check(result, "CreateCompositionPipeline");
            return pipeline;
        }
    }

    internal byte[] Compose(VulkanTextureInfo info, int variant, Action<ImageLayout> stateChanged, bool readback = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (info.Width != _width || info.Height != _height || info.Format != (uint)Format.R8G8B8A8Unorm ||
            info.SampleCount != 1 || info.LevelCount != 1 || (info.Usage & (uint)ImageUsageFlags.SampledBit) == 0 ||
            (info.QueueFamily != Vk.QueueFamilyIgnored && info.QueueFamily != _device.QueueFamily))
            throw new NotSupportedException("Composition requires a sampled RGBA8888 texture on the host queue.");
        var vk = _device.Api;
        var hudImage = new Image(info.Image);
        var hudView = CreateView(hudImage);
        var submitted = false;
        try
        {
            var descriptorImage = new DescriptorImageInfo(_sampler, hudView, ImageLayout.ShaderReadOnlyOptimal);
            var write = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet, DstSet = _set, DstBinding = 0,
                DescriptorCount = 1, DescriptorType = DescriptorType.CombinedImageSampler, PImageInfo = &descriptorImage
            };
            vk.UpdateDescriptorSets(_device.Device, 1, &write, 0, null);
            VulkanDevice.Check(vk.ResetCommandPool(_device.Device, _commandPool, 0), "ResetCompositionCommands");
            var command = _command;
            var begin = new CommandBufferBeginInfo
            { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
            VulkanDevice.Check(vk.BeginCommandBuffer(command, in begin), "BeginCompositionCommands");
            var barrier = new ImageMemoryBarrier
            {
                SType = StructureType.ImageMemoryBarrier, SrcAccessMask = AccessFlags.MemoryWriteBit | AccessFlags.MemoryReadBit,
                DstAccessMask = AccessFlags.ShaderReadBit, OldLayout = (ImageLayout)info.Layout, NewLayout = ImageLayout.ShaderReadOnlyOptimal,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored, DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = hudImage, SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
            };
            vk.CmdPipelineBarrier(command, PipelineStageFlags.AllCommandsBit, PipelineStageFlags.FragmentShaderBit,
                0, 0, null, 0, null, 1, &barrier);
            var render = new RenderPassBeginInfo
            { SType = StructureType.RenderPassBeginInfo, RenderPass = _pass, Framebuffer = _framebuffer, RenderArea = new Rect2D(new Offset2D(0, 0), new Extent2D(_width, _height)) };
            vk.CmdBeginRenderPass(command, in render, SubpassContents.Inline);
            vk.CmdBindPipeline(command, PipelineBindPoint.Graphics, _scenePipeline);
            var colors = SceneColors(variant);
            var push = stackalloc float[8] { colors.Left.Red / 255f, colors.Left.Green / 255f, colors.Left.Blue / 255f, 1,
                colors.Right.Red / 255f, colors.Right.Green / 255f, colors.Right.Blue / 255f, 1 };
            vk.CmdPushConstants(command, _layout, ShaderStageFlags.FragmentBit, 0, 32, push);
            vk.CmdDraw(command, 3, 1, 0, 0);
            vk.CmdBindPipeline(command, PipelineBindPoint.Graphics, _hudPipeline);
            var set = _set;
            vk.CmdBindDescriptorSets(command, PipelineBindPoint.Graphics, _layout, 0, 1, &set, 0, null);
            vk.CmdDraw(command, 3, 1, 0, 0);
            vk.CmdEndRenderPass(command);
            VulkanDevice.Check(vk.EndCommandBuffer(command), "EndCompositionCommands");
            var submit = new SubmitInfo { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &command };
            VulkanDevice.Check(vk.QueueSubmit(_device.Queue, 1, &submit, default), "SubmitComposition");
            submitted = true;
            _device.WaitIdle();
            stateChanged(ImageLayout.ShaderReadOnlyOptimal);
            _outputLayout = ImageLayout.ColorAttachmentOptimal;
            // Presentation can consume Output directly; CPU transfer is assertion-only.
            return readback ? VulkanHostReadback.Read(_device, Output, OutputStateChanged) : [];
        }
        finally
        {
            if (submitted) vk.DeviceWaitIdle(_device.Device);
            vk.DestroyImageView(_device.Device, hudView, null);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var vk = _device.Api;
        vk.DeviceWaitIdle(_device.Device);
        if (_commandPool.Handle != 0) vk.DestroyCommandPool(_device.Device, _commandPool, null);
        if (_hudPipeline.Handle != 0) vk.DestroyPipeline(_device.Device, _hudPipeline, null);
        if (_scenePipeline.Handle != 0) vk.DestroyPipeline(_device.Device, _scenePipeline, null);
        if (_layout.Handle != 0) vk.DestroyPipelineLayout(_device.Device, _layout, null);
        if (_descriptorPool.Handle != 0) vk.DestroyDescriptorPool(_device.Device, _descriptorPool, null);
        if (_setLayout.Handle != 0) vk.DestroyDescriptorSetLayout(_device.Device, _setLayout, null);
        if (_sampler.Handle != 0) vk.DestroySampler(_device.Device, _sampler, null);
        if (_framebuffer.Handle != 0) vk.DestroyFramebuffer(_device.Device, _framebuffer, null);
        if (_pass.Handle != 0) vk.DestroyRenderPass(_device.Device, _pass, null);
        if (_targetView.Handle != 0) vk.DestroyImageView(_device.Device, _targetView, null);
        if (_target.Handle != 0) vk.DestroyImage(_device.Device, _target, null);
        if (_memory.Handle != 0) vk.FreeMemory(_device.Device, _memory, null);
    }
}
