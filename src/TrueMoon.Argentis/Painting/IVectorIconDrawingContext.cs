namespace TrueMoon.Argentis.Painting;

/// <summary>Optional rendering capability for icon contours.</summary>
public interface IVectorIconDrawingContext
{
    /// <summary>Paints an aspect-preserving centered icon with the effective theme color.</summary>
    /// <param name="icon">Normalized contour to draw.</param>
    /// <param name="bounds">Logical destination bounds.</param>
    /// <param name="color">Fill or stroke color.</param>
    void DrawVectorIcon(VectorIcon icon, Rect bounds, Color color);
}