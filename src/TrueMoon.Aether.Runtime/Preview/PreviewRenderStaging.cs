using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Transport;

namespace TrueMoon.Aether.Runtime.Preview;

// Owned by the serialized control protocol. Never observed by the active World.
internal sealed class PreviewRenderStaging
{
    private readonly byte[] _bytes;
    private readonly RenderContentHash _digest;
    internal Guid Operation { get; }
    internal PreviewViewState View { get; }
    internal int Offset { get; private set; }

    internal PreviewRenderStaging(ControlRequest request)
    {
        Operation = SceneWorldStaging.ParseId(request.SceneLoadId);
        var scene = SceneWorldStaging.ParseId(request.SceneId);
        if (request.Revision < 0 || request.SceneBytes is < PreviewRenderPayload.HeaderBytes or > PreviewRenderPayload.MaximumBytes ||
            request.SceneOffset != 0 || request.SceneChunk != null || request.SceneDigest is not { Length: 64 })
            throw new InvalidDataException("Invalid preview render transfer length, digest or initial offset.");
        try { _digest = RenderContentHash.Parse(request.SceneDigest); }
        catch (FormatException error) { throw new InvalidDataException("Invalid preview render transfer digest.", error); }
        View = PreviewViewCodec.Decode(request.PreviewView ?? throw new InvalidDataException("Missing initial preview view."));
        View.Camera.CreateProjection(16, 16);
        if (scene != View.Context.SceneId || request.Revision != View.Context.SceneRevision)
            throw new InvalidDataException("Preview view does not match the transfer scene stamp.");
        _bytes = GC.AllocateUninitializedArray<byte>(request.SceneBytes);
    }

    internal void Match(ControlRequest request)
    {
        if (SceneWorldStaging.ParseId(request.SceneLoadId) != Operation)
            throw new InvalidDataException("Stale preview render transfer operation.");
        if (request.SceneId != "" || request.Revision != 0 || request.SceneBytes != 0 || request.SceneDigest != "")
            throw new InvalidDataException("Preview render transfer header is allowed only in Begin.");
    }

    internal void Append(ControlRequest request)
    {
        Match(request);
        var expected = Math.Min(PreviewRenderPayload.ChunkBytes, _bytes.Length - Offset);
        if (expected == 0 || request.SceneOffset != Offset || request.SceneChunk == null || request.SceneChunk.Length != expected)
            throw new InvalidDataException("Preview render chunks must be complete, contiguous and bounded.");
        request.SceneChunk.CopyTo(_bytes, Offset); Offset += expected;
    }

    internal PreviewRenderPayload Decode(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Offset != _bytes.Length || RenderContentHash.Compute(_bytes) != _digest)
            throw new InvalidDataException("Incomplete or corrupt preview render transfer.");
        var payload = PreviewRenderPayload.DecodeOwned(_bytes, _digest, token);
        payload.ValidateView(View);
        return payload;
    }
}
