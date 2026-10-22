using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace TrueMoon.Aether.Checkpoints;

/// <summary>Canonical little-endian TMCK v2 with stable-ID object states, strict UTF8 and SHA256 integrity. V1 is rejected.</summary>
public static class GameCheckpointCodec
{
    /// <summary>Maximum accepted snapshot payload, before generated control envelope overhead.</summary>
    public const int MaxPayloadBytes = 64 * 1024;
    /// <summary>Fixed header, object count and checksum; scene/object ID strings are variable.</summary>
    public const int FixedBytes = 149;
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
        var objects = checkpoint.State.Objects.Entries;
        var objectIds = objects.Select(entry => Utf8.GetBytes(entry.EntityId)).ToArray();
        var bytes = new byte[checked(FixedBytes + scene.Length + objectIds.Sum(id => 3 + id.Length))]; var span = bytes.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span, Magic); BinaryPrimitives.WriteInt32LittleEndian(span[4..], checkpoint.Version);
        Convert.FromHexString(checkpoint.HostContractId).CopyTo(span[8..]); Convert.FromHexString(checkpoint.SourceGameBuildId).CopyTo(span[40..]);
        BinaryPrimitives.WriteUInt16LittleEndian(span[72..], checked((ushort)scene.Length));
        BinaryPrimitives.WriteInt64LittleEndian(span[74..], checkpoint.SceneRevision);
        BinaryPrimitives.WriteUInt64LittleEndian(span[82..], checkpoint.State.Tick);
        var camera = checkpoint.State.Camera;
        var values = new[] { camera.X, camera.Y, camera.Z, camera.Pitch, camera.Yaw, camera.Roll };
        for (var i = 0; i < values.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(span[(90 + i * 4)..], values[i]);
        span[114] = checkpoint.State.Interaction ? (byte)1 : (byte)0; scene.CopyTo(span[115..]);
        var offset = 115 + scene.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], (ushort)objects.Length); offset += 2;
        for (var i = 0; i < objects.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], (ushort)objectIds[i].Length); offset += 2;
            objectIds[i].CopyTo(span[offset..]); offset += objectIds[i].Length;
            span[offset++] = objects[i].Active ? (byte)1 : (byte)0;
        }
        SHA256.HashData(span[..^32], span[^32..]); return bytes;
    }
    /// <summary>Validates size/version/integrity and every field before creating any game state.</summary>
    public static GameCheckpoint Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < FixedBytes + 1 || bytes.Length > MaxPayloadBytes) throw new InvalidDataException("Invalid checkpoint payload size.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != Magic || BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]) != GameCheckpoint.CurrentVersion)
            throw new InvalidDataException("Unsupported checkpoint magic/schema.");
        var length = BinaryPrimitives.ReadUInt16LittleEndian(bytes[72..]);
        if (length is < 1 or > 512 || bytes.Length < FixedBytes + length || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes[..^32]), bytes[^32..]))
            throw new InvalidDataException("Checkpoint length/integrity mismatch.");
        string scene;
        try { scene = Utf8.GetString(bytes.Slice(115, length)); }
        catch (DecoderFallbackException error) { throw new InvalidDataException("Invalid checkpoint UTF8.", error); }
        if (bytes[114] > 1) throw new InvalidDataException("Invalid checkpoint interaction flag.");
        var camera = new CameraTransform(BinaryPrimitives.ReadSingleLittleEndian(bytes[90..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[94..]),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[98..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[102..]),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[106..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[110..]));
        var offset = 115 + length; var end = bytes.Length - 32;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]); offset += 2;
        if (count > GameObjectStates.MaximumCount) throw new InvalidDataException("Too many checkpoint object states.");
        var objects = new List<GameObjectState>(count); string? previousId = null;
        for (var i = 0; i < count; i++)
        {
            if (offset + 2 > end) throw new InvalidDataException("Truncated object state.");
            var size = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]); offset += 2;
            if (size is < 1 or > 512 || offset + size + 1 > end) throw new InvalidDataException("Invalid object state length.");
            string id;
            try { id = Utf8.GetString(bytes.Slice(offset, size)); }
            catch (DecoderFallbackException error) { throw new InvalidDataException("Invalid object state UTF8.", error); }
            offset += size; var flag = bytes[offset++];
            if (flag > 1 || previousId != null && StringComparer.Ordinal.Compare(previousId, id) >= 0)
                throw new InvalidDataException("Invalid or noncanonical object states.");
            objects.Add(new(id, flag != 0)); previousId = id;
        }
        if (offset != end) throw new InvalidDataException("Trailing checkpoint object data.");
        return new(Convert.ToHexString(bytes.Slice(8, 32)), Convert.ToHexString(bytes.Slice(40, 32)), scene,
            BinaryPrimitives.ReadInt64LittleEndian(bytes[74..]), new(BinaryPrimitives.ReadUInt64LittleEndian(bytes[82..]), camera, bytes[114] != 0)
            { Objects = new(objects) });
    }
}
