using TrueMoon.Argentis.Styling;

namespace TrueMoon.Argentis.Painting;

/// <summary>Stock button label painting using geometry prepared by measurement.</summary>
public sealed class ButtonPresenter : IElementPresenter<Button>
{
    /// <inheritdoc />
    public void Draw(Button state, IDrawingContext context) => DrawLabel(state, context);

    /// <summary>Draws the stock aligned label without visiting children or changing button state.</summary>
    public static void DrawLabel(Button state, IDrawingContext context)
    {
        if (state.LabelInkCenter is not { } center)
        {
            TextPresenter.DrawLabel(state, context);
            return;
        }
        var area = state.Bounds.Deflate(state.Padding);
        var x = area.X + Math.Max(0, (area.Width - state.LabelSize.Width) / 2);
        var y = area.Height > 0 ? area.Y + area.Height / 2 : state.Bounds.Y + state.Bounds.Height / 2;
        context.Text(state.Value, x, y - center, state.IsEffectivelyEnabled ? state.Foreground : state.Theme.Disabled,
            state.FontSize, state.FontFamily, state.FontWeight, state.FontItalic);
    }
}
