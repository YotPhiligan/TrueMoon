using TrueMoon.Argentis;
using TrueMoon.Argentis.Menus;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Windows;

namespace TrueMoon.Alloy.Runtime;

internal static partial class StockPresenters
{
    internal static void AddChrome(Dictionary<Type, ElementPresentation> entries)
    {
        entries.Add(typeof(WindowTitleBar), ElementPresentation.Create<WindowTitleBar>(DrawTitleBar));
        entries.Add(typeof(WindowResizeGrip), ElementPresentation.Create<WindowResizeGrip>(static (grip, context) =>
            UiSymbols.Draw(context, UiSymbol.ResizeGrip, grip.Bounds.Deflate(new Thickness(2)), grip.Theme.Border)));
        entries.Add(typeof(MenuBar.MoreTrigger), ElementPresentation.Create<MenuBar.MoreTrigger>(DrawMoreTrigger));
        entries.Add(typeof(MenuItem), ElementPresentation.Create<MenuItem>(DrawMenuItem));
        entries.Add(typeof(OverlayHost.ValidationHintText), ElementPresentation.Create<OverlayHost.ValidationHintText>(DrawValidationHint));
    }

    private static void DrawTitleBar(WindowTitleBar bar, IDrawingContext context)
    {
        var bounds = bar.Bounds;
        var theme = bar.Theme;
        if (bar.HeaderContent == null)
        {
            var right = bar.MinimizeButton.Bounds.X;
            var caption = new Rect(bounds.X + 12, bounds.Y, Math.Max(0, right - bounds.X - 24), bounds.Height);
            context.Save();
            try
            {
                context.Clip(caption);
                var x = caption.X;
                if (bar.Icon is { } icon)
                {
                    context.Image(icon, new(x, bounds.Y + (bounds.Height - 16) / 2, 16, 16));
                    x += 24;
                }
                context.Text(bar.Title, x, bounds.Y + (bounds.Height - bar.FontSize) / 2,
                    bar.State.Active ? bar.Foreground : theme.Disabled, bar.FontSize, bar.FontFamily, bar.FontWeight, bar.FontItalic);
            }
            finally { context.Restore(); }
        }
        var edge = bar.OverlayContent ? theme.Border with { A = (byte)MathF.Round(40 + 100 * bar.OverlayReveal) }
            : bar.State.Active ? theme.Accent : theme.Border;
        context.Fill(new(bounds.X, bounds.Y + bounds.Height - 1, bounds.Width, 1), edge);
    }

    private static void DrawMoreTrigger(MenuBar.MoreTrigger trigger, IDrawingContext context)
    {
        var bounds = trigger.Bounds;
        var angle = (1 - trigger.RevealProgress) * MathF.PI / 2;
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        var unit = Math.Min(12, Math.Min(bounds.Width, bounds.Height)) / 12;
        var x = bounds.X + bounds.Width / 2;
        var y = bounds.Y + bounds.Height / 2;
        var color = trigger.IsEffectivelyEnabled ? trigger.Foreground : trigger.Theme.Disabled;
        for (var i = -1; i <= 1; i++)
            context.Fill(new(x + i * 4 * unit * cos - unit, y + i * 4 * unit * sin - unit, 2 * unit, 2 * unit), color, unit);
    }

    private static void DrawMenuItem(MenuItem item, IDrawingContext context)
    {
        var bounds = item.Bounds;
        var color = item.IsEffectivelyEnabled ? item.Foreground : item.Theme.Disabled;
        if (item.ActionIcon is { } icon)
            UiSymbols.Draw(context, icon, new(bounds.X + 8, bounds.Y + (bounds.Height - 12) / 2, 12, 12), color);
        var area = bounds.Deflate(item.Padding);
        context.Text(item.Value, area.X, area.Y, color, item.FontSize, item.FontFamily, item.FontWeight, item.FontItalic);
        if (item.Shortcut.Length != 0)
            context.Text(item.Shortcut, bounds.X + bounds.Width - item.Padding.Right - item.ShortcutWidth, area.Y,
                item.Theme.Disabled, item.FontSize, item.FontFamily, item.FontWeight, item.FontItalic);
    }

    private static void DrawValidationHint(OverlayHost.ValidationHintText hint, IDrawingContext context)
    {
        var y = hint.Bounds.Y;
        var lines = hint.Lines;
        for (var i = 0; i < lines.Count; i++)
        {
            context.Text(lines[i], hint.Bounds.X, y, hint.Foreground, hint.FontSize, hint.FontFamily, hint.FontWeight, hint.FontItalic);
            y += hint.LineHeight;
        }
    }
}
