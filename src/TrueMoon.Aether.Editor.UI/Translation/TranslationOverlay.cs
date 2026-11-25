using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using System.Collections.Immutable;
using System.Numerics;
using TrueMoon.Aether.Abstractions.Preview;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.UI.Translation;

/// <summary>Frozen translation overlay for one admitted viewport draw. Contains no controller or UI objects.</summary>
/// <param name="Geometry">Projected gizmo from the presented camera and image bounds.</param>
/// <param name="Clip">Effective viewport clip.</param><param name="Ghosts">Immutable selected geometry.</param>
/// <param name="Handle">Active constraint, or None outside a drag.</param><param name="Delta">Candidate world-space displacement.</param>
public readonly record struct TranslationOverlay(TranslationGeometry Geometry, ViewportImageBounds Clip,
    ImmutableArray<PreviewPickPrimitive> Ghosts, TranslationHandle Handle, Vector3 Delta)
{
    internal void Draw(IDrawingContext drawing, Theme theme, string family, int weight, bool italic)
    {
        var image = Geometry.Image;
        drawing.Save();
        try
        {
            drawing.Clip(new((float)Clip.Left, (float)Clip.Top, (float)Clip.Width, (float)Clip.Height));
            DrawBounds(drawing, default, theme.Focus);
            if (Handle != TranslationHandle.None) DrawBounds(drawing, Delta, theme.Modified);
            foreach (var handle in Geometry.Handles)
            {
                var color = handle.Kind switch
                {
                    TranslationHandle.X or TranslationHandle.YZ => theme.AxisX,
                    TranslationHandle.Y or TranslationHandle.XZ => theme.AxisY,
                    _ => theme.AxisZ
                };
                if (Handle == handle.Kind) color = theme.Focus;
                var points = handle.Points;
                if (points.Length == 2)
                {
                    DrawLine(drawing, points[0], points[1], color);
                    drawing.Fill(new((float)points[1].X - 3, (float)points[1].Y - 3, 6, 6), color);
                    drawing.Text(handle.Kind.ToString(), (float)points[1].X + 5, (float)points[1].Y - 6, color, 12, family, weight, italic);
                }
                else for (var i = 0; i < points.Length; i++) DrawLine(drawing, points[i], points[(i + 1) % points.Length], color with { A = 180 });
            }
            if (Handle != TranslationHandle.None) drawing.Text("Перемещение · отпустите для применения · Esc — отмена",
                (float)Math.Max(image.Left + 8, image.Left + image.Width / 2 - 160),
                (float)Math.Min(image.Top + image.Height, Clip.Top + Clip.Height) - 24,
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

    private void DrawBounds(IDrawingContext drawing, Vector3 delta, Color color)
    {
        foreach (var primitive in Ghosts)
        {
            double left = double.PositiveInfinity, top = double.PositiveInfinity, right = double.NegativeInfinity, bottom = double.NegativeInfinity;
            var valid = true;
            for (var i = 0; i < 8; i++)
            {
                var corner = Vector3.Transform(new((i & 1) == 0 ? -.5f : .5f, (i & 2) == 0 ? -.5f : .5f, (i & 4) == 0 ? -.5f : .5f), primitive.World) + delta;
                if (!TranslationGeometry.Project(corner, Geometry.Projection, Geometry.Image, out var point)) { valid = false; break; }
                left = Math.Min(left, point.X); right = Math.Max(right, point.X); top = Math.Min(top, point.Y); bottom = Math.Max(bottom, point.Y);
            }
            var bounds = new Rect((float)left, (float)top, (float)(right - left), (float)(bottom - top));
            if (valid && float.IsFinite(bounds.X) && float.IsFinite(bounds.Y) && float.IsFinite(bounds.Width) && float.IsFinite(bounds.Height)) drawing.Stroke(bounds, color, 2);
        }
    }
}
