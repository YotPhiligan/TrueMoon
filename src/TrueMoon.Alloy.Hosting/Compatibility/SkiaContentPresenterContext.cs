using SkiaSharp;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Hosting.Compatibility;

public class SkiaContentPresenterContext : IContentPresenterContext
{
    public SKCanvas Canvas { get; }

    public SkiaContentPresenterContext(SKCanvas canvas)
    {
        Canvas = canvas;
    }
}