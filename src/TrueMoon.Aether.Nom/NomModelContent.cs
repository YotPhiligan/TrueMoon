using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Nom;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Retention;

namespace TrueMoon.Aether.Nom;

/// <summary>Publishes canonical model render resources into Nom and captures exact build inputs.</summary>
public sealed class NomModelContent
{
    private const int MaximumRoots = 256;
    private const int MaximumSnapshots = 32;
    internal NomModelContent() { }

    /// <summary>Publishes a single model's render resources as an atomic Nom batch and freezes the acknowledged exact revisions.</summary>
    /// <remarks>The resource commit precedes snapshot creation. If freezing fails, the Nom heads remain committed.
    /// The caller owns snapshot retention and may retry using the current Nom generation.</remarks>
    /// <param name="store">Borrowed project store.</param><param name="model">Published model with persistent resource IDs.</param>
    /// <param name="expectedGeneration">Required Nom content generation.</param><param name="token">Cooperative cancellation.</param>
    /// <returns>A retained Nom snapshot of the complete render closure.</returns>
    public async Task<AssetSnapshotManifest> PublishAsync(IProjectAssetStore store, PublishedModelImport model,
        long expectedGeneration, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(model);
        token.ThrowIfCancellationRequested();
        var project = store.ProjectId;
        var materials = model.Materials.ToDictionary(material => material.AssetId,
            material => Entry(project, material.AssetId, material.Definition));
        var entries = materials.Values.ToList();
        foreach (var mesh in model.Meshes)
        {
            var geometry = Entry(project, mesh.AssetId, mesh.Geometry);
            var binding = new MaterialBindingSetDefinition(geometry.Key, [new(mesh.SlotId, materials[mesh.MaterialId].Key)]);
            entries.Add(geometry);
            entries.Add(Entry(project, mesh.BindingSetId, binding));
        }
        if (entries.Count is < 1 or > MaximumRoots) throw new InvalidDataException("Model render snapshot root budget exceeded.");

        var locators = model.Bindings.ToDictionary(binding => binding.Id, binding => binding.Key.Locator);
        var specs = new List<AssetResourceWrite>(entries.Count);
        var streams = new Dictionary<Guid, Stream>(entries.Count);
        try
        {
            long total = 24;
            foreach (var entry in entries)
            {
                token.ThrowIfCancellationRequested();
                var bytes = RenderContentCodec.Encode(entry.Definition);
                total += bytes.Length + 20;
                if (total > GameBuildRenderContent.MaximumBytes) throw new InvalidDataException("Model render payload exceeds its budget.");
                var key = entry.Key;
                var asset = new AssetRecord(key.AssetId, model.Ledger.SourceId, key.KindId, locators[key.AssetId], []);
                specs.Add(new(asset, bytes.Length, key.Revision.ToString(), bytes.Length));
                streams.Add(key.AssetId, new MemoryStream(bytes, writable: false));
            }

            var ticket = await store.ReserveResourcesAsync(expectedGeneration, specs, GameBuildRenderContent.MaximumBytes, token).ConfigureAwait(false);
            var receipt = await store.PublishResourcesAsync(ticket, streams, token).ConfigureAwait(false);
            return await store.CreateSnapshotAsync(receipt.Resources, MaximumRoots, token).ConfigureAwait(false);
        }
        finally
        {
            foreach (var stream in streams.Values) stream.Dispose();
        }
    }

    /// <summary>Reads and validates the exact members of a retained Nom snapshot into detached build input.</summary>
    /// <param name="store">Borrowed project store that owns the snapshot.</param><param name="snapshotId">Exact Nom snapshot ID.</param>
    /// <param name="token">Cooperative cancellation.</param><returns>Build input independent of the store and its leases.</returns>
    public Task<GameBuildRenderContent> CaptureAsync(IProjectAssetStore store, string snapshotId,
        CancellationToken token = default) => CaptureSnapshotsAsync(store, [snapshotId], token);

    /// <summary>Combines exact retained snapshots from one project store into a single detached build input.</summary>
    /// <remarks>Accepts up to 32 snapshot IDs. Repeated identical assets are included once.
    /// Conflicting versions of the same asset fail capture. Order does not affect the build payload.
    /// The caller keeps the selected snapshots retained until capture completes.</remarks>
    /// <param name="store">Borrowed project store that owns all the snapshots.</param>
    /// <param name="snapshotIds">Exact Nom snapshot IDs to include.</param>
    /// <param name="token">Cooperative cancellation.</param>
    /// <returns>Validated build input independent of the store and its leases.</returns>
    public async Task<GameBuildRenderContent> CaptureSnapshotsAsync(IProjectAssetStore store, IReadOnlyList<string> snapshotIds,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(snapshotIds);
        var count = snapshotIds.Count;
        if (count is < 1 or > MaximumSnapshots) throw new ArgumentOutOfRangeException(nameof(snapshotIds), "Select 1..32 model render snapshots.");
        token.ThrowIfCancellationRequested();
        var selected = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++) selected.Add(snapshotIds[i]);

        var project = store.ProjectId;
        var entries = new List<RenderAssetEntry>();
        var seen = new Dictionary<Guid, (Guid Kind, RenderContentHash Digest, long Length)>();
        long total = 24;
        foreach (var snapshotId in selected)
        {
            token.ThrowIfCancellationRequested();
            await using var snapshot = await store.OpenSnapshotAsync(snapshotId, token).ConfigureAwait(false);
            var members = snapshot.Manifest.Members;
            if (members.Length is < 1 or > MaximumRoots) throw new InvalidDataException("Model render snapshot member budget exceeded.");
            foreach (var member in members)
            {
                token.ThrowIfCancellationRequested();
                var maximum = member.KindId == RenderContentProfile.MeshKindId
                    ? RenderContentProfile.MaximumMeshBytes : RenderContentProfile.MaximumSmallAssetBytes;
                if (member.Length is <= 0 || member.Length > maximum)
                    throw new InvalidDataException("Invalid render snapshot member length.");
                var digest = RenderContentHash.Parse(member.Digest);
                var identity = (member.KindId, digest, member.Length);
                if (seen.TryGetValue(member.AssetId, out var previous))
                {
                    if (previous != identity)
                        throw new InvalidDataException($"Conflicting render revisions for asset {member.AssetId:D} in the selected Nom snapshots.");
                    continue;
                }
                if (entries.Count == RenderContentProfile.MaximumManifestEntries)
                    throw new InvalidDataException("Model render payload exceeds its entry budget.");
                total += member.Length + 20;
                if (total > GameBuildRenderContent.MaximumBytes) throw new InvalidDataException("Model render payload exceeds its budget.");
                await using var input = await snapshot.OpenResourceAsync(member, token).ConfigureAwait(false);
                var bytes = new byte[(int)member.Length];
                await input.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
                var definition = RenderContentCodec.Decode(bytes);
                if (definition.KindId != member.KindId || definition.Revision != digest)
                    throw new InvalidDataException("Nom member does not match the canonical render resource.");
                entries.Add(Entry(project, member.AssetId, definition));
                seen.Add(member.AssetId, identity);
            }
        }
        token.ThrowIfCancellationRequested();
        return new GameBuildRenderContent(project, entries);
    }

    private static RenderAssetEntry Entry(Guid project, Guid id, RenderAssetDefinition definition) =>
        new(new(project, id, definition.KindId, definition.SchemaVersion, definition.Revision), definition);
}
