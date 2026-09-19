using SkiaSharp;
using TrueMoon.Alloy.Platform.Silk;

using var device = new VulkanDevice();
using var extensions = GRVkExtensions.Create(device.GetProcedureAddress,
    device.Instance.Handle, device.PhysicalDevice.Handle, device.InstanceExtensions, device.DeviceExtensions);
using var backend = new GRVkBackendContext
{
    VkInstance = device.Instance.Handle, VkPhysicalDevice = device.PhysicalDevice.Handle,
    VkDevice = device.Device.Handle, VkQueue = device.Queue.Handle,
    GraphicsQueueIndex = device.QueueFamily, MaxAPIVersion = Silk.NET.Vulkan.Vk.Version11,
    Extensions = extensions, GetProcedureAddress = device.GetProcedureAddress
};
using var context = GRContext.CreateVulkan(backend) ?? throw new NotSupportedException("Skia Vulkan context creation failed.");
using var surface = SKSurface.Create(context, false, new SKImageInfo(128, 128))
    ?? throw new NotSupportedException("Skia Vulkan surface creation failed.");
surface.Canvas.Clear(SKColors.Transparent);
using var paint = new SKPaint { Color = SKColors.CornflowerBlue };
surface.Canvas.DrawRect(10, 10, 40, 40, paint);
context.Flush();
context.Submit(true);
using var pixels = new SKBitmap(128, 128);
if (!surface.ReadPixels(pixels.Info, pixels.GetPixels(), pixels.RowBytes, 0, 0))
    throw new InvalidOperationException("GPU readback failed.");
if (pixels.GetPixel(20, 20) != SKColors.CornflowerBlue || pixels.GetPixel(0, 0).Alpha != 0)
    throw new InvalidOperationException("GPU pixels differ from expected color/alpha.");
Console.WriteLine($"Vulkan / Skia smoke passed: {context.Backend}; color and transparent pixels verified.");
