using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Editor.Documents;

namespace TrueMoon.Aether.Editor;

/// <summary>App composition for editor project services, independent of window/engine startup.</summary>
public static class EditorConfigurationExtensions
{
    /// <summary>Registers one App-owned project coordinator and borrowed persistence/preflight/factory dependencies.</summary>
    /// <remarks>Register once. Services registered before this call override the defaults.
    /// Factory products belong to EditorProjects, never to the container.</remarks>
    /// <param name="context">Application composition context.</param>
    /// <returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherEditorProjects(this IAppConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services(services =>
        {
            if (services.Exist<EditorProjects>() || services.Exist<EditorProjectsLifetime>())
                throw new InvalidOperationException("Editor project services are already registered.");
            if (!services.Exist<IProjectStore>()) services.Singleton<IProjectStore>(_ => new JsonProjectStore());
            if (!services.Exist<IProjectToolchain>()) services.Singleton<IProjectToolchain>(_ => new DotnetProjectToolchain());
            if (!services.Exist<IFactory<ProjectSession, ProjectSessionArguments>>())
            {
                var documents = services.Exist<IFactory<SceneDocumentSession, ResolvedProject>>();
                services.Singleton<IFactory<ProjectSession, ProjectSessionArguments>>(resolver => new ProjectSessionFactory(
                    documents ? resolver.Resolve<IFactory<SceneDocumentSession, ResolvedProject>>() : null));
            }
            services.Singleton<EditorProjects>(resolver => new(resolver.Resolve<IProjectStore>(), resolver.Resolve<IProjectToolchain>(),
                    resolver.Resolve<IFactory<ProjectSession, ProjectSessionArguments>>()))
                .Singleton<EditorProjectsLifetime>(resolver => new(resolver.Resolve<EditorProjects>()))
                .Singleton<IStartable>(resolver => resolver.Resolve<EditorProjectsLifetime>())
                .Singleton<IStoppable>(resolver => resolver.Resolve<EditorProjectsLifetime>());
        });
        return context;
    }
}
