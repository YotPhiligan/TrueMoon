using System;
using System.Numerics;
using TrueMoon;
using TrueMoon.Aether.Abstractions.Ecs;
using TrueMoon.Aether.Abstractions.Ecs.Rendering;
using TrueMoon.Aether.Abstractions.Ecs.Transforms;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Abstractions.Sessions;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Rendering;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;
using TrueMoon.Services;

namespace RenderSurface.Game;

public sealed class TexturedMeshGame(IFactory<World> worlds, RenderContentManifest content)
    : IRenderSnapshotGameSession, IGameBuildSessionEntry
{
    private World? _world;
    private EntityHandle _entity;
    private static readonly SceneId Scene = new(new("448edc79-8803-4dc0-b24b-9445695d980b"));

    public static IGameSession Create(IServiceResolver? services, RenderContentManifest content) =>
        new TexturedMeshGame(services?.Resolve<IFactory<World>>() ?? new Factory<World>(() => new()), content);

    public ulong TickNumber { get; private set; }
    public long Revision { get; private set; } = -1;
    public uint PatternColor => 0xff000000;

    public void Load(SceneSnapshot scene)
    {
        if (_world != null) throw new InvalidOperationException("Use a fresh session for a new build.");
        Revision = scene.Revision;
        _world = worlds.Create();
        _entity = _world.CreateEntity();
        var mesh = content.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId);
        var bindings = content.Resolve(RenderContentProfile.CubeBindingSetAssetId, RenderContentProfile.BindingSetKindId);
        _world.AddComponent(_entity, new MeshRenderer(mesh.Key, bindings.Key, true, uint.MaxValue, false, Vector3.One));
        _world.AddComponent(_entity, new WorldTransform(Matrix4x4.Identity, TickNumber));
    }

    public void Tick()
    {
        var world = _world ?? throw new InvalidOperationException("Load first.");
        world.SetComponent(_entity, new WorldTransform(Matrix4x4.Identity, ++TickNumber));
    }

    public RenderSceneSource ReadRenderScene() => new(_world ?? throw new InvalidOperationException("Load first."),
        content, Scene, Revision, RenderCameraSource.External(Matrix4x4.CreateTranslation(0, 0, 4), PerspectiveCamera.Default));

    public void Dispose()
    {
        _world?.Dispose();
        _world = null;
    }
}
