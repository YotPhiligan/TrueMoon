using TrueMoon.Aether.Runtime.Builds;

namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Publishes the resources of one committed import and captures its exact render closure.</summary>
public interface IModelImportContentPublisher
{
    /// <summary>Project that will own the published resources.</summary>
    Guid ProjectId { get; }

    /// <summary>Publishes and freezes the supplied generation for the configured project.</summary>
    /// <param name="model">Exact published ledger generation.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>Detached build resources for this model.</returns>
    Task<GameBuildRenderContent> PublishAsync(PublishedModelImport model, CancellationToken cancellationToken = default);
}
