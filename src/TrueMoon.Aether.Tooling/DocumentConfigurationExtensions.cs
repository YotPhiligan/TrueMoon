using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.Scenes;
using TrueMoon.Aether.Tooling.Scenes.Authoring;

namespace TrueMoon.Aether.Tooling;

/// <summary>App registration for caller/project-owned Editor document sessions borrowing Aether scene persistence.</summary>
public static class DocumentConfigurationExtensions
{
    /// <summary>Registers the catalog factory used by import producers and Editor documents.</summary>
    /// <param name="context">Application composition context.</param>
    /// <returns>The same context.</returns>
    /// <remarks>Register model source capture and import first. Register scene model selection persistence before Editor.</remarks>
    public static IAppConfigurationContext UseAetherProjectModelCatalog(this IAppConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services(services =>
        {
            if (services.Exist<Registration>() || services.Exist<IFactory<ProjectModelCatalog, ResolvedProject>>())
                throw new InvalidOperationException("Register the project model catalog once, before editor documents.");
            if (!services.Exist<IFactory<ModelImportLedgerSession, ModelImportLedgerArguments>>() ||
                !services.Exist<IFactory<ModelSourceCapture, ModelSourceCaptureArguments>>())
                throw new InvalidOperationException("The project model catalog requires model import and source capture.");
            var hasReimport = services.Exist<IFactory<SceneModelReimportSettings, SceneModelReimportRestoreArguments>>();
            var hasImport = services.Exist<IFactory<ModelImportOperation, ModelImportArguments>>();
            services.Singleton<IFactory<ProjectModelCatalog, ResolvedProject>>(resolver =>
            {
                var ledgers = resolver.Resolve<IFactory<ModelImportLedgerSession, ModelImportLedgerArguments>>();
                var captures = resolver.Resolve<IFactory<ModelSourceCapture, ModelSourceCaptureArguments>>();
                var reimport = hasReimport ? resolver.Resolve<IFactory<SceneModelReimportSettings, SceneModelReimportRestoreArguments>>() : null;
                var import = hasImport ? resolver.Resolve<IFactory<ModelImportOperation, ModelImportArguments>>() : null;
                return new Factory<ProjectModelCatalog, ResolvedProject>(project => new(project, ledgers, captures, reimport, import));
            });
        });
        return context;
    }

    /// <summary>Enables project-local persistence and read-only restoration of document model selections.</summary>
    /// <param name="context">Application composition context.</param>
    /// <returns>The same context.</returns>
    /// <remarks>Register before editor document services along with model source capture. Optionally supply an App factory
    /// for <see cref="SceneModelReimportRestoreArguments"/> to restore trusted reimport settings for saved ledger references.</remarks>
    public static IAppConfigurationContext UseAetherSceneModelSelectionPersistence(this IAppConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services(services =>
        {
            if (services.Exist<Registration>() || services.Exist<ModelSelectionPersistence>())
                throw new InvalidOperationException("Register model selection persistence once, before editor documents.");
            services.Singleton<ModelSelectionPersistence>(_ => new());
        });
        return context;
    }

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
            {
                var hasWatches = services.Exist<IFactory<ModelSourceWatch, ModelSourceWatchArguments>>();
                var persistsModels = services.Exist<ModelSelectionPersistence>();
                var hasCatalog = services.Exist<IFactory<ProjectModelCatalog, ResolvedProject>>();
                if (persistsModels && !hasWatches)
                    throw new InvalidOperationException("Register model source capture before enabling model selection persistence.");
                if (hasCatalog && !persistsModels)
                    throw new InvalidOperationException("The Editor model catalog requires scene model selection persistence.");
                var hasImport = services.Exist<IFactory<ModelImportOperation, ModelImportArguments>>() &&
                    services.Exist<IFactory<ModelImportLedgerSession, ModelImportLedgerArguments>>();
                var restoresReimport = hasImport && services.Exist<IFactory<SceneModelReimportSettings, SceneModelReimportRestoreArguments>>();
                services.Singleton<IFactory<SceneDocumentSession, ResolvedProject>>(resolver =>
                    new Factory<SceneDocumentSession, ResolvedProject>(project => new(project, resolver.Resolve<ISceneDocumentStore>()!,
                        resolver.Resolve<IFactory<ISceneAuthoringSession, SceneDocument>>()!)
                    {
                        ModelCatalog = hasCatalog ? resolver.Resolve<IFactory<ProjectModelCatalog, ResolvedProject>>().Create(project) : null,
                        ModelImports = hasWatches ? new(project.Description.Id,
                            resolver.Resolve<IFactory<ModelSourceCapture, ModelSourceCaptureArguments>>(),
                            resolver.Resolve<IFactory<ModelSourceWatch, ModelSourceWatchArguments>>(),
                            hasImport ? resolver.Resolve<IFactory<ModelImportOperation, ModelImportArguments>>() : null,
                            hasImport ? resolver.Resolve<IFactory<ModelImportLedgerSession, ModelImportLedgerArguments>>() : null,
                            persistsModels ? new(project, restoresReimport ?
                                resolver.Resolve<IFactory<SceneModelReimportSettings, SceneModelReimportRestoreArguments>>() : null) : null) : null
                    }));
            }
        });
        return context.UseAetherScenePersistence();
    }

    private sealed class Registration { }
    private sealed class ModelSelectionPersistence { }
}
