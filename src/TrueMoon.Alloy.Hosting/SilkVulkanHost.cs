using Silk.NET.Vulkan;
using SkiaSharp;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;

namespace TrueMoon.Alloy.Hosting;

/// <summary>Connects a Silk Vulkan device to the independent Skia renderer without transferring host ownership.</summary>
public static class SilkVulkanHost
{
    /// <summary>Creates a borrowed descriptor for the Silk convenience host.</summary>
    /// <param name="device">Host-owned device; dispose it after all UI sessions and surfaces.</param>
    /// <returns>A descriptor which roots the host callbacks and never disposes the host.</returns>
    public static VulkanHostContext CreateContext(VulkanDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        device.VerifyAvailable();
        // The convenience device enables no Skia-relevant optional features. Window maintenance1
        // is used by its presenter, not by Skia; do not advertise queried GPU features as enabled.
        return new(device.Instance.Handle, device.PhysicalDevice.Handle, device.Device.Handle, device.Queue.Handle,
            device.QueueFamily, Vk.Version11, device.GetProcedureAddress, device.WaitIdle,
            device.InstanceExtensions, device.DeviceExtensions);
    }

    /// <summary>Creates a UI target on a borrowed Silk Vulkan device.</summary>
    /// <param name="device">Device kept alive by the caller until all UI sessions close.</param>
    /// <returns>A raw-host target which does not own the device.</returns>
    public static VulkanUiTarget CreateTarget(VulkanDevice device) => new(CreateContext(device));

    /// <summary>Creates an offscreen surface; the device and Skia context remain borrowed.</summary>
    /// <param name="device">Host-owned device which outlives the surface.</param>
    /// <param name="context">Vulkan Skia context created on the same device and graphics queue.</param>
    /// <param name="size">Pixel dimensions of the UI surface.</param>
    /// <returns>A caller-owned surface which must be disposed before its context and device.</returns>
    public static VulkanInteropSurface CreateSurface(VulkanDevice device, GRContext context, SKSizeI size)
        => new(CreateContext(device), context, size);
}
