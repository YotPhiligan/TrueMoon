namespace TrueMoon.Argentis;

/// <summary>An unpremultiplied RGBA color; renderers perform alpha conversion.</summary>
public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
{
    /// <summary>Transparent black.</summary>
    public static Color Transparent => new(0, 0, 0, 0);
}