using System.Collections.Immutable;

namespace TrueMoon.Aether.Rendering;

/// <summary>An exact logical key paired with detached immutable CPU content.</summary>
/// <param name="Key">Pinned identity and revision.</param><param name="Definition">Validated content agreeing with the key.</param>
public sealed record RenderAssetEntry(RenderAssetKey Key, RenderAssetDefinition Definition);

/// <summary>Frozen CPU import scope. It has no native resources, async preparation, cache or lease lifetime.</summary>
public sealed class RenderContentManifest
{
    private readonly ImmutableDictionary<Guid, RenderAssetEntry> _byId;
    /// <summary>Snapshots a bounded exact closure, validates all references and computes a deterministic fingerprint.</summary>
    /// <param name="projectId">Nonempty project scope.</param><param name="buildId">Pinned build SHA256.</param>
    /// <param name="generation">Nonempty process-local catalog identity; excluded from the content fingerprint.</param>
    /// <param name="entries">Distinct asset IDs, including every mesh/material dependency.</param>
    public RenderContentManifest(Guid projectId, RenderContentHash buildId, Guid generation, IReadOnlyList<RenderAssetEntry> entries)
    {
        ContentValidation.Require(projectId != Guid.Empty && generation != Guid.Empty && !buildId.IsEmpty, RenderContentError.InvalidIdentity, "Manifest identities must be nonempty.");
        ArgumentNullException.ThrowIfNull(entries);
        ContentValidation.Require(entries.Count <= RenderContentProfile.MaximumManifestEntries, RenderContentError.LimitExceeded, "Manifest entry admission exceeded.");
        ProjectId = projectId; BuildId = buildId; Generation = generation;
        var map = ImmutableDictionary.CreateBuilder<Guid, RenderAssetEntry>();
        var snapshot = ContentValidation.Copy(entries, RenderContentProfile.MaximumManifestEntries, nameof(entries));
        foreach (var entry in snapshot)
        {
            ArgumentNullException.ThrowIfNull(entry); ArgumentNullException.ThrowIfNull(entry.Definition);
            entry.Key.Validate();
            ContentValidation.Require(entry.Key.ProjectId == projectId, RenderContentError.InvalidIdentity, "Manifest assets cannot cross project scopes.");
            ContentValidation.Require(entry.Key.KindId == entry.Definition.KindId, RenderContentError.AssetKindMismatch, "Asset key and definition kinds disagree.");
            ContentValidation.Require(entry.Key.SchemaVersion == entry.Definition.SchemaVersion && entry.Key.Revision == entry.Definition.Revision,
                RenderContentError.RevisionMismatch, "Asset key and canonical content disagree.");
            ContentValidation.Require(map.TryAdd(entry.Key.AssetId, entry), RenderContentError.DuplicateAsset, "A manifest repeats a logical asset ID.");
        }
        _byId = map.ToImmutable();
        Entries = snapshot.OrderBy(static entry => entry.Key.AssetId.ToString("N"), StringComparer.Ordinal).ToImmutableArray();
        foreach (var entry in Entries)
        {
            if (entry.Definition is not MaterialBindingSetDefinition bindings) continue;
            var mesh = Resolve(bindings.Mesh);
            bindings.ValidateAgainst(mesh.Key, (MeshDefinition)mesh.Definition);
            foreach (var binding in bindings.Bindings) Resolve(binding.Material);
        }
        Digest = RenderContentHash.Compute(RenderContentCodec.EncodeManifest(this));
    }
    /// <summary>Gets the frozen project identity.</summary>
    public Guid ProjectId { get; }
    /// <summary>Gets the pinned build identity.</summary>
    public RenderContentHash BuildId { get; }
    /// <summary>Gets the distinct catalog generation.</summary>
    public Guid Generation { get; }
    /// <summary>Gets detached entries sorted by RFC4122 asset-ID bytes.</summary>
    public ImmutableArray<RenderAssetEntry> Entries { get; }
    /// <summary>Gets the canonical project/build/key fingerprint, excluding generation.</summary>
    public RenderContentHash Digest { get; }
    /// <summary>Resolves an authoring ID to exactly one expected kind.</summary>
    /// <param name="assetId">Nonempty persistent ID.</param><param name="expectedKind">Required semantic kind.</param><returns>Frozen entry.</returns>
    public RenderAssetEntry Resolve(Guid assetId, Guid expectedKind)
    {
        ContentValidation.Require(assetId != Guid.Empty, RenderContentError.InvalidIdentity, "An asset ID is required.");
        ContentValidation.Require(RenderContentProfile.IsKind(expectedKind), RenderContentError.AssetKindMismatch, "Unknown expected asset kind.");
        ContentValidation.Require(_byId.TryGetValue(assetId, out var entry), RenderContentError.MissingAsset, "Required asset is absent from the manifest.");
        ContentValidation.Require(entry!.Key.KindId == expectedKind, RenderContentError.AssetKindMismatch, "Required asset kind does not match.");
        return entry;
    }
    /// <summary>Resolves an exact same-project key without selecting the latest revision.</summary>
    /// <param name="key">Required exact key.</param><returns>Frozen entry.</returns>
    public RenderAssetEntry Resolve(RenderAssetKey key)
    {
        key.Validate();
        ContentValidation.Require(key.ProjectId == ProjectId, RenderContentError.InvalidIdentity, "Foreign project asset key.");
        var entry = Resolve(key.AssetId, key.KindId);
        ContentValidation.Require(entry.Key == key, RenderContentError.RevisionMismatch, "Asset revision is not pinned by this manifest.");
        return entry;
    }
    /// <summary>Data-only validator suitable for an explicit registry AssetValidator.</summary>
    /// <param name="assetId">Any required asset ID in this scope.</param><param name="cancellationToken">Cooperative validation cancellation.</param>
    public void ValidateAsset(Guid assetId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ContentValidation.Require(assetId != Guid.Empty, RenderContentError.InvalidIdentity, "An asset ID is required.");
        ContentValidation.Require(_byId.ContainsKey(assetId), RenderContentError.MissingAsset, "Required asset is absent from the manifest.");
    }
}
