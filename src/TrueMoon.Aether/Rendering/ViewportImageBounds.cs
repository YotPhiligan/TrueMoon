namespace TrueMoon.Aether.Rendering;

/// <summary>Frozen logical image rectangle, excluding letterbox bars. No UI or DPI dependency.</summary>
/// <param name="Left">Logical left.</param><param name="Top">Logical top.</param>
/// <param name="Width">Logical width.</param><param name="Height">Logical height.</param>
public readonly record struct ViewportImageBounds(double Left, double Top, double Width, double Height)
{
    /// <summary>Maps half-open logical coordinates to a physical pixel; invalid/outside input is never clamped.</summary>
    /// <param name="x">Logical X, already normalized by the input host.</param><param name="y">Logical Y.</param>
    /// <param name="physicalWidth">Physical image width.</param><param name="physicalHeight">Physical image height.</param>
    /// <param name="pixelX">Pixel column, or zero on failure.</param><param name="pixelY">Pixel row, or zero on failure.</param>
    /// <returns>True only for finite positive bounds and a coordinate inside the image.</returns>
    public bool TryMapPixel(double x, double y, int physicalWidth, int physicalHeight, out int pixelX, out int pixelY)
    {
        pixelX = pixelY = 0;
        var right = Left + Width; var bottom = Top + Height;
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(Left) || !double.IsFinite(Top) ||
            !double.IsFinite(Width) || !double.IsFinite(Height) || !double.IsFinite(right) || !double.IsFinite(bottom) ||
            Width <= 0 || Height <= 0 || right <= Left || bottom <= Top || physicalWidth <= 0 || physicalHeight <= 0 ||
            x < Left || x >= right || y < Top || y >= bottom) return false;
        var px = Math.Floor((x - Left) / Width * physicalWidth);
        var py = Math.Floor((y - Top) / Height * physicalHeight);
        if (px < 0 || px >= physicalWidth || py < 0 || py >= physicalHeight) return false;
        pixelX = (int)px; pixelY = (int)py; return true;
    }
}
