namespace TrueMoon.Argentis;

/// <summary>An immutable outer shadow in logical pixels. It owns no rendering resources.</summary>
public sealed record BoxShadow
{
    /// <summary>Creates a bounded Gaussian shadow, drawn outside the original rounded surface.</summary>
    /// <param name="blurSigma">Gaussian standard deviation, zero to32 logical pixels.</param>
    /// <param name="offsetX">Horizontal displacement, at most64 logical pixels in either direction.</param>
    /// <param name="offsetY">Vertical displacement, at most64 logical pixels in either direction.</param>
    /// <param name="color">Shadow color and opacity.</param>
    public BoxShadow(float blurSigma, float offsetX, float offsetY, Color color)
    {
        if (!float.IsFinite(blurSigma) || blurSigma < 0 || blurSigma > 32) throw new ArgumentOutOfRangeException(nameof(blurSigma));
        if (!float.IsFinite(offsetX) || Math.Abs(offsetX) > 64) throw new ArgumentOutOfRangeException(nameof(offsetX));
        if (!float.IsFinite(offsetY) || Math.Abs(offsetY) > 64) throw new ArgumentOutOfRangeException(nameof(offsetY));
        BlurSigma = blurSigma; OffsetX = offsetX; OffsetY = offsetY; Color = color;
    }
    /// <summary>Gaussian standard deviation in logical pixels.</summary>
    public float BlurSigma { get; }
    /// <summary>Horizontal logical displacement.</summary>
    public float OffsetX { get; }
    /// <summary>Vertical logical displacement.</summary>
    public float OffsetY { get; }
    /// <summary>Shadow color, including opacity.</summary>
    public Color Color { get; }
    internal Rect PaintBounds(Rect bounds)
    {
        var halo = 3 * BlurSigma;
        var left = Math.Min(bounds.X, bounds.X + OffsetX - halo);
        var top = Math.Min(bounds.Y, bounds.Y + OffsetY - halo);
        return new(left, top, Math.Max(bounds.X + bounds.Width, bounds.X + bounds.Width + OffsetX + halo) - left,
            Math.Max(bounds.Y + bounds.Height, bounds.Y + bounds.Height + OffsetY + halo) - top);
    }
}

/// <summary>Optional outer-shadow capability. Legacy drawing contexts simply omit shadows.</summary>
public interface IShadowDrawingContext
{
    /// <summary>Paints only outside the original rounded rectangle, respecting the current ancestor clip.</summary>
    /// <param name="rectangle">Unshifted logical surface.</param><param name="shadow">Borrowed immutable description.</param>
    /// <param name="cornerRadius">Original surface corner radius.</param>
    void DrawShadow(Rect rectangle, BoxShadow shadow, float cornerRadius);
}
