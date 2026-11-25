using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Runtime;

/// <summary>Paints a laid-out element tree in logical coordinates on the UI owner thread.</summary>
public static class UiTreeRenderer
{
    /// <summary>Paints the root and its visible descendants into the supplied context.</summary>
    /// <param name="root">Root of the subtree; borrowed for this call.</param>
    /// <param name="context">Drawing context with the viewport scale already applied.</param>
    /// <param name="presenters">Borrowed presenter registry; when omitted, this draw owns a temporary registry.</param>
    public static void Draw(Element root, IDrawingContext context, UiPresenters? presenters = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(context);
        using var temporary = presenters is null ? UiPresenters.Create() : null;
        DrawNode(root, context, false, presenters ?? temporary!);
    }

    private static void DrawNode(Element node, IDrawingContext context, bool overBackdrop, UiPresenters presenters)
    {
        var bounds = node.Bounds;
        if (!node.IsVisible || node.Opacity == 0 || bounds.Width <= 0 || bounds.Height <= 0) return;
        context.Save();
        try
        {
            if (node.Shadow == null) context.Clip(bounds);
            if (node.Opacity < 1 && context is IOpacityDrawingContext composite)
            {
                composite.SaveOpacity(node.Shadow?.PaintBounds(bounds) ?? bounds, node.Opacity);
                try { DrawSurface(node, context, overBackdrop, presenters); }
                finally { context.Restore(); }
            }
            else DrawSurface(node, context, overBackdrop, presenters);
        }
        finally { context.Restore(); }
    }

    private static void DrawSurface(Element node, IDrawingContext context, bool overBackdrop, UiPresenters presenters)
    {
        if (node.Shadow is { Color.A: > 0 } shadow && context is IShadowDrawingContext drawing)
            drawing.DrawShadow(node.Bounds, shadow, node.CornerRadius);
        if (node.Shadow != null) context.Clip(node.Bounds);
        overBackdrop = ElementChrome.DrawBackground(node, context, overBackdrop);
        var presenter = presenters.Resolve(node);
        presenter?.Before(node, context);
        var children = node.Children;
        var childClip = node.ChildClipBounds;
        if (children.Count != 0 && childClip != node.Bounds)
        {
            context.Save();
            try
            {
                context.Clip(childClip);
                for (var i = 0; i < children.Count; i++) DrawNode(children[i], context, overBackdrop, presenters);
            }
            finally { context.Restore(); }
        }
        else for (var i = 0; i < children.Count; i++) DrawNode(children[i], context, overBackdrop, presenters);
        presenter?.After(node, context);
        ElementChrome.DrawOverlay(node, context);
    }
}
