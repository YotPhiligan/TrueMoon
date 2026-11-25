using TrueMoon.Aether.Ecs;

namespace TrueMoon.Aether.Kul.Runtime;

// Scheduler owns the system; scene subsystem owns its borrowed native dependencies.
internal sealed class AudioBridgeSystem(AudioSceneSubsystem owner) : ISystem
{
    private AudioSceneSubsystem? _owner = owner;
    public void Execute(EcsSystemContext context) => (_owner ?? throw new ObjectDisposedException(nameof(AudioBridgeSystem))).Advance(context);
    public void Stop() => _owner?.SetPlaying(false);
    public void Dispose() => _owner = null;
}
