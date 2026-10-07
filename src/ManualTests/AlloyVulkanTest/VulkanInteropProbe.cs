using TrueMoon.Alloy.Rendering.Skia;
using Silk.NET.Vulkan;
using SkiaSharp;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Hosting;

namespace AlloyVulkanTest;

internal static class VulkanInteropProbe
{
    internal static void Run(VulkanDevice device, GRVkBackendContext backend, bool compose = false)
    {
        SkiaNativeLibrary.VerifyVulkanInterop();
        var transfers = 0;
        var compositions = 0;
        for (var session = 0; session < 2; session++)
        {
            using (var context = GRContext.CreateVulkan(backend)
                ?? throw new NotSupportedException("Skia Vulkan context creation failed."))
            {
                foreach (var size in new[] { new SKSizeI(128, 96), new SKSizeI(192, 128), new SKSizeI(128, 96) })
                {
                    using var surface = SilkVulkanHost.CreateSurface(device, context, size);
                    using var compositor = compose ? new VulkanHostCompositor(device, size.Width, size.Height) : null;
                    ulong previousImage = 0;
                    for (var frame = 0; frame < 3; frame++)
                    {
                        var panel = new SKColor((byte)(100 + frame * 40), 80, 40, 128);
                        surface.Draw(canvas =>
                        {
                            canvas.Clear(SKColors.Transparent);
                            using var paint = new SKPaint { Color = panel };
                            canvas.DrawRect(10, 10, size.Width - 20, size.Height - 20, paint);
                            paint.Color = SKColors.CornflowerBlue;
                            canvas.DrawRect(10, size.Height - 30, 20, 20, paint);
                            using var font = new SKFont(SKTypeface.Default, 16);
                            paint.Color = SKColors.White;
                            canvas.DrawText("VkImage HUD", 24, 42, SKTextAlign.Left, font, paint);
                        });
                        var lease = surface.Acquire();
                        var info = lease.Info;
                        var finalLayout = (ImageLayout)info.Layout;
                        try
                        {
                            if (info.Image == 0 || info.Width != size.Width || info.Height != size.Height)
                                throw new InvalidOperationException("Exported VkImage dimensions are invalid.");
                            if (previousImage != 0 && previousImage != info.Image)
                                throw new InvalidOperationException("Drawing without snapshots unexpectedly replaced the UI image.");
                            previousImage = info.Image;
                            AssertBorrowGuard(() => surface.Draw(_ => { }));
                            AssertBorrowGuard(() => { surface.Acquire(); });
                            AssertBorrowGuard(surface.Dispose);
                            if (compositor != null)
                            {
                                // Sample the same borrowed UI image over two independently drawn scenes.
                                for (var variant = 0; variant < 2; variant++)
                                {
                                    info.Layout = (uint)finalLayout;
                                    var scenePixels = compositor.Compose(info, variant, layout => finalLayout = layout);
                                    AssertComposition(scenePixels, size, panel, variant);
                                    compositions++;
                                }
                            }
                            else
                            {
                                var pixels = VulkanHostReadback.Read(device, info, layout => finalLayout = layout);
                                AssertPixel(pixels, size.Width, 0, 0, new SKColor(0, 0, 0, 0));
                                // Vulkan exposes premultiplied bytes, not SKBitmap's unpremultiplied colors.
                                AssertPixel(pixels, size.Width, 20, 20, new SKColor(
                                    Premultiply(panel.Red, panel.Alpha), Premultiply(panel.Green, panel.Alpha),
                                    Premultiply(panel.Blue, panel.Alpha), panel.Alpha), 1);
                                AssertPixel(pixels, size.Width, 20, size.Height - 20, SKColors.CornflowerBlue);
                                var hasText = false;
                                for (var y = 24; y < 44; y++)
                                for (var x = 24; x < size.Width - 10; x++)
                                {
                                    var offset = (y * size.Width + x) * 4;
                                    if (pixels[offset] > 220 && pixels[offset + 1] > 220 && pixels[offset + 2] > 220 && pixels[offset + 3] > 220)
                                        hasText = true;
                                }
                                if (!hasText) throw new InvalidOperationException("No text pixels in externally consumed image.");
                            }
                            transfers++;
                        }
                        finally { lease.Return(finalLayout); }
                        AssertBorrowGuard(() => lease.Return(finalLayout));
                        try { _ = lease.Info; throw new InvalidOperationException("Returned lease remained accessible."); }
                        catch (ObjectDisposedException) { }
                    }
                }
            }
            device.WaitIdle();
        }
        if (compose)
        {
            Console.WriteLine($"Vulkan external composition passed: {compositions} GPU compositions, {transfers} VkImage leases, premultiplied alpha, text, " +
                "shader-read layout return, redraw, surface recreation and 2 Skia contexts on a host-owned device.");
            Console.WriteLine("Scope: independent Vulkan graphics pipelines; CPU readback only after scene composition. Swapchain remains a separate check.");
            return;
        }
        Console.WriteLine($"Vulkan interop probe passed: {transfers} external VkImage readbacks, layout return, redraw, " +
            "surface recreation and 2 Skia contexts on a host-owned device.");
        Console.WriteLine("Scope: sequential same-queue handoff; readback is assertion-only. Scene composition and swapchain remain separate checks.");
    }

