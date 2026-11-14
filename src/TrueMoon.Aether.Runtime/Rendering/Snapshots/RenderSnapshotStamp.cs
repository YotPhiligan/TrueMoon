using System.Buffers.Binary;

namespace TrueMoon.Aether.Runtime.Rendering.Snapshots;

/// <summary>Bounded mesh-view/1 presentation provenance. Contains values, never resources or native handles.</summary>
public sealed record RenderSnapshotStamp(Guid ViewId, Guid WorldId, Guid SceneId, long SourceRevision, ulong Tick,
    ulong Sequence, ulong BindingRevision, ulong CameraRevision, ulong SurfaceGeneration, string ManifestHash)
{
    /// <summary>Exact encoded byte count, independent of control/scene/frame payload budgets.</summary>
    public const int EncodedBytes = 132;
    /// <summary>Rejects incomplete identity/revisions.</summary>
    public void Validate()
    {
        if (ViewId == Guid.Empty || WorldId == Guid.Empty || SceneId == Guid.Empty || SourceRevision < 0 || Sequence == 0 ||
            BindingRevision == 0 || CameraRevision == 0 || SurfaceGeneration == 0 || ManifestHash is not { Length: 64 } ||
            !ManifestHash.All(static c => c is >= '0' and <= '9' or >= 'A' and <= 'F'))
            throw new InvalidDataException("Invalid mesh-view/1 stamp.");
    }
    /// <summary>Copies the exact provenance of a detached snapshot.</summary>
    public static RenderSnapshotStamp FromSnapshot(RenderSnapshotData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var stamp = new RenderSnapshotStamp(data.ViewId, data.Identity.WorldId, data.SceneId.Value, data.SourceRevision, data.Tick,
            data.Sequence, data.BindingRevision, data.Configuration.CameraRevision, data.Configuration.SurfaceGeneration, Convert.ToHexString(data.ManifestDigest.ToArray()));
        stamp.Validate(); return stamp;
    }
    /// <summary>Encodes the fixed little-endian profile; no variable strings or mesh bytes are transported.</summary>
    public byte[] Encode()
    {
        Validate(); var bytes = new byte[EncodedBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 1);
        ViewId.TryWriteBytes(bytes.AsSpan(4)); WorldId.TryWriteBytes(bytes.AsSpan(20)); SceneId.TryWriteBytes(bytes.AsSpan(36));
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(52), SourceRevision);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(60), Tick); BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(68), Sequence);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(76), BindingRevision); BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(84), CameraRevision);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(92), SurfaceGeneration); Convert.FromHexString(ManifestHash).CopyTo(bytes, 100);
        return bytes;
    }
    /// <summary>Rejects unknown versions, truncation and trailing data before creating a stamp.</summary>
    public static RenderSnapshotStamp Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length != EncodedBytes || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 1) throw new InvalidDataException("Unsupported mesh-view stamp.");
        var stamp = new RenderSnapshotStamp(new(bytes.AsSpan(4, 16)), new(bytes.AsSpan(20, 16)), new(bytes.AsSpan(36, 16)),
            BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(52)), BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(60)),
            BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(68)), BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(76)),
            BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(84)), BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(92)), Convert.ToHexString(bytes.AsSpan(100, 32)));
        stamp.Validate(); return stamp;
    }
}
