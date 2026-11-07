using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;

namespace TrueMoon.Aether.Gpu.Vulkan;

/// <summary>Cached resource-backend observations, including validation during teardown.</summary>
/// <param name="ValidationEnabled">Validation was requested and context created.</param><param name="Errors">Validation errors.</param>
/// <param name="Warnings">Validation warnings.</param><param name="DevicesCreated">Created logical devices.</param><param name="DevicesReleased">Destroyed logical devices.</param>
/// <param name="BuffersCreated">Created geometry/staging buffers.</param><param name="BuffersReleased">Destroyed buffers.</param>
/// <param name="AllocationsCreated">Allocated device memories.</param><param name="AllocationsReleased">Freed device memories.</param>
/// <param name="Uploads">Confirmed uploads.</param><param name="VerifiedUploads">Confirmed diagnostic GPU readbacks matching input SHA256.</param>
/// <param name="LiveUploads">Unreleased upload owners.</param><param name="DeviceName">Copied physical device name.</param><param name="Messages">Bounded validation messages.</param>
public sealed record VulkanResourceDiagnostics(bool ValidationEnabled, int Errors, int Warnings, int DevicesCreated, int DevicesReleased,
    int BuffersCreated, int BuffersReleased, int AllocationsCreated, int AllocationsReleased, int Uploads, int VerifiedUploads,
    int LiveUploads, string? DeviceName, string[] Messages);

