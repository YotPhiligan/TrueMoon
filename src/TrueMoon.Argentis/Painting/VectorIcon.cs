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