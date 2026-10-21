using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Sessions;

public interface IGameSession : IDisposable
{
    void Load(SceneSnapshot scene);
    void Tick();
    ulong TickNumber { get; }
    long Revision { get; }
    uint PatternColor { get; }
}
