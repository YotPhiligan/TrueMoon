namespace TrueMoon.Argentis;

/// <summary>Optional group compositing for an element and its complete subtree.</summary>
public interface IOpacityDrawingContext
{
    /// <summary>Saves a transparent layer, composited once with the given opacity by its matching Restore.</summary>
    /// <param name="bounds">Finite nonnegative layer bounds in logical coordinates.</param>
    /// <param name="opacity">Finite alpha multiplier in the inclusive range zero to one.</param>
    /// <remarks>Pair a successful call with IDrawingContext.Restore. Contexts without this capability
    /// draw fractional-opacity elements opaquely; zero-opacity elements are omitted by Element.</remarks>
    void SaveOpacity(Rect bounds, float opacity);
}
