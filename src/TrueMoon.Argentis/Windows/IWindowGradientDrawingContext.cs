namespace TrueMoon.Argentis.Windows;

/// <summary>Optional rendering support for the themed window underlay.</summary>
public interface IWindowGradientDrawingContext
{
    /// <summary>Fills the rounded bounds with the gradient, or returns false without painting.</summary>
    bool TryDrawWindowGradient(Rect bounds, WindowGradient gradient, float cornerRadius);
}