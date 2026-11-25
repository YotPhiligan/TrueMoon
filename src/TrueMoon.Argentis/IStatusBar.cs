namespace TrueMoon.Argentis;

/// <summary>Adaptive status layout; overflow content remains owned and available to a details presenter.</summary>
public interface IStatusBar
{
    /// <summary>Gets secondary elements currently hidden by the available width, in declaration order.</summary>
    IReadOnlyList<Element> OverflowedItems { get; }
    /// <summary>Raised when responsive layout changes the overflow set.</summary>
    event Action? OverflowChanged;
}