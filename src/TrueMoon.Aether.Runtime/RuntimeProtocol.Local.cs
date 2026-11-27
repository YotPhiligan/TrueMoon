using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Runtime.Checkpoints;
using TrueMoon.Aether.Ecs.Loading;

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
                if(!_hello||_loaded||_stopping||_sceneTransferUsed)throw new InvalidOperationException("Local activation requires one fresh game.");
                var game=SceneGame??throw new NotSupportedException("Selected game does not support full scene World loading.");
                _sceneTransferUsed=true; game.LoadSceneWorld(data,token); _input.Clear(); _loaded=true;
            }
        }
        finally{_worldSerial.Release();}
    }

    internal async Task<GameCheckpoint> CaptureLocalCheckpointAsync(CancellationToken token)
    {
        await _worldSerial.WaitAsync(token).ConfigureAwait(false);
        try
        {
            lock (_gate)
            {
                var outcome = CaptureCheckpoint(out var checkpoint);
                if (outcome != CheckpointOutcome.Captured) throw new CheckpointRejectedException(outcome);
                try { GameCheckpointCodec.SceneByteCount(checkpoint!.SceneId); }
                catch (InvalidDataException) { throw new CheckpointRejectedException(CheckpointOutcome.CaptureRejected); }
                return checkpoint!;
            }
        }
        finally { _worldSerial.Release(); }
    }

    internal async Task RestoreLocalCheckpointAsync(GameCheckpoint checkpoint, CancellationToken token)
    {
        await _worldSerial.WaitAsync(token).ConfigureAwait(false);
        try
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_stopping) throw new InvalidOperationException("Runtime is stopping.");
                if (!string.Equals(checkpoint.SourceGameBuildId, identity.GameBuildId, StringComparison.Ordinal))
                    throw new InvalidDataException("Checkpoint was captured from a different Player Build.");
                var outcome = RestoreCheckpoint(checkpoint, null);
                if (outcome != CheckpointOutcome.Restored) throw new CheckpointRejectedException(outcome);
            }
        }
        finally { _worldSerial.Release(); }
    }
}
