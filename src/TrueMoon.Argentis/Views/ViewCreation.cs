using TrueMoon.Argentis.Styling;

namespace TrueMoon.Argentis.Views;

/// <summary>Prepares a constructed view before its factory publishes it.</summary>
public static class ViewCreation
{
    /// <summary>Configures the style of a view before the caller publishes it.</summary>
    /// <param name="view">A completed, unparented draft on the UI owner thread. The draft is disposed if this call fails during preparation.</param>
    /// <param name="style">A borrowed style declaration, or null to leave the view without a view style.</param>
    /// <returns>The prepared view, owned by the caller.</returns>
    public static TView Prepare<TView>(TView view, IViewStyle<TView>? style = null) where TView : View
    {
        ArgumentNullException.ThrowIfNull(view);
        view.VerifyAccess();
        if (view.Parent != null || view.IsAttached)
            throw new InvalidOperationException("View preparation requires an unparented, unattached view.");

        try
        {
            return view.WithViewStyle(style);
        }
        catch (Exception original)
        {
            DisposeAfterFailure(view, original);
            throw;
        }
    }

    internal static void DisposeAfterFailure(Element element, Exception original)
    {
        try { element.Dispose(); }
        catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
    }
}
