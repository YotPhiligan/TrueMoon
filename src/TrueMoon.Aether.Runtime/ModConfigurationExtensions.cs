using TrueMoon.Aether.Mods;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Mods;
using TrueMoon.Aether.Runtime.Scenes;

namespace TrueMoon.Aether.Runtime;

/// <summary>Optional managed Mod SDK integration, independent of Tooling/UI and native runtime activation.</summary>
public static class ModConfigurationExtensions
{
    /// <summary>Registers one App-owned ModHost and stable factories. Call UseAetherEcs first; no module code runs during setup.</summary>
    /// <param name="context">App composition context.</param><param name="configure">Explicit shared API allowlist and bounded admission policy.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherMods(this IAppConfigurationContext context, Action<ModLoaderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(context); var options = new ModLoaderOptions(); configure?.Invoke(options); var policy = options.Snapshot();
        context.Services(services =>
        {
            if (services.Exist<ModHost>() || services.Exist<IFactory<ModGroup, ModLoadRequest>>()) throw new InvalidOperationException("Aether Mods is already registered.");
            if (!services.Exist<IFactory<World>>()) throw new InvalidOperationException("Call UseAetherEcs before UseAetherMods.");
            RuntimeAppConfigurationExtensions.RegisterSceneResolution(services);
            if (!services.Exist<SceneDocumentCodec>()) services.Singleton<SceneDocumentCodec>(resolver => new(resolver.Resolve<SceneResolutionService>()));
            if (!services.Exist<IFactory<IModEntry, ModEntryArguments>>()) services.Singleton<IFactory<IModEntry, ModEntryArguments>>(_ => new ModEntryFactory());
            services.Singleton<IFactory<ModWorldSession>>(resolver =>
            {
                var worlds = resolver.Resolve<IFactory<World>>(); var resolution = resolver.Resolve<SceneResolutionService>();
                var loaders = new Factory<SceneWorldLoader, ComponentRegistry>(registry => new(registry, worlds, resolution));
                return new Factory<ModWorldSession>(() => new(loaders));
            });
            services.Singleton<IFactory<ModGroup, ModLoadRequest>>(resolver => new ModGroupFactory(policy, resolver,
                resolver.Resolve<IFactory<IModEntry, ModEntryArguments>>(), resolver.Resolve<IFactory<ModWorldSession>>()));
            services.Singleton<IFactory<ModSystemSession, World>>(resolver =>
                new Factory<ModSystemSession, World>(world => new(world, resolver.Resolve<IFactory<ModGroup, ModLoadRequest>>())));
            services.Singleton<ModWorldSnapshotCodec>(resolver => new(resolver.Resolve<SceneDocumentCodec>()));
            services.Singleton<IFactory<ModWorldMigrationSession>>(resolver =>
            {
                var groups = resolver.Resolve<IFactory<ModGroup, ModLoadRequest>>(); var worlds = resolver.Resolve<IFactory<World>>();
                var resolution = resolver.Resolve<SceneResolutionService>(); var codec = resolver.Resolve<ModWorldSnapshotCodec>();
                var loaders = new Factory<SceneWorldLoader, ComponentRegistry>(registry => new(registry, worlds, resolution));
                return new Factory<ModWorldMigrationSession>(() => new(groups, loaders, resolution, codec));
            });
            services.Singleton<ModHost>(resolver => new(resolver.Resolve<IFactory<ModGroup, ModLoadRequest>>(), resolver.Resolve<IFactory<ModSystemSession, World>>(), resolver.Resolve<IFactory<ModWorldMigrationSession>>()));
            services.Singleton<ModAppLifetime>(resolver => new(resolver.Resolve<ModHost>()))
                .Singleton<IStartable>(resolver => resolver.Resolve<ModAppLifetime>())
                .Singleton<IStoppable>(resolver => resolver.Resolve<ModAppLifetime>());
        });
        return context;
    }
}
