namespace TrueMoon.Argentis;

/// <summary>Builds appearance rules scoped to one view, excluding nested views.</summary>
/// <typeparam name="TView">The owning view.</typeparam>
public interface IViewStyleBuilder<TView> where TView : View
{
    /// <summary>Declares a rule for the owning view itself.</summary>
    ViewStyleRule Root();

    /// <summary>Declares a rule for a live part already owned by this view.</summary>
    /// <typeparam name="TElement">Part type.</typeparam>
    /// <param name="selector">Selects a stable part within this view's boundary.</param>
    ViewStyleRule Part<TElement>(Func<TView, TElement> selector) where TElement : Element;

    /// <summary>Declares a rule for existing and future controls with the given role.</summary>
    /// <typeparam name="TElement">Role's control type.</typeparam>
    /// <param name="role">A shared typed token; identity, rather than its name, determines a match.</param>
    ViewStyleRule Role<TElement>(ViewStyleRole<TElement> role) where TElement : Element;
}