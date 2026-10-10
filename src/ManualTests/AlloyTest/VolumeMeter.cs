using TrueMoon.Argentis;

namespace AlloyTest;

// A small custom-painted widget reusing the normalized progress property and theme.
internal sealed class VolumeMeter : ProgressBar
{
    protected override Size MeasureCore(Size available, ITextLayoutService text) => new(200, 36);

    protected override void DrawCore(IDrawingContext context)
    {
        base.DrawCore(context);
        context.Text($"Громкость: {Value:P0}", Bounds.X + 8, Bounds.Y + 8,
            IsEffectivelyEnabled ? Foreground : Theme.Disabled, FontSize, FontFamily);
    }
}
