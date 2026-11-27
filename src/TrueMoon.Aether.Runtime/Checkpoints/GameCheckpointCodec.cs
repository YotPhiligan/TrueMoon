using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using TrueMoon.Aether.Checkpoints;

namespace TrueMoon.Aether.Runtime.Checkpoints;

/// <summary>Canonical little-endian TMCK v3 with stable-ID object states, optional strict UTF-8 game data and SHA-256 integrity.</summary>
public static class GameCheckpointCodec
{
    /// <summary>Maximum wire size before generated control envelope overhead.</summary>
    public const int MaxPayloadBytes = FixedBytes + MaxIdUtf8Bytes + GameObjectStates.MaximumCount * (3 + MaxIdUtf8Bytes)
        + GameCheckpointState.MaxPayloadUtf8Bytes;
    /// <summary>Fixed header, object count, game payload length and checksum; IDs and game data are variable.</summary>
    public const int FixedBytes = HeaderBytes + 2 + 2 + HashBytes;
    private const int HeaderBytes = 115, MaxIdUtf8Bytes = 512, HashBytes = 32;
    private const uint Magic = 0x4b434d54;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    /// <summary>Encodes a detached payload, never aliasing mutable game/wire data.</summary>
    public static byte[] Encode(GameCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        int sceneSize;
        try { sceneSize = Utf8.GetByteCount(checkpoint.SceneId); }
        catch (EncoderFallbackException error) { throw new InvalidDataException("Checkpoint scene is not valid UTF-8.", error); }
        if (sceneSize is < 1 or > MaxIdUtf8Bytes) throw new InvalidDataException("Invalid checkpoint scene UTF-8 length.");
        // The immutable envelope has already validated the object IDs and optional payload.
        var state = checkpoint.State;
        var payload = state.Payload;
        var payloadSize = payload == null ? 0 : Utf8.GetByteCount(payload);
        var objects = state.Objects.Entries;
        Span<int> objectSizes = stackalloc int[objects.Length];
        var size = FixedBytes + sceneSize + payloadSize;
        for (var i = 0; i < objects.Length; i++)
        {
            objectSizes[i] = Utf8.GetByteCount(objects[i].EntityId);
            size += 3 + objectSizes[i];
        }
        var bytes = new byte[size]; var span = bytes.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span, Magic); BinaryPrimitives.WriteInt32LittleEndian(span[4..], checkpoint.Version);
        Convert.FromHexString(checkpoint.HostContractId.AsSpan(), span.Slice(8, HashBytes), out _, out _);
        Convert.FromHexString(checkpoint.SourceGameBuildId.AsSpan(), span.Slice(40, HashBytes), out _, out _);
        BinaryPrimitives.WriteUInt16LittleEndian(span[72..], (ushort)sceneSize);
        BinaryPrimitives.WriteInt64LittleEndian(span[74..], checkpoint.SceneRevision);
        BinaryPrimitives.WriteUInt64LittleEndian(span[82..], state.Tick);
        var camera = state.Camera;
        ReadOnlySpan<float> values = stackalloc float[] { camera.X, camera.Y, camera.Z, camera.Pitch, camera.Yaw, camera.Roll };
        for (var i = 0; i < values.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(span[(90 + i * 4)..], values[i]);
        span[114] = state.Interaction ? (byte)1 : (byte)0;
        Utf8.GetBytes(checkpoint.SceneId, span[HeaderBytes..]);
        var offset = HeaderBytes + sceneSize;
        BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], (ushort)objects.Length); offset += 2;
        for (var i = 0; i < objects.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], (ushort)objectSizes[i]); offset += 2;
            offset += Utf8.GetBytes(objects[i].EntityId, span[offset..]);
            span[offset++] = objects[i].Active ? (byte)1 : (byte)0;
        }
        BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], (ushort)payloadSize); offset += 2;
        if (payload != null) Utf8.GetBytes(payload, span.Slice(offset, payloadSize));
        SHA256.HashData(span[..^HashBytes], span[^HashBytes..]); return bytes;
    }
    /// <summary>Validates size/version/integrity and every field before creating any game state.</summary>
    public static GameCheckpoint Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < FixedBytes + 1 || bytes.Length > MaxPayloadBytes) throw new InvalidDataException("Invalid checkpoint payload size.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != Magic || BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]) != GameCheckpoint.CurrentVersion)
            throw new InvalidDataException("Unsupported checkpoint magic/schema.");
        var length = BinaryPrimitives.ReadUInt16LittleEndian(bytes[72..]);
        Span<byte> hash = stackalloc byte[HashBytes];
        if (length is < 1 or > MaxIdUtf8Bytes || bytes.Length < FixedBytes + length)
            throw new InvalidDataException("Checkpoint length/integrity mismatch.");
        SHA256.HashData(bytes[..^HashBytes], hash);
        if (!CryptographicOperations.FixedTimeEquals(hash, bytes[^HashBytes..]))
            throw new InvalidDataException("Checkpoint length/integrity mismatch.");
        string scene;
        try { scene = Utf8.GetString(bytes.Slice(HeaderBytes, length)); }
        catch (DecoderFallbackException error) { throw new InvalidDataException("Invalid checkpoint scene UTF-8.", error); }
        if (bytes[114] > 1) throw new InvalidDataException("Invalid checkpoint interaction flag.");
        var camera = new CameraTransform(BinaryPrimitives.ReadSingleLittleEndian(bytes[90..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[94..]),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[98..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[102..]),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[106..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[110..]));
        var offset = HeaderBytes + length; var end = bytes.Length - HashBytes;
        var objectLimit = end - 2; // Reserve the mandatory game payload length field.
        var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]); offset += 2;
        if (count > GameObjectStates.MaximumCount) throw new InvalidDataException("Too many checkpoint object states.");
        List<GameObjectState>? objects = count == 0 ? null : new(count);
        string? previousId = null;
        for (var i = 0; i < count; i++)
        {
            if (offset + 2 > objectLimit) throw new InvalidDataException("Truncated object state.");
            var size = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]); offset += 2;
            if (size is < 1 or > MaxIdUtf8Bytes || offset + size + 1 > objectLimit) throw new InvalidDataException("Invalid object state length.");
            string id;
            try { id = Utf8.GetString(bytes.Slice(offset, size)); }
            catch (DecoderFallbackException error) { throw new InvalidDataException("Invalid object state UTF8.", error); }
            offset += size; var flag = bytes[offset++];
            if (flag > 1 || previousId != null && StringComparer.Ordinal.Compare(previousId, id) >= 0)
                throw new InvalidDataException("Invalid or noncanonical object states.");
            objects!.Add(new(id, flag != 0)); previousId = id;
        }
        var payloadSize = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]); offset += 2;
        if (payloadSize > GameCheckpointState.MaxPayloadUtf8Bytes || payloadSize != end - offset)
            throw new InvalidDataException("Invalid checkpoint game payload length.");
        string? payload;
        try { payload = payloadSize == 0 ? null : Utf8.GetString(bytes.Slice(offset, payloadSize)); }
        catch (DecoderFallbackException error) { throw new InvalidDataException("Invalid checkpoint game payload UTF-8.", error); }
        return new(Convert.ToHexString(bytes.Slice(8, 32)), Convert.ToHexString(bytes.Slice(40, 32)), scene,
            BinaryPrimitives.ReadInt64LittleEndian(bytes[74..]), new(BinaryPrimitives.ReadUInt64LittleEndian(bytes[82..]), camera, bytes[114] != 0)
            { Objects = objects == null ? GameObjectStates.Empty : new(objects), Payload = payload });
    }
}
