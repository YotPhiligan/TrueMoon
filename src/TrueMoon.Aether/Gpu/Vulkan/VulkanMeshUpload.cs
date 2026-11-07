using System.Buffers.Binary;
using System.Security.Cryptography;
using Silk.NET.Vulkan;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace TrueMoon.Aether.Gpu.Vulkan;

// A single actual owner survives allocation/submit/cleanup failure. The backend serializes its borrowed context.
internal sealed class VulkanMeshUpload(VulkanRenderResourceBackend backend, RenderMeshUploadRequest request, bool verify) : IRenderMeshUpload
{
    private readonly Lock _gate = new();
    private Task? _job;
    private VkBuffer _geometry, _staging;
    private DeviceMemory _geometryMemory, _stagingMemory;
    private CommandBuffer _command;
    private Fence _fence;
    private bool _submitted;
    private RenderAllocationReservation? _geometryReservation, _stagingReservation, _cpuReservation;
    public bool IsReady { get; private set; }
    public bool IsReleased { get; private set; }
    public Task PrepareAsync(CancellationToken cancellationToken)
    { lock (_gate) return _job ??= PrepareCoreAsync(cancellationToken); }
    private async Task PrepareCoreAsync(CancellationToken token)
    {
        await backend.NativeGate.WaitAsync(token).ConfigureAwait(false);
        try { await Task.Run(() => { token.ThrowIfCancellationRequested(); backend.EnsureContext(); Upload(token); }, CancellationToken.None).ConfigureAwait(false); }
        finally { backend.NativeGate.Release(); }
    }
    private unsafe void Upload(CancellationToken token)
    {
        var vk = backend.Api!; var device = backend.Device; var mesh = (MeshDefinition)request.Mesh.Definition;
        var length = checked(mesh.Vertices.Length * 12 + mesh.Indices.Length * 4);
        _cpuReservation = request.Budget.Reserve(0, 0, length);
        var bytes = new byte[length]; var offset = 0;
        foreach (var vertex in mesh.Vertices)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset), vertex.X); BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 4), vertex.Y);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 8), vertex.Z); offset += 12;
        }
        foreach (var index in mesh.Indices) { BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), index); offset += 4; }
        token.ThrowIfCancellationRequested();
        CreateBuffer((ulong)length, BufferUsageFlags.VertexBufferBit | BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit | BufferUsageFlags.TransferSrcBit, ref _geometry);
        CreateBuffer((ulong)length, BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit, ref _staging);
        vk.GetBufferMemoryRequirements(device, _geometry, out var geometryRequirements); vk.GetBufferMemoryRequirements(device, _staging, out var stagingRequirements);
        _geometryReservation = request.Budget.Reserve(checked((long)geometryRequirements.Size), 0, 0);
        _stagingReservation = request.Budget.Reserve(0, checked((long)stagingRequirements.Size), 0);
        Allocate(_geometry, geometryRequirements, MemoryPropertyFlags.DeviceLocalBit, ref _geometryMemory);
        Allocate(_staging, stagingRequirements, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, ref _stagingMemory);
        void* mapped = null; VulkanRenderResourceBackend.Check(vk.MapMemory(device, _stagingMemory, 0, (ulong)length, 0, &mapped), "MapResourceUpload");
        try { bytes.AsSpan().CopyTo(new Span<byte>(mapped, length)); } finally { vk.UnmapMemory(device, _stagingMemory); }
        token.ThrowIfCancellationRequested();
        var allocate = new CommandBufferAllocateInfo { SType = StructureType.CommandBufferAllocateInfo, CommandPool = backend.Pool, Level = CommandBufferLevel.Primary, CommandBufferCount = 1 };
        fixed (CommandBuffer* command = &_command) VulkanRenderResourceBackend.Check(vk.AllocateCommandBuffers(device, in allocate, command), "AllocateResourceCommand");
        var fence = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
        VulkanRenderResourceBackend.Check(vk.CreateFence(device, in fence, null, out _fence), "CreateResourceFence");
        Begin(); var copy = new BufferCopy(0, 0, (ulong)length); vk.CmdCopyBuffer(_command, _staging, _geometry, 1, in copy);
        var visibility = new BufferMemoryBarrier { SType = StructureType.BufferMemoryBarrier, SrcAccessMask = AccessFlags.TransferWriteBit,
            DstAccessMask = AccessFlags.VertexAttributeReadBit | AccessFlags.IndexReadBit | AccessFlags.TransferReadBit,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored, DstQueueFamilyIndex = Vk.QueueFamilyIgnored, Buffer = _geometry, Offset = 0, Size = (ulong)length };
        vk.CmdPipelineBarrier(_command, PipelineStageFlags.TransferBit, PipelineStageFlags.VertexInputBit | PipelineStageFlags.TransferBit, 0, 0, null, 1, &visibility, 0, null);
        SubmitAndConfirm(); Interlocked.Increment(ref backend.UploadsCompleted);
        if (verify)
        {
            VulkanRenderResourceBackend.Check(vk.ResetCommandBuffer(_command, 0), "ResetResourceReadbackCommand");
            Begin(); vk.CmdCopyBuffer(_command, _geometry, _staging, 1, in copy);
            var host = new BufferMemoryBarrier { SType = StructureType.BufferMemoryBarrier, SrcAccessMask = AccessFlags.TransferWriteBit, DstAccessMask = AccessFlags.HostReadBit,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored, DstQueueFamilyIndex = Vk.QueueFamilyIgnored, Buffer = _staging, Offset = 0, Size = (ulong)length };
            vk.CmdPipelineBarrier(_command, PipelineStageFlags.TransferBit, PipelineStageFlags.HostBit, 0, 0, null, 1, &host, 0, null);
            fixed (Fence* f = &_fence) VulkanRenderResourceBackend.Check(vk.ResetFences(device, 1, f), "ResetResourceReadbackFence");
            SubmitAndConfirm();
            VulkanRenderResourceBackend.Check(vk.MapMemory(device, _stagingMemory, 0, (ulong)length, 0, &mapped), "MapResourceReadback");
            try
            {
                Span<byte> expected = stackalloc byte[32]; Span<byte> actual = stackalloc byte[32];
                SHA256.HashData(bytes, expected); SHA256.HashData(new ReadOnlySpan<byte>(mapped, length), actual);
                ResourceGuard.Require(expected.SequenceEqual(actual), RenderResourceError.BackendFailure, "Resource GPU readback hash differs from the CPU geometry.");
            }
            finally { vk.UnmapMemory(device, _stagingMemory); }
            Interlocked.Increment(ref backend.UploadsVerified);
        }
        // Upload-only buffers are released after confirmed completion, before publishing readiness.
        ReleaseStaging(); _cpuReservation.Dispose(); _cpuReservation = null;
        token.ThrowIfCancellationRequested(); IsReady = true;
    }
    private unsafe void CreateBuffer(ulong size, BufferUsageFlags usage, ref VkBuffer buffer)
    {
        var info = new BufferCreateInfo { SType = StructureType.BufferCreateInfo, Size = size, Usage = usage, SharingMode = SharingMode.Exclusive };
        VulkanRenderResourceBackend.Check(backend.Api!.CreateBuffer(backend.Device, in info, null, out buffer), "CreateResourceBuffer"); Interlocked.Increment(ref backend.BuffersCreated);
    }
    private unsafe void Allocate(VkBuffer buffer, MemoryRequirements requirements, MemoryPropertyFlags flags, ref DeviceMemory memory)
    {
        var info = new MemoryAllocateInfo { SType = StructureType.MemoryAllocateInfo, AllocationSize = requirements.Size, MemoryTypeIndex = backend.MemoryType(requirements.MemoryTypeBits, flags) };
        VulkanRenderResourceBackend.Check(backend.Api!.AllocateMemory(backend.Device, in info, null, out memory), "AllocateResourceMemory"); Interlocked.Increment(ref backend.AllocationsCreated);
        VulkanRenderResourceBackend.Check(backend.Api.BindBufferMemory(backend.Device, buffer, memory, 0), "BindResourceMemory");
    }
    private unsafe void Begin()
    { var begin = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit }; VulkanRenderResourceBackend.Check(backend.Api!.BeginCommandBuffer(_command, in begin), "BeginResourceCommand"); }
    private unsafe void SubmitAndConfirm()
    {
        var vk = backend.Api!; VulkanRenderResourceBackend.Check(vk.EndCommandBuffer(_command), "EndResourceCommand");
        var command = _command; var submit = new SubmitInfo { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &command };
        _submitted = true; // Retain before the call: exceptions/unknown results cannot lose the owner.
        var result = vk.QueueSubmit(backend.Queue, 1, in submit, _fence);
        ResourceGuard.Require(result == Result.Success, RenderResourceError.UnknownOutcome, "Resource submission outcome is unconfirmed: " + result);
        var fence = _fence;
        while (true)
        {
            result = vk.WaitForFences(backend.Device, 1, &fence, true, 10_000_000);
            if (result == Result.Timeout) continue;
            ResourceGuard.Require(result == Result.Success, RenderResourceError.UnknownOutcome, "Resource completion is unconfirmed: " + result);
            _submitted = false; return;
        }
    }
    public async ValueTask DisposeAsync()
    {
        Task? job; lock (_gate) job = _job;
        if (job != null) { try { await job.ConfigureAwait(false); } catch { /* Partial allocation still belongs to this owner. */ } }
        await backend.NativeGate.WaitAsync().ConfigureAwait(false);
        try { if (!IsReleased) ReleaseNative(); }
        finally { backend.NativeGate.Release(); }
    }
    private unsafe void ReleaseStaging()
    {
        var vk = backend.Api!; var device = backend.Device;
        if (_staging.Handle != 0) { vk.DestroyBuffer(device, _staging, null); _staging = default; Interlocked.Increment(ref backend.BuffersReleased); }
        if (_stagingMemory.Handle != 0) { vk.FreeMemory(device, _stagingMemory, null); _stagingMemory = default; Interlocked.Increment(ref backend.AllocationsReleased); }
        _stagingReservation?.Dispose(); _stagingReservation = null;
    }
    private unsafe void ReleaseNative()
    {
        if (backend.Api != null && backend.Device.Handle != 0)
        {
            if (_submitted)
            {
                var idle = backend.Api.DeviceWaitIdle(backend.Device);
                ResourceGuard.Require(idle == Result.Success, RenderResourceError.UnknownOutcome, "Cannot retire an unconfirmed resource submission: " + idle); _submitted = false;
            }
            if (_command.Handle != 0) { var command = _command; backend.Api.FreeCommandBuffers(backend.Device, backend.Pool, 1, &command); _command = default; }
            if (_fence.Handle != 0) { backend.Api.DestroyFence(backend.Device, _fence, null); _fence = default; }
            ReleaseStaging();
            if (_geometry.Handle != 0) { backend.Api.DestroyBuffer(backend.Device, _geometry, null); _geometry = default; Interlocked.Increment(ref backend.BuffersReleased); }
            if (_geometryMemory.Handle != 0) { backend.Api.FreeMemory(backend.Device, _geometryMemory, null); _geometryMemory = default; Interlocked.Increment(ref backend.AllocationsReleased); }
        }
        _geometryReservation?.Dispose(); _geometryReservation = null; _cpuReservation?.Dispose(); _cpuReservation = null;
        IsReady = false; IsReleased = true; backend.Released(this);
    }
}
