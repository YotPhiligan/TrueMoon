using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace TrueMoon.Alloy.Hosting;

/// <summary>Sequential Vulkan presentation: blits a borrowed UI or composed scene image into the window swapchain.</summary>
public sealed unsafe class VulkanWindowPresenter : IDisposable
{
    private readonly VulkanDevice _device;
    private readonly KhrSurface _surfaceApi;
    private readonly KhrSwapchain _swapchainApi;
    private SwapchainKHR _swapchain;
    private Image[] _images = [];
    private Semaphore[] _presentReady = [];
    private readonly List<RetiredGeneration> _retired = [];
    private Fence[] _presentFences = [];
    private bool[] _pendingFences = [], _presented = [];
    private int _liveSwapchains, _liveSemaphores, _liveFences, _livePools, _peakSwapchains, _created, _destroyed, _confirmed;
    private sealed record RetiredGeneration(SwapchainKHR Swapchain, Semaphore[] Semaphores, Fence[] Fences, bool[] PendingFences);
    /// <summary>Whether the device provides explicit fences for presentation-resource completion.</summary>
    public bool UsesPresentFences => _device.PresentFenceExtension != null;
    /// <summary>Resource accounting, also available after disposal for host diagnostics.</summary>
    public VulkanPresentationResources Resources => new(_liveSwapchains, _liveSemaphores, _liveFences, _livePools,
        _retired.Count, _peakSwapchains, _created, _destroyed, _confirmed);
    private Semaphore _acquired;
    private CommandPool _pool;
    private CommandBuffer _command;
    private Extent2D _extent;
    private bool _recreate = true, _disposed;
    /// <summary>Number of successful presentations.</summary>
    public int PresentedFrames { get; private set; }
    /// <summary>Number of successful swapchain generations.</summary>
    public int SwapchainGenerations { get; private set; }
    /// <summary>Uses a borrowed device created for the window surface. The caller disposes it last.</summary>
    public VulkanWindowPresenter(VulkanDevice device)
    {
        _device = device;
        if (device.Surface.Handle == 0) throw new ArgumentException("A window surface is required.", nameof(device));
        if (!device.Api.TryGetInstanceExtension(device.Instance, out _surfaceApi)) throw new NotSupportedException("VK_KHR_surface is unavailable.");
        try
        {
            if (!device.Api.TryGetDeviceExtension(device.Instance, device.Device, out _swapchainApi)) throw new NotSupportedException("VK_KHR_swapchain is unavailable.");
            var semaphoreInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
            VulkanDevice.Check(device.Api.CreateSemaphore(device.Device, in semaphoreInfo, null, out _acquired), "CreateAcquireSemaphore");
            _liveSemaphores++;
            var poolInfo = new CommandPoolCreateInfo
            { SType = StructureType.CommandPoolCreateInfo, QueueFamilyIndex = device.QueueFamily, Flags = CommandPoolCreateFlags.TransientBit };
            VulkanDevice.Check(device.Api.CreateCommandPool(device.Device, in poolInfo, null, out _pool), "CreatePresentCommandPool");
            _livePools++;
            var allocate = new CommandBufferAllocateInfo
            { SType = StructureType.CommandBufferAllocateInfo, CommandPool = _pool, CommandBufferCount = 1, Level = CommandBufferLevel.Primary };
            VulkanDevice.Check(device.Api.AllocateCommandBuffers(device.Device, in allocate, out _command), "AllocatePresentCommands");
        }
        catch { Dispose(); throw; }
    }
    /// <summary>Requests swapchain recreation on the next nonzero frame.</summary>
    public void InvalidateSwapchain() => _recreate = true;

