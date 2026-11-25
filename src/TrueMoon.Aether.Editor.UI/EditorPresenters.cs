using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Editor.UI.Translation;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Windows;

namespace TrueMoon.Aether.Editor.UI;

/// <summary>Exact-type visual registrations for standard and embedded Editor renderers.</summary>
public static class EditorPresenters
{
    /// <summary>Adds the Editor's viewport, island, header, status and inspector visuals to one renderer.</summary>
    /// <param name="presenters">The registry being configured for this session or standalone draw.</param>
    public static void Register(UiPresenters presenters)
    {
        ArgumentNullException.ThrowIfNull(presenters);
        presenters.Register<EditorViewport>(DrawViewport);
        presenters.Register<EditorIsland>(DrawIsland);
        presenters.Register<EditorHeaderView.HeaderHintText>(static (hint, context) =>
        {
            var y = hint.Bounds.Y;
            foreach (var line in hint.Lines)
            {
                context.Text(line, hint.Bounds.X, y, hint.Foreground, hint.FontSize, hint.FontFamily, hint.FontWeight, hint.FontItalic);
                y += hint.LineHeight;
            }
        });
        presenters.Register<EditorHeaderView.BrandMark>(static (mark, context) =>
        {
            var bounds = mark.Bounds;
            var size = Math.Min(20, Math.Min(bounds.Width, bounds.Height));
            UiSymbols.Draw(context, UiSymbol.TrueMoon, new(bounds.X + (bounds.Width - size) / 2,
                bounds.Y + (bounds.Height - size) / 2, size, size), mark.Foreground);
        });
        presenters.Register<EditorHeaderView.CaptionText>(static (caption, context) =>
            context.Text(caption.DisplayText, caption.Bounds.X, caption.Bounds.Y + caption.Bounds.Height / 2 - caption.InkCenter,
                caption.Foreground, caption.FontSize, caption.FontFamily, caption.FontWeight, caption.FontItalic));
        presenters.Register<EditorStatusBarView.DetailsAction>(static (action, context) =>
            UiSymbols.Draw(context, action.Symbol, new(action.Bounds.X + (action.Bounds.Width - 14) / 2,
                action.Bounds.Y + (action.Bounds.Height - 14) / 2, 14, 14), action.Foreground));
        presenters.Register<EditorStatusBarView.SegmentButton>(static (button, context) =>
        {
            if (button.IsHovered || button.IsPressed)
            {
                var width = Math.Min(button.Bounds.Width, button.LabelWidth + 20 + button.Padding.Left + button.Padding.Right);
                context.Fill(new(button.Bounds.X, button.Bounds.Y, width, button.Bounds.Height), button.IsPressed ? button.Theme.ControlPressed : button.Theme.ControlHover);
            }
            DrawSegment(context, button, button.Symbol, button.DisplayText);
        });
        presenters.Register<EditorStatusBarView.SegmentLabel>(static (label, context) => DrawSegment(context, label, label.Symbol, label.DisplayText));
        presenters.Register<EditorInspectorView.ComponentHeader>(static (header, context) =>
        {
            ButtonPresenter.DrawLabel(header, context);
            UiSymbols.Draw(context, header.Expanded ? UiSymbol.ChevronDown : UiSymbol.ChevronRight,
                new(header.Bounds.X + 3, header.Bounds.Y + (header.Bounds.Height - 12) / 2, 12, 12), header.Foreground);
        });
    }

    private static void DrawSegment(IDrawingContext context, Element element, UiSymbol symbol, string display)
    {
        var area = element.Bounds.Deflate(element.Padding);
        var color = element.IsEffectivelyEnabled ? element.Foreground : element.Theme.Disabled;
        UiSymbols.Draw(context, symbol, new(area.X, area.Y + (area.Height - 14) / 2, 14, 14), color);
        context.Text(display, area.X + 20, area.Y + (area.Height - element.FontSize) / 2, color,
            element.FontSize, element.FontFamily, element.FontWeight, element.FontItalic);
    }

    private static void DrawIsland(EditorIsland island, IDrawingContext context)
    {
        if (island.Child is not { IsVisible: true } child) return;
        var area = child.Bounds;
        for (var spread = 6; spread >= 1; spread--)
            context.Fill(new(area.X - spread, area.Y - spread + 2, area.Width + spread * 2, area.Height + spread * 2),
                new(0, 0, 0, (byte)(island.Theme.Surface.R < 128 ? 8 : 5)), child.CornerRadius + spread);
    }

    private static void DrawViewport(EditorViewport viewport, IDrawingContext context)
    {
        var state = viewport.BeginDraw();
        var bounds = state.Geometry.Bounds;
        var theme = state.Theme;
        if (state.Frame is {} frame && state.Image is {} image)
        {
            context.Fill(bounds, new(10, 15, 26));
            if (!state.Geometry.HasVisibleImage) return;
            context.Image(image, state.Geometry.Image);
            if (state.Overlay is {} overlay)
                TranslationOverlayPainter.Draw(overlay, context, theme, state.FontFamily, state.FontWeight, state.FontItalic);
            viewport.CompleteDraw(state, frame);
            return;
        }
        if (theme.WindowGradient is not { } gradient || context is not IWindowGradientDrawingContext window ||
            !window.TryDrawWindowGradient(bounds, gradient, 0)) context.Fill(bounds, theme.Surface);
        var line = theme.Foreground with { A = 24 };
        for (var x = bounds.X; x < bounds.X + bounds.Width; x += 32) context.Fill(new(x, bounds.Y, 1, bounds.Height), line);
        for (var y = bounds.Y; y < bounds.Y + bounds.Height; y += 32) context.Fill(new(bounds.X, y, bounds.Width, 1), line);
        context.Text(state.EmptyText, bounds.X + 24, bounds.Y + Math.Max(16, bounds.Height / 2 - 12),
            theme.Foreground, state.FontSize, state.FontFamily, state.FontWeight, state.FontItalic);
    }
}
