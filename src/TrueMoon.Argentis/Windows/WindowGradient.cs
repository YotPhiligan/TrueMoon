namespace TrueMoon.Argentis.Windows;

/// <summary>Three opaque color stops from the upper left to the lower right of a window surface.</summary>
public sealed record WindowGradient
{
    /// <summary>Creates a static gradient with equally spaced color stops.</summary>
    public WindowGradient(Color start, Color middle, Color end)
    {
        if (start.A != 255 || middle.A != 255 || end.A != 255)
            throw new ArgumentException("Window gradient colors must be opaque.");
        Start = start;
        Middle = middle;
        End = end;
    }

    /// <summary>Color at the upper left.</summary>
    public Color Start { get; }
    /// <summary>Color at the midpoint.</summary>
    public Color Middle { get; }
    /// <summary>Color at the lower right.</summary>
    public Color End { get; }
}

/// <summary>Optional rendering support for the themed window underlay.</summary>
public interface IWindowGradientDrawingContext
{
    /// <summary>Fills the rounded bounds with the gradient, or returns false without painting.</summary>
    bool TryDrawWindowGradient(Rect bounds, WindowGradient gradient, float cornerRadius);
}
