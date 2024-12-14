using TrueMoon.Alloy.Presenters;
using TrueMoon.Aluminum;

namespace TrueMoon.Alloy;

public class SkiaContentPresenterFactory : IFactory<IContentPresenter>
{
    public IContentPresenter? Create()
    {
        throw new NotImplementedException();
    }

    public IContentPresenter? Create<TData>(TData? data = default)
    {
        return new CommonContentPresenter<TData>(data);
    }
}