namespace TrueMoon.Argentis;

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