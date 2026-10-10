using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Windowing;

namespace TrueMoon.Alloy.Platform.Silk;

/// <summary>Owns a Vulkan device and one graphics queue. All queue access must be serialized by the host.</summary>
public sealed unsafe class VulkanDevice : IDisposable
{
    private bool _disposed;
    private readonly IWindow? _window;
    private readonly Action<string>? _validationMessage;
    private DebugUtilsMessengerCallbackFunctionEXT? _debugCallback;
    private ExtDebugUtils? _debugUtils;
    private DebugUtilsMessengerEXT _debugMessenger;
    private int _validationErrors;
    private int _validationWarnings;
    private readonly bool _enablePresentFences;
    /// <summary>The enabled swapchain maintenance extension, or null on legacy/offscreen devices.</summary>
    public string? PresentFenceExtension { get; private set; }
    /// <summary>Whether Khronos core and synchronization validation were requested.</summary>
    public bool ValidationEnabled => _validationMessage != null;
    /// <summary>Number of validation errors, including errors reported during disposal.</summary>
    public int ValidationErrorCount => Volatile.Read(ref _validationErrors);
    /// <summary>Number of validation warnings reported by the debug messenger.</summary>
    public int ValidationWarningCount => Volatile.Read(ref _validationWarnings);
    /// <summary>The Vulkan entry points.</summary>
    public Vk Api { get; } = Vk.GetApi();
    /// <summary>The instance owned by this host.</summary>
    public Instance Instance { get; private set; }
    /// <summary>The selected GPU.</summary>
    public PhysicalDevice PhysicalDevice { get; private set; }
    /// <summary>The logical device.</summary>
    public Device Device { get; private set; }
    /// <summary>The graphics and presentation queue.</summary>
    public Queue Queue { get; private set; }
    /// <summary>The queue family index.</summary>
    public uint QueueFamily { get; private set; }
    /// <summary>The optional window surface.</summary>
    public SurfaceKHR Surface { get; private set; }
    /// <summary>Enabled instance extensions.</summary>
    public string[] InstanceExtensions { get; private set; } = [];
    /// <summary>Enabled device extensions.</summary>
    public string[] DeviceExtensions { get; private set; } = [];
    /// <summary>Creates a graphics device, optionally capable of presenting to a Silk window.</summary>
    /// <param name="window">Optional window whose presentation surface is owned by this host.</param>
    /// <param name="validationMessage">If supplied, requires Khronos validation and reports warnings/errors, with synchronization validation enabled.</param>
    public VulkanDevice(IWindow? window = null, Action<string>? validationMessage = null)
        : this(window, validationMessage, true) { }

    /// <summary>Creates a device with optional presentation fences; disabling them exercises the legacy retirement path.</summary>
    public VulkanDevice(IWindow? window, Action<string>? validationMessage, bool enablePresentFences)
    {
        _window = window;
        _validationMessage = validationMessage;
        _enablePresentFences = enablePresentFences;
        try { Initialize(); }
        catch (Exception error) { UiCleanup.Complete(error, Dispose); throw; }
    }

