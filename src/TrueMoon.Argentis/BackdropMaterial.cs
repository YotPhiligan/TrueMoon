namespace TrueMoon.Argentis;

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

/// <summary>Optional drawing capability; IDrawingContext implementations remain source/binary compatible.</summary>
public interface IBackdropDrawingContext
{
    /// <summary>Blurs earlier paint, then applies tint within the current clip and rounded rectangle.</summary>
    /// <remarks>Returns false without painting when unsupported; the caller paints the opaque fallback.
    /// Foreground and children must be drawn afterwards. No previous-frame or desktop sampling is implied.</remarks>
    bool TryDrawBackdrop(Rect rectangle, BackdropMaterial material, float cornerRadius);
}
