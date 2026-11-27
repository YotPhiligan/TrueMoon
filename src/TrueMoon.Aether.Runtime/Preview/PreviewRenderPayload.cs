using System.Collections.Immutable;
using System.Numerics;
using System.Security.Cryptography;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Runtime.Preview;

internal readonly record struct PreviewRenderRow(EntityId Entity, Matrix4x4 World, Guid MeshId, Guid BindingId,
    bool Enabled, uint Layers, bool TintEnabled, Vector3 Color)
{
    internal bool IsMesh => MeshId != Guid.Empty;
}

internal sealed class PreviewRenderPayload
{
    internal const int Version = 1, MaximumRows = 64, ChunkBytes = 32 * 1024;
    internal const int MaximumBytes = GameBuildRenderContent.MaximumBytes + 16 * 1024;
    internal const int HeaderBytes = 36; // magic, scene, revision, content length, row count
    private const int ContentOffset = HeaderBytes - sizeof(int);
    private const int Magic = 0x31525054; // TPR1
    private const int PrimitiveBytes = 1 + 16 + 64 + 12, MeshBytes = PrimitiveBytes + 32 + 2 + 4;
    private static readonly Lazy<RenderContentHash> PreviewBuild = new(() => RenderContentHash.Parse(BuildIdentity.PreviewId));
    private readonly byte[] _bytes;
    private readonly RenderContentManifest? _decodedManifest;

    internal SceneId SceneId { get; }
    internal long Revision { get; }
    internal GameBuildRenderContent Content { get; }
    internal ImmutableArray<PreviewRenderRow> Rows { get; }
    internal RenderContentHash Digest { get; }
    internal int Length => _decodedManifest != null ? _bytes.Length : _bytes.Length + Content.EncodedLength;

    internal byte[] CopyChunk(int offset, int length)
    {
        if (_decodedManifest != null) return _bytes.AsSpan(offset, length).ToArray();
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (length > Length - offset) throw new ArgumentOutOfRangeException(nameof(length));
        var result = GC.AllocateUninitializedArray<byte>(length);
        Span<byte> remaining = result;
        CopySegment(_bytes.AsSpan(0, ContentOffset), ref offset, ref remaining);
        CopySegment(Content.EncodedBytes, ref offset, ref remaining);
        CopySegment(_bytes.AsSpan(ContentOffset), ref offset, ref remaining);
        return result;
    }

    private static void CopySegment(scoped ReadOnlySpan<byte> source, ref int skip, ref Span<byte> destination)
    {
        if (skip >= source.Length) { skip -= source.Length; return; }
        var count = Math.Min(source.Length - skip, destination.Length);
        source.Slice(skip, count).CopyTo(destination);
        destination = destination[count..]; skip = 0;
    }

    internal PreviewRenderPayload(SceneId sceneId, long revision, GameBuildRenderContent content, ImmutableArray<PreviewRenderRow> rows)
    {
        ArgumentNullException.ThrowIfNull(content);
        SceneId = sceneId; Revision = revision; Content = content; Rows = rows;
        Validate();
        // Keep the canonical TRC1 in its existing immutable allocation. TPR1 inserts it between the
        // fixed header and row tail, so only RPC chunks need to materialize bytes across the join.
        var length = HeaderBytes;
        foreach (var row in Rows) length += row.IsMesh ? MeshBytes : PrimitiveBytes;
        _bytes = GC.AllocateUninitializedArray<byte>(length);
        using var stream = new MemoryStream(_bytes, writable: true);
        using var writer = new BinaryWriter(stream);
        writer.Write(Magic); WriteId(writer, sceneId.Value); writer.Write(revision);
        writer.Write(Content.EncodedLength); writer.Write(Rows.Length);
        foreach (var row in Rows)
        {
            writer.Write(row.IsMesh ? (byte)1 : (byte)2);
            WriteId(writer, row.Entity.Value); WriteMatrix(writer, row.World);
            if (row.IsMesh)
            {
                WriteId(writer, row.MeshId); WriteId(writer, row.BindingId);
                writer.Write(row.Enabled); writer.Write(row.Layers); writer.Write(row.TintEnabled);
            }
            writer.Write(row.Color.X); writer.Write(row.Color.Y); writer.Write(row.Color.Z);
        }
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(_bytes.AsSpan(0, ContentOffset));
        hash.AppendData(Content.EncodedBytes);
        hash.AppendData(_bytes.AsSpan(ContentOffset));
        Span<byte> digest = stackalloc byte[32];
        hash.GetHashAndReset(digest); Digest = RenderContentHash.FromBytes(digest);
    }

    private PreviewRenderPayload(SceneId sceneId, long revision, GameBuildRenderContent content, ImmutableArray<PreviewRenderRow> rows,
        byte[] bytes, RenderContentHash digest, RenderContentManifest manifest)
    {
        SceneId = sceneId; Revision = revision; Content = content; Rows = rows;
        _bytes = bytes; Digest = digest; _decodedManifest = manifest;
        Validate();
    }

    internal RenderContentManifest CreateManifest() => _decodedManifest ?? new(Content.Entries[0].Key.ProjectId,
        PreviewBuild.Value, Guid.NewGuid(), Content.Entries);

