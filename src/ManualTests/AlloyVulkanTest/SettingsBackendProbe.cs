using AlloyTest;
using Silk.NET.OpenGL;
using Silk.NET.Vulkan;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

internal static class SettingsBackendProbe
{
    private sealed record Result(Rect[] Bounds, string[] States, byte[] Pixels, int Draws);

    internal static void Run(bool validation)
    {
        foreach (var scale in new[] { 1f, 1.5f, 2f })
        {
            var viewport = new UiViewport((int)(900 * scale), (int)(1100 * scale), scale);
            var raster = Exercise(new SkiaRasterRenderBackend(), new RasterUiTarget(), viewport, ui =>
            { using var image = ui.SnapshotRasterImage(); return RasterProbe.ReadSnapshot(image); });
            using (var window = new SilkWindowHost(320, 240, "Shared settings OpenGL", true))
            using (var gl = GL.GetApi(window.NativeWindow))
            {
                Match(raster, Exercise(new SkiaOpenGLRenderBackend(), OpenGLProbe.Target(window), viewport, ui =>
                { using var image = ui.ReadbackOpenGLImage(); return RasterProbe.ReadSnapshot(image); }));
                Require(gl.GetError() == GLEnum.NoError, "Settings OpenGL/teardown generated an error.");
            }
            var device = new VulkanDevice(validationMessage: validation ? Console.Error.WriteLine : null);
            using (device)
            {
                Match(raster, Exercise(new SkiaVulkanRenderBackend(), SilkVulkanHost.CreateTarget(device), viewport, ui =>
                {
                    var lease = ui.AcquireVulkanTexture(); var layout = (ImageLayout)lease.Info.Layout;
                    try { return VulkanHostReadback.Read(device, lease.Info, state => layout = state); }
                    finally { lease.Return(layout); }
                }));
                device.WaitIdle();
            }
            Require(device.ValidationErrorCount == 0 && device.ValidationWarningCount == 0, "Settings Vulkan validation failed, including teardown.");
            Console.WriteLine($"Shared settings scale {scale}: raster/OpenGL/Vulkan layout, 40 interaction steps, selected RGBA pixels agree; {raster.Draws} draws per backend; static/suspend/restore/disposal passed.");
        }
        if (validation) Console.WriteLine("Settings comparison Vulkan validation: 0 errors, 0 warnings, including teardown.");
    }

    private static Result Exercise(IRenderBackend backend, UiRenderTarget target, UiViewport viewport, Func<UiSession, byte[]> read)
    {
        var model = new SettingsModel(); var view = new View1(model); var script = new SettingsFormSmoke();
        var states = new List<string>(); var bounds = new List<Rect>(); var draws = 0;
        using var ui = UiSession.Create(view, backend, target, viewport);
        for (var frame = 0; frame < 40; frame++)
        {
            if (ui.Update()) draws++;
            bounds.AddRange(Descendants(view).Select(element => element.Bounds));
            script.Frame(ui);
            states.Add($"{model.Name}|{model.Enabled}|{model.Volume:R}|{model.IsLight}|{model.IsCompact}|{model.ControlsEnabled}|{model.LoadStatus}|{view.NameEditor.SelectionStart}/{view.NameEditor.SelectionLength}/{view.NameEditor.IsFocused}|{view.RowScroll.Offset:R}|{string.Join(';', model.Rows.Select(row => row.Name))}");
        }
        for (var frame = 0; frame < 5; frame++) Require(!ui.Update(), "Static settings kept drawing.");
        ui.Resize(new UiViewport(0, 0, viewport.Scale)); Require(!ui.Update(), "Zero-sized settings drew.");
        ui.Resize(viewport); Require(ui.Update() && !ui.Update(), "Settings restore did not draw exactly once.");
        var pixels = read(ui); var selected = new List<byte>();
        void Select(float x, float y)
        {
            var offset = ((int)(y * viewport.Scale) * viewport.Width + (int)(x * viewport.Scale)) * 4;
            selected.AddRange(pixels[offset..(offset + 4)]);
        }
        Select(2, 2); // Theme background, away from glyphs/antialiased edges.
        var button = view.DarkButton.Bounds;
        Select(button.X + button.Width - 6, button.Y + button.Height / 2);
        Select(view.Logo.Bounds.X + 3, view.Logo.Bounds.Y + 3);
        Select(view.Meter.Bounds.X + 2, view.Meter.Bounds.Y + 2);
        Select(view.RowScroll.Bounds.X + 2, view.RowScroll.Bounds.Y + 2);
        Require(selected[3] == 255 && selected[7] == 255 && selected[11] == 255, "Settings background/button/image became transparent.");
        ui.Dispose(); script.VerifyDisposed();
        var preserved = model.Name; model.Name = "After disposal"; model.AddRow();
        Require(view.NameEditor.Value == preserved && view.RowList.IsDisposed, "Disposed settings still observed the model.");
        return new Result(bounds.ToArray(), states.ToArray(), selected.ToArray(), draws);
    }

    private static void Match(Result expected, Result actual)
    {
        Require(expected.Bounds.SequenceEqual(actual.Bounds), "Settings logical layout differs across backends.");
        Require(expected.States.SequenceEqual(actual.States) && expected.Draws == actual.Draws, "Settings input/model/selection/scroll/redraw differs across backends.");
        Require(expected.Pixels.Length == actual.Pixels.Length && expected.Pixels.Zip(actual.Pixels).All(pair => Math.Abs(pair.First - pair.Second) <= 1), "Settings selected RGBA pixels differ across backends.");
    }
    private static IEnumerable<Element> Descendants(Element root)
    { yield return root; foreach (var child in root.Children) foreach (var element in Descendants(child)) yield return element; }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
