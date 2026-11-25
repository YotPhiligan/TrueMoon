using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Windows;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Runtime;

internal static class ElementChrome
{
    internal static bool DrawBackground(Element node, IDrawingContext context, bool overBackdrop)
    {
        var backdrop = context as IBackdropDrawingContext;
        var material = node.Backdrop;
        var bounds = node.Bounds;
        var radius = node.CornerRadius;
        if (material != null)
        {
            if (backdrop != null && backdrop.TryDrawBackdrop(bounds, material, radius))
                overBackdrop = material.Tint.A < 255 && (material.BlurSigma > 0 || overBackdrop);
            else
            {
                context.Fill(bounds, material.Fallback, radius);
                overBackdrop = false;
            }
        }
        var color = node.Background;
        if (color.A == 0) return overBackdrop;
        if (material == null && node.EffectiveBackgroundGradient is { } gradient &&
            context is IWindowGradientDrawingContext window && window.TryDrawWindowGradient(bounds, gradient, radius)) return false;
        if (material == null && backdrop != null && color.A == 255 && node.Theme.ControlTintAlpha < 255 && node.HasThemedGlass)
        {
            var tint = node.GlassTint(color);
            if (overBackdrop || node.Theme.ControlBlurSigma == 0)
            {
                context.Fill(bounds, tint, radius);
                return overBackdrop;
            }
            if (backdrop.TryDrawBackdrop(bounds, node.ControlBackdrop(tint, color), radius)) return true;
        }
        context.Fill(bounds, color, radius);
        return overBackdrop && color.A < 255;
    }

    internal static void DrawOverlay(Element node, IDrawingContext context)
    {
        var badge = node.ValidationIconBounds;
        var radius = node.CornerRadius;
        var bounds = node.Bounds;
        var plate = badge;
        if (badge.Width > 0)
        {
            if (radius > 0)
            {
                var inset = Math.Min(Math.Max(node.BorderWidth + 1, radius / 2), Math.Min(badge.Width, badge.Height) / 4);
                var available = badge.Deflate(new Thickness(inset));
                var size = Math.Min(28, Math.Min(available.Width, available.Height));
                plate = new(available.X + available.Width - size, available.Y + (available.Height - size) / 2, size, size);
            }
            context.Fill(plate, node.Theme.Error, Math.Min(radius, Math.Min(plate.Width, plate.Height) / 2));
        }
        if (node.BorderWidth > 0)
        {
            var inset = node.BorderWidth / 2;
            context.Stroke(bounds.Deflate(new Thickness(inset)), node.BorderColor, node.BorderWidth,
                node.HasEditorOutline ? Math.Max(0, radius - inset) : radius);
        }
        if (node.HasError && node.HasEditorOutline)
        {
            var space = Math.Min(plate.Width, plate.Height);
            var size = Math.Min(14, radius > 0 ? space * .65f : Math.Max(0, space - 4));
            if (size > 0) UiSymbols.Draw(context, UiSymbol.Warning,
                new(plate.X + (plate.Width - size) / 2, plate.Y + (plate.Height - size) / 2, size, size), node.Theme.OnError);
        }
        else if (node.HasError)
        {
            if (!node.HasSharedValidation) context.Fill(new Rect(bounds.X, bounds.Y, Math.Min(3, bounds.Width), bounds.Height), node.Theme.Error);
        }
        else if (node.IsModified) context.Fill(new Rect(bounds.X + Math.Max(0, bounds.Width - 5), bounds.Y + 2,
            Math.Min(3, bounds.Width), Math.Min(3, bounds.Height)), node.Theme.Modified);
        if (node.IsFocused && node.IsEffectivelyEnabled)
        {
            var focus = badge.Width > 0 ? bounds with { Width = Math.Max(0, badge.X - bounds.X) } : bounds;
            if (focus.Width > 4 && focus.Height > 4)
                context.Stroke(focus.Deflate(new Thickness(2)), node.Theme.Focus, 2, node.HasEditorOutline ? Math.Max(0, radius - 2) : radius);
        }
    }
}
