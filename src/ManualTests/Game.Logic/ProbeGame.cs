using TrueMoon.Aether.Contracts;

namespace Game.Logic;

// ENG1 stub only: checkpoint/replacement and frame production belong to ENG2/3.
public sealed class ProbeGame : IGameSession
{
    private SceneSnapshot? _scene;
    private bool _disposed;
    public ulong TickNumber { get; private set; }
    public long Revision => _scene?.Revision ?? -1;
    public uint PatternColor => 0xff2080c0;
    public void Load(SceneSnapshot scene)
    {
        ObjectDisposedException.ThrowIf(_disposed, this); ArgumentNullException.ThrowIfNull(scene);
        _scene = scene; TickNumber = 0;
    }
    public void Tick()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_scene == null) throw new InvalidOperationException("No scene loaded.");
        TickNumber = checked(TickNumber + 1);
    }
    public void Dispose() { _disposed = true; _scene = null; }
}
