using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Services;

namespace TrueMoon.Aether;

/// <summary>Connects neutral scene loading and runtime schema bindings through App without starting runtime/UI/native resources.</summary>
public static class EcsSceneLoadingConfigurationExtensions
{
    /// <summary>Registers one App-local registry, stateless loader/codec and typed caller-owned scene factory after UseAetherEcs.</summary>
    /// <param name="context">App configuration.</param><param name="registryFactory">Composition-root factory for frozen bindings and borrowed dependencies.</param>
    /// <param name="configure">Optional limits, validated/copied during configuration.</param><returns>The same configuration context.</returns>
    /// <remarks>App never owns returned SceneWorld instances. Dispose them before releasing registry/provider dependencies.</remarks>
    public static IAppConfigurationContext UseAetherSceneLoading(this IAppConfigurationContext context,
        Func<IServiceResolver, ComponentRegistry> registryFactory, Action<SceneWorldLoadOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(registryFactory);
        var options = new SceneWorldLoadOptions(); configure?.Invoke(options); var limits = options.Snapshot();
        context.Services(services =>
        {
            if (!services.Exist<IFactory<World>>()) throw new InvalidOperationException("Call UseAetherEcs before scene loading.");
            if (services.Exist<ComponentRegistry>() || services.Exist<SceneWorldLoader>() || services.Exist<IFactory<SceneWorld, SceneWorldLoadRequest>>() || services.Exist<SceneWorldCodec>())
                throw new InvalidOperationException("Aether scene loading is already registered.");
            AppConfigurationExtensions.RegisterSceneResolution(services);
            services.Singleton<ComponentRegistry>(resolver => registryFactory(resolver) ?? throw new InvalidOperationException("Registry factory returned null."));
            services.Singleton<SceneWorldLoader>(resolver => new(resolver.Resolve<ComponentRegistry>(), resolver.Resolve<IFactory<World>>(), resolver.Resolve<SceneResolutionService>(), limits));
            services.Singleton<IFactory<SceneWorld, SceneWorldLoadRequest>>(resolver => resolver.Resolve<SceneWorldLoader>());
            services.Singleton<SceneWorldCodec>(CreateNeutralCodec);
        });
        return context;
    }

    // Invoked only inside registration factories; does not activate persistence/UI or game registries.
    internal static SceneWorldCodec CreateNeutralCodec(IServiceResolver resolver)
    {
        var codec = resolver.TryResolve<SceneFileCodec>();
        if (codec == null)
        {
            var validators = resolver.TryResolve<IFactory<ISceneAuthoringSession, SceneDocument>>() ??
                new SceneAuthoringSessionFactory(resolver.Resolve<SceneResolutionService>(), new SceneAuthoringOptions());
            codec = new SceneFileCodec(validators);
        }
        return new(codec);
    }
}
