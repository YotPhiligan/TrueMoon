using TrueMoon.Configuration;
using TrueMoon.Diagnostics;
using TrueMoon.Exceptions;
using TrueMoon.Services;

namespace TrueMoon;

public class AppBuilder : IAppBuilder
{
    private readonly IServiceResolverBuilder _serviceResolverBuilder;
    private readonly List<Action<IAppConfigurationContext>> _configureActions = [];
    private readonly List<Action<IConfigurationBuilder>> _configurationBuilderActions = [];

    public AppBuilder(IServiceResolverBuilder serviceResolverBuilder)
    {
        _serviceResolverBuilder = serviceResolverBuilder;
    }
    
    public IAppBuilder Configuration(Action<IConfigurationBuilder> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _configurationBuilderActions.Add(action);
        return this;
    }

    public IAppBuilder Setup(Action<IAppConfigurationContext> action)
    {
        _configureActions.Add(action);
        return this;
    }
    
    public IApp Build()
    {
        var configurationBuilder = new ConfigurationBuilder();
        
        foreach (var action in _configurationBuilderActions)
        {
            action(configurationBuilder);
        }
        
        var configuration = configurationBuilder.Build();
        
        var ctx = new AppConfigurationContext();
        
        ctx.Services(t=>t
            .Singleton<IApp,DefaultApp>()
            .Instance(configuration)
            .Composite<DefaultAppLifetime,IAppLifetimeHandler,IAppLifetime>()
            .Singleton(typeof(IEventsSource<>),typeof(EventsSource<>))
        );
        
        foreach (var action in ctx.GetConfigurations())
        {
            action(configuration);
        }
        
        foreach (var action in _configureActions)
        {
            action(ctx);
        }
        
        var serviceResolver = _serviceResolverBuilder.Build(configuration, ctx.GetServicesRegistrations());
        
        var app = CreateApp(serviceResolver);
        
        return app;
    }

    protected virtual IApp CreateApp(IServiceResolver serviceResolver)
    {
        var app = serviceResolver.Resolve<IApp>() ?? throw new AppCreationException($"Failed to instantiate the \"{nameof(IApp)}\"");
        return app;
    }

    public static IAppBuilder Create(Action<IAppBuilderConfigurationContext> action)
    {
        var ctx = new AppBuilderConfigurationContext();
        action(ctx);
        var serviceResolverBuilder = ctx.GetServiceResolverBuilder();
        return new AppBuilder(serviceResolverBuilder);
    }
}