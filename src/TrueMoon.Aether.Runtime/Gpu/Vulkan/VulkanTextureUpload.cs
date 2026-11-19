using Silk.NET.Vulkan;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Runtime.Rendering.Resources;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace TrueMoon.Aether.Runtime.Gpu.Vulkan;

// Kept by the counted device cache, including after partial preparation or an unconfirmed queue outcome.
internal sealed class VulkanTextureUpload(VulkanRenderResourceBackend backend, RenderTextureUploadRequest request) : IRenderTextureUpload
{
    private readonly Lock _gate = new();
    private Task? _job;
    private Image _image;
    private DeviceMemory _imageMemory, _stagingMemory;
    private VkBuffer _staging;
    private CommandBuffer _command;
    private Fence _fence;
    private bool _submitted;
    private ImageView _view;
    private Sampler _sampler;
    private DescriptorSetLayout _setLayout;
    private DescriptorPool _descriptorPool;
    private DescriptorSet _descriptor;
    private RenderAllocationReservation? _imageReservation, _stagingReservation;
    public bool IsReady { get; private set; }
    public bool IsReleased { get; private set; }
    internal VulkanRenderResourceBackend Backend => backend;
    internal DescriptorSet Descriptor => IsReady && !IsReleased ? _descriptor : throw new InvalidOperationException("Texture upload is not live.");

    public Task PrepareAsync(CancellationToken cancellationToken)
    {
        lock (_gate) return _job ??= PrepareCoreAsync(cancellationToken);
    }

