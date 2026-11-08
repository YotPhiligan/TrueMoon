namespace TrueMoon.Aether.Preview;

/// <summary>Explicit opening/view/content identity. Same-ID document reopen must use a fresh DocumentEpoch.</summary>
/// <param name="DocumentEpoch">Fresh opening/project epoch.</param><param name="ViewId">Active viewport binding identity.</param>
/// <param name="ContentId">Fresh token for the exact committed projected payload.</param><param name="SceneId">Persistent scene GUID.</param>
/// <param name="SceneRevision">Exact committed authoring revision.</param>
public sealed record PreviewViewContext(Guid DocumentEpoch, Guid ViewId, Guid ContentId, Guid SceneId, long SceneRevision)
{
    /// <summary>Rejects incomplete contexts before control or render publication.</summary>
    public void Validate()
    {
        if (DocumentEpoch == Guid.Empty || ViewId == Guid.Empty || ContentId == Guid.Empty || SceneId == Guid.Empty || SceneRevision < 0)
            throw new InvalidDataException("Invalid preview view context.");
    }
}
