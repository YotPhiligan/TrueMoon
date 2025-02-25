using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Exceptions;

namespace TrueMoon;

public static class App
{
    private static readonly IEventsSource ConfiguratorSource = new EventsSource("App");

    public static IAppBuilder Builder(Action<IAppBuilderConfigurationContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var builder = AppBuilder.Create(action);
        return builder;
    }

    /// <summary>
    /// Run new app
    /// </summary>
    /// <param name="action">configuration delegate</param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="AppCreationException"></exception>
    public static Task RunAsync(Action<IAppConfigurationContext> action, CancellationToken cancellationToken = default)
    {
        var builder = Builder(_ => {});
        ConfiguratorSource.Trace("Builder ready");
        
        return RunAsync(builder, action, cancellationToken);
    }
    
    public static async Task RunAsync(IAppBuilder builder, Action<IAppConfigurationContext> action, CancellationToken cancellationToken = default)
    {
        builder.Setup(action);
        
        await using var app = builder.Build();
        ConfiguratorSource.Trace("Created");
        
        using var runEvent = ConfiguratorSource.UseActivity();
        
        try
        {
            var lifetimeHandler = app.Services.Resolve<IAppLifetimeHandler>();
            if (lifetimeHandler == null)
            {
                throw new AppCreationException($"{nameof(IAppLifetimeHandler)} is missing");
            }
            
            await app.StartAsync(cancellationToken);
            
            ConfiguratorSource.Trace("Started");
            
            await lifetimeHandler.WaitAsync(cancellationToken);

            ConfiguratorSource.Trace("Stopping");
            lifetimeHandler.Stopping();
        
            await app.StopAsync(cancellationToken);

            lifetimeHandler.Stopped();
            
            ConfiguratorSource.Trace("Stopped");
        }
        catch (Exception e)
        {
            ConfiguratorSource.Exception(e);
        }
    }
    
    /// <summary>
    /// Run new app
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <exception cref="AppCreationException"></exception>
    public static Task RunAsync<T>(CancellationToken cancellationToken = default) where T : class 
    {
        var configurator = Activator.CreateInstance<T>();
        var type = configurator.GetType();
        var method = type.GetMethod("Configure");
        if (method == null)
        {
            throw new AppCreationException($"{typeof(T)} does not contain method \"Configure\"");
        }

        var parameters = method.GetParameters();
        
        if (parameters.Length == 0 || parameters.First().ParameterType != typeof(IAppConfigurationContext))
        {
            throw new AppCreationException($"{typeof(T)} does not contain method \"Configure\" with \"{nameof(IAppConfigurationContext)}\" parameter");
        }
        
        return RunAsync(t=>method.Invoke(configurator, [t]), cancellationToken);
    }
}