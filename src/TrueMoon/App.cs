using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Exceptions;

namespace TrueMoon;

public static class App
{
    private static readonly IEventsSource AppSource = new EventsSource($"{typeof(App).FullName}");
    
    /// <summary>
    /// Configure new app
    /// </summary>
    /// <param name="action">configuration delegate</param>
    /// <returns>instance of created app</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="AppCreationException"></exception>
    public static IApp Create(Action<IAppCreationContext> action, CancellationTokenSource? cancellationTokenSource = default)
    {
        // if (AppSource.IsEnabled($"{DiagnosticEventsRoot}.CreateStart"))
        // {
        //     AppSource.Write($"{DiagnosticEventsRoot}.CreateStart", Message.Trace());
        // }
        
        using var createEvent = AppSource.UseActivity();

        ArgumentNullException.ThrowIfNull(action);

        try
        {
            var dependenciesContext = new DependenciesRegistrationContext();
        
            IPathResolver pathResolver = new PathResolver();
        
            IConfiguration configuration = CreateConfiguration(pathResolver);
        
            IAppCreationContext ctx = new AppCreationContext(configuration, dependenciesContext);
        
            ctx.AddDependencies(t=>t
                .Add<IApp,DefaultApp>()
                .Add(pathResolver)
                .Add(configuration)
                .Add(new AppCancellationTokenSourceHandle(cancellationTokenSource ?? new CancellationTokenSource()))
                .Add<DefaultAppLifetime>(d=>d
                    .With<IAppLifetimeHandler>()
                    .With<IAppLifetime>()
                )
                .Add(typeof(IEventsSource<>), typeof(EventsSource<>))
            );

            ctx.UseProvider<SimpleDependencyInjectionProvider>();
        
            try
            {
                action(ctx);
            }
            catch (Exception e)
            {
                throw new AppCreationException("Failed to create the app", e);
            }

            var modules = ctx.GetModules()
                .OrderBy(t=>t.ExecutionFlowOrder)
                .ToList();
        
            foreach (var module in modules)
            {
                try
                {
                    module.Configure(ctx);
                }
                catch (Exception e)
                {
                    throw new AppCreationException($"Failed to configure \"{module.Name}\" module", e);
                }
            }
        
            if (ctx is not IServiceProviderBuilder serviceProviderBuilder)
            {
                throw new AppCreationException($"Failed to instantiate the \"{nameof(IApp)}\"");
            }
        
            var serviceProvider = serviceProviderBuilder.Build();

            foreach (var module in modules)
            {
                try
                {
                    module.Execute(serviceProvider, configuration);
                }
                catch (Exception e)
                {
                    throw new AppCreationException($"Failed to execute \"{module.Name}\" module", e);
                }
            }
        
            var app = serviceProvider.Resolve<IApp>() ?? throw new AppCreationException($"Failed to instantiate the \"{nameof(IApp)}\"");
            
            Console.CancelKeyPress += (_, _) =>
            {
                app.Services.Resolve<IAppLifetime>()?.Cancel();
            };
            
            return app;
        }
        catch (Exception e)
        {
            AppSource.Exception(e);
            throw;
        }
    }

    private static Configuration.Configuration CreateConfiguration(IPathResolver pathResolver)
    {
        var argsSection = new CommandLineArgsProvider();
        var jsonProvider = new JsonConfigurationProvider(pathResolver);
        var defaultProvider = new DefaultConfigurationProvider();
        var configuration = new Configuration.Configuration(new List<IConfigurationProvider>
        {
            argsSection,
            defaultProvider,
            jsonProvider
        });
        
        return configuration;
    }

    /// <summary>
    /// Run new app
    /// </summary>
    /// <param name="action">configuration delegate</param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="AppCreationException"></exception>
    public static async Task RunAsync(Action<IAppCreationContext> action, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        await using var app = Create(action, cts);
        
        using var runEvent = AppSource.UseActivity();
        
        try
        {
            var lifetimeHandler = app.Services.Resolve<IAppLifetimeHandler>();
            if (lifetimeHandler == null)
            {
                throw new AppCreationException($"{nameof(IAppLifetimeHandler)} is missing");
            }
            
            await app.StartAsync(cts.Token);
            
            AppSource.Trace("Started");
            
            await lifetimeHandler.WaitAsync(cts.Token);

            AppSource.Trace("Stopping");
            lifetimeHandler.Stopping();
        
            await app.StopAsync(cts.Token);

            lifetimeHandler.Stopped();
            
            AppSource.Trace("Stopped");
        }
        catch (Exception e)
        {
            AppSource.Exception(e);
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
        
        if (parameters.Length == 0 || parameters.First().ParameterType != typeof(IAppCreationContext))
        {
            throw new AppCreationException($"{typeof(T)} does not contain method \"Configure\" with \"{nameof(IAppCreationContext)}\" parameter");
        }
        
        return RunAsync(t=>method.Invoke(configurator, [t]), cancellationToken);
    }
}