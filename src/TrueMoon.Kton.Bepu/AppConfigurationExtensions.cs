using TrueMoon.Configuration;
using TrueMoon.Kton.Bepu.Hosting;

namespace TrueMoon.Kton.Bepu;

/// <summary>App composition for Kton's BEPU provider.</summary>
public static class AppConfigurationExtensions
{
    /// <summary>Validates and snapshots settings; registers one owned host and borrowed factory/lifecycle adapters.</summary>
    /// <param name="context">App composition context. Register before physics consumer startup services.</param>
    /// <param name="configure">Pure startup option overrides.</param><returns>The same context.</returns>
    /// <remarks>Build/start create no world, backend pool or worker. Worlds are caller-owned and are never disposed
    /// by App. Stop closes creation and retries unpublished failed-creation cleanup. No-argument Create uses configured
    /// defaults; explicit PhysicsWorldOptions replace them completely. Duplicate provider/factory registration is rejected.</remarks>
    public static IAppConfigurationContext UseBepuPhysics(this IAppConfigurationContext context, Action<BepuPhysicsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = new BepuPhysicsOptions();
        configure?.Invoke(options);
        return Register(context, options.Snapshot());
    }

    /// <summary>Binds the exact registered Kton schema using the common App configuration stage.</summary>
    /// <param name="context">App context supporting the configuration stage.</param>
    /// <param name="configure">Pure explicit overrides, applied after sources.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredBepuPhysics(this IAppConfigurationContext context, Action<BepuPhysicsOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, BepuPhysicsConfigurationSchema.Default,
            (app, value) => Register(app, value.Snapshot()), configure);

    private static IAppConfigurationContext Register(IAppConfigurationContext context, PhysicsWorldOptions defaults)
    {
        context.Services(services =>
        {
            if (services.Exist<BepuPhysicsHost>() || services.Exist<BepuPhysicsWorldFactory>() ||
                services.Exist<AppPhysicsWorldFactory>() || services.Exist<PhysicsLifecycle>() ||
                services.Exist<IFactory<IPhysicsWorld, PhysicsWorldOptions>>() || services.Exist<IFactory<IPhysicsWorld>>())
                throw new InvalidOperationException("Kton physics or its world factory is already registered in this App.");
            services.Singleton<BepuPhysicsHost>(_ => new BepuPhysicsHost(defaults));
            services.Singleton<AppPhysicsWorldFactory>(resolver => new AppPhysicsWorldFactory(resolver.Resolve<BepuPhysicsHost>()));
            services.Singleton<IFactory<IPhysicsWorld, PhysicsWorldOptions>>(resolver => resolver.Resolve<AppPhysicsWorldFactory>());
            services.Singleton<IFactory<IPhysicsWorld>>(resolver => resolver.Resolve<AppPhysicsWorldFactory>());
            services.Singleton<PhysicsLifecycle>(resolver => new PhysicsLifecycle(resolver.Resolve<BepuPhysicsHost>()));
            services.Singleton<IStartable>(resolver => resolver.Resolve<PhysicsLifecycle>());
            services.Singleton<IStoppable>(resolver => resolver.Resolve<PhysicsLifecycle>());
        });
        return context;
    }
}
