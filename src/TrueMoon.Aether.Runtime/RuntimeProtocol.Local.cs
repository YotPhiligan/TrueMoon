using TrueMoon.Aether.Abstractions.Ecs.Loading;

namespace TrueMoon.Aether.Runtime;

public sealed partial class RuntimeProtocol
{
    // Local Player activation uses the same game/World boundary, without fake RPC chunks or bootstrap.
    internal async Task LoadLocalWorldAsync(SceneWorldData data,CancellationToken token)
    {
        await _worldSerial.WaitAsync(token).ConfigureAwait(false);
        try
        {
            lock(_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed,this);
                if(!_hello||_loaded||_stopping||_awaitingRestore||_sceneTransferUsed)throw new InvalidOperationException("Local activation requires one fresh game.");
                var game=SceneGame??throw new NotSupportedException("Selected game does not support full scene World loading.");
                _sceneTransferUsed=true; game.LoadSceneWorld(data,token); _input.Clear(); _loaded=true;
            }
        }
        finally{_worldSerial.Release();}
    }
}
