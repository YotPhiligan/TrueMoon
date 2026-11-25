using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Windows;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

internal static partial class FontTypographyProbe
{
    internal static bool UseWindowUnderlayScene { get; set; }

    private static Capture ExerciseWindowUnderlay(IRenderBackend backend, UiRenderTarget target, UiViewport viewport,
        bool light, Func<UiSession, byte[]> read)
    {
        var theme = light ? Theme.Light : Theme.Dark;
        var gradient = theme.WindowGradient!;
        var material = theme.PanelBackdrop!;
        var canvas = new EditorViewport { EmptyText = "Откройте и запустите сцену", IsVisible = false };
        var field = new TextBox
        {
            Value = "Test Scene", Width = 350, Height = 32, HorizontalAlignment = LayoutAlignment.Start,
            HasError = true, ErrorMessage = "Имя уже используется"
        };
        var card = new Border
        {
            Width = 652, Height = 182, Margin = new(28, 100, 0, 0), Padding = new(18), CornerRadius = 10,
            Backdrop = material, Shadow = theme.PanelShadow, IsVisible = false,
            HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start
        }.Content(new VStack { Spacing = 10 }.WithChildren(
            new Text("Инспектор сцены") { FontSize = 18, FontWeight = 600 },
            new Text("Название"), field, new Button("Применить") { HorizontalAlignment = LayoutAlignment.Start }));
        var content = new Panel(); content.Add(canvas); content.Add(card);
        var frame = WindowFrame.CreateDefault("Alloy Editor", content);
        frame.TitleBarOverContent = true;
        using var ui = UiSession.Create(frame, backend, target, viewport);
        ui.SetTheme(theme);
        Require(ui.Update(), "The default window did not paint.");
        var windowPixels = read(ui);
        var hues = new[] { Sample(windowPixels, 45, 62), Sample(windowPixels, 350, 320), Sample(windowPixels, 688, 580) };
        Require(hues.All(color => color.A == 255) && hues[0].B > hues[0].R &&
            hues[1].R > hues[1].G && hues[1].B > hues[1].G && hues[2].G > hues[2].R &&
            Near(hues[1], gradient.Middle, 4), $"The default frame lost its three-color gradient: {string.Join(", ", hues.AsEnumerable())}.");

        var explicitFill = new Color(91, 81, 71);
        frame.Background = explicitFill;
        Require(ui.Update() && Sample(read(ui), 350, 320) == explicitFill, "An explicit frame background lost precedence.");
        frame.Clear(UiProperties.Background);
        ui.SetTheme(theme with { WindowGradient = null });
        Require(ui.Update() && Sample(read(ui), 350, 320) == theme.Surface, "Disabling the gradient did not restore the solid fill.");

        ui.SetTheme(theme);
        canvas.IsVisible = true;
        Require(ui.Update(), "The editor viewport did not paint.");
        var bare = read(ui);
        card.IsVisible = true;
        Require(ui.Update() && !ui.Update() && !ui.NeedsRender, "The window underlay did not settle at idle.");
        var pixels = read(ui);
        var underlay = new[] { Sample(bare, 72, 266), Sample(bare, 585, 266) };
        var throughGlass = new[] { Sample(pixels, 72, 266), Sample(pixels, 585, 266) };
        Require(underlay[0].G != underlay[1].G && throughGlass[0] != throughGlass[1], "The panel flattened the underlay colors.");
        for (var i = 0; i < underlay.Length; i++)
        {
            var color = underlay[i]; var tint = material.Tint;
            byte Blend(byte source, byte overlay) => (byte)((source * (255 - tint.A) + overlay * tint.A + 127) / 255);
            var expected = new Color(Blend(color.R, tint.R), Blend(color.G, tint.G), Blend(color.B, tint.B));
            Require(Near(throughGlass[i], expected, 3), $"Glass did not sample the gradient in the editor: {throughGlass[i]} vs {expected}.");
        }
        Require(Sample(pixels, 715, 250) == Sample(bare, 715, 250), "Glass paint escaped the panel and shadow.");
        var badge = field.ValidationIconBounds;
        Require(Near(Sample(pixels, badge.X + badge.Width / 2, badge.Y + 5), theme.Error, 2),
            "The validation badge lost its solid color.");
        var bounds = new[] { frame.Bounds, canvas.Bounds, card.Bounds, field.Bounds, badge };
        ui.Dispose();
        Require(frame.IsDisposed && canvas.IsDisposed && field.IsDisposed, "The window did not dispose its content.");
        return new(bounds, pixels, new { Window = hues, Underlay = underlay, ThroughGlass = throughGlass });

        Color Sample(byte[] image, float x, float y)
        {
            var index = ((int)(y * viewport.Scale) * viewport.Width + (int)(x * viewport.Scale)) * 4;
            return new(image[index], image[index + 1], image[index + 2], image[index + 3]);
        }

        static bool Near(Color actual, Color expected, int tolerance) =>
            actual.A == expected.A && Math.Abs(actual.R - expected.R) <= tolerance &&
            Math.Abs(actual.G - expected.G) <= tolerance && Math.Abs(actual.B - expected.B) <= tolerance;
    }
}
