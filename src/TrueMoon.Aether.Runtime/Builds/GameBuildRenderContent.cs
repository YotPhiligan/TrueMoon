using System.Buffers.Binary;
using System.Collections.Immutable;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Runtime.Builds;

/// <summary>Detached canonical render resources to include in an explicit game build.</summary>
public sealed class GameBuildRenderContent
{
    /// <summary>Maximum complete encoded render payload for this build profile.</summary>
    public const int MaximumBytes = 12 * 1024 * 1024;
    private const int Magic = 0x31435254; // TRC1
    private readonly ReadOnlyMemory<byte> _bytes;
    private readonly ImmutableArray<RenderAssetEntry> _entries;

    /// <summary>Exact canonical resource identities in this frozen payload.</summary>
    public ImmutableArray<RenderAssetKey> AssetKeys { get; }
    internal ImmutableArray<RenderAssetEntry> Entries => _entries;
    internal int EncodedLength => _bytes.Length;
    internal ReadOnlySpan<byte> EncodedBytes => _bytes.Span;

    private GameBuildRenderContent(ReadOnlyMemory<byte> bytes, ImmutableArray<RenderAssetEntry> entries)
    {
        _bytes = bytes;
        _entries = entries;
        AssetKeys = entries.Select(static entry => entry.Key).ToImmutableArray();
    }

    internal static GameBuildRenderContent ReadOwned(ReadOnlyMemory<byte> bytes, RenderContentHash buildId, out RenderContentManifest manifest)
    {
        manifest = Read(bytes.Span, buildId);
        return new(bytes, manifest.Entries);
    }

    /// <summary>Validates the complete render closure and captures its canonical bytes.</summary>
    /// <param name="projectId">Project that owns all asset keys.</param>
    /// <param name="entries">Exact CPU mesh, material and binding entries.</param>
    public GameBuildRenderContent(Guid projectId, IReadOnlyList<RenderAssetEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0) throw new InvalidDataException("A render payload must contain at least one asset.");
        // The real build identity is assigned after publishing the payload digest.
        var validated = new RenderContentManifest(projectId, RenderContentHash.Compute("TRC1"u8), Guid.NewGuid(), entries);
        _entries = validated.Entries;
        AssetKeys = _entries.Select(entry => entry.Key).ToImmutableArray();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Magic);
        writer.Write(projectId.ToByteArray());
        writer.Write(validated.Entries.Length);
        foreach (var entry in validated.Entries)
        {
            var bytes = RenderContentCodec.Encode(entry.Definition);
            if (stream.Length + 20 + bytes.Length > MaximumBytes) throw new InvalidDataException("Game build render payload exceeds its budget.");
            writer.Write(entry.Key.AssetId.ToByteArray());
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
        _bytes = stream.ToArray();
    }

    /// <summary>Creates a new frozen payload by replacing exact resource revisions and retaining the other entries.</summary>
    /// <param name="previous">Exact keys to replace.</param>
    /// <param name="replacement">Replacement render closure for the same project.</param>
    /// <returns>Validated canonical payload with the replacement and all unaffected resources.</returns>
    public GameBuildRenderContent WithReplacedAssets(IEnumerable<RenderAssetKey> previous, GameBuildRenderContent replacement)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(replacement);
        var removed = previous.ToHashSet();
        if (removed.Count == 0 || !removed.IsSubsetOf(AssetKeys))
            throw new InvalidDataException("The replaced resource revisions must belong to the frozen payload.");
        return new(_entries[0].Key.ProjectId, [.. _entries.Where(entry => !removed.Contains(entry.Key)), .. replacement._entries]);
    }

    internal void WriteTo(Stream output) => output.Write(_bytes.Span);

    internal static RenderContentManifest Read(byte[] bytes, RenderContentHash buildId) => Read(bytes.AsSpan(), buildId);

    private static RenderContentManifest Read(ReadOnlySpan<byte> bytes, RenderContentHash buildId)
    {
        if (bytes.Length is < 24 or > MaximumBytes || BinaryPrimitives.ReadInt32LittleEndian(bytes) != Magic)
            throw new InvalidDataException("Unsupported game build render payload.");
        var project = new Guid(bytes.Slice(4, 16));
        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes[20..]);
        bytes = bytes[24..];
        if (count is < 1 or > RenderContentProfile.MaximumManifestEntries)
            throw new InvalidDataException("Game build render entry count exceeds its budget.");
        var entries = ImmutableArray.CreateBuilder<RenderAssetEntry>(count);
        Guid? previous = null;
        for (var i = 0; i < count; i++)
        {
            if (bytes.Length < 20) throw new InvalidDataException("Truncated game build render payload.");
            var asset = new Guid(bytes[..16]);
            // Guid comparison uses the same field order as the canonical N-format order.
            if (previous.HasValue && previous.Value.CompareTo(asset) >= 0)
                throw new InvalidDataException("Game build render asset order must be canonical and unique.");
            previous = asset;
            var length = BinaryPrimitives.ReadInt32LittleEndian(bytes[16..]);
            bytes = bytes[20..];
            if (length is < 1 or > RenderContentProfile.MaximumMeshBytes || length > bytes.Length)
                throw new InvalidDataException("Invalid game build render entry length.");
            var definition = RenderContentCodec.Decode(bytes[..length]);
            bytes = bytes[length..];
            entries.Add(new(new(project, asset, definition.KindId, definition.SchemaVersion, definition.Revision), definition));
        }
        if (!bytes.IsEmpty) throw new InvalidDataException("Unexpected trailing game build render bytes.");
        return new(project, buildId, Guid.NewGuid(), entries.MoveToImmutable());
    }
}
