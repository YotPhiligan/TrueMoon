namespace TrueMoon.Alloy.Platform.Silk;

// Windows client coordinates are physical pixels; Argentis coordinates are 96-DPI units.
internal readonly record struct WindowPixelScale
{
    internal float Scale { get; }
    internal WindowPixelScale(uint dpi)
    {
        if (dpi == 0) throw new ArgumentOutOfRangeException(nameof(dpi));
        Scale = dpi / 96f;
    }
    internal float ToLogical(float pixels) => pixels / Scale;
    internal int RoundPixels(float logical) => Convert(logical, 0);
    internal int MinimumPixels(float logical) => Convert(logical, 1);
    internal int MaximumPixels(float logical) => Convert(logical, -1);
    private int Convert(float logical, int rounding)
    {
        if (!float.IsFinite(logical) || logical < 0) throw new ArgumentOutOfRangeException(nameof(logical));
        var pixels = (double)logical * Scale;
        var rounded = rounding > 0 ? Math.Ceiling(pixels) : rounding < 0 ? Math.Floor(pixels)
            : Math.Round(pixels, MidpointRounding.AwayFromZero);
        if (rounded > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(logical), "The scaled size exceeds native coordinates.");
        return (int)rounded;
    }
}
