namespace TrueMoon.Alloy;

public class SkiaGlViewPresenterFactory : IFactory<IViewPresenter>
{
    public SkiaGlViewPresenterFactory()
    {
        
    }
    
    public IViewPresenter Create()
    {
        //return new SkiaGlViewPresenter();
        return new SkiaViewPresenter();
    }

    public IViewPresenter? Create<TData>(TData? data = default) => Create();
}