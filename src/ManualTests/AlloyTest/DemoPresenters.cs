using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis.Painting;

namespace AlloyTest;

internal static class DemoPresenters
{
    internal static void Configure(UiPresenters presenters)
    {
        presenters.Register<CustomTitleBar>(
            static (bar, context) => context.Fill(bar.Bounds, bar.Theme.Control));
        presenters.Register<View1.LogoMark>(
            static (mark, context) => UiSymbols.Draw(context, UiSymbol.TrueMoon, mark.Bounds, mark.Foreground));
        presenters.Register<VolumeMeter>(static (meter, context) =>
        {
            ProgressBarPresenter.DrawFill(meter, context);
            context.Text($"Громкость: {meter.Value:P0}", meter.Bounds.X + 8, meter.Bounds.Y + 8,
                meter.IsEffectivelyEnabled ? meter.Foreground : meter.Theme.Disabled, meter.FontSize, meter.FontFamily);
        });
    }
}
