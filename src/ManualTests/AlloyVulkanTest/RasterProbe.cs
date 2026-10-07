using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon.Extensions.DependencyInjection;

namespace AlloyVulkanTest;

internal static class RasterProbe
{
    internal static void Run(string? outputPath)
    {
        var builder = App.Builder(b => b.UseDI());
        builder.Setup(app => app.UseAlloy(options => options.UseSkiaRaster().UseExternalHost()));
        using var app = builder.Build(); app.StartAsync().GetAwaiter().GetResult();
        var factory = (HostedUiSessionFactory)app.Services.GetService(typeof(IUiSessionFactory))!;
        Require(app.Services.GetService(typeof(HostedUiWindow)) == null, "Raster external hosting must not create a window.");
        var hud = new BackendHudView();
        var ui = factory.Create(hud, new RasterUiTarget(), new UiViewport(320, 180));
        Require(ui.Rendering is SkiaRasterSurface, "UseSkiaRaster must register the CPU renderer.");
        Require(ui.Update() && !ui.Update(), "Static raster UI must not redraw.");
        var clicks = 0; hud.Action.Click += () => { clicks++; hud.Metrics.Value = $"Actions: {clicks}"; };
        var x = hud.Action.Bounds.X + 5; var y = hud.Action.Bounds.Y + 5;
        Require(ui.HandleInput(new UiInput(InputKind.PointerDown, x, y)).PointerCaptured, "Raster HUD must capture pointer input.");
        Require(ui.HandleInput(new UiInput(InputKind.PointerUp, x, y)).Handled && clicks == 1, "Raster HUD must activate its button.");
        Require(ui.Update(), "Changing the metrics must invalidate the UI.");
        using var snapshot = ui.SnapshotRasterImage();
        Require(!snapshot.IsTextureBacked && snapshot.Width == 320 && snapshot.Height == 180, "Expected a CPU raster image.");
        if (outputPath != null)
        {
            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var encoded = snapshot.Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new InvalidOperationException("Could not encode raster snapshot.");
            using var stream = File.Create(outputPath); encoded.SaveTo(stream);
            Console.WriteLine($"Raster PNG: {outputPath}");
        }
        app.StopAsync().GetAwaiter().GetResult();
        Require(ui.IsDisposed && hud.IsDisposed && factory.ActiveSessionCount == 0, "App stop must release raster registry/tree.");
        Require(ReadSnapshot(snapshot)[^1] == 0, "Snapshot must remain readable after App/session disposal.");
        Console.WriteLine("Raster hosting passed: CPU RGBA snapshot, retained input/invalidation, App/DI lifecycle; no window or Vulkan device created.");
    }

    internal static void Compare(bool validation)
    {
        var raster = RenderFrames(new SkiaRasterRenderBackend(), new RasterUiTarget(), session =>
        { using var image = session.SnapshotRasterImage(); return ReadSnapshot(image); });
        CompareFrames(raster, OpenGLProbe.RenderFrames());
        var device = new VulkanDevice(validationMessage: validation ? Console.Error.WriteLine : null);
        using (device)
        {
            var vulkan = RenderFrames(new SkiaVulkanRenderBackend(), SilkVulkanHost.CreateTarget(device), session =>
            {
                var lease = session.AcquireVulkanTexture(); var layout = (ImageLayout)lease.Info.Layout;
                try { return VulkanHostReadback.Read(device, lease.Info, state => layout = state); }
                finally { lease.Return(layout); }
            });
            CompareFrames(raster, vulkan);
        }
        Require(device.ValidationErrorCount == 0 && device.ValidationWarningCount == 0, "Backend comparison validation failed, including teardown.");
        Console.WriteLine("Backend comparison passed: one unchanged Argentis view on raster/OpenGL/Vulkan, 3 viewports (scales 1/1.5/2), matching layout/input/selected RGBA pixels and retained redraw.");
        if (validation) Console.WriteLine("Vulkan core/synchronization validation: 0 errors, 0 warnings, including backend comparison teardown.");
    }

