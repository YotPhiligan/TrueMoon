using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace TrueMoon.Aether.Checkpoints;

/// <summary>Canonical little-endian TMCK v1 snapshot with strict UTF8, finite floats and SHA256 integrity.</summary>
public static class GameCheckpointCodec
{
    /// <summary>Maximum accepted snapshot payload, before generated control envelope overhead.</summary>
    public const int MaxPayloadBytes = 64 * 1024;
    /// <summary>Fixed header plus checksum; the only variable field is a scene ID of at most 512 UTF8 bytes.</summary>
    public const int FixedBytes = 147;
    private const uint Magic = 0x4b434d54;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    /// <summary>Encodes a detached payload, never aliasing mutable game/wire data.</summary>
    public static byte[] Encode(GameCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint); checkpoint.State.Validate();
        byte[] scene;
        try { scene = Utf8.GetBytes(checkpoint.SceneId); }
        catch (EncoderFallbackException error) { throw new InvalidDataException("Checkpoint scene is not valid UTF8.", error); }
        if (scene.Length is < 1 or > 512) throw new InvalidDataException("Checkpoint scene UTF8 length exceeds its bound.");
        var bytes = new byte[checked(FixedBytes + scene.Length)]; var span = bytes.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span, Magic); BinaryPrimitives.WriteInt32LittleEndian(span[4..], checkpoint.Version);
        Convert.FromHexString(checkpoint.HostContractId).CopyTo(span[8..]); Convert.FromHexString(checkpoint.SourceGameBuildId).CopyTo(span[40..]);
        BinaryPrimitives.WriteUInt16LittleEndian(span[72..], checked((ushort)scene.Length));
        BinaryPrimitives.WriteInt64LittleEndian(span[74..], checkpoint.SceneRevision);
        BinaryPrimitives.WriteUInt64LittleEndian(span[82..], checkpoint.State.Tick);
        var camera = checkpoint.State.Camera;
        var values = new[] { camera.X, camera.Y, camera.Z, camera.Pitch, camera.Yaw, camera.Roll };
        for (var i = 0; i < values.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(span[(90 + i * 4)..], values[i]);
        span[114] = checkpoint.State.Interaction ? (byte)1 : (byte)0; scene.CopyTo(span[115..]);
        SHA256.HashData(span[..^32], span[^32..]); return bytes;
    }
    /// <summary>Validates size/version/integrity and every field before creating any game state.</summary>
    public static GameCheckpoint Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < FixedBytes + 1 || bytes.Length > MaxPayloadBytes) throw new InvalidDataException("Invalid checkpoint payload size.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != Magic || BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]) != GameCheckpoint.CurrentVersion)
            throw new InvalidDataException("Unsupported checkpoint magic/schema.");
        var length = BinaryPrimitives.ReadUInt16LittleEndian(bytes[72..]);
        if (length is < 1 or > 512 || bytes.Length != FixedBytes + length || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes[..^32]), bytes[^32..]))
            throw new InvalidDataException("Checkpoint length/integrity mismatch.");
        string scene;
        try { scene = Utf8.GetString(bytes.Slice(115, length)); }
        catch (DecoderFallbackException error) { throw new InvalidDataException("Invalid checkpoint UTF8.", error); }
        if (bytes[114] > 1) throw new InvalidDataException("Invalid checkpoint interaction flag.");
        var camera = new CameraTransform(BinaryPrimitives.ReadSingleLittleEndian(bytes[90..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[94..]),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[98..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[102..]),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[106..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[110..]));
        return new(Convert.ToHexString(bytes.Slice(8, 32)), Convert.ToHexString(bytes.Slice(40, 32)), scene,
            BinaryPrimitives.ReadInt64LittleEndian(bytes[74..]), new(BinaryPrimitives.ReadUInt64LittleEndian(bytes[82..]), camera, bytes[114] != 0));
    }
}
