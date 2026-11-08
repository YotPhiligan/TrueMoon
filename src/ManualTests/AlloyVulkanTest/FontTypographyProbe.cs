using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Silk.NET.OpenGL;
using Silk.NET.Vulkan;
using SkiaSharp;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

// Direct, low-level renderer fixture; no application services or native resources belong to the controls.
internal static partial class FontTypographyProbe
{
    private sealed record Capture(Rect[] Bounds, byte[] Pixels, object? BlurWitness = null);

    internal static void Run(string output, bool validation, bool useDefaults = false, bool decorations = false)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new ArgumentException("Use a fresh font output directory.");
        Directory.CreateDirectory(output);
        var file = Path.Combine(AppContext.BaseDirectory, "Assets/Fonts/NotoSans-Regular.ttf");
        var fonts = useDefaults ? null : SkiaFontCatalog.FromSystemFonts(["Segoe UI Variable Text", "Segoe UI"],
            SkiaFontCatalog.Load("Fixed", new Dictionary<string, string> { ["Fixed"] = file }));
        var captures = new List<object>(); var comparisons = new List<object>(); var hardware = new List<object>();
        var validationReports = new List<object>();
        foreach (var light in new[] { false, true })
        foreach (var scale in new[] { 1f, 1.5f, 2f })
        {
            var viewport = new UiViewport((int)(740 * scale), (int)(650 * scale), scale);
            var suffix = $"{(light ? "light" : "dark")}-{(int)(scale * 100)}";
            var raster = Exercise(fonts == null ? new SkiaRasterRenderBackend() : new SkiaRasterRenderBackend(fonts), new RasterUiTarget(), viewport, light,
                ui => { using var image = ui.SnapshotRasterImage(); return RasterProbe.ReadSnapshot(image); }, decorations);
            Save("raster", raster);
            using (var window = new SilkWindowHost(320, 240, "Font typography OpenGL", true))
            using (var gl = GL.GetApi(window.NativeWindow))
            {
                hardware.Add(new { Backend = "opengl", Renderer = gl.GetStringS(StringName.Renderer), Version = gl.GetStringS(StringName.Version) });
                var result = Exercise(fonts == null ? new SkiaOpenGLRenderBackend() : new SkiaOpenGLRenderBackend(fonts), OpenGLProbe.Target(window), viewport, light,
                    ui => { using var image = ui.ReadbackOpenGLImage(); return RasterProbe.ReadSnapshot(image); }, decorations);
                Save("opengl", result); Compare("opengl", result);
                Require(gl.GetError() == GLEnum.NoError, "Font OpenGL probe generated an error.");
            }
            var device = new VulkanDevice(validationMessage: validation ? Console.Error.WriteLine : null);
            using (device)
            {
                hardware.Add(VulkanHardware(device));
                var result = Exercise(fonts == null ? new SkiaVulkanRenderBackend() : new SkiaVulkanRenderBackend(fonts), SilkVulkanHost.CreateTarget(device), viewport, light, ui =>
                {
                    var lease = ui.AcquireVulkanTexture(); var layout = (ImageLayout)lease.Info.Layout;
                    try { return VulkanHostReadback.Read(device, lease.Info, state => layout = state); }
                    finally { lease.Return(layout); }
                }, decorations);
                Save("vulkan", result); Compare("vulkan", result); device.WaitIdle();
            }
            validationReports.Add(new { Scene = suffix, Errors = device.ValidationErrorCount, Warnings = device.ValidationWarningCount });
            Require(device.ValidationErrorCount == 0 && device.ValidationWarningCount == 0, "Font Vulkan validation failed, including teardown.");
            Console.WriteLine($"Typography {suffix}: 3 backends, identical logical layout, font-weight update, selection/input/idle/cleanup passed.");

            void Save(string backend, Capture capture)
            {
                var name = $"{backend}-{suffix}.png";
                using var bitmap = new SKBitmap(new SKImageInfo(viewport.Width, viewport.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
                Marshal.Copy(capture.Pixels, 0, bitmap.GetPixels(), capture.Pixels.Length);
                using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(Path.Combine(output, name)); data.SaveTo(stream);
                captures.Add(new { File = name, viewport.Width, viewport.Height, viewport.Scale, capture.Bounds, capture.BlurWitness });
            }
            void Compare(string backend, Capture actual)
            {
                Require(raster.Bounds.SequenceEqual(actual.Bounds), $"Font logical layout differs on {backend}.");
                // Rounded decoration corners may have different CPU/GPU coverage.
                // Flat interiors remain strict (2/255), with the same symmetric1px edge envelope.
                var result = PixelDifference.Compare(raster.Pixels, actual.Pixels, viewport.Width, viewport.Height, 2, 1, decorations ? 32 : 24);
                comparisons.Add(new { Backend = backend, Scene = suffix, Result = result });
                Require(result.Passed, $"Font pixels outside established tolerance on {backend}: {result}.");
            }
        }
        var fontDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var hashes = new[] { file, SkiaNativeLibrary.LoadedPath!, Path.Combine(fontDirectory, "segoeui.ttf"),
            Path.Combine(fontDirectory, "seguisb.ttf"), Path.Combine(fontDirectory, "SegUIVar.ttf") }
            .Where(File.Exists).Select(path => new { File = path, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) }).ToArray();
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Profile = decorations ? "validation-shadows" : "typography", DecorationShadows = decorations && !OmitDecorationShadows,
            OpaqueDecorationMenu = decorations && !UseDecorationBackdrop,
            DecorationBlurEnabled = decorations && UseDecorationBackdrop && !OmitDecorationBlur,
            PhysicalDpi = false, AutomaticOpticalSizing = false, ValidationEnabled = validation, DefaultPolicy = useDefaults,
            OS = Environment.OSVersion.VersionString, Runtime = Environment.Version.ToString(), SkiaSharp = typeof(SKFont).Assembly.FullName,
            SystemFamilies = SKFontManager.Default.FontFamilies.Where(family => family.StartsWith("Segoe UI", StringComparison.Ordinal)),
            Hashes = hashes, Hardware = hardware, Captures = captures, Comparisons = comparisons, ValidationReports = validationReports,
            Scope = decorations ? "Source/published renderer fixture: validation icons/live tooltip, panel/menu shadows and actual frosted backdrop (opaque control optional). No physical input/DPI, VRAM or full release acceptance." :
                "Source/published renderer fixture; system family fallback, regular400/semibold600/italic, plain/rich/editable text; no per-glyph fallback or fresh-package/physical acceptance."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Typography: {captures.Count} captures, {comparisons.Count} full-frame comparisons; " +
            (validation ? "Vulkan validation 0 errors/0 warnings including teardown. " : "Vulkan validation disabled. ") + output);
    }

    private static unsafe object VulkanHardware(VulkanDevice device)
    {
        device.Api.GetPhysicalDeviceProperties(device.PhysicalDevice, out var properties);
        return new { Backend = "vulkan", Name = Marshal.PtrToStringUTF8((nint)properties.DeviceName),
            properties.DriverVersion, properties.ApiVersion, properties.VendorID, properties.DeviceID };
    }

    private static Capture Exercise(IRenderBackend backend, UiRenderTarget target, UiViewport viewport, bool light, Func<UiSession, byte[]> read, bool decorations)
    {
        if (decorations) return ExerciseDecorations(backend, target, viewport, light, read);
        var theme = (light ? Theme.Light : Theme.Dark) with { FontFamily = "Segoe UI Variable Text", FontSize = 14 };
        var root = new VStack { Spacing = 4, Padding = new(8), Background = theme.Surface };
        root.Add(new Text("System fonts: Regular 400 / Semibold 600") { FontWeight = 600, FontSize = 18 });
        foreach (var family in new[] { "Segoe UI", "Segoe UI Variable Text", "Segoe UI Variable Small" })
        foreach (var size in new[] { 11, 12, 14 })
        foreach (var weight in new[] { 400, 600 })
            root.Add(new Text($"{family} {size}/{weight}: Aa Mg Привет Ёй 0123456789") { FontFamily = family, FontSize = size, FontWeight = weight });
        root.Add(new Text("Italic 600: Aa Привет") { FontFamily = "Segoe UI", FontWeight = 600, FontItalic = true });
        root.Add(new Text("Missing family → configured system fallback") { FontFamily = "TrueMoon-missing-font", FontWeight = 600 });
        var editor = new TextBox { Value = "Preserved draft: Привет WWW iii", FontWeight = 400 };
        root.Add(editor);
        var rich = new RichTextEditor { Document = new([new("Rich Regular "), new("Semibold Привет", new() { FontWeight = 600 }),
            new(" Italic", new() { Italic = true })]), Height = 44 };
        root.Add(rich);
        using var ui = UiSession.Create(root, backend, target, viewport);
        ui.SetTheme(theme); Require(ui.Update() && !ui.Update(), "Font fixture did not become idle.");
        ui.Focus(editor); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        editor.FontWeight = 600; ui.Update();
        Require(editor.IsFocused && editor.SelectionLength == editor.Value.Length, "Weight update lost editor focus/selection.");
        ui.Focus(rich); rich.Select(5, 7); rich.FontWeight = 600; ui.Update();
        Require(rich.IsFocused && rich.SelectionStart == 5 && rich.SelectionLength == 7, "Rich font update lost selection.");
        Require(!ui.Update(), "Font fixture kept redrawing while idle.");
        var pixels = read(ui); var bounds = root.Children.Select(child => child.Bounds).ToArray();
        ui.Dispose(); Require(root.IsDisposed && editor.IsDisposed && rich.IsDisposed, "Font fixture did not dispose its tree.");
        return new(bounds, pixels);
    }

    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