    private async Task PrepareCoreAsync(CancellationToken token)
    {
        await backend.NativeGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                backend.EnsureContext();
                Upload(token);
            }, CancellationToken.None).ConfigureAwait(false);
        }
        finally { backend.NativeGate.Release(); }
    }

    private unsafe void Upload(CancellationToken token)
    {
        var vk = backend.Api!;
        var device = backend.Device;
        var texture = (BaseColorTextureDefinition)request.Texture.Definition;
        const Format format = Format.R8G8B8A8Srgb;
        vk.GetPhysicalDeviceFormatProperties(backend.Physical, format, out var properties);
        const FormatFeatureFlags required = FormatFeatureFlags.SampledImageBit | FormatFeatureFlags.TransferDstBit;
        if ((properties.OptimalTilingFeatures & required) != required)
            throw new NotSupportedException("Vulkan optimal RGBA8 sRGB sampling and transfer destination support is required.");

        var extent = new Extent3D((uint)texture.Width, (uint)texture.Height, 1);
        var image = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo, ImageType = ImageType.Type2D, Format = format, Extent = extent,
            MipLevels = 1, ArrayLayers = 1, Samples = SampleCountFlags.Count1Bit, Tiling = ImageTiling.Optimal,
            Usage = ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit, SharingMode = SharingMode.Exclusive
        };
        VulkanRenderResourceBackend.Check(vk.CreateImage(device, in image, null, out _image), "CreateBaseColorImage");
        Interlocked.Increment(ref backend.TextureImagesCreated);
        vk.GetImageMemoryRequirements(device, _image, out var imageMemory);
        _imageReservation = request.Budget.Reserve(checked((long)imageMemory.Size), 0, 0);
        var allocate = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo, AllocationSize = imageMemory.Size,
            MemoryTypeIndex = backend.MemoryType(imageMemory.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
        };
        VulkanRenderResourceBackend.Check(vk.AllocateMemory(device, in allocate, null, out _imageMemory), "AllocateBaseColorImage");
        Interlocked.Increment(ref backend.AllocationsCreated);
        VulkanRenderResourceBackend.Check(vk.BindImageMemory(device, _image, _imageMemory, 0), "BindBaseColorImage");

        var length = (ulong)texture.Pixels.Length;
        var staging = new BufferCreateInfo { SType = StructureType.BufferCreateInfo, Size = length,
            Usage = BufferUsageFlags.TransferSrcBit, SharingMode = SharingMode.Exclusive };
        VulkanRenderResourceBackend.Check(vk.CreateBuffer(device, in staging, null, out _staging), "CreateTextureStaging");
        Interlocked.Increment(ref backend.BuffersCreated);
        vk.GetBufferMemoryRequirements(device, _staging, out var stagingMemory);
        _stagingReservation = request.Budget.Reserve(0, checked((long)stagingMemory.Size), 0);
        allocate.AllocationSize = stagingMemory.Size;
        allocate.MemoryTypeIndex = backend.MemoryType(stagingMemory.MemoryTypeBits,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        VulkanRenderResourceBackend.Check(vk.AllocateMemory(device, in allocate, null, out _stagingMemory), "AllocateTextureStaging");
        Interlocked.Increment(ref backend.AllocationsCreated);
        VulkanRenderResourceBackend.Check(vk.BindBufferMemory(device, _staging, _stagingMemory, 0), "BindTextureStaging");
        void* mapped = null;
        VulkanRenderResourceBackend.Check(vk.MapMemory(device, _stagingMemory, 0, length, 0, &mapped), "MapTextureStaging");
        try { texture.Pixels.AsSpan().CopyTo(new Span<byte>(mapped, texture.Pixels.Length)); }
        finally { vk.UnmapMemory(device, _stagingMemory); }

        token.ThrowIfCancellationRequested();
        CopyToImage(extent);
        ReleaseStaging();
        token.ThrowIfCancellationRequested();
        CreateBinding(format);
        token.ThrowIfCancellationRequested();
        backend.TextureUploaded(request.Texture.Key);
        IsReady = true;
    }

    private unsafe void CopyToImage(Extent3D extent)
    {
        var vk = backend.Api!;
        var device = backend.Device;
        var allocate = new CommandBufferAllocateInfo { SType = StructureType.CommandBufferAllocateInfo, CommandPool = backend.Pool,
            Level = CommandBufferLevel.Primary, CommandBufferCount = 1 };
        fixed (CommandBuffer* target = &_command)
            VulkanRenderResourceBackend.Check(vk.AllocateCommandBuffers(device, in allocate, target), "AllocateTextureCommand");
        var fenceInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
        VulkanRenderResourceBackend.Check(vk.CreateFence(device, in fenceInfo, null, out _fence), "CreateTextureFence");
        var begin = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
        VulkanRenderResourceBackend.Check(vk.BeginCommandBuffer(_command, in begin), "BeginTextureCommand");
        var transition = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier, Image = _image, SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
            OldLayout = ImageLayout.Undefined, NewLayout = ImageLayout.TransferDstOptimal, DstAccessMask = AccessFlags.TransferWriteBit,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored, DstQueueFamilyIndex = Vk.QueueFamilyIgnored
        };
        vk.CmdPipelineBarrier(_command, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 1, &transition);
        var copy = new BufferImageCopy { ImageSubresource = new(ImageAspectFlags.ColorBit, 0, 0, 1), ImageExtent = extent };
        vk.CmdCopyBufferToImage(_command, _staging, _image, ImageLayout.TransferDstOptimal, 1, &copy);
        transition.OldLayout = ImageLayout.TransferDstOptimal;
        transition.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
        transition.SrcAccessMask = AccessFlags.TransferWriteBit;
        transition.DstAccessMask = AccessFlags.ShaderReadBit;
        vk.CmdPipelineBarrier(_command, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0, 0, null, 0, null, 1, &transition);
        VulkanRenderResourceBackend.Check(vk.EndCommandBuffer(_command), "EndTextureCommand");
        var command = _command;
        var submit = new SubmitInfo { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &command };
        _submitted = true;
        var result = vk.QueueSubmit(backend.Queue, 1, in submit, _fence);
        ResourceGuard.Require(result == Result.Success, RenderResourceError.UnknownOutcome, "Texture submission outcome is unconfirmed: " + result);
        var fence = _fence;
        result = vk.WaitForFences(device, 1, &fence, true, 5_000_000_000);
        ResourceGuard.Require(result == Result.Success, RenderResourceError.UnknownOutcome, "Texture upload completion is unconfirmed: " + result);
        _submitted = false;
    }

    internal static unsafe DescriptorSetLayout CreateLayout(Vk vk, Device device)
    {
        var binding = new DescriptorSetLayoutBinding { Binding = 0, DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit };
        var layout = new DescriptorSetLayoutCreateInfo { SType = StructureType.DescriptorSetLayoutCreateInfo, BindingCount = 1, PBindings = &binding };
        VulkanRenderResourceBackend.Check(vk.CreateDescriptorSetLayout(device, in layout, null, out var result), "CreateBaseColorDescriptorLayout");
        return result;
    }

    private unsafe void CreateBinding(Format format)
    {
        var vk = backend.Api!;
        var device = backend.Device;
        var view = new ImageViewCreateInfo { SType = StructureType.ImageViewCreateInfo, Image = _image, Format = format,
            ViewType = ImageViewType.Type2D, SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1) };
        VulkanRenderResourceBackend.Check(vk.CreateImageView(device, in view, null, out _view), "CreateBaseColorView");
        var sampler = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo, MinFilter = Filter.Nearest, MagFilter = Filter.Nearest,
            MipmapMode = SamplerMipmapMode.Nearest, AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge, AddressModeW = SamplerAddressMode.ClampToEdge
        };
        VulkanRenderResourceBackend.Check(vk.CreateSampler(device, in sampler, null, out _sampler), "CreateBaseColorSampler");
        Interlocked.Increment(ref backend.TextureSamplersCreated);
        _setLayout = CreateLayout(vk, device);
        var size = new DescriptorPoolSize(DescriptorType.CombinedImageSampler, 1);
        var pool = new DescriptorPoolCreateInfo { SType = StructureType.DescriptorPoolCreateInfo, MaxSets = 1, PoolSizeCount = 1, PPoolSizes = &size };
        VulkanRenderResourceBackend.Check(vk.CreateDescriptorPool(device, in pool, null, out _descriptorPool), "CreateBaseColorDescriptorPool");
        var setLayout = _setLayout;
        var set = new DescriptorSetAllocateInfo { SType = StructureType.DescriptorSetAllocateInfo, DescriptorPool = _descriptorPool,
            DescriptorSetCount = 1, PSetLayouts = &setLayout };
        fixed (DescriptorSet* result = &_descriptor)
            VulkanRenderResourceBackend.Check(vk.AllocateDescriptorSets(device, in set, result), "AllocateBaseColorDescriptor");
        var image = new DescriptorImageInfo(_sampler, _view, ImageLayout.ShaderReadOnlyOptimal);
        var write = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _descriptor, DstBinding = 0,
            DescriptorCount = 1, DescriptorType = DescriptorType.CombinedImageSampler, PImageInfo = &image };
        vk.UpdateDescriptorSets(device, 1, &write, 0, null);
    }

    public async ValueTask DisposeAsync()
    {
        Task? job;
        lock (_gate) job = _job;
        if (job != null) { try { await job.ConfigureAwait(false); } catch { /* The owner still retains partial preparation. */ } }
        await backend.NativeGate.WaitAsync().ConfigureAwait(false);
        try { if (!IsReleased) ReleaseNative(); }
        finally { backend.NativeGate.Release(); }
    }

    private unsafe void ReleaseStaging()
    {
        var vk = backend.Api!;
        var device = backend.Device;
        if (_command.Handle != 0) { var command = _command; vk.FreeCommandBuffers(device, backend.Pool, 1, &command); _command = default; }
        if (_fence.Handle != 0) { vk.DestroyFence(device, _fence, null); _fence = default; }
        if (_staging.Handle != 0) { vk.DestroyBuffer(device, _staging, null); _staging = default; Interlocked.Increment(ref backend.BuffersReleased); }
        if (_stagingMemory.Handle != 0) { vk.FreeMemory(device, _stagingMemory, null); _stagingMemory = default; Interlocked.Increment(ref backend.AllocationsReleased); }
        _stagingReservation?.Dispose();
        _stagingReservation = null;
    }

    private unsafe void ReleaseNative()
    {
        if (backend.Api is { } vk && backend.Device.Handle != 0)
        {
            var device = backend.Device;
            if (_submitted)
            {
                var idle = vk.DeviceWaitIdle(device);
                ResourceGuard.Require(idle == Result.Success, RenderResourceError.UnknownOutcome, "Texture upload is still owned: " + idle);
                _submitted = false;
            }
            ReleaseStaging();
            if (_descriptorPool.Handle != 0) vk.DestroyDescriptorPool(device, _descriptorPool, null);
            if (_setLayout.Handle != 0) vk.DestroyDescriptorSetLayout(device, _setLayout, null);
            if (_sampler.Handle != 0) { vk.DestroySampler(device, _sampler, null); Interlocked.Increment(ref backend.TextureSamplersReleased); }
            if (_view.Handle != 0) vk.DestroyImageView(device, _view, null);
            if (_image.Handle != 0) { vk.DestroyImage(device, _image, null); Interlocked.Increment(ref backend.TextureImagesReleased); }
            if (_imageMemory.Handle != 0) { vk.FreeMemory(device, _imageMemory, null); Interlocked.Increment(ref backend.AllocationsReleased); }
        }
        _imageReservation?.Dispose();
        _imageReservation = null;
        IsReady = false;
        IsReleased = true;
        backend.Released(this);
    }
}
