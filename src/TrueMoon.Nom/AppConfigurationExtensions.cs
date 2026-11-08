using TrueMoon.Nom.Backup;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Hosting;

namespace TrueMoon.Nom;

/// <summary>Connects Nom's store factory and resource ownership to App without opening any project.</summary>
public static class AppConfigurationExtensions
{
    /// <summary>Validates and snapshots limits, registering one owned host and non-owning factory/lifecycle aliases.</summary>
    /// <param name="context">App composition context. Call before registering consumer startup services.</param>
    /// <param name="configure">Optional limit configuration, executed once when attaching.</param>
    /// <returns>The same context.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An option exceeds its supported admission bounds.</exception>
    /// <exception cref="InvalidOperationException">Nom or its typed factory is already registered in this graph.</exception>
    /// <remarks>Use from App.Build, App.RunAsync or Builder.Setup. Build/start open no catalog.
    /// The App owns NomStoreHost; project consumers dispose factory products early. Stop/failed startup drain through
    /// one shared lifecycle adapter, and resolver disposal provides fallback cleanup without owning aliases twice.</remarks>
    public static IAppConfigurationContext UseNom(this IAppConfigurationContext context, Action<NomOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = new NomOptions(); configure?.Invoke(options); var settings = options.Snapshot();
        context.Services(services =>
        {
            if (services.Exist<NomStoreHost>() || services.Exist<NomLifecycle>() || services.Exist<IFactory<IProjectAssetStore, OpenAssetStoreArguments>>() || services.Exist<IFactory<IProjectAssetStore, RestoreAssetStoreArguments>>())
                throw new InvalidOperationException("Nom is already registered in this App.");
            services.Singleton<NomStoreHost>(_ => new NomStoreHost(new CatalogFactory(settings.MaximumOpenStores), settings));
            services.Singleton<IFactory<IProjectAssetStore, OpenAssetStoreArguments>>(resolver =>
                new Factory<IProjectAssetStore, OpenAssetStoreArguments>(resolver.Resolve<NomStoreHost>().Create));
            services.Singleton<IFactory<IProjectAssetStore, RestoreAssetStoreArguments>>(resolver =>
                new Factory<IProjectAssetStore, RestoreAssetStoreArguments>(resolver.Resolve<NomStoreHost>().Create));
            services.Singleton<NomLifecycle>(resolver => new NomLifecycle(resolver.Resolve<NomStoreHost>()));
            services.Singleton<IStartable>(resolver => resolver.Resolve<NomLifecycle>());
            services.Singleton<IStoppable>(resolver => resolver.Resolve<NomLifecycle>());
        });
        return context;
    }
}
