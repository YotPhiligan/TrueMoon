namespace TrueMoon.Argentis.Painting;

/// <summary>An immutable frosted background over content painted earlier in the same window.</summary>
public sealed record BackdropMaterial
{
    /// <summary>Creates a material. It owns no resources; styles and elements may share it.</summary>
    /// <param name="blurSigma">Gaussian standard deviation in logical pixels, from zero to 64.</param>
    /// <param name="tint">Color blended over the blurred backdrop; its alpha controls translucency.</param>
    /// <param name="fallback">Opaque fill used when the drawing backend cannot sample its backdrop.</param>
    public BackdropMaterial(float blurSigma, Color tint, Color fallback)
    {
        if (!float.IsFinite(blurSigma) || blurSigma < 0 || blurSigma > 64)
            throw new ArgumentOutOfRangeException(nameof(blurSigma));
        if (fallback.A != 255) throw new ArgumentException("Backdrop fallback must be opaque.", nameof(fallback));
        BlurSigma = blurSigma; Tint = tint; Fallback = fallback;
    }
    /// <summary>Gaussian standard deviation in logical pixels. Zero selects tint without blur.</summary>
    public float BlurSigma { get; }
    /// <summary>Translucent overlay color.</summary>
    public Color Tint { get; }
    /// <summary>Opaque color for drawing contexts without backdrop support.</summary>
    public Color Fallback { get; }
}