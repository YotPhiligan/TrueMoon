using TrueMoon.Aether.Abstractions.Runtime;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Abstractions.Sessions;
using TrueMoon.Services;

namespace TrueMoon.ManualTests.ContractExtension;

/// <summary>Neutral prepared-game fixture; references only the mod SDK and common contracts.</summary>
public sealed class ExtensionGame : IGameSession, IGameSessionEntry
{
    private bool _disposed;
    public ulong TickNumber { get; private set; }
    public long Revision { get; private set; } = -1;
    public uint PatternColor { get; private set; }
    public static IGameSession Create(IServiceResolver? services) => new ExtensionGame();
    public void Load(SceneSnapshot scene)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Revision = scene.Revision; TickNumber = 0;
        PatternColor = checked((uint)scene.CopyEntities().Sum(entity => entity.X));
    }
    public void Tick() { ObjectDisposedException.ThrowIf(_disposed, this); TickNumber++; PatternColor += 3; }
    public void Dispose() { _disposed = true; Revision = -1; }
}
