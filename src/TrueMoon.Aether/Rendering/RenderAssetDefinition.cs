namespace TrueMoon.Aether.Rendering;

/// <summary>Immutable CPU asset data. It owns managed values, not native resources or service lifetimes.</summary>
public abstract class RenderAssetDefinition
{
    private protected RenderAssetDefinition(Guid kindId, int schemaVersion)
    { ContentValidation.Version(schemaVersion); KindId = kindId; SchemaVersion = schemaVersion; }
    /// <summary>Gets the exact semantic kind.</summary>
    public Guid KindId { get; }
    /// <summary>Gets the supported exact schema version.</summary>
    public int SchemaVersion { get; }
    /// <summary>Gets SHA256 of the canonical payload, computed after complete validation.</summary>
    public RenderContentHash Revision { get; private set; }
    private protected void Complete() => Revision = RenderContentHash.Compute(RenderContentCodec.Encode(this));
    /// <summary>Creates a validated project-scoped reference to this immutable content.</summary>
    /// <param name="projectId">Project identity.</param><param name="assetId">Persistent asset identity.</param><returns>Exact unmanaged key.</returns>
    public RenderAssetKey CreateKey(Guid projectId, Guid assetId)
    { var key = new RenderAssetKey(projectId, assetId, KindId, SchemaVersion, Revision); key.Validate(); return key; }
}
