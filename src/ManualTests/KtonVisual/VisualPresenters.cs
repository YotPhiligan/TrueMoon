using TrueMoon.Alloy.Runtime;

namespace TrueMoon.ManualTests.KtonVisual;

internal static class VisualPresenters
{
    internal static void Configure(UiPresenters presenters)
    {
        presenters.Register<VisualCanvas>(static (canvas, context) =>
        {
            if (canvas.Image is not { } image) return;
            var version = canvas.ImageVersion;
            context.Image(image, canvas.DrawBounds);
            canvas.ReportDrawn(version);
        });
    }
}
