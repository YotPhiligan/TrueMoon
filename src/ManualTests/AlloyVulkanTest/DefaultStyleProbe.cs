using System.Text.Json;
using AlloyTest;
using SkiaSharp;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

internal static class DefaultStyleProbe
{
    internal static void Run(string output)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new ArgumentException("Use a fresh gallery output directory.");
        Directory.CreateDirectory(output);
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string> { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets/Fonts/NotoSans-Regular.ttf") });
        var captures = new List<object>();
        foreach (var light in new[] { false, true })
        foreach (var scale in new[] { 1f, 1.5f, 2f })
        {
            var gallery = new DefaultStyleGallery(); var frame = WindowFrame.CreateDefault("TrueMoon — UI Gallery", gallery);
            frame.ApplyWindowState(new(true, false, true, true, true));
            using var ui = UiSession.Create(frame, new SkiaRasterRenderBackend(fonts), new RasterUiTarget(), new UiViewport((int)(980 * scale), (int)(780 * scale), scale));
            ui.SetTheme((light ? Theme.Light : Theme.Dark) with { FontFamily = "Noto Sans" }); ui.Update(); ui.Focus(gallery.NameEditor); ui.Update();
            using var snapshot = ui.SnapshotRasterImage(); using var data = snapshot.Encode(SKEncodedImageFormat.Png, 100);
            var name = $"{(light ? "light" : "dark")}-{(int)(scale * 100)}.png"; using (var stream = File.Create(Path.Combine(output, name))) data.SaveTo(stream);
            if (ui.Update()) throw new InvalidOperationException("Static gallery must return to idle.");
            captures.Add(new { File = name, Scale = scale, Width = snapshot.Width, Height = snapshot.Height });
        }
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new { Passed = true, PhysicalDpi = false, Captures = captures }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Default style gallery: six dark/light raster captures at synthetic 100/150/200%; {output}");
    }
}
