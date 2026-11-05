namespace TrueMoon.Argentis;

/// <summary>Fluent configuration of standalone window components.</summary>
public static class WindowExtensions
{
    /// <summary>Sets the window caption.</summary>
    /// <typeparam name="TWindow">Concrete window type.</typeparam><param name="window">The component.</param><param name="title">Caption.</param>
    /// <returns>The same component.</returns>
    public static TWindow WithTitle<TWindow>(this TWindow window, string title) where TWindow : Window
    { ArgumentNullException.ThrowIfNull(window); window.Title = title; return window; }
    /// <summary>Requests the client area independently of content layout sizing.</summary>
    /// <typeparam name="TWindow">Concrete window type.</typeparam><param name="window">The component.</param>
    /// <param name="width">Positive whole logical width, at most 32768.</param><param name="height">Positive whole logical height, at most 32768.</param>
    /// <returns>The same component.</returns>
    public static TWindow WithClientSize<TWindow>(this TWindow window, float width, float height) where TWindow : Window
    { ArgumentNullException.ThrowIfNull(window); window.ClientSize = new(width, height); return window; }
}
