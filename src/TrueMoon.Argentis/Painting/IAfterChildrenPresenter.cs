namespace TrueMoon.Argentis.Painting;

/// <summary>Optional second phase of a presenter, for custom overlays above the node's children.</summary>
public interface IAfterChildrenPresenter<in TState> : IElementPresenter<TState>
{
    /// <summary>Draws above descendants, before the shared border and focus/validation indicators.</summary>
    void DrawAfterChildren(TState state, IDrawingContext context);
}
