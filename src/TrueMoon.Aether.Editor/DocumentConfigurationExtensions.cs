using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;

namespace TrueMoon.Aether.Editor;

/// <summary>App registration for caller/project-owned Editor document sessions borrowing Aether scene persistence.</summary>
public static class DocumentConfigurationExtensions
{
    /// <summary>Connects documents after UseAetherAuthoring, reusing Aether scene persistence and existing overrides.
    /// Call before UseAetherEditorProjects when project-owned documents are wanted; attach Editor documents once.</summary>
    /// <param name="context">Application composition context.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherEditorDocuments(this IAppConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services(services =>
        {
            if (services.Exist<Registration>()) throw new InvalidOperationException("Editor documents are already registered.");
            if (!services.Exist<IFactory<ISceneAuthoringSession, SceneDocument>>())
                throw new InvalidOperationException("Call UseAetherAuthoring before UseAetherEditorDocuments.");
            services.Singleton<Registration>(_ => new());
            if (!services.Exist<IFactory<SceneDocumentSession, ResolvedProject>>())
                services.Singleton<IFactory<SceneDocumentSession, ResolvedProject>>(resolver =>
                    new Factory<SceneDocumentSession, ResolvedProject>(project => new(project, resolver.Resolve<ISceneDocumentStore>()!,
                        resolver.Resolve<IFactory<ISceneAuthoringSession, SceneDocument>>()!)));
        });
        return context.UseAetherScenePersistence();
    }

    private sealed class Registration { }
}
