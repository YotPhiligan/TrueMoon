namespace TrueMoon.Argentis.Bindings;

/// <summary>Turns one bound source property value into the UI property value and back.</summary>
/// <typeparam name="TSourceValue">Type of the selected source property.</typeparam>
/// <typeparam name="TValue">Type of the bound UI property.</typeparam>
public interface ITwoWayValueConverter<TSourceValue, TValue>
{
    /// <summary>Projects the source value onto the UI value. Throwing leaves the UI value unchanged.</summary>
    TValue Convert(TSourceValue value);

    /// <summary>Accepts the current UI value back. False keeps the source value and the user's draft
    /// and must report a non-empty reason; the binding shows it as the control's error message.</summary>
    bool TryConvertBack(TValue value, out TSourceValue sourceValue, out string? error);
}
