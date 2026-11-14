using System.Numerics;
using TrueMoon.Aether.Abstractions.Runtime;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Abstractions.Sessions;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;
using TrueMoon.Services;

namespace RenderSurface.Game;

// Resources upload successfully; the required view then rejects an invalid rigid camera.
public sealed class FailedMeshGame : IRenderSnapshotGameSession, IGameSessionEntry
{
    private readonly MeshGame _inner;
    private FailedMeshGame(MeshGame inner) => _inner = inner;
    public static IGameSession Create(IServiceResolver? services) => new FailedMeshGame((MeshGame)MeshGame.Create(services));
    public ulong TickNumber => _inner.TickNumber;
    public long Revision => _inner.Revision;
    public uint PatternColor => _inner.PatternColor;
    public void Load(SceneSnapshot scene) => _inner.Load(scene);
    public void Tick() => _inner.Tick();
    public RenderSceneSource ReadRenderScene() => _inner.ReadRenderScene() with { Camera = RenderCameraSource.External(default, default) };
    public void Dispose() => _inner.Dispose();
}
