namespace TrueMoon.Argentis.Painting;

/// <summary>An immutable icon contour in a square 24-unit view box.</summary>
public sealed class VectorIcon
{
    internal VectorIcon(string pathData, float strokeWidth = 0) => (PathData, StrokeWidth) = (pathData, strokeWidth);

    /// <summary>SVG path commands in icon coordinates.</summary>
    public string PathData { get; }

    /// <summary>Stroke width in icon units. Zero selects filling.</summary>
    public float StrokeWidth { get; }

    /// <summary>Width and height of the icon view box.</summary>
    public const float ViewBoxSize = 24;
}

/// <summary>Optional rendering capability for icon contours.</summary>
public interface IVectorIconDrawingContext
{
    /// <summary>Paints an aspect-preserving centered icon with the effective theme color.</summary>
    /// <param name="icon">Normalized contour to draw.</param>
    /// <param name="bounds">Logical destination bounds.</param>
    /// <param name="color">Fill or stroke color.</param>
    void DrawVectorIcon(VectorIcon icon, Rect bounds, Color color);
}
