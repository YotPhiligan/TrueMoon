using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.Retention;

/// <summary>A durable immutable manifest of an exact dependency closure.</summary>
public sealed class AssetSnapshotManifest
{
    private readonly AssetResourceVersion[] members;
    /// <summary>Gets the canonical store-scoped manifest digest.</summary>
    public string Id { get; }
    /// <summary>Gets the content generation frozen at creation.</summary>
    public long Generation { get; }
    /// <summary>Gets a detached array of exact members sorted by revision.</summary>
    public AssetResourceVersion[] Members => members.ToArray();
    internal AssetSnapshotManifest(string id, long generation, AssetResourceVersion[] members)
    { Id = id; Generation = generation; this.members = members.ToArray(); }
}
