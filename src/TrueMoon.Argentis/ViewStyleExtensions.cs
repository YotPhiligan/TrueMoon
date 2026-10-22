namespace TrueMoon.Argentis;

/// <summary>Applies typed view styles and semantic roles without rebuilding UI trees.</summary>
public static class ViewStyleExtensions
{
    /// <summary>Replaces or removes the view style layer, preserving local and element style values.</summary>
    /// <typeparam name="TView">The concrete view.</typeparam>
    /// <param name="view">The style owner.</param><param name="style">Borrowed declaration; null removes the layer.</param>
    /// <returns>The same view.</returns>
    /// <remarks>Declaration/value validation failures preserve the previous layer. Observer errors propagate after commit.</remarks>
    public static TView WithViewStyle<TView>(this TView view, IViewStyle<TView>? style) where TView : View
    {
        ArgumentNullException.ThrowIfNull(view);
        view.VerifyAccess();
        ViewStyleScope? scope = null;
        if (style != null)
        {
            var builder = new ViewStyleBuilder<TView>(view);
            style.Configure(builder);
            scope = builder.Build();
        }

        view.ReplaceViewStyle(scope);
        return view;
    }

    /// <summary>Adds a typed semantic role used by the nearest owning view's style.</summary>
    /// <typeparam name="T">The concrete element.</typeparam><typeparam name="TElement">Role's control type.</typeparam>
    /// <param name="element">The element to mark.</param><param name="role">A shared role token.</param>
    /// <returns>The same concrete element.</returns>
    public static T WithRole<T, TElement>(this T element, ViewStyleRole<TElement> role)
        where T : TElement where TElement : Element
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(role);
        element.AddViewStyleRole(role);
        return element;
    }
}