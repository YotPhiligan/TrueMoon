namespace TrueMoon.Argentis;

/// <summary>Declares a reusable view's appearance without retaining controls or application state.</summary>
/// <typeparam name="TView">The view whose public parts are styled.</typeparam>
public interface IViewStyle<TView> where TView : View
{
    /// <summary>Declares rules in increasing precedence. Called once for each application to a view.</summary>
    /// <param name="styles">A builder valid only during this declaration.</param>
    void Configure(IViewStyleBuilder<TView> styles);
}