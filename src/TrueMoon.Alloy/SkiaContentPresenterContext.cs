using SkiaSharp;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy;

public class SkiaContentPresenterContext : IContentPresenterContext
{
    public SKCanvas Canvas { get; }

    public SkiaContentPresenterContext(SKCanvas canvas)
    {
        Canvas = canvas;
    }
}