using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Runtime.Preview;
using TrueMoon.Aether.Runtime.Transport;

namespace TrueMoon.Aether.Runtime.Sessions;

public sealed partial class SessionSupervisor
{
    private (string Operation, int Bytes, string Digest)? _activePreviewRender;

    internal Task StartPreviewRenderViewAsync(PreviewRenderPayload payload, PreviewViewState view, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(payload); ArgumentNullException.ThrowIfNull(view);
        payload.ValidateView(view);
        return StartCoreAsync(null, null, null, "normal", token, role: RuntimeRole.EditPreview, previewView: view, previewRender: payload);
    }

    private async Task<ControlReply> TransferPreviewRenderAsync(PreviewRenderPayload payload, PreviewViewState view, ControlReply hello, CancellationToken token)
    {
        if (hello.PreviewRenderVersion != PreviewRenderPayload.Version)
            throw new NotSupportedException("Runtime does not support the required preview render payload version.");
        ValidatePreviewRenderProfile(hello);
        if (hello.SceneLoadId != "" || hello.SceneLoadOffset != 0 || hello.PreviewRenderDigest != "" || hello.PreviewViewStamp != null)
            throw new InvalidDataException("Hello returned a previously used preview runtime.");
        var operation = Guid.NewGuid().ToString("D"); var digest = payload.Digest.ToString();
        var begin = await ExchangeAsync(CommandKind.BeginPreviewRender, request =>
        {
            Stamp(request); request.SceneId = payload.SceneId.Value.ToString("D"); request.Revision = payload.Revision;
            request.SceneBytes = payload.Length; request.SceneDigest = digest; request.PreviewView = PreviewViewCodec.Encode(view);
        }, token).ConfigureAwait(false);
        ValidateProgress(begin, 0, false);
        var offset = 0;
        while (offset < payload.Length)
        {
            token.ThrowIfCancellationRequested();
            var size = Math.Min(PreviewRenderPayload.ChunkBytes, payload.Length - offset);
            var append = await ExchangeAsync(CommandKind.AppendPreviewRender, request =>
            { Stamp(request); request.SceneOffset = offset; request.SceneChunk = payload.CopyChunk(offset, size); }, token).ConfigureAwait(false);
            offset += size;
            ValidateProgress(append, offset, false);
        }
        // A failed/cancelled exchange does not establish which sequence the child accepted.
        // Startup failure closes/reaps that runtime instead of sending Abort into an uncertain pipe.
        var activation = await ExchangeAsync(CommandKind.ActivatePreviewRender, Stamp, token).ConfigureAwait(false);
        ValidateProgress(activation, payload.Length, true);
        ValidatePreviewReply(activation, view);
        _activePreviewRender = (operation, payload.Length, digest);
        return activation;

        void Stamp(ControlRequest request) => request.SceneLoadId = operation;
        void ValidateProgress(ControlReply reply, int expectedOffset, bool active)
        {
            ValidatePreviewRenderProfile(reply);
            if (reply.SceneLoadId != operation || reply.SceneLoadOffset != expectedOffset ||
                reply.PreviewRenderDigest != (active ? digest : "") || reply.Phase != (active ? "Paused" : "HelloAccepted") ||
                (active ? reply.Revision != payload.Revision : reply.PreviewViewStamp != null))
                throw new InvalidDataException("Unexpected preview render transfer acknowledgement.");
        }
    }

    private void ValidateActivePreviewRender(ControlReply reply)
    {
        if (_activePreviewRender is not { } active) return;
        ValidatePreviewRenderProfile(reply);
        if (reply.SceneLoadId != active.Operation || reply.SceneLoadOffset != active.Bytes || reply.PreviewRenderDigest != active.Digest)
            throw new InvalidDataException("Active preview render transfer identity changed.");
    }

    private static void ValidatePreviewRenderProfile(ControlReply reply)
    {
        if (reply.PreviewRenderVersion != PreviewRenderPayload.Version || reply.PreviewViewVersion != PreviewViewCodec.Version || reply.Tick != 0 ||
            reply.RenderViewVersion != 0 || reply.RenderViewReady || reply.SceneWorldVersion != 0 || reply.SceneRegistryId != "" ||
            reply.SceneChunkBytes != 0 || reply.SceneMaximumBytes != 0 || reply.SceneWorldId != "" ||
            reply.SceneEntityCount != 0 || reply.SceneComponentCount != 0)
            throw new InvalidDataException("Unexpected render preview capabilities or game readiness.");
    }
}