/// <summary>Lazy standalone Vulkan geometry preparation context. It performs no shader, draw or surface work.</summary>
/// <remarks>Uploads borrow this externally synchronized context. Their owners must release before backend disposal.</remarks>
public sealed class VulkanRenderResourceBackend : IRenderResourceBackend, IFactory<IRenderMeshUpload, RenderMeshUploadRequest>
{
    private readonly Lock _ownersGate = new();
    private readonly HashSet<VulkanMeshUpload> _uploads = [];
    private readonly RenderDeviceRequest _request;
    internal readonly SemaphoreSlim NativeGate = new(1, 1);
    internal Vk? Api;
    internal Device Device;
    internal PhysicalDevice Physical;
    internal Queue Queue;
    internal CommandPool Pool;
    private Instance _instance;
    private ulong _messenger;
    private GCHandle _callbackRoot;
    private bool _contextReady, _closed;
    private Exception? _contextFailure;
    private readonly ConcurrentQueue<string> _messages = new();
    private string? _deviceName;
    private int _errors, _warnings, _devicesCreated, _devicesReleased;
    internal int BuffersCreated, BuffersReleased, AllocationsCreated, AllocationsReleased, UploadsCompleted, UploadsVerified;
    /// <summary>Stores arguments without loading Vulkan or creating native objects.</summary><param name="request">Explicit context/validation settings.</param>
    public VulkanRenderResourceBackend(RenderDeviceRequest request)
    { ArgumentNullException.ThrowIfNull(request); request.Identity.Validate(); ResourceGuard.Require(request.DeviceGeneration != 0, RenderResourceError.InvalidIdentity, "Zero device generation."); _request = request; }
    /// <summary>Gets this CPU-only upload factory; no second DI owner is registered.</summary>
    public IFactory<IRenderMeshUpload, RenderMeshUploadRequest> MeshUploads => this;
    /// <summary>Whether all native context ownership released.</summary>
    public bool IsReleased { get; private set; }
    /// <summary>Gets copied diagnostics without native queries.</summary>
    public VulkanResourceDiagnostics Diagnostics
    {
        get
        {
            lock (_ownersGate) return new(_request.EnableValidation && _devicesCreated != 0, _errors, _warnings, _devicesCreated, _devicesReleased,
                BuffersCreated, BuffersReleased, AllocationsCreated, AllocationsReleased, UploadsCompleted, UploadsVerified, _uploads.Count, _deviceName, _messages.ToArray());
        }
    }
    /// <summary>Returns an owner before allocation/upload begins.</summary><param name="arguments">Geometry and mandatory budget.</param><returns>Caller-owned upload.</returns>
    public IRenderMeshUpload Create(RenderMeshUploadRequest arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(arguments.Mesh); ArgumentNullException.ThrowIfNull(arguments.Budget);
        arguments.Mesh.Key.Validate(RenderContentProfile.MeshKindId);
        ResourceGuard.Require(arguments.Mesh.Definition is MeshDefinition && arguments.Mesh.Key.Revision == arguments.Mesh.Definition.Revision,
            RenderResourceError.InvalidIdentity, "Upload requires matching validated geometry.");
        lock (_ownersGate)
        {
            ResourceGuard.Require(!_closed, RenderResourceError.Closed, "Vulkan backend is closed.");
            var upload = new VulkanMeshUpload(this, arguments, _request.VerifyUploads); _uploads.Add(upload); return upload;
        }
    }
    internal void Released(VulkanMeshUpload upload) { lock (_ownersGate) _uploads.Remove(upload); }
    internal unsafe void EnsureContext()
    {
        if (_contextReady) return;
        if (_contextFailure != null) throw new RenderResourceException(RenderResourceError.BackendFailure, "Failed Vulkan context requires disposal/recreation.", _contextFailure);
        try
        {
            Api = Vk.GetApi();
            var application = new ApplicationInfo { SType = StructureType.ApplicationInfo, ApiVersion = Vk.Version11 };
            var create = new InstanceCreateInfo { SType = StructureType.InstanceCreateInfo, PApplicationInfo = &application };
            using var extensions = SilkMarshal.StringArrayToMemory(_request.EnableValidation ? ["VK_EXT_debug_utils", "VK_EXT_validation_features"] : []);
            using var layers = SilkMarshal.StringArrayToMemory(_request.EnableValidation ? ["VK_LAYER_KHRONOS_validation"] : []);
            var debug = new DebugUtilsMessengerCreateInfoEXT { SType = StructureType.DebugUtilsMessengerCreateInfoExt,
                MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt,
                MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt };
            var feature = ValidationFeatureEnableEXT.SynchronizationValidationExt;
            var validation = new ValidationFeaturesEXT { SType = StructureType.ValidationFeaturesExt, EnabledValidationFeatureCount = 1, PEnabledValidationFeatures = &feature, PNext = &debug };
            if (_request.EnableValidation)
            {
                _callbackRoot = GCHandle.Alloc(this); debug.PUserData = (void*)GCHandle.ToIntPtr(_callbackRoot); debug.PfnUserCallback = new PfnDebugUtilsMessengerCallbackEXT(&ValidationCallback);
                create.PNext = &validation; create.EnabledLayerCount = 1; create.PpEnabledLayerNames = (byte**)layers.Handle;
                create.EnabledExtensionCount = 2; create.PpEnabledExtensionNames = (byte**)extensions.Handle;
            }
            Check(Api.CreateInstance(in create, null, out _instance), "CreateResourceInstance");
            if (_request.EnableValidation)
            {
                var function = (delegate* unmanaged<nint, DebugUtilsMessengerCreateInfoEXT*, void*, ulong*, Result>)Api.GetInstanceProcAddr(_instance, "vkCreateDebugUtilsMessengerEXT").Handle;
                if (function == null) throw new NotSupportedException("Vulkan debug messenger is unavailable.");
                ulong messenger = 0; Check(function(_instance.Handle, &debug, null, &messenger), "CreateResourceDebugMessenger"); _messenger = messenger;
            }
            uint count = 0; Check(Api.EnumeratePhysicalDevices(_instance, &count, null), "EnumerateResourceDevices");
            if (count is 0 or > 16) throw new NotSupportedException("Resource physical-device count.");
            var devices = new PhysicalDevice[count]; fixed (PhysicalDevice* pointer = devices) Check(Api.EnumeratePhysicalDevices(_instance, &count, pointer), "EnumerateResourceDevices");
            uint? selectedFamily = null;
            foreach (var physical in devices)
            {
                uint familyCount = 0; Api.GetPhysicalDeviceQueueFamilyProperties(physical, &familyCount, null);
                if (familyCount is 0 or > 256) continue;
                var families = new QueueFamilyProperties[familyCount]; fixed (QueueFamilyProperties* pointer = families) Api.GetPhysicalDeviceQueueFamilyProperties(physical, &familyCount, pointer);
                var index = Array.FindIndex(families, static family => family.QueueCount > 0 && (family.QueueFlags & QueueFlags.GraphicsBit) != 0);
                if (index < 0) continue;
                Physical = physical; selectedFamily = (uint)index;
                Api.GetPhysicalDeviceProperties(physical, out var properties);
                var name = new ReadOnlySpan<byte>(properties.DeviceName, 256); var end = name.IndexOf((byte)0);
                _deviceName = System.Text.Encoding.UTF8.GetString(end < 0 ? name : name[..end]); break;
            }
            if (selectedFamily == null) throw new NotSupportedException("No graphics resource queue.");
            float priority = 1;
            var queue = new DeviceQueueCreateInfo { SType = StructureType.DeviceQueueCreateInfo, QueueFamilyIndex = selectedFamily.Value, QueueCount = 1, PQueuePriorities = &priority };
            var device = new DeviceCreateInfo { SType = StructureType.DeviceCreateInfo, QueueCreateInfoCount = 1, PQueueCreateInfos = &queue };
            Check(Api.CreateDevice(Physical, in device, null, out Device), "CreateResourceDevice"); Interlocked.Increment(ref _devicesCreated);
            Api.GetDeviceQueue(Device, selectedFamily.Value, 0, out Queue);
            var pool = new CommandPoolCreateInfo { SType = StructureType.CommandPoolCreateInfo, QueueFamilyIndex = selectedFamily.Value, Flags = CommandPoolCreateFlags.ResetCommandBufferBit };
            Check(Api.CreateCommandPool(Device, in pool, null, out Pool), "CreateResourceCommandPool");
            _contextReady = true;
        }
        catch (Exception error) { _contextFailure = error; throw; }
    }
    internal unsafe uint MemoryType(uint bits, MemoryPropertyFlags required)
    {
        Api!.GetPhysicalDeviceMemoryProperties(Physical, out var properties);
        for (var i = 0; i < properties.MemoryTypeCount; i++)
            if ((bits & (1u << i)) != 0 && (properties.MemoryTypes[i].PropertyFlags & required) == required) return (uint)i;
        throw new NotSupportedException("Required Vulkan resource memory type is unavailable.");
    }
    internal static void Check(Result result, string operation)
    { if (result != Result.Success) throw new RenderResourceException(RenderResourceError.BackendFailure, operation + ": " + result); }
    /// <summary>Requires uploads to be released, then destroys the context after confirmed idle. Unknown outcome retains ownership.</summary>
    public async ValueTask DisposeAsync()
    {
        lock (_ownersGate) { if (IsReleased) return; _closed = true; ResourceGuard.Require(_uploads.Count == 0, RenderResourceError.CleanupFailed, "Release Vulkan upload owners before their context."); }
        await NativeGate.WaitAsync().ConfigureAwait(false);
        try { if (!IsReleased) ReleaseContext(); }
        finally { NativeGate.Release(); }
    }
    private unsafe void ReleaseContext()
    {
        if (Api != null)
        {
            if (Device.Handle != 0)
            {
                var idle = Api.DeviceWaitIdle(Device);
                ResourceGuard.Require(idle == Result.Success, RenderResourceError.UnknownOutcome, "Resource device idle is unconfirmed: " + idle);
                if (Pool.Handle != 0) { Api.DestroyCommandPool(Device, Pool, null); Pool = default; }
                Api.DestroyDevice(Device, null); Device = default; Interlocked.Increment(ref _devicesReleased);
            }
            if (_messenger != 0)
            {
                var destroy = (delegate* unmanaged<nint, ulong, void*, void>)Api.GetInstanceProcAddr(_instance, "vkDestroyDebugUtilsMessengerEXT").Handle;
                if (destroy == null) throw new NotSupportedException("Vulkan debug messenger destroy is unavailable.");
                destroy(_instance.Handle, _messenger, null); _messenger = 0;
            }
            if (_instance.Handle != 0) { Api.DestroyInstance(_instance, null); _instance = default; }
            Api.Dispose(); Api = null;
        }
        if (_callbackRoot.IsAllocated) _callbackRoot.Free(); IsReleased = true;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe Silk.NET.Core.Bool32 ValidationCallback(DebugUtilsMessageSeverityFlagsEXT severity, DebugUtilsMessageTypeFlagsEXT type,
        DebugUtilsMessengerCallbackDataEXT* data, void* user)
    {
        try
        {
            var owner = (VulkanRenderResourceBackend)GCHandle.FromIntPtr((nint)user).Target!;
            if ((severity & DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt) != 0) Interlocked.Increment(ref owner._errors);
            if ((severity & DebugUtilsMessageSeverityFlagsEXT.WarningBitExt) != 0) Interlocked.Increment(ref owner._warnings);
            if (data != null && data->PMessage != null)
            {
                lock (owner._messages)
                {
                    if (owner._messages.Count < 32)
                    { var length = 0; while (length < 4096 && data->PMessage[length] != 0) length++; owner._messages.Enqueue(System.Text.Encoding.UTF8.GetString(new ReadOnlySpan<byte>(data->PMessage, length))); }
                }
            }
        }
        catch { /* Never unwind into the native callback ABI. */ }
        return false;
    }
}
