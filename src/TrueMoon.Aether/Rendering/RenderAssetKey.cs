namespace TrueMoon.Aether.Rendering;

/// <summary>Exact project-scoped logical resource reference, without managed/native ownership.</summary>
/// <param name="ProjectId">Nonempty project identity.</param><param name="AssetId">Persistent asset identity.</param>
/// <param name="KindId">Supported asset kind.</param><param name="SchemaVersion">Exact version one.</param>
/// <param name="Revision">Canonical content SHA256.</param>
public readonly record struct RenderAssetKey(Guid ProjectId, Guid AssetId, Guid KindId, int SchemaVersion, RenderContentHash Revision)
{
    /// <summary>Rejects default, unknown kind/version and missing digest.</summary>
    public void Validate()
    {
        ContentValidation.Require(ProjectId != Guid.Empty && AssetId != Guid.Empty && !Revision.IsEmpty,
            RenderContentError.InvalidIdentity, "Asset keys require project, asset and content revision.");
        ContentValidation.Require(RenderContentProfile.IsKind(KindId), RenderContentError.AssetKindMismatch, "Unsupported asset kind.");
        ContentValidation.Version(SchemaVersion);
    }
    /// <summary>Validates an exact semantic kind.</summary><param name="expectedKind">Required asset kind.</param>
    public void Validate(Guid expectedKind)
    {
        Validate();
        ContentValidation.Require(KindId == expectedKind, RenderContentError.AssetKindMismatch, "The asset has the wrong semantic kind.");
    }
}
