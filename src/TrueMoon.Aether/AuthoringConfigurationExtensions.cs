using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;

namespace TrueMoon.Aether;

/// <summary>Connects local authoring to App without runtime, game assembly, native device, window or IPC startup.</summary>
public static class AuthoringConfigurationExtensions
{
    /// <summary>Registers an App-local factory for independent caller-owned scene sessions and shared stateless resolution.
    /// Can be composed with UseAether in either order. Register once; no session is opened during App startup.</summary>
    /// <param name="context">Application configuration.</param>
    /// <param name="configure">Optional limits, copied and validated during configuration.</param>
    /// <returns>The same configuration context.</returns>
    /// <exception cref="InvalidOperationException">The session factory is already registered.</exception>
    public static IAppConfigurationContext UseAetherAuthoring(this IAppConfigurationContext context,
        Action<SceneAuthoringOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = new SceneAuthoringOptions(); configure?.Invoke(options);
        var snapshot = options.Snapshot();
        context.Services(services =>
        {
            if (services.Exist<IFactory<ISceneAuthoringSession, SceneDocument>>())
                throw new InvalidOperationException("Aether authoring is already registered.");
            AppConfigurationExtensions.RegisterSceneResolution(services);
            services.Singleton<IFactory<ISceneAuthoringSession, SceneDocument>>(resolver =>
                new SceneAuthoringSessionFactory(resolver.Resolve<SceneResolutionService>(), snapshot));
        });
        return context;
    }
}
