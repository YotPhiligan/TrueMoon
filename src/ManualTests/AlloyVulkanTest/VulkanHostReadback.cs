using TrueMoon.Alloy.Rendering.Skia;
using Silk.NET.Vulkan;
using TrueMoon.Alloy.Platform.Silk;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace AlloyVulkanTest;

// External consumer: raw Vulkan commands on the host-owned graphics queue.
// CPU transfer is assertion-only; this probe does not yet implement scene composition.
internal static unsafe class VulkanHostReadback
{
    internal static byte[] Read(VulkanDevice device, VulkanTextureInfo info, Action<ImageLayout> stateChanged)
        => Read(device.Api, device.Device, device.PhysicalDevice, device.Queue, device.QueueFamily, info, stateChanged);

    internal static byte[] Read(Vk api, Device logicalDevice, PhysicalDevice physicalDevice, Queue queue,
        uint queueFamily, VulkanTextureInfo info, Action<ImageLayout> stateChanged)
        => Read(new ReadbackHost(api, logicalDevice, physicalDevice, queue, queueFamily), info, stateChanged);

    private sealed record ReadbackHost(Vk Api, Device Device, PhysicalDevice PhysicalDevice, Queue Queue, uint QueueFamily)
    {
        internal void WaitIdle() => VulkanDevice.Check(Api.DeviceWaitIdle(Device), "DeviceWaitIdle");
    }

    private static byte[] Read(ReadbackHost device, VulkanTextureInfo info, Action<ImageLayout> stateChanged)
    {
        if (info.Format != (uint)Format.R8G8B8A8Unorm || info.SampleCount != 1 || info.LevelCount != 1 ||
            (info.Usage & (uint)ImageUsageFlags.TransferSrcBit) == 0)
            throw new NotSupportedException("The probe requires single-level RGBA8888 with transfer-source usage.");
        var bytes = checked((int)(info.Width * info.Height * 4));
        var vk = device.Api;
        Buffer buffer = default;
        DeviceMemory memory = default;
        CommandPool pool = default;
        var submitted = false;
        try
        {
            var bufferInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo, Size = (ulong)bytes,
                Usage = BufferUsageFlags.TransferDstBit, SharingMode = SharingMode.Exclusive
            };
            VulkanDevice.Check(vk.CreateBuffer(device.Device, in bufferInfo, null, out buffer), "CreateReadbackBuffer");
            vk.GetBufferMemoryRequirements(device.Device, buffer, out var requirements);
            vk.GetPhysicalDeviceMemoryProperties(device.PhysicalDevice, out var properties);
            uint? memoryType = null;
            var requiredFlags = MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit;
            for (uint i = 0; i < properties.MemoryTypeCount; i++)
                if ((requirements.MemoryTypeBits & (1u << (int)i)) != 0 &&
                    (properties.MemoryTypes[(int)i].PropertyFlags & requiredFlags) == requiredFlags)
                {
                    memoryType = i;
                    break;
                }
            var allocation = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo, AllocationSize = requirements.Size,
                MemoryTypeIndex = memoryType ?? throw new NotSupportedException("No coherent readback memory type.")
            };
            VulkanDevice.Check(vk.AllocateMemory(device.Device, in allocation, null, out memory), "AllocateReadbackMemory");
            VulkanDevice.Check(vk.BindBufferMemory(device.Device, buffer, memory, 0), "BindReadbackMemory");
            var poolInfo = new CommandPoolCreateInfo
            {
                SType = StructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = device.QueueFamily, Flags = CommandPoolCreateFlags.TransientBit
            };
            VulkanDevice.Check(vk.CreateCommandPool(device.Device, in poolInfo, null, out pool), "CreateProbeCommandPool");
            var commandInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo, CommandPool = pool,
                Level = CommandBufferLevel.Primary, CommandBufferCount = 1
            };
            CommandBuffer command;
            VulkanDevice.Check(vk.AllocateCommandBuffers(device.Device, in commandInfo, &command), "AllocateProbeCommandBuffer");
            var begin = new CommandBufferBeginInfo
            {
                SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit
            };
            VulkanDevice.Check(vk.BeginCommandBuffer(command, in begin), "BeginProbeCommandBuffer");
            var image = new Image(info.Image);
            var barrier = new ImageMemoryBarrier
            {
                SType = StructureType.ImageMemoryBarrier,
                SrcAccessMask = AccessFlags.MemoryWriteBit | AccessFlags.MemoryReadBit,
                DstAccessMask = AccessFlags.TransferReadBit,
                OldLayout = (ImageLayout)info.Layout, NewLayout = ImageLayout.TransferSrcOptimal,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored, DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = image, SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
            };
            vk.CmdPipelineBarrier(command, PipelineStageFlags.AllCommandsBit, PipelineStageFlags.TransferBit,
                0, 0, null, 0, null, 1, &barrier);
            var copy = new BufferImageCopy
            {
                ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
                ImageExtent = new Extent3D(info.Width, info.Height, 1)
            };
            vk.CmdCopyImageToBuffer(command, image, ImageLayout.TransferSrcOptimal, buffer, 1, &copy);
            var hostBarrier = new BufferMemoryBarrier
            {
                SType = StructureType.BufferMemoryBarrier,
                SrcAccessMask = AccessFlags.TransferWriteBit, DstAccessMask = AccessFlags.HostReadBit,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored, DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Buffer = buffer, Offset = 0, Size = Vk.WholeSize
            };
            vk.CmdPipelineBarrier(command, PipelineStageFlags.TransferBit, PipelineStageFlags.HostBit,
                0, 0, null, 1, &hostBarrier, 0, null);
            VulkanDevice.Check(vk.EndCommandBuffer(command), "EndProbeCommandBuffer");
            var submit = new SubmitInfo
            {
                SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &command
            };
            VulkanDevice.Check(vk.QueueSubmit(device.Queue, 1, &submit, default), "SubmitHostReadback");
            submitted = true;
            device.WaitIdle();
            stateChanged(ImageLayout.TransferSrcOptimal);
            void* mapped;
            VulkanDevice.Check(vk.MapMemory(device.Device, memory, 0, (ulong)bytes, 0, &mapped), "MapReadbackMemory");
            try { return new ReadOnlySpan<byte>(mapped, bytes).ToArray(); }
            finally { vk.UnmapMemory(device.Device, memory); }
        }
        finally
        {
            // Never free an in-flight command buffer or readback allocation.
            if (submitted) vk.DeviceWaitIdle(device.Device);
            if (pool.Handle != 0) vk.DestroyCommandPool(device.Device, pool, null);
            if (buffer.Handle != 0) vk.DestroyBuffer(device.Device, buffer, null);
            if (memory.Handle != 0) vk.FreeMemory(device.Device, memory, null);
        }
    }
}
