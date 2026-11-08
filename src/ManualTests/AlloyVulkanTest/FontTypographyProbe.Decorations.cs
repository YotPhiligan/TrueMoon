using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

internal static partial class FontTypographyProbe
{
    internal static bool OmitDecorationShadows { get; set; }
    internal static bool UseDecorationBackdrop { get; set; }
    internal static bool OmitDecorationBlur { get; set; }
    private static Capture ExerciseDecorations(IRenderBackend backend, UiRenderTarget target, UiViewport viewport, bool light, Func<UiSession, byte[]> read)
    {
        var theme = light ? Theme.Light : Theme.Dark;
        if (OmitDecorationShadows) theme = theme with { PanelShadow = null, MenuShadow = null };
        var content = new Panel { Background = theme.Control };
        var stack = new VStack { Spacing = 12 };
        var card = new Border { Width = 500, Height = 320, Margin = new(28, 28, 0, 0), Padding = new(16), CornerRadius = 8,
            Background = theme.Surface, Shadow = theme.PanelShadow, HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start }.Content(stack);
        stack.Add(new Text("Validation · поля и тени") { FontSize = 18, FontWeight = 600 });
        var box = new TextBox { Value = "new value rejected", Height = 32, HasError = true, ErrorMessage = "Значение отклонено: требуется конечное число > 0.\nЧерновик сохранён; исправьте его перед применением." };
        var number = new NumberBox("X") { Height = 32, HasError = true, ErrorMessage = "Введите конечное число с точкой" }; number.Editor.Value = "-";
        var rich = new RichTextEditor { Text = "Отклонённый текст\nВведённые данные и выделение сохранены", Height = 70, MinHeight = 0,
            HasError = true, ErrorMessage = "Текст не соответствует правилам валидатора" };
        stack.Add(box); stack.Add(number); stack.Add(rich); content.Add(card);
        if (UseDecorationBackdrop) content.Add(new BackdropSplit
        {
            Width = 280, Height = 200, Margin = new(350, 360, 0, 0),
            HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start
        });
        var menuBackdrop = UseDecorationBackdrop ? theme.MenuBackdrop : null;
        if (OmitDecorationBlur && menuBackdrop != null) menuBackdrop = new(0, menuBackdrop.Tint, menuBackdrop.Fallback);
        var menu = new MenuPopup([new("apply", "Применить", new DelegateUiCommand(() => { })), new("cancel", "Отменить", new DelegateUiCommand(() => { }))], () => { })
        { Width = 200, Backdrop = menuBackdrop, Margin = new(390, 400, 0, 0), HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start };
        content.Add(menu);
        var host = new OverlayHost(content); using var ui = UiSession.Create(host, backend, target, viewport); ui.SetTheme(theme); ui.Update();
        ui.Focus(box); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true)); ui.Update();
        // Sample before the first field's hint covers lower fields.
        var validationPixels = read(ui);
        foreach (var field in new Element[] { box, number.Editor, rich })
        {
            var badge = field.ValidationIconBounds;
            Require(badge == new Rect(field.Bounds.X + field.Bounds.Width - field.Bounds.Height, field.Bounds.Y, field.Bounds.Height, field.Bounds.Height), "Validation badge is not a full-height square.");
            var offset = ((int)((badge.Y + 3) * viewport.Scale) * viewport.Width + (int)((badge.X + 2) * viewport.Scale)) * 4;
            Require(Math.Abs(validationPixels[offset] - theme.Error.R) <= 2 && Math.Abs(validationPixels[offset + 1] - theme.Error.G) <= 2 && Math.Abs(validationPixels[offset + 2] - theme.Error.B) <= 2,
                $"Validation badge lost its solid error fill at {badge}: actual {validationPixels[offset]}/{validationPixels[offset + 1]}/{validationPixels[offset + 2]}, expected {theme.Error}.");
        }
        var b = box.ValidationIconBounds; ui.HandleInput(new(InputKind.PointerMove, b.X + b.Width / 2, b.Y + b.Height / 2));
        host.AdvanceFrame(TimeSpan.FromMilliseconds(500), ui); ui.Update();
        Require(host.Popup is Border { Child: ScrollViewer { Child: Text hint } } && hint.Value == box.ErrorMessage,
            "Validation hint did not display the exact validator error.");
        Require(box.IsFocused && box.SelectionLength == box.Value.Length && number.Editor.Value == "-", "Validation changed input/selection.");
        Require(menu.Shadow == theme.MenuShadow && card.Shadow == theme.PanelShadow, "Semantic surfaces lost theme shadows.");
        Require(!ui.Update(), "Decoration fixture did not become idle.");
        var pixels = read(ui);
        var bounds = new[] { card.Bounds, box.Bounds, box.ValidationIconBounds, number.Editor.Bounds, number.Editor.ValidationIconBounds,
            rich.Bounds, rich.ValidationIconBounds, menu.Bounds, host.Popup!.Bounds };
        object? witness = null;
        if (UseDecorationBackdrop && theme.MenuBackdrop is { BlurSigma: > 0 } backdrop)
        {
            // Bottom padding avoids the menu rows/text; the black/white split extends
            // beyond the whole kernel halo so only horizontal Gaussian mixing is sampled.
            var mid = menu.Bounds.X + menu.Bounds.Width / 2; var y = menu.Bounds.Y + menu.Bounds.Height - 2;
            byte Red(float x) => pixels[((int)(y * viewport.Scale) * viewport.Width + (int)(x * viewport.Scale)) * 4];
            var left = Red(mid - 3); var right = Red(mid + 3);
            var flatLeft = backdrop.Tint.R * backdrop.Tint.A / 255f;
            var flatRight = flatLeft + 255 - backdrop.Tint.A;
            Require(left > flatLeft + 8 && right < flatRight - 8 && left < right,
                $"Backdrop blur missing: actual {left}/{right}, unblurred {flatLeft}/{flatRight}.");
            witness = new { LeftRed = left, RightRed = right, UnblurredLeftRed = flatLeft, UnblurredRightRed = flatRight, BlurSigma = backdrop.BlurSigma };
        }
        ui.Dispose(); Require(host.IsDisposed && box.IsDisposed && menu.IsDisposed && rich.IsDisposed, "Decoration tree/popup cleanup failed.");
        return new(bounds, pixels, witness);
    }
    private sealed class BackdropSplit : Element
    {
        protected override void DrawCore(IDrawingContext context)
        {
            context.Fill(new(Bounds.X, Bounds.Y, Bounds.Width / 2, Bounds.Height), new(0, 0, 0));
            context.Fill(new(Bounds.X + Bounds.Width / 2, Bounds.Y, Bounds.Width / 2, Bounds.Height), new(255, 255, 255));
        }
    }
}