    private static void CompareFrames(List<Frame> expected, List<Frame> actual)
    {
        Require(expected.Count == 3 && actual.Count == 3, "Expected three viewports per backend.");
        for (var i = 0; i < expected.Count; i++)
        {
            Require(expected[i].Viewport == actual[i].Viewport && expected[i].Panel == actual[i].Panel && expected[i].Button == actual[i].Button,
                "The same view must have matching logical layout across backends.");
            Require(expected[i].Input == actual[i].Input, "Input/capture must not depend on the backend.");
            Require(expected[i].Pixels.Length == actual[i].Pixels.Length && expected[i].Pixels.Zip(actual[i].Pixels).All(pair => Math.Abs(pair.First - pair.Second) <= 1),
                "Selected transparency/premultiplied panel/button pixels must agree across backends.");
        }
    }
    internal sealed record Frame(UiViewport Viewport, Rect Panel, Rect Button, UiInputResult Input, byte[] Pixels);
    internal static List<Frame> RenderFrames(IRenderBackend backend, UiRenderTarget target, Func<UiSession, byte[]> read)
    {
        var hud = new BackendHudView(); var clicks = 0;
        hud.Action.Click += () => { clicks++; hud.Metrics.Value = $"Actions: {clicks}"; };
        using var ui = UiSession.Create(hud, backend, target, new UiViewport(320, 180));
        var frames = new List<Frame>();
        foreach (var viewport in new[] { new UiViewport(320, 180), new UiViewport(480, 270, 1.5f), new UiViewport(320, 180, 2) })
        {
            ui.Resize(viewport); hud.Title.Value = $"HUD {frames.Count}";
            Require(ui.Update() && !ui.Update(), "Both backends must respect retained invalidation.");
            var x = hud.Action.Bounds.X + 5; var y = hud.Action.Bounds.Y + 5;
            var input = ui.HandleInput(new UiInput(InputKind.PointerDown, x, y));
            Require(input.Handled && input.PointerCaptured && input.KeyboardFocused, "Expected shared focus/capture behavior.");
            Require(ui.HandleInput(new UiInput(InputKind.PointerUp, x, y)).Handled, "Expected shared click behavior.");
            Require(ui.Update() && clicks == frames.Count + 1, "Click must update retained metrics once.");
            var pixels = read(ui);
            var selected = new List<byte>();
            void Select(int px, int py) { var offset = (py * viewport.Width + px) * 4; selected.AddRange(pixels[offset..(offset + 4)]); }
            Select(viewport.Width - 1, viewport.Height - 1);
            Select((int)((hud.Overlay.Bounds.X + 4) * viewport.Scale), (int)((hud.Overlay.Bounds.Y + 4) * viewport.Scale));
            Select((int)((hud.Action.Bounds.X + hud.Action.Bounds.Width - 8) * viewport.Scale),
                (int)((hud.Action.Bounds.Y + hud.Action.Bounds.Height / 2) * viewport.Scale));
            Require(selected[3] == 0 && selected[7] == 128 && selected[11] == 255, "Expected transparent background, alpha panel and opaque button.");
            frames.Add(new Frame(viewport, hud.Overlay.Bounds, hud.Action.Bounds, input, selected.ToArray()));
        }
        return frames;
    }

    internal static byte[] ReadSnapshot(SKImage image)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        Require(image.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0), "Could not read CPU snapshot.");
        var pixels = new byte[bitmap.RowBytes * bitmap.Height]; Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length); return pixels;
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    // The view has no renderer-specific code; both paths instantiate exactly this class.
    private sealed class BackendHudView : Panel
    {
        internal Border Overlay { get; } = new()
        { Width = 140, Height = 100, Margin = new Thickness(8), Padding = new Thickness(8), Background = new Color(40, 120, 200, 128),
            HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start };
        internal Text Title { get; } = new("Raster HUD") { FontSize = 12 };
        internal Text Metrics { get; } = new("Actions: 0") { FontSize = 12 };
        internal Button Action { get; } = new("Change scene") { Height = 28, FontSize = 12 };
        internal BackendHudView()
        {
            var stack = new VStack { Spacing = 5 }; stack.Add(Title).Add(Metrics).Add(Action);
            Overlay.SetContent(stack); Add(Overlay);
        }
    }
}