    private void Initialize()
    {
        var app = new ApplicationInfo { SType = StructureType.ApplicationInfo, ApiVersion = Vk.Version11 };
        uint count = 0;
        if (_window != null)
        {
            var extensions = _window.VkSurface!.GetRequiredExtensions(out count);
            InstanceExtensions = Enumerable.Range(0, (int)count)
                .Select(i => Marshal.PtrToStringUTF8((nint)extensions[i])!).ToArray();
            if (_enablePresentFences)
            {
                var available = AvailableExtensions();
                if (available.Contains("VK_KHR_get_surface_capabilities2"))
                {
                    var optional = new[] { "VK_KHR_get_surface_capabilities2", "VK_KHR_surface_maintenance1", "VK_EXT_surface_maintenance1" };
                    InstanceExtensions = [.. InstanceExtensions, .. optional.Where(available.Contains)];
                }
            }
        }
        if (ValidationEnabled)
        {
            RequireValidationLayer();
            InstanceExtensions = [.. InstanceExtensions, ExtDebugUtils.ExtensionName, "VK_EXT_validation_features"];
            _debugCallback = ReportValidation;
        }
        using var instanceNames = SilkMarshal.StringArrayToMemory(InstanceExtensions);
        using var layerNames = SilkMarshal.StringArrayToMemory(ValidationEnabled ? ["VK_LAYER_KHRONOS_validation"] : []);
        var debugInfo = new DebugUtilsMessengerCreateInfoEXT
        {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.WarningBitExt | DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
            MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt
        };
        if (_debugCallback != null) debugInfo.PfnUserCallback = new PfnDebugUtilsMessengerCallbackEXT(_debugCallback);
        var synchronization = ValidationFeatureEnableEXT.SynchronizationValidationExt;
        var validationFeatures = new ValidationFeaturesEXT
        {
            SType = StructureType.ValidationFeaturesExt,
            EnabledValidationFeatureCount = 1,
            PEnabledValidationFeatures = &synchronization,
            PNext = &debugInfo
        };
        var create = new InstanceCreateInfo
        {
            SType = StructureType.InstanceCreateInfo, PApplicationInfo = &app,
            EnabledExtensionCount = (uint)InstanceExtensions.Length, PpEnabledExtensionNames = (byte**)instanceNames.Handle,
            EnabledLayerCount = ValidationEnabled ? 1u : 0u, PpEnabledLayerNames = (byte**)layerNames.Handle,
            PNext = ValidationEnabled ? &validationFeatures : null
        };
        Check(Api.CreateInstance(in create, null, out var instance), "CreateInstance");
        Instance = instance;
        if (ValidationEnabled)
        {
            if (!Api.TryGetInstanceExtension(Instance, out _debugUtils))
                throw new NotSupportedException("VK_EXT_debug_utils is unavailable.");
            Check(_debugUtils.CreateDebugUtilsMessenger(Instance, in debugInfo, null, out _debugMessenger), "CreateDebugUtilsMessenger");
        }
        KhrSurface? surfaceApi = null;
        if (_window != null)
        {
            Surface = _window.VkSurface!.Create<AllocationCallbacks>(new VkHandle(Instance.Handle), null).ToSurface();
            if (!Api.TryGetInstanceExtension(Instance, out surfaceApi))
                throw new NotSupportedException("VK_KHR_surface is unavailable.");
            DeviceExtensions = [KhrSwapchain.ExtensionName];
        }
        try
        {
            var gpus = Api.GetPhysicalDevices(Instance);
            foreach (var gpu in gpus)
            {
                uint familyCount = 0;
                Api.GetPhysicalDeviceQueueFamilyProperties(gpu, ref familyCount, null);
                var families = new QueueFamilyProperties[familyCount];
                fixed (QueueFamilyProperties* familyPtr = families)
                    Api.GetPhysicalDeviceQueueFamilyProperties(gpu, ref familyCount, familyPtr);
                for (uint i = 0; i < families.Length; i++)
                {
                    if ((families[i].QueueFlags & QueueFlags.GraphicsBit) == 0) continue;
                    if (surfaceApi != null)
                    {
                        Check(surfaceApi.GetPhysicalDeviceSurfaceSupport(gpu, i, Surface, out var supported), "SurfaceSupport");
                        if (!supported) continue;
                    }
                    PhysicalDevice = gpu;
                    QueueFamily = i;
                    break;
                }
                if (PhysicalDevice.Handle != 0) break;
            }
        }
        finally { surfaceApi?.Dispose(); }
        if (PhysicalDevice.Handle == 0) throw new NotSupportedException("No Vulkan graphics/present queue is available.");
        float priority = 1;
        var queueInfo = new DeviceQueueCreateInfo
        {
            SType = StructureType.DeviceQueueCreateInfo, QueueFamilyIndex = QueueFamily,
            QueueCount = 1, PQueuePriorities = &priority
        };
        var maintenance = new PhysicalDeviceSwapchainMaintenance1FeaturesEXT
        { SType = StructureType.PhysicalDeviceSwapchainMaintenance1FeaturesExt };
        if (_window != null && _enablePresentFences)
        {
            var available = AvailableExtensions(PhysicalDevice);
            var extension = InstanceExtensions.Contains("VK_KHR_surface_maintenance1") && available.Contains("VK_KHR_swapchain_maintenance1")
                ? "VK_KHR_swapchain_maintenance1"
                : InstanceExtensions.Contains("VK_EXT_surface_maintenance1") && available.Contains("VK_EXT_swapchain_maintenance1")
                    ? "VK_EXT_swapchain_maintenance1" : null;
            if (extension != null)
            {
                var features = new PhysicalDeviceFeatures2 { SType = StructureType.PhysicalDeviceFeatures2, PNext = &maintenance };
                Api.GetPhysicalDeviceFeatures2(PhysicalDevice, &features);
                if (maintenance.SwapchainMaintenance1)
                { PresentFenceExtension = extension; DeviceExtensions = [.. DeviceExtensions, extension]; }
            }
        }
        using var names = SilkMarshal.StringArrayToMemory(DeviceExtensions);
        var deviceInfo = new DeviceCreateInfo
        {
            SType = StructureType.DeviceCreateInfo, QueueCreateInfoCount = 1, PQueueCreateInfos = &queueInfo,
            EnabledExtensionCount = (uint)DeviceExtensions.Length,
            PpEnabledExtensionNames = (byte**)names.Handle,
            PNext = PresentFenceExtension != null ? &maintenance : null
        };
        Check(Api.CreateDevice(PhysicalDevice, in deviceInfo, null, out var device), "CreateDevice");
        Device = device;
        Api.GetDeviceQueue(Device, QueueFamily, 0, out var queue);
        Queue = queue;
    }

