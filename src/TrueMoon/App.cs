using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using TrueMoon.Exceptions;

namespace TrueMoon;

public static class App
{
    private static readonly IEventsSource ConfiguratorSource = new EventsSource("TrueMoon.App");

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
        ArgumentNullException.ThrowIfNull(action);
        var builder = Builder(_ => {});
        ConfiguratorSource.Trace("Builder ready");
        
        return RunAsync(builder, action, cancellationToken);
    }
    
    public static async Task RunAsync(IAppBuilder builder, Action<IAppConfigurationContext> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(action);
        builder.Setup(action);
        
        var app = builder.Build();
        ConfiguratorSource.Trace("Created");
        
        using var runEvent = ConfiguratorSource.UseActivity();
        
        try
        {
            await AppRunner.RunAsync(app, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            ConfiguratorSource.Exception(e);
            throw;
        }
    }
    
    /// <summary>
    /// Run new app
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <exception cref="AppCreationException"></exception>
    public static Task RunAsync<T>(CancellationToken cancellationToken = default) where T : class 
    {
        var method = typeof(T).GetMethod("Configure", BindingFlags.Public | BindingFlags.Instance,
            [typeof(IAppConfigurationContext)]);
        if (method == null || method.ReturnType != typeof(void))
        {
            throw new AppCreationException($"{typeof(T)} does not contain method \"Configure\" with \"{nameof(IAppConfigurationContext)}\" parameter");
        }
        
        var configurator = Activator.CreateInstance<T>();
        return RunAsync(context =>
        {
            try { method.Invoke(configurator, [context]); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            }
        }, cancellationToken);
    }
    
    public static IApp Build(Action<IAppConfigurationContext>? action = null)
    {
        var builder = Builder(_ => {});
        ConfiguratorSource.Trace("Builder ready");
        
        if (action != null)
        {
            builder.Setup(action);
        }
        
        var app = builder.Build();
        ConfiguratorSource.Trace("Created");

        return app;
    }
}
