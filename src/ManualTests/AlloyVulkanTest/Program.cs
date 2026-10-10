using SkiaSharp;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using AlloyVulkanTest;

// Isolation must run before loading native Skia: only raw GL/Vulkan calls are used.
if (args.Contains("--vulkan-transparency-isolation")) { VulkanTransparencyIsolation.Run(args); return; }

var nativePathIndex = Array.IndexOf(args, "--native-skia");
if (nativePathIndex >= 0 && nativePathIndex + 1 == args.Length)
    throw new ArgumentException("--native-skia requires a DLL path.");
SkiaNativeLibrary.Initialize(nativePathIndex >= 0 ? args[nativePathIndex + 1] : null);
Console.WriteLine($"Native Skia: {SkiaNativeLibrary.LoadedPath}");
if (args.Contains("--window-dpi")) { WindowDpiProbe.Run(args); return; }
if (args.Contains("--window-chrome")) { await WindowChromeProbe.RunAsync(args.Contains("--validation")); return; }
if (args.Contains("--window-snap")) { WindowSnapProbe.Run(args.Contains("--validation")); return; }
if (args.Contains("--window-snap-input")) { WindowSnapProbe.RunInput(args); return; }
if (args.Contains("--window-snap-bar")) { WindowSnapProbe.RunBarInput(args); return; }
if (args.Contains("--snap-keyboard-control")) { WindowSnapProbe.RunKeyboardControl(); return; }
if (args.Contains("--window-appearance")) { WindowAppearanceProbe.Run(args); return; }
if (args.Contains("--window-transparency")) { WindowTransparencyProbe.Run(args); return; }
if (args.Contains("--settings-baseline")) { SettingsPerformanceProbe.Run(args); return; }
if (args.Contains("--settings-failure")) { await RenderingFailureProbe.RunAsync(args.Contains("--validation")); return; }
if (args.Contains("--settings-compare")) { SettingsBackendProbe.Run(args.Contains("--validation")); return; }
if (args.Contains("--settings-hud") || args.Contains("--settings-hud-demo"))
{ SettingsHudProbe.Run(args.Contains("--validation"), args.Contains("--settings-hud-demo")); return; }
if (args.Contains("--raster"))
{
    var outputIndex = Array.IndexOf(args, "--raster-output");
    if (outputIndex >= 0 && outputIndex + 1 == args.Length) throw new ArgumentException("--raster-output requires a PNG path.");
    RasterProbe.Run(outputIndex >= 0 ? args[outputIndex + 1] : null); return;
}
if (args.Contains("--backend-compare")) { RasterProbe.Compare(args.Contains("--validation")); return; }
if (args.Contains("--opengl")) { OpenGLProbe.Run(); return; }
if (args.Contains("--opengl-window")) { await RuntimeProbe.RunWindowAsync(false, true); return; }
if (args.Contains("--opengl-failure")) { await OpenGLProbe.RunFailureAsync(); return; }
if (args.Contains("--raw-host")) { VulkanRawHostProbe.Run(args.Contains("--validation")); return; }
if (args.Contains("--retirement-soak"))
{
    var cycleIndex = Array.IndexOf(args, "--soak-cycles");
    if (cycleIndex >= 0 && cycleIndex + 1 == args.Length) throw new ArgumentException("--soak-cycles requires a count.");
    var cycles = cycleIndex >= 0 ? int.Parse(args[cycleIndex + 1]) : 200;
    VulkanRetirementProbe.Run(args.Contains("--validation"), cycles, args.Contains("--legacy-presentation"));
    return;
}
if (args.Contains("--runtime")) { RuntimeProbe.Run(args.Contains("--validation")); return; }
if (args.Contains("--window")) { await RuntimeProbe.RunWindowAsync(args.Contains("--validation")); return; }
if (args.Contains("--hud-window") || args.Contains("--hud-demo") || args.Contains("--raw-hud-window"))
{ VulkanHudWindowProbe.Run(args.Contains("--validation"), args.Contains("--hud-demo"), args.Contains("--raw-hud-window")); return; }
if (args.Contains("--interop-check") || args.Contains("--interop") || args.Contains("--interop-compose"))
{
    try { SkiaNativeLibrary.VerifyVulkanInterop(); }
    catch (NotSupportedException error)
    {
        Console.Error.WriteLine(error.Message);
        Environment.ExitCode = 2;
        return;
    }
    if (args.Contains("--interop-check"))
    {
        Console.WriteLine("TrueMoon Vulkan interop ABI v1 verified.");
        return;
    }
}

