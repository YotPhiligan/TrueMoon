using TrueMoon.Aether.Kul.Runtime.Content;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Kul;

namespace TrueMoon.Aether.Kul.Runtime;

internal sealed class AudioSceneSubsystemFactory(IFactory<AudioSession> sessions, IFactory<IAudioContentLease, AudioContentLoadArguments> content,
    IFactory<AudioBridgeSystem, AudioSceneSubsystem> systems, AudioContentReference reference, AudioBridgeOptions options)
    : IFactory<AudioSceneSubsystem, SceneSubsystemArguments>
{
    private readonly AudioBridgeOptions _options = options.Snapshot();
    public AudioSceneSubsystem Create(SceneSubsystemArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(arguments.Scene);
        var owner = new AudioSceneSubsystem(arguments, _options);
        try { owner.Prepare(sessions, content, systems, reference, arguments.CancellationToken); return owner; }
        catch (Exception error)
        {
            try { owner.Dispose(); }
            catch (Exception cleanup) { throw new SceneWorldPreparationException(new AggregateException(error, cleanup), owner); }
            throw;
        }
    }
}
