using TrueMoon.Aluminum;
using TrueMoon.Diagnostics;

namespace TrueMoon.Alloy;

public class ViewHandleFactory : IFactory<IViewHandle>
{
    private readonly IEventsSourceFactory _eventsSourceFactory;
    private readonly IFactory<IGraphicsPlatform> _graphicsPlatformFactory;
    private readonly IFactory<IViewPresenter> _viewPresenterFactory;
    private readonly IVisualTreeBuilder _visualTreeBuilder;
    private readonly IAppLifetime _appLifetime;

    public ViewHandleFactory(IEventsSourceFactory eventsSourceFactory,
        IFactory<IGraphicsPlatform> graphicsPlatformFactory,
        IFactory<IViewPresenter> viewPresenterFactory,
        IVisualTreeBuilder visualTreeBuilder,
        IAppLifetime appLifetime)
    {
        _eventsSourceFactory = eventsSourceFactory;
        _graphicsPlatformFactory = graphicsPlatformFactory;
        _viewPresenterFactory = viewPresenterFactory;
        _visualTreeBuilder = visualTreeBuilder;
        _appLifetime = appLifetime;
    }
    
    private ViewHandle CreateCore()
    {
        var viewHandle = new ViewHandle(_eventsSourceFactory.Create("Window"), 
            _graphicsPlatformFactory.Create()!, 
            _viewPresenterFactory.Create()!, 
            _visualTreeBuilder, 
            _appLifetime);
        return viewHandle;
    }    
    
    public IViewHandle? Create()
    {
        return CreateCore();
    }

    public IViewHandle? Create<TData>(TData? data = default)
    {
        var handle = CreateCore();
        if (data is IView view)
        {
            handle.Attach(view);
        }

        return handle;
    }
}