using SkiaSharp;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Presenters;

public class RectangleContentPresenter : IContentPresenter<Rectagle>
{
    public RectangleContentPresenter(Rectagle? content)
    {
        Content = content;
    }

    public void Present(double t, IContentPresenterContext context)
    {
        if (context is SkiaContentPresenterContext ctx)
        {
            using var skPaint = new SKPaint();
            skPaint.Color = SKColors.GreenYellow;
            ctx.Canvas.DrawRect(0, 0, Content.Width, Content.Height, skPaint);
        }
    }

    public Rectagle? Content { get; }
}