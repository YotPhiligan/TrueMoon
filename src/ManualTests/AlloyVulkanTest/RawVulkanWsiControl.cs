using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Platform.Silk;

namespace AlloyVulkanTest;

// Hidden Win32 STATIC window is owned by the caller; no GLFW, Silk Window,
// VulkanDevice, Skia or presentation. Only Silk's raw Vulkan bindings are used.
internal static unsafe class RawVulkanWsiControl
{
    internal static void Run(nint hwnd, string stage, bool surfaceFirst, int repeats, uint vendor, Action<string> checkpoint)
    {
        using var api = Vk.GetApi();
        Instance instance = default;
        SurfaceKHR surface = default;
        Device device = default;
        KhrSurface? surfaceApi = null;
        KhrWin32Surface? win32Api = null;
        Exception? failure = null;
        try
        {
            using var extensions = SilkMarshal.StringArrayToMemory([KhrSurface.ExtensionName, KhrWin32Surface.ExtensionName]);
            var application = new ApplicationInfo { SType = StructureType.ApplicationInfo, ApiVersion = Vk.Version11 };
            var createInstance = new InstanceCreateInfo { SType = StructureType.InstanceCreateInfo, PApplicationInfo = &application,
                EnabledExtensionCount = 2, PpEnabledExtensionNames = (byte**)extensions.Handle };
            VulkanDevice.Check(api.CreateInstance(in createInstance, null, out instance), "Raw.CreateInstance");
            if (!api.TryGetInstanceExtension(instance, out surfaceApi) || !api.TryGetInstanceExtension(instance, out win32Api)
                || surfaceApi == null || win32Api == null)
                throw new NotSupportedException("Raw Win32 Vulkan surface extensions unavailable.");
            var createSurface = new Win32SurfaceCreateInfoKHR { SType = StructureType.Win32SurfaceCreateInfoKhr,
                Hwnd = hwnd, Hinstance = NativeWindowDiagnostics.Module };
            VulkanDevice.Check(win32Api.CreateWin32Surface(instance, in createSurface, null, out surface), "Raw.CreateWin32Surface");
            checkpoint("raw-after-surface");
            PhysicalDevice selected = default;
            uint family = 0;
            foreach (var gpu in api.GetPhysicalDevices(instance))
            {
                api.GetPhysicalDeviceProperties(gpu, out var candidate);
                if (vendor != 0 && candidate.VendorID != vendor) continue;
                uint count = 0;
                api.GetPhysicalDeviceQueueFamilyProperties(gpu, ref count, null);
                var families = new QueueFamilyProperties[count];
                fixed (QueueFamilyProperties* pointer = families) api.GetPhysicalDeviceQueueFamilyProperties(gpu, ref count, pointer);
                for (uint i = 0; i < count; i++)
                {
                    if ((families[i].QueueFlags & QueueFlags.GraphicsBit) == 0) continue;
                    VulkanDevice.Check(surfaceApi.GetPhysicalDeviceSurfaceSupport(gpu, i, surface, out var supported), "Raw.SurfaceSupport");
                    if (!supported) continue;
                    selected = gpu; family = i; break;
                }
                if (selected.Handle != 0) break;
            }
            if (selected.Handle == 0) throw new NotSupportedException($"No raw graphics/present GPU matching vendor 0x{vendor:X} (zero selects first eligible).");
            api.GetPhysicalDeviceProperties(selected, out var properties);
            Console.WriteLine($"Raw Vulkan GPU: {System.Runtime.InteropServices.Marshal.PtrToStringUTF8((nint)properties.DeviceName)}; vendor=0x{properties.VendorID:X}; device=0x{properties.DeviceID:X}; driver={properties.DriverVersion}.");
            using var deviceExtensions = SilkMarshal.StringArrayToMemory([KhrSwapchain.ExtensionName]);
            float priority = 1;
            var queue = new DeviceQueueCreateInfo { SType = StructureType.DeviceQueueCreateInfo, QueueFamilyIndex = family,
                QueueCount = 1, PQueuePriorities = &priority };
            var createDevice = new DeviceCreateInfo { SType = StructureType.DeviceCreateInfo, QueueCreateInfoCount = 1, PQueueCreateInfos = &queue,
                EnabledExtensionCount = 1, PpEnabledExtensionNames = (byte**)deviceExtensions.Handle };
            VulkanDevice.Check(api.CreateDevice(selected, in createDevice, null, out device), "Raw.CreateDevice");
            checkpoint("raw-after-device");
            for (var repeat = 0; repeat < repeats; repeat++)
                VulkanTransparencyIsolation.RunSwapchainControl(new VulkanTransparencyIsolation.WsiContext(api, instance, selected, device, surface),
                    stage, phase => checkpoint($"wsi-{repeat + 1}/{phase}"));
        }
        catch (Exception error) { failure = error; }
        void DestroySurface()
        {
            if (surface.Handle == 0) return;
            surfaceApi!.DestroySurface(instance, surface, null); surface = default;
            checkpoint("raw-after-surface-destroy");
        }
        void DestroyDevice()
        {
            if (device.Handle == 0) return;
            // No acquire, submit or present occurred; there is no GPU/presentation work to drain.
            api.DestroyDevice(device, null); device = default;
            checkpoint("raw-after-device-destroy");
        }
        UiCleanup.Complete(failure,
            surfaceFirst ? DestroySurface : DestroyDevice,
            surfaceFirst ? DestroyDevice : DestroySurface,
            () => { if (instance.Handle != 0) { api.DestroyInstance(instance, null); checkpoint("raw-after-instance-destroy"); } },
            () => win32Api?.Dispose(), () => surfaceApi?.Dispose());
    }
}
