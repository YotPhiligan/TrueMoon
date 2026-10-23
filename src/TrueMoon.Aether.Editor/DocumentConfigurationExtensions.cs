using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;

namespace TrueMoon.Aether.Editor;

/// <summary>App registration for document persistence and caller/project-owned document sessions.</summary>
public static class DocumentConfigurationExtensions
{
    /// <summary>Connects documents after UseAetherAuthoring, before UseAetherEditorProjects when project-owned documents are wanted.</summary>
    /// <param name="context">Application composition context.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherEditorDocuments(this IAppConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services(services =>
        {
            if (services.Exist<SceneFileCodec>()) throw new InvalidOperationException("Editor documents are already registered.");
            if (!services.Exist<IFactory<ISceneAuthoringSession, SceneDocument>>())
                throw new InvalidOperationException("Call UseAetherAuthoring before UseAetherEditorDocuments.");
            services.Singleton<SceneFileCodec>(resolver => new(resolver.Resolve<IFactory<ISceneAuthoringSession, SceneDocument>>()!));
            if (!services.Exist<ISceneDocumentStore>())
                services.Singleton<ISceneDocumentStore>(resolver => new JsonSceneDocumentStore(resolver.Resolve<SceneFileCodec>()!));
            if (!services.Exist<IFactory<SceneDocumentSession, ResolvedProject>>())
                services.Singleton<IFactory<SceneDocumentSession, ResolvedProject>>(resolver =>
                    new Factory<SceneDocumentSession, ResolvedProject>(project => new(project, resolver.Resolve<ISceneDocumentStore>()!,
                        resolver.Resolve<IFactory<ISceneAuthoringSession, SceneDocument>>()!)));
        });
        return context;
    }
}
