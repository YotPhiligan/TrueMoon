using TrueMoon.Argentis;

namespace TrueMoon.Alloy;

public class PresentationInitializer : IPresentationInitializer, IStartable
{
    private readonly IViewManager _viewManager;
    private readonly PresentationConfiguration _presentationConfiguration;
    private readonly IServiceProvider _serviceProvider;

    public PresentationInitializer(IViewManager viewManager, 
        PresentationConfiguration presentationConfiguration,
        IServiceProvider serviceProvider)
    {
        _viewManager = viewManager;
        _presentationConfiguration = presentationConfiguration;
        _serviceProvider = serviceProvider;
    }
    
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_presentationConfiguration.StartupViewType is not null)
        {
            var startupView = (IView)_serviceProvider.GetService(_presentationConfiguration.StartupViewType)!;
            _presentationConfiguration.StartupViewCreationDelegate?.Invoke(startupView);
            _viewManager.Show(startupView);
        }
        else
        {
            _viewManager.ShowEmpty();
        }
        
        return Task.CompletedTask;
    }
}