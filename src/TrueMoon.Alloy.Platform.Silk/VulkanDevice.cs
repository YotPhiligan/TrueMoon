using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace TrueMoon.Alloy.Platform.Silk;

/// <summary>Owns a Vulkan device and one graphics queue. All queue access must be serialized by the host.</summary>
public sealed unsafe class VulkanDevice : IDisposable
{
    private bool _disposed;
    private readonly IWindow? _window;
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
    public VulkanDevice(IWindow? window = null)
    {
        _window = window;
        try { Initialize(); }
        catch { Dispose(); throw; }
    }

    private void Initialize()
    {
        var app = new ApplicationInfo { SType = StructureType.ApplicationInfo, ApiVersion = Vk.Version11 };
        uint count = 0;
        byte** extensions = null;
        if (_window != null)
        {
            extensions = _window.VkSurface!.GetRequiredExtensions(out count);
            InstanceExtensions = Enumerable.Range(0, (int)count)
                .Select(i => Marshal.PtrToStringUTF8((nint)extensions[i])!).ToArray();
        }
        var create = new InstanceCreateInfo
        {
            SType = StructureType.InstanceCreateInfo, PApplicationInfo = &app,
            EnabledExtensionCount = count, PpEnabledExtensionNames = extensions
        };
        Check(Api.CreateInstance(in create, null, out var instance), "CreateInstance");
        Instance = instance;
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
        using var names = SilkMarshal.StringArrayToMemory(DeviceExtensions);
        var deviceInfo = new DeviceCreateInfo
        {
            SType = StructureType.DeviceCreateInfo, QueueCreateInfoCount = 1, PQueueCreateInfos = &queueInfo,
            EnabledExtensionCount = (uint)DeviceExtensions.Length,
            PpEnabledExtensionNames = (byte**)names.Handle
        };
        Check(Api.CreateDevice(PhysicalDevice, in deviceInfo, null, out var device), "CreateDevice");
        Device = device;
        Api.GetDeviceQueue(Device, QueueFamily, 0, out var queue);
        Queue = queue;
    }

    /// <summary>Resolves a Vulkan procedure for a consumer such as Skia.</summary>
    public nint GetProcedureAddress(string name, nint instance, nint device) =>
        device != 0 ? Api.GetDeviceProcAddr(new Device(device), name) : Api.GetInstanceProcAddr(new Instance(instance), name);

    /// <summary>Waits for outstanding GPU work before destroying or handing off resources.</summary>
    public void WaitIdle() => Check(Api.DeviceWaitIdle(Device), "DeviceWaitIdle");

    /// <summary>Throws an actionable error for a failed Vulkan operation.</summary>
    public static void Check(Result result, string operation)
    {
        if (result != Result.Success) throw new InvalidOperationException($"Vulkan {operation}: {result}");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Device.Handle != 0)
        {
            Api.DeviceWaitIdle(Device);
            Api.DestroyDevice(Device, null);
        }
        if (Surface.Handle != 0 && Api.TryGetInstanceExtension<KhrSurface>(Instance, out var surfaceApi))
        {
            surfaceApi.DestroySurface(Instance, Surface, null);
            surfaceApi.Dispose();
        }
        if (Instance.Handle != 0) Api.DestroyInstance(Instance, null);
        Api.Dispose();
    }
}
