using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether;

/// <summary>Connects the Aether engine module to TrueMoon App services and lifecycle.</summary>
public static class AppConfigurationExtensions
{
    /// <summary>Registers one App-owned engine host. Game sessions are started explicitly.</summary>
    /// <param name="context">The application configuration context.</param>
    /// <param name="configure">Engine identity, report directory, deadlines and runtime factory.</param>
    /// <returns>The same application configuration context for chaining.</returns>
    /// <exception cref="InvalidOperationException">Aether or its runtime factory is already registered.</exception>
    public static IAppConfigurationContext UseAether(this IAppConfigurationContext context, Action<AetherOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new AetherOptions(); configure(options);
        var snapshot = options.Snapshot();
        context.Services(services =>
        {
            if (services.Exist<AetherHost>() || services.Exist<IRuntimeFactory>() || services.Exist<AetherAppLifetime>())
                throw new InvalidOperationException("Aether is already registered. Configure its runtime factory through UseAether.");
            services.Singleton<IRuntimeFactory>(resolver => snapshot.RuntimeFactory(resolver)
                    ?? throw new InvalidOperationException("Aether runtime factory returned null."))
                .Singleton<AetherHost>(resolver => new AetherHost(resolver.Resolve<IRuntimeFactory>(), snapshot))
                .Singleton<AetherAppLifetime>(resolver => new AetherAppLifetime(resolver.Resolve<AetherHost>()))
                .Singleton<IStartable>(resolver => resolver.Resolve<AetherAppLifetime>())
                .Singleton<IStoppable>(resolver => resolver.Resolve<AetherAppLifetime>());
        });
        return context;
    }
}
