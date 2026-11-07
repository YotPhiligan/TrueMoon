using System.Security.Cryptography;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Transport;

namespace TrueMoon.Aether.Runtime;

/// <summary>One bounded, sequential initial transfer. Owned by the serialized runtime protocol.</summary>
internal sealed class SceneWorldStaging
{
    internal const int Version = 1, ChunkBytes = 32 * 1024;
    private readonly byte[] _bytes, _digest;
    internal readonly Guid Operation, Registry, Scene;
    internal readonly long Revision;
    internal int Offset { get; private set; }

    internal SceneWorldStaging(ControlRequest request, Guid registry)
    {
        Operation = ParseId(request.SceneLoadId); Registry = ParseId(request.SceneRegistryId);
        Scene = ParseId(request.SceneId); Revision = request.Revision;
        if (Registry != registry || Revision < 0 || request.SceneBytes < SceneWorldCodec.HeaderBytes || request.SceneBytes > SceneWorldCodec.MaximumBytes ||
            request.SceneOffset != 0 || request.SceneChunk is { Length: > 0 })
            throw new InvalidDataException("Invalid scene transfer identity, length or initial offset.");
        if (request.SceneDigest is not { Length: 64 } || !request.SceneDigest.All(Uri.IsHexDigit))
            throw new InvalidDataException("Missing exact scene payload SHA256.");
        _digest = Convert.FromHexString(request.SceneDigest); _bytes = new byte[request.SceneBytes];
    }
    internal void Match(ControlRequest request, Guid registry)
    {
        if (ParseId(request.SceneLoadId) != Operation || ParseId(request.SceneRegistryId) != Registry || registry != Registry)
            throw new InvalidDataException("Stale scene transfer operation or registry generation.");
    }
    internal void Append(ControlRequest request, Guid registry)
    {
        Match(request, registry);
        var expected = Math.Min(ChunkBytes, _bytes.Length - Offset);
        if (expected == 0 || request.SceneOffset != Offset || request.SceneChunk == null || request.SceneChunk.Length != expected)
            throw new InvalidDataException("Scene chunks must be complete, contiguous and bounded.");
        request.SceneChunk.CopyTo(_bytes, Offset); Offset += expected;
    }
    internal SceneWorldData Decode(SceneWorldCodec codec, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Offset != _bytes.Length || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(_bytes), _digest))
            throw new InvalidDataException("Incomplete or corrupt scene transfer.");
        var data = codec.Decode(_bytes, token);
        if (data.Scene.Id.Value != Scene || data.Scene.Revision != Revision)
            throw new InvalidDataException("Scene payload does not match its transfer stamp.");
        return data;
    }
    internal static Guid ParseId(string value)
    {
        if (value is not { Length: 36 } || !Guid.TryParseExact(value, "D", out var id) || id == Guid.Empty)
            throw new InvalidDataException("Scene transfer IDs must be nonempty GUIDs in exact D format.");
        return id;
    }
}
