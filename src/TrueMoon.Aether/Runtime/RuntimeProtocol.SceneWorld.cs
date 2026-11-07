using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Runtime;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;

namespace TrueMoon.Aether.Runtime;

public sealed partial class RuntimeProtocol
{
    private SceneWorldStaging? _sceneStage;
    private readonly CancellationTokenSource _sceneEnd = new();
    private bool _sceneActivating;
    private bool _sceneTransferUsed, _disposed;
    private string _sceneOperation = "";
    private int _sceneOffset;
    private SceneWorldGameSession? SceneGame => role == RuntimeRole.Game && sceneWorldCodec != null ? game as SceneWorldGameSession : null;

    private void ExecuteSceneWorld(CommandKind kind, ControlRequest request, CancellationToken token)
    {
        var session = SceneGame ?? throw new NotSupportedException("This runtime does not advertise full scene World loading.");
        token.ThrowIfCancellationRequested();
        if (_loaded || _awaitingRestore) throw new InvalidOperationException("Full scene activation requires a fresh runtime without checkpoint restore.");
        switch (kind)
        {
            case CommandKind.BeginSceneWorld:
                if (_sceneTransferUsed) throw new InvalidOperationException("Only one initial scene transfer is admitted per runtime generation.");
                var stage = new SceneWorldStaging(request, session.RegistryId);
                _sceneStage = stage; _sceneOperation = stage.Operation.ToString("D"); _sceneOffset = 0; _sceneTransferUsed = true;
                break;
            case CommandKind.AppendSceneWorld:
                var append = _sceneStage ?? throw new InvalidOperationException("No scene transfer is active.");
                append.Append(request, session.RegistryId); _sceneOffset = append.Offset; break;
            case CommandKind.ActivateSceneWorld:
                var complete = _sceneStage ?? throw new InvalidOperationException("No scene transfer is active.");
                complete.Match(request, session.RegistryId);
                // Relinquish staging on every activation outcome. Failed/cancelled initial loads cannot be replayed.
                _sceneStage = null;
                using (var activation = CancellationTokenSource.CreateLinkedTokenSource(token, _sceneEnd.Token))
                {
                    _sceneActivating = true;
                    try
                    {
                        var data = complete.Decode(sceneWorldCodec!, activation.Token);
                        session.LoadSceneWorld(data, activation.Token);
                        _input.Clear(); _loaded = true;
                    }
                    finally { _sceneActivating = false; }
                }
                break;
            case CommandKind.AbortSceneWorld:
                var abort = _sceneStage ?? throw new InvalidOperationException("No scene transfer is active.");
                abort.Match(request, session.RegistryId); _sceneStage = null; break;
        }
    }
    private void DescribeSceneWorld(ControlReply reply)
    {
        if (SceneGame is not { } session) return;
        reply.SceneWorldVersion = SceneWorldStaging.Version; reply.SceneRegistryId = session.RegistryId.ToString("D");
        reply.SceneMaximumBytes = SceneWorldCodec.MaximumBytes; reply.SceneChunkBytes = SceneWorldStaging.ChunkBytes;
        reply.SceneLoadId = _sceneOperation; reply.SceneLoadOffset = _sceneOffset;
        if (session.ActiveScene is { } scene)
        {
            reply.SceneWorldId = scene.World.Id.ToString("D"); reply.SceneEntityCount = scene.Entities.Count;
            reply.SceneComponentCount = session.SceneComponentCount;
        }
    }
    /// <summary>Closes protocol admission and releases staging after server/worker drain. The game is borrowed.</summary>
    public void Dispose()
    {
        // Signal before waiting for the command lock: a cooperative importer must be able to finish.
        Exception? cancellationError = null;
        try { _sceneEnd.Cancel(); } catch (ObjectDisposedException) { }
        catch (Exception error) { cancellationError = error; }
        lock (_gate)
        {
            if (_disposed) return;
            if (_sceneActivating) throw new InvalidOperationException("Leave the scene importer before disposing its protocol.");
            _disposed = true; _stopping = true; _playing = false; _input.Clear(); _sceneStage = null;
            _sceneEnd.Dispose();
        }
        if (cancellationError != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cancellationError).Throw();
    }
}
