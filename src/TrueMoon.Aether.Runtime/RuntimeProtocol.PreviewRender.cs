using TrueMoon.Aether.Runtime.Preview;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Runtime.Transport;

namespace TrueMoon.Aether.Runtime;

public sealed partial class RuntimeProtocol
{
    internal bool PreviewSurfaceAvailable { get; init; }
    private PreviewGameSession? PreviewRenderGame => role == RuntimeRole.EditPreview && PreviewSurfaceAvailable &&
        game is PreviewGameSession { CanLoadRender: true } preview ? preview : null;
    private PreviewRenderStaging? _previewRenderStage;
    private string _previewRenderOperation = "", _previewRenderDigest = "";
    private int _previewRenderOffset;

    private void ExecutePreviewRender(CommandKind kind, ControlRequest request, CancellationToken token)
    {
        var session = PreviewRenderGame ?? throw new NotSupportedException("This runtime does not advertise render preview loading.");
        token.ThrowIfCancellationRequested();
        if (_loaded) throw new InvalidOperationException("Render preview activation requires a fresh runtime.");
        if (request.SceneRegistryId != "" || request.Entities != null || request.Checkpoint != null || request.Surface != null)
            throw new InvalidDataException("A render preview transfer cannot contain other scene profiles.");
        if (kind == CommandKind.BeginPreviewRender)
        {
            if (_previewRenderOperation.Length != 0) throw new InvalidOperationException("Only one initial preview render transfer is admitted per generation.");
            var stage = new PreviewRenderStaging(request);
            _previewRenderStage = stage; _previewRenderOperation = stage.Operation.ToString("D");
            return;
        }
        var active = _previewRenderStage ?? throw new InvalidOperationException("No preview render transfer is active.");
        if (kind == CommandKind.AppendPreviewRender)
        {
            active.Append(request); _previewRenderOffset = active.Offset;
            return;
        }
        active.Match(request);
        if (request.SceneChunk != null || request.SceneOffset != 0)
            throw new InvalidDataException("Unexpected preview render chunk.");
        _previewRenderStage = null;
        if (kind == CommandKind.AbortPreviewRender) return;

        using var activation = CancellationTokenSource.CreateLinkedTokenSource(token, _sceneEnd.Token);
        _sceneActivating = true;
        try
        {
            var payload = active.Decode(activation.Token);
            var digest = payload.Digest.ToString();
            session.LoadRender(payload, active.View, activation.Token);
            _input.Clear(); _previewRenderDigest = digest; _loaded = true;
        }
        finally { _sceneActivating = false; }
    }

    private void DescribePreviewRender(ControlReply reply)
    {
        if (PreviewRenderGame == null) return;
        reply.PreviewRenderVersion = PreviewRenderPayload.Version;
        reply.PreviewRenderDigest = _previewRenderDigest;
        reply.SceneLoadId = _previewRenderOperation;
        reply.SceneLoadOffset = _previewRenderOffset;
    }
}
