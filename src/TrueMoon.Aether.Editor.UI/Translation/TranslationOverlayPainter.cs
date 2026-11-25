using System.Numerics;
using TrueMoon.Argentis;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;

namespace TrueMoon.Aether.Editor.UI.Translation;

internal static class TranslationOverlayPainter
{
    internal static void Draw(in TranslationOverlay overlay, IDrawingContext drawing, Theme theme, string family, int weight, bool italic)
    {
        var image = overlay.Geometry.Image;
        var clip = overlay.Clip;
        drawing.Save();
        try
        {
            drawing.Clip(new((float)clip.Left, (float)clip.Top, (float)clip.Width, (float)clip.Height));
            DrawBounds(overlay, drawing, default, theme.Focus);
            if (overlay.Handle != TranslationHandle.None) DrawBounds(overlay, drawing, overlay.Delta, theme.Modified);
            foreach (var handle in overlay.Geometry.Handles)
            {
                var color = handle.Kind switch
                {
                    TranslationHandle.X or TranslationHandle.YZ => theme.AxisX,
                    TranslationHandle.Y or TranslationHandle.XZ => theme.AxisY,
                    _ => theme.AxisZ
                };
                if (overlay.Handle == handle.Kind) color = theme.Focus;
                var points = handle.Points;
                if (points.Length == 2)
                {
                    DrawLine(drawing, points[0], points[1], color);
                    drawing.Fill(new((float)points[1].X - 3, (float)points[1].Y - 3, 6, 6), color);
                    drawing.Text(handle.Kind.ToString(), (float)points[1].X + 5, (float)points[1].Y - 6, color, 12, family, weight, italic);
                }
                else for (var i = 0; i < points.Length; i++) DrawLine(drawing, points[i], points[(i + 1) % points.Length], color with { A = 180 });
            }
            if (overlay.Handle != TranslationHandle.None) drawing.Text("Перемещение · отпустите для применения · Esc — отмена",
                (float)Math.Max(image.Left + 8, image.Left + image.Width / 2 - 160),
                (float)Math.Min(image.Top + image.Height, clip.Top + clip.Height) - 24,
                theme.Modified, 12, family, weight, italic);
        }
        finally { drawing.Restore(); }
    }

    private static void DrawLine(IDrawingContext drawing, TranslationPoint a, TranslationPoint b, Color color)
    {
        var steps = (int)Math.Clamp(Math.Ceiling(Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y))), 1, 192);
        for (var i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            drawing.Fill(new((float)(a.X + (b.X - a.X) * t) - 1, (float)(a.Y + (b.Y - a.Y) * t) - 1, 2, 2), color);
        }
    }

    private static void DrawBounds(in TranslationOverlay overlay, IDrawingContext drawing, Vector3 delta, Color color)
    {
        foreach (var primitive in overlay.Ghosts)
        {
            double left = double.PositiveInfinity, top = double.PositiveInfinity, right = double.NegativeInfinity, bottom = double.NegativeInfinity;
            var valid = true;
            for (var i = 0; i < 8; i++)
            {
                var corner = Vector3.Transform(new((i & 1) == 0 ? -.5f : .5f, (i & 2) == 0 ? -.5f : .5f, (i & 4) == 0 ? -.5f : .5f), primitive.World) + delta;
                if (!TranslationGeometry.Project(corner, overlay.Geometry.Projection, overlay.Geometry.Image, out var point)) { valid = false; break; }
                left = Math.Min(left, point.X); right = Math.Max(right, point.X); top = Math.Min(top, point.Y); bottom = Math.Max(bottom, point.Y);
            }
            var bounds = new Rect((float)left, (float)top, (float)(right - left), (float)(bottom - top));
            if (valid && float.IsFinite(bounds.X) && float.IsFinite(bounds.Y) && float.IsFinite(bounds.Width) && float.IsFinite(bounds.Height)) drawing.Stroke(bounds, color, 2);
        }
    }
}