// The host owns the Vulkan device. Each simulated UI session owns only its Skia resources.
var validation = args.Contains("--validation");
VulkanDevice device;
try { device = new VulkanDevice(validationMessage: validation ? message => Console.Error.WriteLine($"Vulkan validation: {message}") : null); }
catch (NotSupportedException error)
{
    Console.Error.WriteLine(error.Message);
    Environment.ExitCode = 2;
    return;
}
using (device) { RunGpuProbe(device, args); }
if (validation)
{
    if (device.ValidationErrorCount != 0)
        throw new InvalidOperationException($"Vulkan validation failed: {device.ValidationErrorCount} errors, {device.ValidationWarningCount} warnings, including resource disposal.");
    Console.WriteLine($"Vulkan core/synchronization validation passed: 0 errors, {device.ValidationWarningCount} warnings, including resource disposal.");
}

static void RunGpuProbe(VulkanDevice device, string[] args)
{
    using var extensions = GRVkExtensions.Create(device.GetProcedureAddress,
        device.Instance.Handle, device.PhysicalDevice.Handle, device.InstanceExtensions, device.DeviceExtensions);
    using var backend = new GRVkBackendContext
    {
        VkInstance = device.Instance.Handle, VkPhysicalDevice = device.PhysicalDevice.Handle,
        VkDevice = device.Device.Handle, VkQueue = device.Queue.Handle,
        GraphicsQueueIndex = device.QueueFamily, MaxAPIVersion = Silk.NET.Vulkan.Vk.Version11,
        Extensions = extensions, GetProcedureAddress = device.GetProcedureAddress
    };
    var compositions = 0;
    if (args.Contains("--interop") || args.Contains("--interop-compose"))
    {
        VulkanInteropProbe.Run(device, backend, args.Contains("--interop-compose"));
        return;
    }
    for (var session = 0; session < 2; session++)
    {
        using (var context = GRContext.CreateVulkan(backend)
            ?? throw new NotSupportedException("Skia Vulkan context creation failed."))
        {
            if (context.Backend != GRBackend.Vulkan)
                throw new InvalidOperationException("Expected a Vulkan-backed context.");

            // Recreate surfaces at different sizes, then return to the original size.
            foreach (var size in new[] { new SKSizeI(128, 96), new SKSizeI(192, 128), new SKSizeI(128, 96) })
                compositions += VerifyComposition(context, size);
        }

        // This must remain valid after disposing the Skia context: the host still owns the device.
        device.WaitIdle();
    }

    Console.WriteLine($"Vulkan / Skia smoke passed: {compositions} GPU compositions, premultiplied alpha, text, " +
        "surface recreation and 2 Skia sessions on one host-owned device verified.");
    Console.WriteLine("Scope: offscreen composition within one Skia context per session; external VkImage handoff and swapchain are not exercised.");
}

