namespace TrueMoon.Argentis.Painting;

/// <summary>Optional drawing capability; IDrawingContext implementations remain source/binary compatible.</summary>
public interface IBackdropDrawingContext
{
    /// <summary>Blurs earlier paint, then applies tint within the current clip and rounded rectangle.</summary>
    /// <remarks>Returns false without painting when unsupported; the caller paints the opaque fallback.
    /// Foreground and children must be drawn afterwards. No previous-frame or desktop sampling is implied.</remarks>
    bool TryDrawBackdrop(Rect rectangle, BackdropMaterial material, float cornerRadius);
}