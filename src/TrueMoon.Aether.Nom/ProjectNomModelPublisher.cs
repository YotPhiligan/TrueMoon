using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Nom;

namespace TrueMoon.Aether.Nom;

// The project owns its Nom files. Each publication owns and closes its store and transient capture snapshot.
internal sealed class ProjectNomModelPublisher(ResolvedProject project,
    IFactory<IProjectAssetStore, OpenAssetStoreArguments> stores, NomModelContent content) : IModelImportContentPublisher
{
    public Guid ProjectId => project.Description.Id;

    public async Task<GameBuildRenderContent> PublishAsync(PublishedModelImport model, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Path.Combine(project.ContentDirectory, ".aether-model-content");
        var mode = Directory.Exists(path) ? AssetStoreOpenMode.Open : AssetStoreOpenMode.Create;
        // Admission has already checked cancellation. Let the first creation reach its durable root before observing a UI cancel.
        var openingToken = mode == AssetStoreOpenMode.Create ? CancellationToken.None : cancellationToken;
        await using var store = stores.Create(new(path, ProjectId, ProjectId, mode, openingToken));
        var snapshot = await content.PublishAsync(store, model, store.ContentGeneration, cancellationToken).ConfigureAwait(false);
        try
        {
            return await content.CaptureAsync(store, snapshot.Id, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await store.RemoveSnapshotAsync(snapshot.Id, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