    private static byte Premultiply(byte value, byte alpha) => (byte)MathF.Round(value * alpha / 255f);

    private static void AssertComposition(byte[] pixels, SKSizeI size, SKColor panel, int variant)
    {
        var scene = VulkanHostCompositor.SceneColors(variant);
        AssertPixel(pixels, size.Width, 0, 0, scene.Left);
        AssertPixel(pixels, size.Width, size.Width - 1, 0, scene.Right);
        AssertPixel(pixels, size.Width, 20, 20, Blend(panel, scene.Left), 1);
        AssertPixel(pixels, size.Width, size.Width - 20, 20, Blend(panel, scene.Right), 1);
        AssertPixel(pixels, size.Width, 20, size.Height - 20, SKColors.CornflowerBlue);
        var hasText = false;
        for (var y = 24; y < 44; y++)
        for (var x = 24; x < size.Width - 10; x++)
        {
            var offset = (y * size.Width + x) * 4;
            if (pixels[offset] > 220 && pixels[offset + 1] > 220 && pixels[offset + 2] > 220 && pixels[offset + 3] == 255)
                hasText = true;
        }
        if (!hasText) throw new InvalidOperationException("No text pixels in independently composed Vulkan scene.");
        // An opaque scene must stay opaque everywhere, including antialiased text edges.
        for (var i = 3; i < pixels.Length; i += 4)
            if (pixels[i] != 255) throw new InvalidOperationException("Composition lost scene opacity.");
    }

    private static SKColor Blend(SKColor source, SKColor background)
    {
        byte Channel(byte foreground, byte behind) => (byte)MathF.Round(
            Premultiply(foreground, source.Alpha) + behind * (255 - source.Alpha) / 255f);
        return new SKColor(Channel(source.Red, background.Red), Channel(source.Green, background.Green),
            Channel(source.Blue, background.Blue), 255);
    }

    private static void AssertBorrowGuard(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Borrowed surface operation was not rejected.");
    }

    private static void AssertPixel(byte[] pixels, int width, int x, int y, SKColor expected, int tolerance = 0)
    {
        var offset = (y * width + x) * 4;
        if (Math.Abs(pixels[offset] - expected.Red) > tolerance ||
            Math.Abs(pixels[offset + 1] - expected.Green) > tolerance ||
            Math.Abs(pixels[offset + 2] - expected.Blue) > tolerance ||
            Math.Abs(pixels[offset + 3] - expected.Alpha) > tolerance)
            throw new InvalidOperationException($"Unexpected external Vulkan pixel at ({x}, {y}); expected {expected}, " +
                $"actual RGBA=({pixels[offset]},{pixels[offset + 1]},{pixels[offset + 2]},{pixels[offset + 3]}).");
    }
}
