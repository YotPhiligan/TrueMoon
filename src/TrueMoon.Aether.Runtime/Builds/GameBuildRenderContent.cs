using System.Collections.Immutable;
using TrueMoon.Aether.Abstractions.Rendering;

namespace TrueMoon.Aether.Runtime.Builds;

/// <summary>Detached canonical render resources to include in an explicit game build.</summary>
public sealed class GameBuildRenderContent
{
    /// <summary>Maximum complete encoded render payload for this build profile.</summary>
    public const int MaximumBytes = 12 * 1024 * 1024;
    private const int Magic = 0x31435254; // TRC1
    private readonly byte[] _bytes;

    /// <summary>Validates the complete render closure and captures its canonical bytes.</summary>
    /// <param name="projectId">Project that owns all asset keys.</param>
    /// <param name="entries">Exact CPU mesh, material and binding entries.</param>
    public GameBuildRenderContent(Guid projectId, IReadOnlyList<RenderAssetEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0) throw new InvalidDataException("A render payload must contain at least one asset.");
        // The real build identity is assigned after publishing the payload digest.
        var validated = new RenderContentManifest(projectId, RenderContentHash.Compute("TRC1"u8), Guid.NewGuid(), entries);
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

    internal void WriteTo(Stream output) => output.Write(_bytes);

    internal static RenderContentManifest Read(byte[] bytes, RenderContentHash buildId)
    {
        try
        {
            using var reader = new BinaryReader(new MemoryStream(bytes, writable: false));
            if (reader.ReadInt32() != Magic) throw new InvalidDataException("Unsupported game build render payload.");
            var project = ReadId(reader);
            var count = reader.ReadInt32();
            if (count is < 1 or > RenderContentProfile.MaximumManifestEntries)
                throw new InvalidDataException("Game build render entry count exceeds its budget.");
            var entries = ImmutableArray.CreateBuilder<RenderAssetEntry>(count);
            Guid? previous = null;
            for (var i = 0; i < count; i++)
            {
                var asset = ReadId(reader);
                // Guid comparison uses the same field order as the canonical N-format order.
                if (previous.HasValue && previous.Value.CompareTo(asset) >= 0)
                    throw new InvalidDataException("Game build render asset order must be canonical and unique.");
                previous = asset;
                var length = reader.ReadInt32();
                if (length is < 1 or > RenderContentProfile.MaximumMeshBytes || length > reader.BaseStream.Length - reader.BaseStream.Position)
                    throw new InvalidDataException("Invalid game build render entry length.");
                var definition = RenderContentCodec.Decode(reader.ReadBytes(length));
                entries.Add(new(new(project, asset, definition.KindId, definition.SchemaVersion, definition.Revision), definition));
            }
            if (reader.BaseStream.Position != reader.BaseStream.Length)
                throw new InvalidDataException("Unexpected trailing game build render bytes.");
            return new RenderContentManifest(project, buildId, Guid.NewGuid(), entries.MoveToImmutable());
        }
        catch (EndOfStreamException error)
        {
            throw new InvalidDataException("Truncated game build render payload.", error);
        }
    }

    private static Guid ReadId(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(16);
        if (bytes.Length != 16) throw new EndOfStreamException();
        return new Guid(bytes);
    }
}
