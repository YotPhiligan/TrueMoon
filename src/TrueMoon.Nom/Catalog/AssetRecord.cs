using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Retention;

namespace TrueMoon.Nom.Catalog;

/// <summary>Detached logical asset identity and bounded opaque metadata, independent of its resource bytes.</summary>
public sealed class AssetRecord
{
    private readonly byte[] metadata;
    private readonly string[] tags;
    private readonly AssetResourceVersion[] dependencies;
    /// <summary>Gets the stable logical asset identifier.</summary>
    public Guid Id { get; }
    /// <summary>Gets the source identifier.</summary>
    public Guid Source { get; }
    /// <summary>Gets the consumer-defined resource kind.</summary>
    public Guid Kind { get; }
    /// <summary>Gets the ordinal semantic locator within the source and kind.</summary>
    public string Locator { get; }
    /// <summary>Gets a defensive copy of the opaque metadata bytes.</summary>
    public byte[] Metadata => metadata.ToArray();
    /// <summary>Gets a detached array of canonical ordinal tags.</summary>
    public string[] Tags => tags.ToArray();
    /// <summary>Gets detached exact dependency versions, sorted by revision.</summary>
    public AssetResourceVersion[] Dependencies => dependencies.ToArray();
    /// <summary>Creates a descriptor without tags or dependencies, preserving the original constructor contract.</summary>
    /// <param name="id">Nonempty logical identifier.</param><param name="source">Nonempty source identifier.</param>
    /// <param name="kind">Nonempty kind identifier.</param><param name="locator">Bounded ordinal locator.</param><param name="metadata">Bounded copied metadata.</param>
    public AssetRecord(Guid id, Guid source, Guid kind, string locator, byte[] metadata)
        : this(id,source,kind,locator,metadata,null,null) { }
    /// <summary>Creates an immutable descriptor, copying and validating its metadata.</summary>
    /// <param name="id">Nonempty logical identifier.</param><param name="source">Nonempty source identifier.</param>
    /// <param name="kind">Nonempty kind identifier.</param><param name="locator">Nonblank locator, at most1024UTF-8bytes and no control characters.</param>
    /// <param name="metadata">At most64KiB of consumer-defined metadata.</param>
    /// <param name="tags">At most32 unique ordinal tags, each at most128UTF-8bytes.</param>
    /// <param name="dependencies">At most32 unique exact dependency revisions.</param>
    public AssetRecord(Guid id, Guid source, Guid kind, string locator, byte[] metadata,
        IReadOnlyList<string>? tags = null, IReadOnlyList<AssetResourceVersion>? dependencies = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (id == Guid.Empty || source == Guid.Empty || kind == Guid.Empty || string.IsNullOrWhiteSpace(locator))
            throw new InvalidDataException("Empty identity.");
        if (metadata.Length > CatalogCodec.MaximumMetadata || CatalogCodec.Utf8.GetByteCount(locator) > 1024 || locator.Any(char.IsControl))
            throw new InvalidDataException("Asset field budget.");
        Id = id; Source = source; Kind = kind; Locator = locator; this.metadata = metadata.ToArray();
        this.tags = NomRetentionCodec.CaptureTags(tags);
        this.dependencies = NomRetentionCodec.CaptureVersions(dependencies, 32);
    }
}
