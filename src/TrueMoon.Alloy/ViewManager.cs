using TrueMoon.Aluminum;

namespace TrueMoon.Alloy;

public class ViewManager : IViewManager
{
    private readonly List<IViewHandle> _viewHandles = [];
    private IViewHandle? _mainViewHandle;
    private readonly IFactory<IViewHandle> _viewHandleFactory;
    private readonly IAppLifetime _appLifetime;

    public ViewManager(IFactory<IViewHandle> viewHandleFactory, IAppLifetime appLifetime)
    {
        _viewHandleFactory = viewHandleFactory;
        _appLifetime = appLifetime;
    }
    
    public void Show(IView view)
    {
        if (_mainViewHandle != null && _mainViewHandle.View == view)
        {
            _mainViewHandle.Run();
            return;
        }

        var viewHandle = _viewHandles.FirstOrDefault(t => t.View == view);
        
        if (viewHandle != null)
        {
            viewHandle.Run();
            return;
        }
        
        viewHandle = _viewHandleFactory.Create(view);
        if (viewHandle == null)
        {
            throw new InvalidOperationException("Failed to create view handle");
        }
        
        viewHandle.Run();
        
        _viewHandles.Add(viewHandle);

        if (_mainViewHandle != null) return;
        
        _mainViewHandle = viewHandle;
        viewHandle.Closed += OnMainViewClosed;
    }

    private void OnMainViewClosed()
    {
        _appLifetime.Cancel();
    }
}