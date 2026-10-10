namespace TrueMoon.Alloy;

/// <summary>Mutually exclusive desktop window composition modes.</summary>
public enum WindowTransparencyMode
{
    /// <summary>Ignores framebuffer alpha and preserves an opaque window.</summary>
    Opaque,
    /// <summary>Applies uniform opacity to the window, including its system decoration.</summary>
    Opacity,
    /// <summary>Composites premultiplied framebuffer alpha with the desktop.</summary>
    PerPixel
}

/// <summary>Immutable creation settings, independent of the graphics backend and platform.</summary>
public sealed record WindowAppearance
{
    /// <summary>Desktop composition mode. Cannot change after creating the window.</summary>
    public WindowTransparencyMode Transparency { get; init; }
    /// <summary>Initial uniform opacity from zero to one; only valid in Opacity mode.</summary>
    public float Opacity { get; init; } = 1;
    /// <summary>Whether the operating system draws its standard frame and title bar.</summary>
    public bool Decorated { get; init; } = true;

    /// <summary>Rejects unknown modes, invalid opacity, and incompatible combinations.</summary>
    public void Validate()
    {
        if (!Enum.IsDefined(Transparency)) throw new ArgumentOutOfRangeException(nameof(Transparency));
        ValidateOpacity(Opacity);
        if (Transparency != WindowTransparencyMode.Opacity && Opacity != 1)
            throw new ArgumentException("Uniform opacity requires Opacity mode; it cannot be combined with per-pixel alpha.", nameof(Opacity));
    }
    /// <summary>Validates a uniform opacity value without allocating a native window.</summary>
    /// <param name="opacity">Finite opacity in the inclusive range zero to one.</param>
    public static void ValidateOpacity(float opacity)
    {
        if (!float.IsFinite(opacity) || opacity < 0 || opacity > 1) throw new ArgumentOutOfRangeException(nameof(opacity));
    }
}

/// <summary>Native window capabilities; Vulkan composite-alpha support must be checked separately by its presenter.</summary>
/// <param name="TransparentFramebuffer">Whether the window exposes a transparent framebuffer.</param>
/// <param name="WindowOpacity">Whether uniform window opacity can be queried and changed.</param>
public sealed record WindowAppearanceCapabilities(bool TransparentFramebuffer, bool WindowOpacity);

/// <summary>Optional window appearance operations. All operations use the window owner thread.</summary>
public interface IWindowAppearanceHost : IWindowHost
{
    /// <summary>Validated creation settings.</summary>
    WindowAppearance Appearance { get; }
    /// <summary>Native capabilities, independent of Vulkan surface composite-alpha support.</summary>
    WindowAppearanceCapabilities AppearanceCapabilities { get; }
    /// <summary>Current native uniform opacity.</summary>
    float Opacity { get; }
    /// <summary>Changes uniform opacity only when the window was created in Opacity mode.</summary>
    /// <param name="opacity">Finite opacity from zero to one.</param>
    void SetOpacity(float opacity);
}
