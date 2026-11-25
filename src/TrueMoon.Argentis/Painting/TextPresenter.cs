using TrueMoon.Argentis.Styling;

namespace TrueMoon.Argentis.Painting;

/// <summary>Stock label painting for display text and controls that compose a text label.</summary>
public sealed class TextPresenter : IElementPresenter<Text>
{
    /// <inheritdoc />
    public void Draw(Text state, IDrawingContext context) => DrawLabel(state, context);

    /// <summary>Paints the label in the padded bounds, using its effective enabled state.</summary>
    public static void DrawLabel(Text state, IDrawingContext context)
    {
        var area = state.Bounds.Deflate(state.Padding);
        context.Text(state.Value, area.X, area.Y, state.IsEffectivelyEnabled ? state.Foreground : state.Theme.Disabled,
            state.FontSize, state.FontFamily, state.FontWeight, state.FontItalic);
    }
}
