namespace TrueMoon.Argentis.Painting;

/// <summary>Optional outer-shadow capability. Legacy drawing contexts simply omit shadows.</summary>
public interface IShadowDrawingContext
{
    /// <summary>Paints only outside the original rounded rectangle, respecting the current ancestor clip.</summary>
    /// <param name="rectangle">Unshifted logical surface.</param><param name="shadow">Borrowed immutable description.</param>
    /// <param name="cornerRadius">Original surface corner radius.</param>
    void DrawShadow(Rect rectangle, BoxShadow shadow, float cornerRadius);
}