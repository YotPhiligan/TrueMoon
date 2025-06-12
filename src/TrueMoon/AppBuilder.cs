using TrueMoon.Configuration;
using TrueMoon.Diagnostics;
using TrueMoon.Exceptions;
using TrueMoon.Services;

namespace TrueMoon;

public class AppBuilder : IAppBuilder
{
    private readonly IServiceResolverBuilder _serviceResolverBuilder;
    private readonly List<Action<IAppConfigurationContext>> _configureActions = [];
    private Action<IConfigurationBuilder>? _configurationBuilderAction;

    public AppBuilder(IServiceResolverBuilder serviceResolverBuilder)
    {
        _serviceResolverBuilder = serviceResolverBuilder;
    }
    
    public IAppBuilder Configuration(Action<IConfigurationBuilder> action)
    {
        _configurationBuilderAction = action;
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
        _configurationBuilderAction?.Invoke(configurationBuilder);
        
        var configuration = configurationBuilder.Build();
        
        var ctx = new AppConfigurationContext();
        
        ctx.Services(t=>t
            .Singleton<IApp,DefaultApp>()
            .Instance(configuration)
            .Composite<DefaultAppLifetime,IAppLifetimeHandler,IAppLifetime>()
            .Singleton(typeof(IEventsSource<>),typeof(EventsSource<>))
        );
        
        foreach (var action in _configureActions)
        {
            action(ctx);
        }

        foreach (var action in ctx.GetConfigurations())
        {
            action(configuration);
        }
        
        var serviceResolver = _serviceResolverBuilder.Build(ctx.GetServicesRegistrations());
        
        // var modules = ctx.GetModules()
        //     .OrderBy(t=>t.ExecutionFlowOrder)
        //     .ToList();
        //
        // foreach (var module in modules)
        // {
        //     try
        //     {
        //         module.Configure(ctx);
        //     }
        //     catch (Exception e)
        //     {
        //         throw new AppCreationException($"Failed to configure \"{module.Name}\" module", e);
        //     }
        // }

        // foreach (var module in modules)
        // {
        //     try
        //     {
        //         module.Execute(serviceProvider, configuration);
        //     }
        //     catch (Exception e)
        //     {
        //         throw new AppCreationException($"Failed to execute \"{module.Name}\" module", e);
        //     }
        // }
        
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