static int VerifyComposition(GRContext context, SKSizeI size)
{
    var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var hud = SKSurface.Create(context, false, info)
        ?? throw new NotSupportedException("Skia Vulkan HUD surface creation failed.");
    using var scene = SKSurface.Create(context, false, info)
        ?? throw new NotSupportedException("Skia Vulkan scene surface creation failed.");
    using var typeface = SKTypeface.FromFamilyName("Segoe UI")
        ?? throw new NotSupportedException("The smoke example requires the Segoe UI font.");
    using var font = new SKFont(typeface, 16);
    using var paint = new SKPaint();
    var panel = new SKColor(200, 100, 50, 128);

    hud.Canvas.Clear(SKColors.Transparent);
    paint.Color = panel;
    hud.Canvas.DrawRect(10, 10, size.Width - 20, size.Height - 20, paint);
    paint.Color = SKColors.White;
    hud.Canvas.DrawText("GPU HUD", 24, 42, SKTextAlign.Left, font, paint);
    paint.Color = SKColors.CornflowerBlue;
    hud.Canvas.DrawRect(10, size.Height - 30, 20, 20, paint);
    context.Flush();
    context.Submit(true);

    using var texture = hud.Snapshot();
    if (!texture.IsTextureBacked || texture.AlphaType != SKAlphaType.Premul)
        throw new InvalidOperationException("HUD snapshot must be a premultiplied GPU texture.");

    // Reuse the same HUD texture over two host backgrounds. No pixel transfer occurs in composition.
    var backgrounds = new[] { new SKColor(20, 40, 60), new SKColor(80, 60, 40) };
    using var scenePixels = new SKBitmap(info);
    foreach (var background in backgrounds)
    {
        scene.Canvas.Clear(background);
        scene.Canvas.DrawImage(texture, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        context.Flush();
        context.Submit(true);

        // CPU readback is used only to assert the result after GPU composition.
        ReadPixels(scene, scenePixels);
        AssertColor("transparent area preserves the scene", scenePixels.GetPixel(0, 0), background);
        AssertColor("premultiplied panel blends over the scene", scenePixels.GetPixel(20, 20), Over(panel, background), 1);
        AssertColor("opaque marker replaces the scene", scenePixels.GetPixel(20, size.Height - 20), SKColors.CornflowerBlue);
    }

    using var hudPixels = new SKBitmap(info);
    ReadPixels(hud, hudPixels);
    if (hudPixels.GetPixel(0, 0).Alpha != 0)
        throw new InvalidOperationException("HUD background is not transparent.");
    if (Math.Abs(hudPixels.GetPixel(20, 20).Alpha - panel.Alpha) > 1)
        throw new InvalidOperationException("HUD panel alpha was not preserved.");

    var hasTextPixels = false;
    for (var y = 24; y < 44 && !hasTextPixels; y++)
    for (var x = 24; x < size.Width - 10; x++)
    {
        var pixel = hudPixels.GetPixel(x, y);
        if (pixel.Red > 220 && pixel.Green > 220 && pixel.Blue > 220 && pixel.Alpha > 220)
        {
            hasTextPixels = true;
            break;
        }
    }
    if (!hasTextPixels)
        throw new InvalidOperationException("No visible text pixels were rendered in the HUD label area.");

    return backgrounds.Length;
}

static void ReadPixels(SKSurface surface, SKBitmap pixels)
{
    if (!surface.ReadPixels(pixels.Info, pixels.GetPixels(), pixels.RowBytes, 0, 0))
        throw new InvalidOperationException("GPU readback failed.");
}

static SKColor Over(SKColor foreground, SKColor background)
{
    var alpha = foreground.Alpha / 255f;
    return new SKColor(
        (byte)MathF.Round(foreground.Red * alpha + background.Red * (1 - alpha)),
        (byte)MathF.Round(foreground.Green * alpha + background.Green * (1 - alpha)),
        (byte)MathF.Round(foreground.Blue * alpha + background.Blue * (1 - alpha)));
}

static void AssertColor(string scenario, SKColor actual, SKColor expected, int tolerance = 0)
{
    if (Math.Abs(actual.Red - expected.Red) > tolerance || Math.Abs(actual.Green - expected.Green) > tolerance ||
        Math.Abs(actual.Blue - expected.Blue) > tolerance || Math.Abs(actual.Alpha - expected.Alpha) > tolerance)
        throw new InvalidOperationException($"{scenario}: expected {expected}, got {actual}.");
}
