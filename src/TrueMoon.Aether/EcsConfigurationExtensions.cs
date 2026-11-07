using TrueMoon.Aether.Ecs;

namespace TrueMoon.Aether;

/// <summary>Registers headless ECS creation without runtime, rendering, native resources or automatic World ownership.</summary>
public static class EcsConfigurationExtensions
{
    /// <summary>Registers one App-local factory for independent caller-owned Worlds.
    /// Register once; startup creates no World. The session must dispose its results before releasing borrowed dependencies.</summary>
    /// <param name="context">Application configuration.</param>
    /// <param name="configure">Optional limits, validated and copied during configuration.</param>
    /// <returns>The same configuration context.</returns>
    /// <exception cref="InvalidOperationException">The World factory is already registered.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Configured storage limits are invalid.</exception>
    public static IAppConfigurationContext UseAetherEcs(this IAppConfigurationContext context,
        Action<WorldOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = new WorldOptions(); configure?.Invoke(options);
        var snapshot = options.Snapshot();
        context.Services(services =>
        {
            if (services.Exist<IFactory<World>>()) throw new InvalidOperationException("Aether ECS is already registered.");
            services.Singleton<IFactory<World>>(_ => new Factory<World>(() => new World(snapshot)));
        });
        return context;
    }

    /// <summary>Registers an App-local scheduler factory after UseAetherEcs. Systems consume registered IFactory&lt;T,World&gt; dependencies.</summary>
    /// <param name="context">Application configuration.</param><param name="configure">System metadata; copied before registration.</param>
    /// <param name="configureOptions">Optional graph/phase limits, copied before activation.</param><returns>The same configuration context.</returns>
    /// <remarks>Successful schedulers and their systems are caller-owned; the App never disposes their borrowed World.</remarks>
    public static IAppConfigurationContext UseAetherEcsSystems(this IAppConfigurationContext context,
        Action<EcsSystemsBuilder> configure, Action<EcsSchedulerOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(configure);
        var options = new EcsSchedulerOptions(); configureOptions?.Invoke(options); var limits = options.Snapshot();
        var builder = new EcsSystemsBuilder(); configure(builder); var definitions = builder.Snapshot(limits);
        context.Services(services =>
        {
            if (!services.Exist<IFactory<World>>()) throw new InvalidOperationException("Call UseAetherEcs before registering systems.");
            if (services.Exist<IFactory<EcsScheduler, World>>()) throw new InvalidOperationException("Aether ECS systems are already registered.");
            services.Singleton<IFactory<EcsScheduler, World>>(resolver =>
            {
                var registrations = definitions.Select(d => EcsSystemRegistration.FromPlan(d.Plan, d.Resolve(resolver))).ToArray();
                return new Factory<EcsScheduler, World>(world => EcsScheduler.Create(world, registrations, limits));
            });
        });
        return context;
    }
}