    // Called only after the staging owner checked length, digest and transfer identity and relinquished the array.
    internal static PreviewRenderPayload DecodeOwned(byte[] bytes, RenderContentHash digest, CancellationToken token)
    {
        if (bytes.Length is < HeaderBytes or > MaximumBytes) throw new InvalidDataException("Invalid TPR1 payload size.");
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream);
        try
        {
            if (reader.ReadInt32() != Magic) throw new InvalidDataException("Unsupported preview render payload.");
            var scene = ReadId(reader); var revision = reader.ReadInt64(); var contentLength = reader.ReadInt32();
            if (scene == Guid.Empty || revision < 0 || contentLength is < 24 or > GameBuildRenderContent.MaximumBytes || contentLength > bytes.Length - HeaderBytes)
                throw new InvalidDataException("Invalid preview render scene stamp or content length.");
            var content = bytes.AsMemory((int)stream.Position, contentLength);
            stream.Position += contentLength;
            var count = reader.ReadInt32();
            if (count is < 1 or > MaximumRows) throw new InvalidDataException("Preview render row admission exceeded.");
            var rows = ImmutableArray.CreateBuilder<PreviewRenderRow>(count);
            for (var i = 0; i < count; i++)
            {
                var kind = reader.ReadByte(); var id = ReadId(reader);
                if (id == Guid.Empty) throw new InvalidDataException("Preview render entities require nonempty IDs.");
                var entity = new EntityId(id); var world = ReadMatrix(reader);
                if (kind == 1)
                {
                    var mesh = ReadId(reader); var bindings = ReadId(reader);
                    if (mesh == Guid.Empty || bindings == Guid.Empty) throw new InvalidDataException("Mesh rows require exact asset IDs.");
                    var enabled = ReadBool(reader); var layers = reader.ReadUInt32(); var tint = ReadBool(reader);
                    rows.Add(new(entity, world, mesh, bindings, enabled, layers, tint, ReadColor(reader)));
                }
                else if (kind == 2) rows.Add(new(entity, world, default, default, false, 0, false, ReadColor(reader)));
                else throw new InvalidDataException("Unknown preview render row kind.");
            }
            if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected trailing preview render bytes.");
            token.ThrowIfCancellationRequested();
            var decoded = GameBuildRenderContent.ReadOwned(content, PreviewBuild.Value, out var manifest);
            token.ThrowIfCancellationRequested();
            return new(new(scene), revision, decoded, rows.MoveToImmutable(), bytes, digest, manifest);
        }
        catch (EndOfStreamException error) { throw new InvalidDataException("Truncated preview render payload.", error); }
    }

    internal void ValidateView(PreviewViewState view)
    {
        view.Camera.CreateProjection(16, 16);
        if (view.Context.SceneId != SceneId.Value || view.Context.SceneRevision != Revision)
            throw new InvalidDataException("Preview view does not describe this render payload.");
    }

    private void Validate()
    {
        if (SceneId.Value == Guid.Empty || Revision < 0 || Rows.IsDefaultOrEmpty || Rows.Length > MaximumRows ||
            Content.EncodedLength is < 24 or > GameBuildRenderContent.MaximumBytes)
            throw new InvalidDataException("Invalid preview render identity, row count or content length.");
        var byId = new Dictionary<Guid, RenderAssetEntry>(Content.Entries.Length);
        foreach (var entry in Content.Entries) byId.Add(entry.Key.AssetId, entry);
        var entities = new HashSet<EntityId>(Rows.Length);
        var reachable = new HashSet<Guid>();
        foreach (var row in Rows)
        {
            if (row.Entity.Value == Guid.Empty || !entities.Add(row.Entity))
                throw new InvalidDataException("Preview render entity IDs must be nonempty and distinct.");
            // The common render interval and affine domain is identical for stored sRGB and linear tint.
            new RenderPrimitive(row.World, row.Color).Validate();
            if (!row.IsMesh)
            {
                if (row.BindingId != Guid.Empty || row.Enabled || row.Layers != 0 || row.TintEnabled)
                    throw new InvalidDataException("A primitive row cannot carry mesh properties.");
                continue;
            }
            if (!byId.TryGetValue(row.MeshId, out var mesh) || mesh.Definition is not MeshDefinition ||
                !byId.TryGetValue(row.BindingId, out var binding) || binding.Definition is not MaterialBindingSetDefinition set || set.Mesh != mesh.Key)
                throw new InvalidDataException("Preview render row does not reference an exact mesh and its binding set.");
            reachable.Add(row.MeshId);
            if (!reachable.Add(row.BindingId)) continue;
            foreach (var slot in set.Bindings)
            {
                if (!reachable.Add(slot.Material.AssetId)) continue;
                if (byId[slot.Material.AssetId].Definition is OpaqueUnlitMaterialDefinition { BaseColorTexture: { } texture })
                    reachable.Add(texture.AssetId);
            }
        }
        if (reachable.Count != byId.Count) throw new InvalidDataException("Preview render content includes unreachable assets.");
    }

    private static void WriteId(BinaryWriter writer, Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes); writer.Write(bytes);
    }

    private static Guid ReadId(BinaryReader reader)
    {
        Span<byte> bytes = stackalloc byte[16];
        reader.BaseStream.ReadExactly(bytes); return new(bytes);
    }

    private static bool ReadBool(BinaryReader reader) => reader.ReadByte() switch
    {
        0 => false, 1 => true, _ => throw new InvalidDataException("Preview booleans must be zero or one.")
    };
    private static Vector3 ReadColor(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    private static Matrix4x4 ReadMatrix(BinaryReader reader) => new(
        reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
        reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
        reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
        reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    private static void WriteMatrix(BinaryWriter writer, Matrix4x4 world)
    {
        ReadOnlySpan<float> values = [world.M11, world.M12, world.M13, world.M14, world.M21, world.M22, world.M23, world.M24,
            world.M31, world.M32, world.M33, world.M34, world.M41, world.M42, world.M43, world.M44];
        foreach (var value in values) writer.Write(value);
    }
}
