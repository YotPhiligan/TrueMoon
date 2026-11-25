namespace TrueMoon.Argentis;

/// <summary>A rectangle in device-independent pixels.</summary>
public readonly record struct Rect(float X, float Y, float Width, float Height)
{
    /// <summary>Tests the half-open rectangle.</summary>
    public bool Contains(float x, float y) => x >= X && y >= Y && x < X + Width && y < Y + Height;
    /// <summary>Insets the rectangle, clamping its dimensions to zero.</summary>
    public Rect Deflate(Thickness t) => new(X + t.Left, Y + t.Top,
        Math.Max(0, Width - t.Horizontal), Math.Max(0, Height - t.Vertical));
}