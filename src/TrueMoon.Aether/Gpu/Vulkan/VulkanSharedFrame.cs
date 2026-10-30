using System.Diagnostics;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Gpu.Interop;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace TrueMoon.Aether.Gpu.Vulkan;

/// <summary>App-owned dedicated one-frame Vulkan exporter. No queue waits are submitted before peer CPU completion.</summary>
public sealed class VulkanSharedFrame : IGpuResource
{
    private readonly SessionIdentity _identity;
    private readonly GpuAdapterId _adapter;
    private readonly int _width, _height;
    private readonly VulkanSharedFrame? _deviceOwner;
    private readonly bool _reusable;
    private GpuFrameStamp _stamp;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Vk? _vk;
    private Instance _instance;
    private PhysicalDevice _physical;
    private Device _device;
    private Queue _queue;
    private uint _family;
    private Image _image;
    private DeviceMemory _memory, _stagingMemory;
    private Buffer _staging;
    private Semaphore _ready, _returned;
    private Fence _fence;
    private CommandPool _pool;
    private CommandBuffer _command;
    private VulkanPrimitiveRenderer? _renderer;
    private GpuNtHandle? _memoryHandle, _readyHandle, _returnHandle;
    private GpuFrameOffer? _offer;
    private bool _submitted, _produced, _retired;
    private int _closing;
    private readonly bool _validation;
    private GCHandle _callbackRoot;
    private ulong _messenger;
    private int _validationErrors, _validationWarnings;
    private readonly ConcurrentQueue<string> _validationMessages = new();
    internal int DeviceCreations { get; private set; }
    internal int DeviceReleases { get; private set; }
    internal GpuDeviceObservation? DeviceObservation { get; private set; }
    /// <summary>Core/synchronization validation and ownership snapshot, including teardown messages.</summary>
    public GpuProducerDiagnostics Diagnostics => new(_validation, Volatile.Read(ref _validationErrors), Volatile.Read(ref _validationWarnings),
        Array.AsReadOnly(_validationMessages.ToArray()), IsReleased, Volatile.Read(ref GpuNtHandle.ActiveCount));
    /// <summary>Whether all Vulkan/NT resources were freed.</summary>
    public bool IsReleased { get; private set; }
    /// <summary>Validated descriptor and source-process NT tokens; never use these tokens as local handles in the peer.</summary>
    public GpuFrameOffer Offer => _offer ?? throw new InvalidOperationException("GPU producer is not initialized.");
    internal VulkanSharedFrame(SessionIdentity identity, GpuAdapterId adapter, int width, int height, bool validation)
        : this(new(identity), adapter, width, height, validation, null) { }
    internal VulkanSharedFrame(GpuFrameStamp stamp, GpuAdapterId adapter, int width, int height, bool validation, VulkanSharedFrame? deviceOwner)
    { _stamp = stamp; _identity = stamp.Identity; _adapter = adapter; _width = width; _height = height; _validation = validation; _deviceOwner = deviceOwner; _reusable = stamp.Version == 2; }
    internal async Task InitializeAsync(CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try { EnsureOpen(); Initialize(); }
        finally { _gate.Release(); }
    }
    private unsafe void Initialize()
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess) throw new PlatformNotSupportedException("GPU import profile requires Windows x64.");
        _identity.Validate(); Surfaces.GpuSurfaceOffer.ValidateExtent(_width, _height);
        if (_width == 0) throw new ArgumentOutOfRangeException(nameof(_width));
        if (_deviceOwner == null)
        {
        _vk = Vk.GetApi(); var app = new ApplicationInfo { SType = StructureType.ApplicationInfo, ApiVersion = Vk.Version11 };
        var create = new InstanceCreateInfo { SType = StructureType.InstanceCreateInfo, PApplicationInfo = &app };
        using var validationExtensions = SilkMarshal.StringArrayToMemory(_validation ? ["VK_EXT_debug_utils", "VK_EXT_validation_features"] : []);
        using var layers = SilkMarshal.StringArrayToMemory(_validation ? ["VK_LAYER_KHRONOS_validation"] : []);
        var debug = new DebugUtilsMessengerCreateInfoEXT { SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt,
            MessageType = DebugUtilsMessageTypeFlagsEXT.ValidationBitExt | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt | DebugUtilsMessageTypeFlagsEXT.GeneralBitExt };
        var sync = ValidationFeatureEnableEXT.SynchronizationValidationExt;
        var validation = new ValidationFeaturesEXT { SType = StructureType.ValidationFeaturesExt, EnabledValidationFeatureCount = 1, PEnabledValidationFeatures = &sync, PNext = &debug };
        if (_validation)
        {
            _callbackRoot = GCHandle.Alloc(this); debug.PUserData = (void*)GCHandle.ToIntPtr(_callbackRoot); debug.PfnUserCallback = new PfnDebugUtilsMessengerCallbackEXT(&ValidationCallback);
            create.PNext = &validation; create.EnabledExtensionCount = 2; create.PpEnabledExtensionNames = (byte**)validationExtensions.Handle;
            create.EnabledLayerCount = 1; create.PpEnabledLayerNames = (byte**)layers.Handle;
        }
        Check(_vk.CreateInstance(in create, null, out _instance), "CreateInstance");
        if (_validation)
        {
            var createMessenger = (delegate* unmanaged<nint, DebugUtilsMessengerCreateInfoEXT*, void*, ulong*, Result>)_vk.GetInstanceProcAddr(_instance, "vkCreateDebugUtilsMessengerEXT").Handle;
            if (createMessenger == null) throw new NotSupportedException("vkCreateDebugUtilsMessengerEXT");
            ulong messenger = 0; Check(createMessenger(_instance.Handle, &debug, null, &messenger), "CreateDebugMessenger"); _messenger = messenger;
        }
        uint count = 0; Check(_vk.EnumeratePhysicalDevices(_instance, &count, null), "EnumeratePhysicalDevices count");
        if (count is 0 or > 16) throw new NotSupportedException("GPU physical device count.");
        var devices = new PhysicalDevice[count]; fixed (PhysicalDevice* pointer = devices) Check(_vk.EnumeratePhysicalDevices(_instance, &count, pointer), "EnumeratePhysicalDevices");
        var matches = 0; GpuDeviceObservation? selectedDevice = null;
        foreach (var physical in devices)
        {
            var ids = new PhysicalDeviceIDProperties { SType = StructureType.PhysicalDeviceIDProperties };
            var props = new PhysicalDeviceProperties2 { SType = StructureType.PhysicalDeviceProperties2, PNext = &ids }; _vk.GetPhysicalDeviceProperties2(physical, &props);
            if (Convert.ToHexString(new ReadOnlySpan<byte>(ids.DeviceUuid, 16)) == _adapter.DeviceUuid && Convert.ToHexString(new ReadOnlySpan<byte>(ids.DriverUuid, 16)) == _adapter.DriverUuid)
            {
                _physical = physical; matches++;
                var nameBytes = new ReadOnlySpan<byte>(props.Properties.DeviceName, 256); var end = nameBytes.IndexOf((byte)0);
                selectedDevice = new("Vulkan", System.Text.Encoding.UTF8.GetString(end < 0 ? nameBytes : nameBytes[..end]),
                    props.Properties.ApiVersion, props.Properties.DriverVersion, props.Properties.VendorID, props.Properties.DeviceID);
            }
        }
        if (matches != 1) throw new NotSupportedException("No unique matching Vulkan device/driver.");
        uint queues = 0; _vk.GetPhysicalDeviceQueueFamilyProperties(_physical, &queues, null);
        if (queues is 0 or > 256) throw new NotSupportedException("Graphics queue count.");
        var families = new QueueFamilyProperties[queues]; fixed (QueueFamilyProperties* pointer = families) _vk.GetPhysicalDeviceQueueFamilyProperties(_physical, &queues, pointer);
        var family = Array.FindIndex(families, q => q.QueueCount > 0 && q.QueueFlags.HasFlag(QueueFlags.GraphicsBit));
        if (family < 0) throw new NotSupportedException("No graphics queue."); _family = (uint)family;
        float priority = 1;
        var queueInfo = new DeviceQueueCreateInfo { SType = StructureType.DeviceQueueCreateInfo, QueueFamilyIndex = _family, QueueCount = 1, PQueuePriorities = &priority };
        using var extensions = SilkMarshal.StringArrayToMemory(["VK_KHR_external_memory_win32", "VK_KHR_external_semaphore_win32"]);
        var deviceInfo = new DeviceCreateInfo { SType = StructureType.DeviceCreateInfo, QueueCreateInfoCount = 1, PQueueCreateInfos = &queueInfo,
            EnabledExtensionCount = 2, PpEnabledExtensionNames = (byte**)extensions.Handle };
        Check(_vk.CreateDevice(_physical, in deviceInfo, null, out _device), "CreateDevice"); DeviceCreations++; _vk.GetDeviceQueue(_device, _family, 0, out _queue);
        DeviceObservation = selectedDevice;
        }
        else
        {
            // Only the surface accesses these borrowed contexts and serializes all queue operations.
            _deviceOwner.EnsureOpen(); _vk = _deviceOwner._vk ?? throw new InvalidOperationException("Shared Vulkan device was not initialized."); _physical = _deviceOwner._physical;
            DeviceObservation = _deviceOwner.DeviceObservation;
            _device = _deviceOwner._device; _queue = _deviceOwner._queue; _family = _deviceOwner._family;
        }
        var externalImage = new ExternalMemoryImageCreateInfo { SType = StructureType.ExternalMemoryImageCreateInfo, HandleTypes = ExternalMemoryHandleTypeFlags.OpaqueWin32Bit };
        var imageInfo = new ImageCreateInfo { SType = StructureType.ImageCreateInfo, PNext = &externalImage, ImageType = ImageType.Type2D, Format = Format.R8G8B8A8Unorm,
            Extent = new((uint)_width, (uint)_height, 1), MipLevels = 1, ArrayLayers = 1, Samples = SampleCountFlags.Count1Bit, Tiling = ImageTiling.Optimal,
            Usage = ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit | ImageUsageFlags.ColorAttachmentBit,
            SharingMode = SharingMode.Exclusive, InitialLayout = ImageLayout.Undefined };
        Check(_vk.CreateImage(_device, in imageInfo, null, out _image), "CreateImage");
        _vk.GetImageMemoryRequirements(_device, _image, out var requirements);
        if (requirements.Size > 64 * 1024 * 1024) throw new NotSupportedException("GPU allocation exceeds profile limit.");
        var dedicated = new MemoryDedicatedAllocateInfo { SType = StructureType.MemoryDedicatedAllocateInfo, Image = _image };
        var export = new ExportMemoryAllocateInfo { SType = StructureType.ExportMemoryAllocateInfo, PNext = &dedicated, HandleTypes = ExternalMemoryHandleTypeFlags.OpaqueWin32Bit };
        var allocation = new MemoryAllocateInfo { SType = StructureType.MemoryAllocateInfo, PNext = &export, AllocationSize = requirements.Size, MemoryTypeIndex = MemoryType(requirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit) };
        Check(_vk.AllocateMemory(_device, in allocation, null, out _memory), "AllocateMemory image"); Check(_vk.BindImageMemory(_device, _image, _memory, 0), "BindImageMemory");
        var semaphoreExport = new ExportSemaphoreCreateInfo { SType = StructureType.ExportSemaphoreCreateInfo, HandleTypes = ExternalSemaphoreHandleTypeFlags.OpaqueWin32Bit };
        var semaphoreInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo, PNext = &semaphoreExport };
        Check(_vk.CreateSemaphore(_device, in semaphoreInfo, null, out _ready), "CreateSemaphore ready"); Check(_vk.CreateSemaphore(_device, in semaphoreInfo, null, out _returned), "CreateSemaphore return");
        var fenceInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo }; Check(_vk.CreateFence(_device, in fenceInfo, null, out _fence), "CreateFence");
        var poolInfo = new CommandPoolCreateInfo { SType = StructureType.CommandPoolCreateInfo, QueueFamilyIndex = _family, Flags = CommandPoolCreateFlags.ResetCommandBufferBit };
        Check(_vk.CreateCommandPool(_device, in poolInfo, null, out _pool), "CreateCommandPool");
        var commandInfo = new CommandBufferAllocateInfo { SType = StructureType.CommandBufferAllocateInfo, CommandPool = _pool, Level = CommandBufferLevel.Primary, CommandBufferCount = 1 };
        Check(_vk.AllocateCommandBuffers(_device, in commandInfo, out _command), "AllocateCommandBuffers");
        _memoryHandle = ExportMemory(); _readyHandle = ExportSemaphore(_ready); _returnHandle = ExportSemaphore(_returned);
        _offer = new(_stamp, _adapter, _width, _height, requirements.Size, _memoryHandle.Token, _readyHandle.Token, _returnHandle.Token);
        _offer.Validate(_identity, _adapter, _reusable);
    }
    private unsafe void EnsureStaging()
    {
        if (_staging.Handle != 0) return;
        var size = checked(_width * _height * 4);
        var bufferInfo = new BufferCreateInfo { SType = StructureType.BufferCreateInfo, Size = (ulong)size, Usage = BufferUsageFlags.TransferSrcBit, SharingMode = SharingMode.Exclusive };
        Check(_vk!.CreateBuffer(_device, in bufferInfo, null, out _staging), "CreateBuffer");
        _vk.GetBufferMemoryRequirements(_device, _staging, out var bufferRequirements);
        var stagingAllocation = new MemoryAllocateInfo { SType = StructureType.MemoryAllocateInfo, AllocationSize = bufferRequirements.Size,
            MemoryTypeIndex = MemoryType(bufferRequirements.MemoryTypeBits, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit) };
        Check(_vk.AllocateMemory(_device, in stagingAllocation, null, out _stagingMemory), "AllocateMemory staging"); Check(_vk.BindBufferMemory(_device, _staging, _stagingMemory, 0), "BindBufferMemory");
    }
    private unsafe uint MemoryType(uint bits, MemoryPropertyFlags flags)
    {
        _vk!.GetPhysicalDeviceMemoryProperties(_physical, out var properties);
        for (var i = 0; i < properties.MemoryTypeCount; i++) if ((bits & (1u << i)) != 0 && (properties.MemoryTypes[i].PropertyFlags & flags) == flags) return (uint)i;
        throw new NotSupportedException("No suitable GPU memory type: " + flags);
    }
    private unsafe GpuNtHandle ExportMemory()
    {
        var function = (delegate* unmanaged<nint, MemoryGetWin32HandleInfoKHR*, nint*, Result>)_vk!.GetDeviceProcAddr(_device, "vkGetMemoryWin32HandleKHR").Handle;
        if (function == null) throw new NotSupportedException("vkGetMemoryWin32HandleKHR");
        var info = new MemoryGetWin32HandleInfoKHR { SType = StructureType.MemoryGetWin32HandleInfoKhr, Memory = _memory, HandleType = ExternalMemoryHandleTypeFlags.OpaqueWin32Bit };
        nint handle = 0; Check(function(_device.Handle, &info, &handle), "GetMemoryWin32Handle"); return new(handle);
    }
    private unsafe GpuNtHandle ExportSemaphore(Semaphore semaphore)
    {
        var function = (delegate* unmanaged<nint, SemaphoreGetWin32HandleInfoKHR*, nint*, Result>)_vk!.GetDeviceProcAddr(_device, "vkGetSemaphoreWin32HandleKHR").Handle;
        if (function == null) throw new NotSupportedException("vkGetSemaphoreWin32HandleKHR");
        var info = new SemaphoreGetWin32HandleInfoKHR { SType = StructureType.SemaphoreGetWin32HandleInfoKhr, Semaphore = semaphore, HandleType = ExternalSemaphoreHandleTypeFlags.OpaqueWin32Bit };
        nint handle = 0; Check(function(_device.Handle, &info, &handle), "GetSemaphoreWin32Handle"); return new(handle);
    }
    /// <summary>Uploads the reference, releases image ownership to EXTERNAL and observes the ready fence before returning.</summary>
    public async Task<GpuFrameCompletion> ProduceAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try { EnsureOpen(); if (_produced || _offer == null) throw new InvalidOperationException("Producer is not in initial state."); UploadReference(GpuReferencePattern.Create(_width, _height)); Submit(false); await FenceAsync(token).ConfigureAwait(false); _produced = true; return new(Offer.Stamp, true); }
        finally { _gate.Release(); }
    }
    internal async Task<GpuFrameCompletion> ProduceSlotAsync(GpuFrameStamp stamp, CancellationToken token, byte[]? pixels = null, Rendering.PrimitiveScene? scene = null)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            EnsureOpen(); Surfaces.GpuSlotLedger.ValidateStamp(stamp, _identity);
            if (!_reusable || _submitted || (_produced && !_retired) || stamp.Slot != Offer.Stamp.Slot || stamp.SurfaceGeneration != Offer.Stamp.SurfaceGeneration ||
                stamp.FrameId != (_produced ? checked(_stamp.FrameId + 1) : 1)) throw new InvalidOperationException("GPU slot cannot be reused before retirement.");
            if (scene != null && pixels is { Length: > 0 } || scene == null && pixels != null && pixels.Length != checked(_width * _height * 4)) throw new InvalidDataException("Invalid GPU workload shape.");
            scene?.CreateTransform(_width, _height);
            if (scene != null && _renderer == null)
            {
                var renderer = new VulkanPrimitiveRenderer(_vk!, _physical, _device, _image, _width, _height);
                try { renderer.Initialize(); _renderer = renderer; } catch { renderer.Dispose(); throw; }
            }
            _stamp = stamp; if (scene == null) UploadReference(pixels); Submit(false, scene); await FenceAsync(token).ConfigureAwait(false);
            _produced = true; _retired = false; return new(stamp, true);
        }
        finally { _gate.Release(); }
    }
    private unsafe void UploadReference(byte[]? supplied = null)
    {
        EnsureStaging();
        var pixels = supplied ?? GpuReferencePattern.Create(_width, _height, _stamp); void* mapped = null;
        Check(_vk!.MapMemory(_device, _stagingMemory, 0, (ulong)pixels.Length, 0, &mapped), "MapMemory reused staging");
        try { pixels.CopyTo(new Span<byte>(mapped, pixels.Length)); } finally { _vk.UnmapMemory(_device, _stagingMemory); }
    }
    /// <summary>Waits on the already CPU-confirmed GL return signal, reacquires ownership and observes retirement completion.</summary>
    public async Task<GpuFrameCompletion> RetireAsync(GpuFrameCompletion release, CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            EnsureOpen(); if (!_produced || _retired || !release.FenceCompleted || release.Stamp != _stamp) throw new InvalidOperationException("GL return completion is not confirmed.");
            Submit(true); await FenceAsync(token).ConfigureAwait(false); _retired = true; return new(_stamp, true);
        }
        finally { _gate.Release(); }
    }
    private unsafe void Submit(bool returned, Rendering.PrimitiveScene? scene = null)
    {
        Check(_vk!.ResetFences(_device, 1, in _fence), "ResetFences"); Check(_vk.ResetCommandBuffer(_command, 0), "ResetCommandBuffer");
        var begin = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
        Check(_vk.BeginCommandBuffer(_command, in begin), "BeginCommandBuffer");
        var barrier = new ImageMemoryBarrier { SType = StructureType.ImageMemoryBarrier, Image = _image,
            SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1), OldLayout = returned || _produced ? ImageLayout.General : ImageLayout.Undefined,
            NewLayout = returned ? ImageLayout.General : scene == null ? ImageLayout.TransferDstOptimal : ImageLayout.ColorAttachmentOptimal, SrcQueueFamilyIndex = returned ? Vk.QueueFamilyExternal : Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = returned ? _family : Vk.QueueFamilyIgnored, DstAccessMask = returned ? AccessFlags.MemoryReadBit : scene == null ? AccessFlags.TransferWriteBit : AccessFlags.ColorAttachmentWriteBit };
        var writeStage = scene == null ? PipelineStageFlags.TransferBit : PipelineStageFlags.ColorAttachmentOutputBit;
        _vk.CmdPipelineBarrier(_command, PipelineStageFlags.TopOfPipeBit, returned ? PipelineStageFlags.AllCommandsBit : writeStage, 0, 0, null, 0, null, 1, &barrier);
        if (!returned)
        {
            if (scene == null)
            {
                var copy = new BufferImageCopy { ImageSubresource = new(ImageAspectFlags.ColorBit, 0, 0, 1), ImageExtent = new((uint)_width, (uint)_height, 1) };
                _vk.CmdCopyBufferToImage(_command, _staging, _image, ImageLayout.TransferDstOptimal, 1, &copy);
            }
            else _renderer!.Draw(_command, scene);
            barrier.OldLayout = scene == null ? ImageLayout.TransferDstOptimal : ImageLayout.ColorAttachmentOptimal; barrier.NewLayout = ImageLayout.General;
            barrier.SrcAccessMask = scene == null ? AccessFlags.TransferWriteBit : AccessFlags.ColorAttachmentWriteBit; barrier.DstAccessMask = 0;
            barrier.SrcQueueFamilyIndex = _family; barrier.DstQueueFamilyIndex = Vk.QueueFamilyExternal;
            _vk.CmdPipelineBarrier(_command, writeStage, PipelineStageFlags.BottomOfPipeBit, 0, 0, null, 0, null, 1, &barrier);
        }
        Check(_vk.EndCommandBuffer(_command), "EndCommandBuffer");
        var command = _command; var ready = _ready; var wait = _returned; var stage = PipelineStageFlags.AllCommandsBit;
        var submit = new SubmitInfo { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &command,
            SignalSemaphoreCount = returned ? 0u : 1u, PSignalSemaphores = returned ? null : &ready,
            WaitSemaphoreCount = returned ? 1u : 0u, PWaitSemaphores = returned ? &wait : null, PWaitDstStageMask = returned ? &stage : null };
        Check(_vk.QueueSubmit(_queue, 1, in submit, _fence), "QueueSubmit"); _submitted = true;
    }
    private async Task FenceAsync(CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            var result = _vk!.GetFenceStatus(_device, _fence);
            if (result == Result.Success) { _submitted = false; return; }
            if (result != Result.NotReady) Check(result, "GetFenceStatus");
            if (clock.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("Vulkan fence completion remains unconfirmed.");
            await Task.Delay(2, token).ConfigureAwait(false);
        }
    }
    private void EnsureOpen() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _closing) != 0, this);
    /// <summary>Never destroys resources still in submitted use; a failed fence wait retains ownership for retry.</summary>
    public async ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _closing, 1); await _gate.WaitAsync().ConfigureAwait(false);
        try { if (IsReleased) return; if (_submitted) await FenceAsync(CancellationToken.None).ConfigureAwait(false); Destroy(); IsReleased = true; }
        finally { _gate.Release(); }
    }
    private unsafe void Destroy()
    {
        _returnHandle?.Dispose(); _returnHandle = null; _readyHandle?.Dispose(); _readyHandle = null; _memoryHandle?.Dispose(); _memoryHandle = null;
        if (_device.Handle != 0)
        {
            if (_pool.Handle != 0) _vk!.DestroyCommandPool(_device, _pool, null);
            _renderer?.Dispose(); _renderer = null;
            if (_fence.Handle != 0) _vk!.DestroyFence(_device, _fence, null);
            if (_returned.Handle != 0) _vk!.DestroySemaphore(_device, _returned, null);
            if (_ready.Handle != 0) _vk!.DestroySemaphore(_device, _ready, null);
            if (_staging.Handle != 0) _vk!.DestroyBuffer(_device, _staging, null);
            if (_stagingMemory.Handle != 0) _vk!.FreeMemory(_device, _stagingMemory, null);
            if (_image.Handle != 0) _vk!.DestroyImage(_device, _image, null);
            if (_memory.Handle != 0) _vk!.FreeMemory(_device, _memory, null);
            if (_deviceOwner == null) { _vk!.DestroyDevice(_device, null); DeviceReleases++; } _device = default;
        }
        if (_instance.Handle != 0)
        {
            if (_messenger != 0)
            {
                var destroy = (delegate* unmanaged<nint, ulong, void*, void>)_vk!.GetInstanceProcAddr(_instance, "vkDestroyDebugUtilsMessengerEXT").Handle;
                if (destroy == null) throw new NotSupportedException("vkDestroyDebugUtilsMessengerEXT"); destroy(_instance.Handle, _messenger, null); _messenger = 0;
            }
            _vk!.DestroyInstance(_instance, null); _instance = default;
        }
        if (_callbackRoot.IsAllocated) _callbackRoot.Free();
        if (_deviceOwner == null) _vk?.Dispose(); _vk = null;
    }
    private static void Check(Result result, string operation) { if (result != Result.Success) throw new IOException(operation + ": " + result); }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe Silk.NET.Core.Bool32 ValidationCallback(DebugUtilsMessageSeverityFlagsEXT severity, DebugUtilsMessageTypeFlagsEXT type, DebugUtilsMessengerCallbackDataEXT* data, void* user)
    {
        try
        {
            var frame = (VulkanSharedFrame)GCHandle.FromIntPtr((nint)user).Target!;
            if ((severity & DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt) != 0) Interlocked.Increment(ref frame._validationErrors);
            if ((severity & DebugUtilsMessageSeverityFlagsEXT.WarningBitExt) != 0) Interlocked.Increment(ref frame._validationWarnings);
            if (data != null && data->PMessage != null && frame._validationMessages.Count < 64)
            {
                var length = 0; while (length < 16384 && data->PMessage[length] != 0) length++;
                frame._validationMessages.Enqueue(System.Text.Encoding.UTF8.GetString(new ReadOnlySpan<byte>(data->PMessage, length)));
            }
        }
        catch (Exception) { /* Never unwind through the validation callback ABI. */ }
        return false;
    }
}

/// <summary>Bounded validation messages and actual producer resource-release evidence.</summary>
public sealed record GpuProducerDiagnostics(bool ValidationEnabled, int Errors, int Warnings, IReadOnlyList<string> Messages, bool Released, int OpenProcessHandles);
