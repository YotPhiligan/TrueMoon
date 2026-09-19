namespace TrueMoon.Argentis;

/// <summary>A size in device-independent pixels.</summary>
public readonly record struct Size(float Width, float Height);
/// <summary>A rectangle in device-independent pixels.</summary>
public readonly record struct Rect(float X, float Y, float Width, float Height)
{
    /// <summary>Tests the half-open rectangle.</summary>
    public bool Contains(float x, float y) => x >= X && y >= Y && x < X + Width && y < Y + Height;
    /// <summary>Insets the rectangle, clamping its dimensions to zero.</summary>
    public Rect Deflate(Thickness t) => new(X + t.Left, Y + t.Top,
        Math.Max(0, Width - t.Horizontal), Math.Max(0, Height - t.Vertical));
}
/// <summary>Four edge distances in logical pixels.</summary>
public readonly record struct Thickness(float Left, float Top, float Right, float Bottom)
{
    /// <summary>Creates equal edge distances.</summary>
    public Thickness(float all) : this(all, all, all, all) { }
    /// <summary>Total horizontal distance.</summary>
    public float Horizontal => Left + Right;
    /// <summary>Total vertical distance.</summary>
    public float Vertical => Top + Bottom;
}
/// <summary>An unpremultiplied RGBA color; renderers perform alpha conversion.</summary>
public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
{
    /// <summary>Transparent black.</summary>
    public static Color Transparent => new(0, 0, 0, 0);
}
/// <summary>Alignment within an allocated layout slot.</summary>
public enum LayoutAlignment { Stretch, Start, Center, End }
