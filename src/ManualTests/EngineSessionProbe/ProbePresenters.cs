using TrueMoon.Alloy.Runtime;

namespace EngineSessionProbe;

internal static class ProbePresenters
{
    internal static void Configure(UiPresenters presenters)
    {
        presenters.Register<GpuViewportCanvas>(static (canvas, context) =>
        {
            var bounds = canvas.Bounds;
            var image = canvas.Image;
            var stamp = canvas.Stamp;
            context.Fill(bounds, new(19, 37, 61));
            context.Save();
            try
            {
                context.Clip(bounds);
                if (image != null) context.Image(image, new(bounds.X + 8, bounds.Y + 8, canvas.PixelWidth * canvas.Zoom, canvas.PixelHeight * canvas.Zoom));
            }
            finally { context.Restore(); }
            canvas.ReportDrawn(image != null ? stamp : null);
        });
        presenters.Register<ViewportCanvas>(static (canvas, context) =>
        {
            context.Fill(canvas.Bounds, new(15, 19, 28));
            var gpuFrame = canvas.GpuFrame;
            if (canvas.Image is not { } image || (gpuFrame != null && !canvas.IsGpuCurrent))
            {
                canvas.ReportDrawn(null, null);
                return;
            }
            var imageFrame = canvas.ImageFrame;
            context.Image(image, canvas.ImageBounds);
            canvas.ReportDrawn(imageFrame, gpuFrame);
        });
    }
}
