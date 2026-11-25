namespace TrueMoon.Argentis.Painting;

/// <summary>Draws node content from a paint state without owning the node or traversing children.</summary>
/// <typeparam name="TState">Prepared visual inputs for one draw call.</typeparam>
public interface IElementPresenter<in TState>
{
    /// <summary>Paints content after the background and before children.</summary>
    /// <param name="state">State for this node and frame.</param>
    /// <param name="context">Borrowed logical-coordinate drawing context.</param>
    void Draw(TState state, IDrawingContext context);
}
