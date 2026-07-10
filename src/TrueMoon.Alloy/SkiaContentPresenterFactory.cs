using TrueMoon.Alloy.Presenters;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy;

public class SkiaContentPresenterFactory : IFactory<IContentPresenter>
{
    public IContentPresenter? Create()
    {
        throw new NotImplementedException();
    }

    public IContentPresenter? Create<TData>(TData? data = default)
    {
        if (data is Rectagle rect)
        {
            return new RectangleContentPresenter(rect);
        }

        return new CommonContentPresenter<TData>(data);
    }
}