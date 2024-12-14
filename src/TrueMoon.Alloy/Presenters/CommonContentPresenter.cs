using TrueMoon.Aluminum;

namespace TrueMoon.Alloy.Presenters;

public class CommonContentPresenter<T> : IContentPresenter<T>
{
    public CommonContentPresenter(T? content)
    {
        Content = content;
    }

    public void Present(double t, IContentPresenterContext context)
    {
        if (context is not SkiaContentPresenterContext ctx)
        {
            throw new InvalidOperationException($"Invalid presenter context - {context}");
        }
        
        //ctx.Canvas.DrawRect();
    }

    public T? Content { get; }
}