    private HashSet<string> AvailableExtensions(PhysicalDevice gpu = default)
    {
        uint count = 0;
        var device = gpu.Handle != 0;
        Check(device ? Api.EnumerateDeviceExtensionProperties(gpu, (byte*)null, ref count, null)
            : Api.EnumerateInstanceExtensionProperties((byte*)null, ref count, null), "EnumerateExtensions");
        var extensions = new ExtensionProperties[count];
        var names = new HashSet<string>(StringComparer.Ordinal);
        fixed (ExtensionProperties* ptr = extensions)
        {
            Check(device ? Api.EnumerateDeviceExtensionProperties(gpu, (byte*)null, ref count, ptr)
                : Api.EnumerateInstanceExtensionProperties((byte*)null, ref count, ptr), "EnumerateExtensions");
            for (var i = 0; i < count; i++) names.Add(Marshal.PtrToStringUTF8((nint)ptr[i].ExtensionName)!);
        }
        return names;
    }

    private void RequireValidationLayer()
    {
        uint layerCount = 0;
        Check(Api.EnumerateInstanceLayerProperties(ref layerCount, null), "EnumerateInstanceLayers");
        var layers = new LayerProperties[layerCount];
        fixed (LayerProperties* ptr = layers)
        {
            Check(Api.EnumerateInstanceLayerProperties(ref layerCount, ptr), "EnumerateInstanceLayers");
            for (var i = 0; i < layerCount; i++)
                if (Marshal.PtrToStringUTF8((nint)ptr[i].LayerName) == "VK_LAYER_KHRONOS_validation") return;
        }
        throw new NotSupportedException("VK_LAYER_KHRONOS_validation is unavailable. Set VK_LAYER_PATH to the SDK validation manifest directory.");
    }

    private uint ReportValidation(DebugUtilsMessageSeverityFlagsEXT severity, DebugUtilsMessageTypeFlagsEXT type,
        DebugUtilsMessengerCallbackDataEXT* data, void* userData)
    {
        // No managed exception may cross this native callback boundary. The delegate stays rooted
        // through DestroyInstance, including the temporary messenger in InstanceCreateInfo.pNext.
        try
        {
            if ((severity & DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt) != 0) Interlocked.Increment(ref _validationErrors);
            if ((severity & DebugUtilsMessageSeverityFlagsEXT.WarningBitExt) != 0) Interlocked.Increment(ref _validationWarnings);
            var message = data == null ? "No validation message." : Marshal.PtrToStringUTF8((nint)data->PMessage);
            _validationMessage?.Invoke($"[{severity}/{type}] {message}");
        }
        catch { Interlocked.Increment(ref _validationErrors); }
        return Vk.False;
    }

    /// <summary>Resolves a Vulkan procedure for a consumer such as Skia.</summary>
    public nint GetProcedureAddress(string name, nint instance, nint device)
    {
        VerifyAvailable();
        return device != 0 ? Api.GetDeviceProcAddr(new Device(device), name) : Api.GetInstanceProcAddr(new Instance(instance), name);
    }

    /// <summary>Rejects access after this host has destroyed its native device.</summary>
    public void VerifyAvailable() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>Waits for outstanding GPU work before destroying or handing off resources.</summary>
    public void WaitIdle() { VerifyAvailable(); Check(Api.DeviceWaitIdle(Device), "DeviceWaitIdle"); }

    /// <summary>Throws an actionable error for a failed Vulkan operation.</summary>
    public static void Check(Result result, string operation)
    {
        if (result == Result.ErrorDeviceLost) throw new UiRenderingException("Vulkan", operation, UiRenderingFailureKind.DeviceLost,
            new InvalidOperationException($"Vulkan {operation}: {result}"));
        if (result != Result.Success) throw new InvalidOperationException($"Vulkan {operation}: {result}");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        Exception? failure = null;
        if (Device.Handle != 0)
        {
            try { Check(Api.DeviceWaitIdle(Device), "Dispose.DeviceWaitIdle"); }
            catch (UiRenderingException error) when (error.Kind == UiRenderingFailureKind.DeviceLost) { failure = error; }
        }
        _disposed = true;
        UiCleanup.Complete(failure,
            () => { if (Device.Handle != 0) Api.DestroyDevice(Device, null); },
            () =>
            {
                if (Surface.Handle != 0 && Api.TryGetInstanceExtension<KhrSurface>(Instance, out var surfaceApi))
                {
                    try { surfaceApi.DestroySurface(Instance, Surface, null); }
                    finally { surfaceApi.Dispose(); }
                }
            },
            () => { if (_debugMessenger.Handle != 0) _debugUtils!.DestroyDebugUtilsMessenger(Instance, _debugMessenger, null); },
            () => _debugUtils?.Dispose(),
            () => { if (Instance.Handle != 0) Api.DestroyInstance(Instance, null); },
            () => GC.KeepAlive(_debugCallback), Api.Dispose);
    }
}
