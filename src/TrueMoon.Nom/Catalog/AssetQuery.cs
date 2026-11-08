namespace TrueMoon.Nom.Catalog;

/// <summary>Intersects optional source, kind and ordinal tag filters over logical assets.</summary>
/// <param name="Source">Optional nonempty source identifier.</param><param name="Kind">Optional nonempty kind identifier.</param>
/// <param name="Tag">Optional exact ordinal tag.</param><param name="IncludeDeleted">Includes retained identity tombstones.</param>
public sealed record AssetQuery(Guid? Source = null, Guid? Kind = null, string? Tag = null, bool IncludeDeleted = false);

/// <summary>An opaque continuation bound to this store, query and exact catalog sequence.</summary>
public sealed class AssetPageCursor
{
    internal Guid Store { get; }
    internal long Sequence { get; }
    internal AssetQuery Query { get; }
    internal Guid After { get; }
    internal AssetPageCursor(Guid store, long sequence, AssetQuery query, Guid after)
    { Store = store; Sequence = sequence; Query = query; After = after; }
}

/// <summary>A detached bounded page in ascending Guid order.</summary>
/// <param name="Items">Independent descriptors.</param><param name="Next">Continuation or null at the end.</param>
/// <param name="Generation">Content generation observed by this page.</param>
public sealed record AssetPage(AssetRecord[] Items, AssetPageCursor? Next, long Generation);
