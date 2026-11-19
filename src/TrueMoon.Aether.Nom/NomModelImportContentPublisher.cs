using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Nom;

namespace TrueMoon.Aether.Nom;

// The project owner retains Nom revisions and owns the borrowed store.
internal sealed class NomModelImportContentPublisher(NomModelContent content, IProjectAssetStore store) : IModelImportContentPublisher
{
    public Guid ProjectId => store.ProjectId;

    public async Task<GameBuildRenderContent> PublishAsync(PublishedModelImport model, CancellationToken cancellationToken)
    {
        var snapshot = await content.PublishAsync(store, model, store.ContentGeneration, cancellationToken).ConfigureAwait(false);
        return await content.CaptureAsync(store, snapshot.Id, cancellationToken).ConfigureAwait(false);
    }
}
