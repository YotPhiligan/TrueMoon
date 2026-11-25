using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis;

namespace AlloyTest;

// A small custom-painted widget reusing the normalized progress property and theme.
internal sealed class VolumeMeter : ProgressBar
{
    protected override Size MeasureCore(Size available, ITextLayoutService text) => new(200, 36);

    private static readonly ElementPresentation Painter = ElementPresentation.Create<VolumeMeter>(static (meter, context) =>
    {
        ProgressBarPresenter.DrawFill(meter, context);
        context.Text($"Громкость: {meter.Value:P0}", meter.Bounds.X + 8, meter.Bounds.Y + 8,
            meter.IsEffectivelyEnabled ? meter.Foreground : meter.Theme.Disabled, meter.FontSize, meter.FontFamily);
    });
    public override ElementPresentation Presentation => Painter;
}
