using TrueMoon.Aether.Abstractions.Scenes;

namespace TrueMoon.Aether.Abstractions.Sessions;


/// <summary>One caller-owned game session. Its owner stops/drains use before disposal.</summary>
public interface IGameSession : IDisposable
{
    /// <summary>Activates a captured immutable scene once.</summary><param name="scene">Detached scene revision.</param>
    void Load(SceneSnapshot scene);
    /// <summary>Runs one synchronous fixed update. The caller serializes access and observes cooperative cancellation.</summary>
    void Tick();
    /// <summary>Completed simulation ticks.</summary>
    ulong TickNumber { get; }
    /// <summary>Captured source scene revision.</summary>
    long Revision { get; }
    /// <summary>Compatibility diagnostic pattern color; not a rendering backend.</summary>
    uint PatternColor { get; }
}
