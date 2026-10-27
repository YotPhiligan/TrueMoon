using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;

namespace TrueMoon.Aether;

/// <summary>Connects shared scene encoding and persistence without an Editor, runtime, window or native startup.</summary>
public static class ScenePersistenceConfigurationExtensions
{
    /// <summary>Registers default shared scene codec/store after authoring. Existing registrations are preserved;
    /// repeated attachment is idempotent. Registration and App startup perform no file IO or session creation.</summary>
    /// <param name="context">Application composition context.</param>
    /// <returns>The same context.</returns>
    /// <exception cref="InvalidOperationException">The borrowed authoring session factory is missing.</exception>
    public static IAppConfigurationContext UseAetherScenePersistence(this IAppConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services(services =>
        {
            if (!services.Exist<IFactory<ISceneAuthoringSession, SceneDocument>>())
                throw new InvalidOperationException("Call UseAetherAuthoring before UseAetherScenePersistence.");
            if (!services.Exist<SceneFileCodec>())
                services.Singleton<SceneFileCodec>(resolver => new(resolver.Resolve<IFactory<ISceneAuthoringSession, SceneDocument>>()!));
            if (!services.Exist<ISceneDocumentStore>())
                services.Singleton<ISceneDocumentStore>(resolver => new JsonSceneDocumentStore(resolver.Resolve<SceneFileCodec>()!));
        });
        return context;
    }
}
