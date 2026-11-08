using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Preview;

/// <summary>Exact little-endian preview-view/1 control payload and frame stamp; no reserved fields are reinterpreted.</summary>
public static class PreviewViewCodec
{
    /// <summary>Explicit supported preview capability version.</summary>
    public const int Version = 1;
    /// <summary>Exact stamp bytes: version, four ASCII-N GUIDs, Int64 scene revision and UInt64 camera revision.</summary>
    public const int StampBytes = 148;
    /// <summary>Exact control envelope bytes, including stamp, row-major pose, projection, pivot and distance.</summary>
    public const int StateBytes = 256;

    /// <summary>Encodes a detached whole state.</summary><param name="state">Validated immutable camera/context.</param><returns>New256-byte envelope.</returns>
    public static byte[] Encode(PreviewViewState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var bytes = new byte[StateBytes]; "TMPVIEW1"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), Version);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), StateBytes);
        WriteStamp(bytes.AsSpan(16, StampBytes), state.Stamp);
        var m = state.Camera.World;
        float[] values = [m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44,
            state.Camera.Projection.FieldOfView,state.Camera.Projection.Near,state.Camera.Projection.Far,
            state.Camera.Pivot.X,state.Camera.Pivot.Y,state.Camera.Pivot.Z,state.Camera.Distance];
        for (var i = 0; i < values.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(164 + i * 4), values[i]);
        return bytes;
    }

    /// <summary>Decodes exact version/length and rejects malformed rigid/numeric values before mutation.</summary>
    /// <param name="bytes">Complete256-byte envelope.</param><returns>Detached immutable state.</returns>
    public static PreviewViewState Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != StateBytes || !bytes[..8].SequenceEqual("TMPVIEW1"u8) || BinaryPrimitives.ReadInt32LittleEndian(bytes[8..]) != Version ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes[12..]) != StateBytes) throw new InvalidDataException("Unsupported preview view envelope.");
        var stamp = ReadStamp(bytes.Slice(16, StampBytes)) ?? throw new InvalidDataException("Missing preview view stamp.");
        Span<float> v = stackalloc float[23];
        for (var i = 0; i < v.Length; i++) v[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes[(164 + i * 4)..]);
        try
        {
            return new(new(stamp.DocumentEpoch, stamp.ViewId, stamp.ContentId, stamp.SceneId, stamp.SceneRevision),
                new(new(v[0],v[1],v[2],v[3],v[4],v[5],v[6],v[7],v[8],v[9],v[10],v[11],v[12],v[13],v[14],v[15]),
                    new(v[16],v[17],v[18]),new(v[19],v[20],v[21]),v[22],stamp.CameraRevision));
        }
        catch (Exception error) when (error is ArgumentException or RenderContentException)
        { throw new InvalidDataException("Invalid preview camera values.", error); }
    }

    /// <summary>Writes the explicit frame-v2 stamp field. Null is represented by exactly zero bytes.</summary>
    /// <param name="bytes">Exactly148bytes.</param><param name="stamp">Capture-time identity, or null for untagged frames.</param>
    public static void WriteStamp(Span<byte> bytes, PreviewFrameStamp? stamp)
    {
        if (bytes.Length != StampBytes) throw new ArgumentException("Invalid stamp buffer.", nameof(bytes));
        bytes.Clear(); if (stamp == null) return; stamp.Validate();
        BinaryPrimitives.WriteInt32LittleEndian(bytes, Version);
        Encoding.ASCII.GetBytes(stamp.DocumentEpoch.ToString("N"), bytes[4..36]);
        Encoding.ASCII.GetBytes(stamp.ViewId.ToString("N"), bytes[36..68]);
        Encoding.ASCII.GetBytes(stamp.ContentId.ToString("N"), bytes[68..100]);
        Encoding.ASCII.GetBytes(stamp.SceneId.ToString("N"), bytes[100..132]);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[132..], stamp.SceneRevision);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[140..], stamp.CameraRevision);
    }

    /// <summary>Reads and validates the explicit stamp field; malformed/unsupported data is never treated as untagged.</summary>
    /// <param name="bytes">Exactly148bytes.</param><returns>Valid stamp, or null only for the all-zero representation.</returns>
    public static PreviewFrameStamp? ReadStamp(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != StampBytes) throw new InvalidDataException("Invalid preview stamp length.");
        var version = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (version == 0)
        {
            foreach (var value in bytes) if (value != 0) throw new InvalidDataException("Malformed absent preview stamp.");
            return null;
        }
        if (version != Version) throw new InvalidDataException("Unsupported preview stamp version.");
        var result = new PreviewFrameStamp(GuidAt(bytes,4),GuidAt(bytes,36),GuidAt(bytes,68),GuidAt(bytes,100),
            BinaryPrimitives.ReadInt64LittleEndian(bytes[132..]),BinaryPrimitives.ReadUInt64LittleEndian(bytes[140..]));
        result.Validate(); return result;
    }
    private static Guid GuidAt(ReadOnlySpan<byte> bytes, int offset) => Guid.TryParseExact(Encoding.ASCII.GetString(bytes.Slice(offset,32)), "N", out var id)
        ? id : throw new InvalidDataException("Invalid preview GUID.");
}
