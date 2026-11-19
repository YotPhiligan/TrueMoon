using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Nom;

namespace TrueMoon.Aether.Nom;

/// <summary>Optional authoring App composition for Aether model assets in Nom.</summary>
public static class NomContentAppConfigurationExtensions
{
    /// <summary>Registers the stateless bridge and a model publisher factory that borrows a caller-owned Nom store.</summary>
    /// <param name="context">Authoring composition root.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherNomContent(this IAppConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services(services =>
        {
            if (services.Exist<NomModelContent>()) throw new InvalidOperationException("Aether Nom content is already registered.");
            services.Singleton<NomModelContent>(_ => new());
            if (!services.Exist<IFactory<IModelImportContentPublisher, IProjectAssetStore>>())
                services.Singleton<IFactory<IModelImportContentPublisher, IProjectAssetStore>>(resolver =>
                {
                    var bridge = resolver.Resolve<NomModelContent>();
                    return new Factory<IModelImportContentPublisher, IProjectAssetStore>(store =>
                        new NomModelImportContentPublisher(bridge, store ?? throw new ArgumentNullException(nameof(store))));
                });
        });
        return context;
    }

    /// <summary>Configures the worker and project-local Nom content for Editor import and reimport.</summary>
    /// <param name="context">Authoring composition root.</param>
    /// <param name="worker">Trusted worker deployment from the host.</param>
    /// <param name="scratchDirectory">Absolute caller-selected parent for temporary worker files.</param>
    /// <returns>The same context.</returns>
    /// <remarks>Call after Nom and Aether Nom content, and before the project model catalog and Editor.</remarks>
    public static IAppConfigurationContext UseAetherNomProjectModelImports(this IAppConfigurationContext context,
        ModelImportWorkerDeployment worker, string scratchDirectory)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentException.ThrowIfNullOrWhiteSpace(scratchDirectory);
        if (!Path.IsPathFullyQualified(scratchDirectory)) throw new ArgumentException("Specify an absolute scratch path.", nameof(scratchDirectory));
        var scratch = Path.GetFullPath(scratchDirectory);
        context.Services(services =>
        {
            if (!services.Exist<NomModelContent>() || !services.Exist<IFactory<IProjectAssetStore, OpenAssetStoreArguments>>())
                throw new InvalidOperationException("Register Nom and Aether Nom content before project model imports.");
            if (services.Exist<IFactory<SceneModelReimportSettings, SceneModelReimportRestoreArguments>>())
                throw new InvalidOperationException("Project model import settings are already registered.");
            services.Singleton<IFactory<SceneModelReimportSettings, SceneModelReimportRestoreArguments>>(resolver =>
            {
                var stores = resolver.Resolve<IFactory<IProjectAssetStore, OpenAssetStoreArguments>>();
                var content = resolver.Resolve<NomModelContent>();
                return new Factory<SceneModelReimportSettings, SceneModelReimportRestoreArguments>(request => new(
                    request.Ledger, worker, scratch, new ProjectNomModelPublisher(request.Project, stores, content)));
            });
        });
        return context;
    }
}