    private bool Recreate(UiViewport viewport)
    {
        var vk = _device.Api;
        _device.WaitIdle();
        VulkanDevice.Check(_surfaceApi.GetPhysicalDeviceSurfaceCapabilities(_device.PhysicalDevice, _device.Surface, out var capabilities), "SurfaceCapabilities");
        if ((capabilities.SupportedUsageFlags & ImageUsageFlags.TransferDstBit) == 0)
            throw new NotSupportedException("Surface must support GPU transfer destination presentation.");
        uint count = 0;
        VulkanDevice.Check(_surfaceApi.GetPhysicalDeviceSurfaceFormats(_device.PhysicalDevice, _device.Surface, ref count, null), "SurfaceFormats");
        var formats = new SurfaceFormatKHR[count];
        fixed (SurfaceFormatKHR* ptr = formats)
            VulkanDevice.Check(_surfaceApi.GetPhysicalDeviceSurfaceFormats(_device.PhysicalDevice, _device.Surface, ref count, ptr), "SurfaceFormats");
        var format = formats.FirstOrDefault(f => f.Format == Format.B8G8R8A8Unorm && f.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr);
        if (format.Format == Format.Undefined) format = formats.FirstOrDefault(f => f.Format == Format.R8G8B8A8Unorm);
        if (format.Format == Format.Undefined) throw new NotSupportedException("No UNORM RGBA/BGRA swapchain format.");
        vk.GetPhysicalDeviceFormatProperties(_device.PhysicalDevice, Format.R8G8B8A8Unorm, out var sourceProperties);
        vk.GetPhysicalDeviceFormatProperties(_device.PhysicalDevice, format.Format, out var destinationProperties);
        if ((sourceProperties.OptimalTilingFeatures & FormatFeatureFlags.BlitSrcBit) == 0 ||
            (destinationProperties.OptimalTilingFeatures & FormatFeatureFlags.BlitDstBit) == 0)
            throw new NotSupportedException("Device must support RGBA/BGRA blitting.");
        _extent = capabilities.CurrentExtent.Width != uint.MaxValue ? capabilities.CurrentExtent : new Extent2D(
            Math.Clamp((uint)viewport.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width),
            Math.Clamp((uint)viewport.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height));
        if (_extent.Width == 0 || _extent.Height == 0) return false;
        var imageCount = capabilities.MinImageCount + 1;
        if (capabilities.MaxImageCount != 0) imageCount = Math.Min(imageCount, capabilities.MaxImageCount);
        var alpha = new[] { CompositeAlphaFlagsKHR.OpaqueBitKhr, CompositeAlphaFlagsKHR.PreMultipliedBitKhr,
            CompositeAlphaFlagsKHR.PostMultipliedBitKhr, CompositeAlphaFlagsKHR.InheritBitKhr }
            .First(a => (capabilities.SupportedCompositeAlpha & a) != 0);
        var create = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr, Surface = _device.Surface, MinImageCount = imageCount,
            ImageFormat = format.Format, ImageColorSpace = format.ColorSpace, ImageExtent = _extent, ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.TransferDstBit, ImageSharingMode = SharingMode.Exclusive,
            PreTransform = capabilities.CurrentTransform, CompositeAlpha = alpha, PresentMode = PresentModeKHR.FifoKhr,
            Clipped = true, OldSwapchain = _swapchain
        };
        VulkanDevice.Check(_swapchainApi.CreateSwapchain(_device.Device, in create, null, out var replacement), "CreateSwapchain");
        _liveSwapchains++; _created++; _peakSwapchains = Math.Max(_peakSwapchains, _liveSwapchains);
        if (_swapchain.Handle != 0)
        {
            var old = new RetiredGeneration(_swapchain, _presentReady, _presentFences, _pendingFences);
            if (UsesPresentFences || _presented.Any(value => value)) _retired.Add(old);
            else DestroyGeneration(old); // Never presented; DeviceWaitIdle already completed any GPU use.
        }
        _swapchain = replacement;
        _presentReady = []; _presentFences = []; _pendingFences = []; _presented = []; _images = [];
        count = 0;
        VulkanDevice.Check(_swapchainApi.GetSwapchainImages(_device.Device, _swapchain, ref count, null), "SwapchainImages");
        _images = new Image[count];
        fixed (Image* ptr = _images)
            VulkanDevice.Check(_swapchainApi.GetSwapchainImages(_device.Device, _swapchain, ref count, ptr), "SwapchainImages");
        _presentReady = new Semaphore[count];
        _presentFences = UsesPresentFences ? new Fence[count] : [];
        _pendingFences = new bool[count]; _presented = new bool[count];
        var semaphoreInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
        var fenceInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
        for (var i = 0; i < _presentReady.Length; i++)
        {
            VulkanDevice.Check(vk.CreateSemaphore(_device.Device, in semaphoreInfo, null, out _presentReady[i]), "CreatePresentSemaphore");
            _liveSemaphores++;
            if (UsesPresentFences)
            {
                VulkanDevice.Check(vk.CreateFence(_device.Device, in fenceInfo, null, out _presentFences[i]), "CreatePresentFence");
                _liveFences++;
            }
        }
        if (UsesPresentFences) CollectRetired(true);
        _recreate = false; SwapchainGenerations++;
        return true;
    }

    /// <summary>Presents the current UI texture. Returns false while suspended or requiring recreation.</summary>
    public bool Present(UiSession session)
    {
        session.VerifyAccess();
        var viewport = session.Viewport;
        if (viewport.IsEmpty) return false;
        var lease = session.AcquireVulkanTexture();
        var layout = (ImageLayout)lease.Info.Layout;
        try { return PresentImage(lease.Info, viewport, state => layout = state); }
        finally { lease.Return(layout); }
    }

    /// <summary>Presents a borrowed RGBA image, including a host-composited scene. Does not destroy or return its source.</summary>
    /// <remarks>Use the same device/serialized queue. The callback records the source layout after completed GPU work,
    /// including when presentation requires recreation. The source owner retains responsibility for its lifetime.</remarks>
    public bool PresentImage(VulkanTextureInfo info, UiViewport viewport, Action<ImageLayout> stateChanged)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(stateChanged);
        viewport.Validate();
        if (viewport.IsEmpty) return false;
        if (info.Image == 0 || info.Width == 0 || info.Height == 0 || info.Format != (uint)Format.R8G8B8A8Unorm ||
            info.SampleCount != 1 || info.LevelCount != 1 || (info.Usage & (uint)ImageUsageFlags.TransferSrcBit) == 0 ||
            (info.QueueFamily != Vk.QueueFamilyIgnored && info.QueueFamily != _device.QueueFamily))
            throw new ArgumentException("Presentation requires a transfer-source RGBA image on the host queue.", nameof(info));
        if ((_recreate || _extent.Width != viewport.Width || _extent.Height != viewport.Height) && !Recreate(viewport)) return false;
        uint index = 0;
        var acquire = _swapchainApi.AcquireNextImage(_device.Device, _swapchain, ulong.MaxValue, _acquired, default, &index);
        if (acquire == Result.ErrorOutOfDateKhr) { _recreate = true; return false; }
        if (acquire != Result.Success && acquire != Result.SuboptimalKhr) VulkanDevice.Check(acquire, "AcquireSwapchainImage");
        if (acquire == Result.SuboptimalKhr) _recreate = true;
        if (UsesPresentFences)
        {
            ConfirmFence(_presentFences, _pendingFences, (int)index, true);
            var fence = _presentFences[index];
            VulkanDevice.Check(_device.Api.ResetFences(_device.Device, 1, &fence), "ResetPresentFence");
        }
        var layout = (ImageLayout)info.Layout;
        {
            var vk = _device.Api;
            VulkanDevice.Check(vk.ResetCommandPool(_device.Device, _pool, 0), "ResetPresentCommands");
            var command = _command;
            var begin = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
            VulkanDevice.Check(vk.BeginCommandBuffer(command, in begin), "BeginPresentCommands");
            var barriers = stackalloc ImageMemoryBarrier[2];
            barriers[0] = new ImageMemoryBarrier
            {
                SType = StructureType.ImageMemoryBarrier, SrcAccessMask = AccessFlags.MemoryWriteBit | AccessFlags.MemoryReadBit,
                DstAccessMask = AccessFlags.TransferReadBit, OldLayout = layout, NewLayout = ImageLayout.TransferSrcOptimal,
                Image = new Image(info.Image), SrcQueueFamilyIndex = Vk.QueueFamilyIgnored, DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
            };
            barriers[1] = new ImageMemoryBarrier
            {
                SType = StructureType.ImageMemoryBarrier, DstAccessMask = AccessFlags.TransferWriteBit,
                OldLayout = ImageLayout.Undefined, NewLayout = ImageLayout.TransferDstOptimal, Image = _images[index],
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored, DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
            };
            vk.CmdPipelineBarrier(command, PipelineStageFlags.AllCommandsBit, PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 2, barriers);
            var blit = new ImageBlit
            {
                SrcSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
                DstSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1)
            };
            blit.SrcOffsets[0] = new Offset3D(0, 0, 0); blit.SrcOffsets[1] = new Offset3D((int)info.Width, (int)info.Height, 1);
            blit.DstOffsets[0] = new Offset3D(0, 0, 0); blit.DstOffsets[1] = new Offset3D((int)_extent.Width, (int)_extent.Height, 1);
            vk.CmdBlitImage(command, new Image(info.Image), ImageLayout.TransferSrcOptimal, _images[index], ImageLayout.TransferDstOptimal, 1, &blit, Filter.Nearest);
            var presentBarrier = barriers[1];
            presentBarrier.SrcAccessMask = AccessFlags.TransferWriteBit; presentBarrier.DstAccessMask = 0;
            presentBarrier.OldLayout = ImageLayout.TransferDstOptimal; presentBarrier.NewLayout = ImageLayout.PresentSrcKhr;
            vk.CmdPipelineBarrier(command, PipelineStageFlags.TransferBit, PipelineStageFlags.BottomOfPipeBit, 0, 0, null, 0, null, 1, &presentBarrier);
            VulkanDevice.Check(vk.EndCommandBuffer(command), "EndPresentCommands");
            var acquired = _acquired; var ready = _presentReady[index]; var stage = PipelineStageFlags.TransferBit;
            var submit = new SubmitInfo
            { SType = StructureType.SubmitInfo, WaitSemaphoreCount = 1, PWaitSemaphores = &acquired, PWaitDstStageMask = &stage,
                CommandBufferCount = 1, PCommandBuffers = &command, SignalSemaphoreCount = 1, PSignalSemaphores = &ready };
            VulkanDevice.Check(vk.QueueSubmit(_device.Queue, 1, &submit, default), "SubmitPresentCommands");
            _device.WaitIdle(); stateChanged(ImageLayout.TransferSrcOptimal);
            // Waiting for the submission also completes its acquire-semaphore wait. Reacquiring a
            // previously presented replacement image proves all predecessor generations can retire.
            if (!UsesPresentFences && _presented[index] && _retired.Count != 0)
            {
                _confirmed++;
                foreach (var generation in _retired) DestroyGeneration(generation);
                _retired.Clear();
            }
            var swapchain = _swapchain;
            var presentFence = UsesPresentFences ? _presentFences[index] : default;
            var fenceInfo = new SwapchainPresentFenceInfoEXT
            { SType = StructureType.SwapchainPresentFenceInfoExt, SwapchainCount = 1, PFences = &presentFence };
            var present = new PresentInfoKHR
            { SType = StructureType.PresentInfoKhr, WaitSemaphoreCount = 1, PWaitSemaphores = &ready,
                SwapchainCount = 1, PSwapchains = &swapchain, PImageIndices = &index, PNext = UsesPresentFences ? &fenceInfo : null };
            var result = _swapchainApi.QueuePresent(_device.Queue, in present);
            if (result is Result.Success or Result.SuboptimalKhr or Result.ErrorOutOfDateKhr or Result.ErrorSurfaceLostKhr)
            { _pendingFences[index] = UsesPresentFences; _presented[index] = true; }
            if (result is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr) _recreate = true;
            else VulkanDevice.Check(result, "QueuePresent");
            _device.WaitIdle();
            if (UsesPresentFences) CollectRetired(false);
            if (result == Result.ErrorOutOfDateKhr) return false;
            PresentedFrames++;
            return true;
        }
    }

    private bool ConfirmFence(Fence[] fences, bool[] pending, int index, bool wait)
    {
        if (!pending[index]) return true;
        var fence = fences[index];
        var result = wait ? _device.Api.WaitForFences(_device.Device, 1, &fence, true, ulong.MaxValue)
            : _device.Api.GetFenceStatus(_device.Device, fence);
        if (result == Result.NotReady) return false;
        VulkanDevice.Check(result, "ConfirmPresentationFence");
        pending[index] = false; _confirmed++;
        return true;
    }
    private void CollectRetired(bool wait)
    {
        for (var i = _retired.Count - 1; i >= 0; i--)
        {
            var generation = _retired[i];
            var complete = true;
            for (var j = 0; j < generation.Fences.Length; j++)
                complete &= ConfirmFence(generation.Fences, generation.PendingFences, j, wait);
            if (!complete) continue;
            DestroyGeneration(generation); _retired.RemoveAt(i);
        }
    }
    private void DestroyGeneration(RetiredGeneration generation)
    {
        var vk = _device.Api;
        _swapchainApi.DestroySwapchain(_device.Device, generation.Swapchain, null);
        _liveSwapchains--; _destroyed++;
        foreach (var semaphore in generation.Semaphores)
            if (semaphore.Handle != 0) { vk.DestroySemaphore(_device.Device, semaphore, null); _liveSemaphores--; }
        foreach (var fence in generation.Fences)
            if (fence.Handle != 0) { vk.DestroyFence(_device.Device, fence, null); _liveFences--; }
    }
    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        var vk = _device.Api; vk.DeviceWaitIdle(_device.Device);
        if (UsesPresentFences)
        {
            CollectRetired(true);
            for (var i = 0; i < _presentFences.Length; i++) ConfirmFence(_presentFences, _pendingFences, i, true);
        }
        // Legacy shutdown retains the unextended Vulkan WaitIdle limitation; resize retirement above
        // uses replacement-image reacquisition rather than treating WaitIdle as presentation proof.
        foreach (var generation in _retired) DestroyGeneration(generation);
        _retired.Clear();
        if (_swapchain.Handle != 0) DestroyGeneration(new(_swapchain, _presentReady, _presentFences, _pendingFences));
        if (_acquired.Handle != 0) { vk.DestroySemaphore(_device.Device, _acquired, null); _liveSemaphores--; }
        if (_pool.Handle != 0) { vk.DestroyCommandPool(_device.Device, _pool, null); _livePools--; }
        _swapchainApi?.Dispose(); _surfaceApi.Dispose();
        _disposed = true;
    }
}
