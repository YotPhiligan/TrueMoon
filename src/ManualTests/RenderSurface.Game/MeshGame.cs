using TrueMoon.Aether.Abstractions.Ecs.Motion;
using System.Numerics;
using System.Security.Cryptography;
using TrueMoon;
using TrueMoon.Aether.Abstractions.Ecs;
using TrueMoon.Aether.Abstractions.Ecs.Rendering;
using TrueMoon.Aether.Abstractions.Ecs.Transforms;
using TrueMoon.Aether.Abstractions.Input;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Abstractions.Runtime;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Abstractions.Sessions;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Rendering;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;
using TrueMoon.Services;

namespace RenderSurface.Game;

// Selected game fixture: the parent never loads this assembly. All resource/device
// ownership belongs to the runtime view; the game owns only its factory-created World.
public sealed class MeshGame : IRenderSnapshotGameSession, IInputGameSession, IGameSessionEntry, IGameScenarioObservationSource
{
    private readonly IFactory<World> _worlds;
    private World? _world;
    private EntityHandle _first, _second;
    private RenderContentManifest _content;
    private bool _grey, _small;
    private MeshGame(IFactory<World> worlds)
    {
        _worlds = worlds;
        var build = RenderContentHash.FromBytes(SHA256.HashData(File.ReadAllBytes(typeof(MeshGame).Assembly.Location)));
        _content = BuiltinRenderContent.Create(new("4716d534-193a-4b12-a058-73a71d1fb340"), build, Guid.NewGuid());
    }
    public static IGameSession Create(IServiceResolver? services) =>
        new MeshGame(services?.Resolve<IFactory<World>>() ?? new Factory<World>(() => new()));
    public ulong TickNumber { get; private set; }
    public long Revision { get; private set; } = -1;
    public uint PatternColor => 0xff000000;
    public void Load(SceneSnapshot scene)
    {
        if (_world != null) throw new InvalidOperationException("Use a fresh game for a new scene.");
        Revision = scene.Revision; _world = _worlds.Create();
        _first = Add(-1); _second = Add(1);
    }
    private EntityHandle Add(float x)
    {
        var entity = _world!.CreateEntity();
        _world.AddComponent(entity, new MeshRenderer(_content.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId).Key,
            _content.Resolve(RenderContentProfile.CubeBindingSetAssetId, RenderContentProfile.BindingSetKindId).Key, true, uint.MaxValue, false, Vector3.One));
        _world.AddComponent(entity, new WorldTransform(Matrix4x4.CreateTranslation(x, 0, 0), TickNumber)); return entity;
    }
    public void Tick() => Tick(default);
    public void Tick(GameInputTick input)
    {
        input.Validate(); var world = _world ?? throw new InvalidOperationException("Load first.");
        if ((input.Buttons & GameButtons.Forward) != 0 && world.IsAlive(_first))
            world.SetComponent(_first, world.GetComponent<MeshRenderer>(_first) with { TintEnabled = true, TintLinear = new(.5f, 0, 0) });
        if ((input.Buttons & GameButtons.Backward) != 0 && world.IsAlive(_first))
            world.SetComponent(_first, world.GetComponent<MeshRenderer>(_first) with { TintEnabled = false });
        if ((input.Buttons & GameButtons.Left) != 0) { _grey = !_grey; ChangeContent(); }
        if ((input.Buttons & GameButtons.Right) != 0 && world.IsAlive(_first)) world.DestroyEntity(_first);
        if (input.InteractPressed) { _small = !_small; ChangeContent(); }
        TickNumber++;
        var entities = new List<EntityHandle>(); world.Query<WorldTransform>().ForEach((EntityHandle entity, in WorldTransform _) => entities.Add(entity));
        foreach (var entity in entities) world.SetComponent(entity, world.GetComponent<WorldTransform>(entity) with { Tick = TickNumber });
    }
    private void ChangeContent()
    {
        var builtin = BuiltinRenderContent.Create(_content.ProjectId, _content.BuildId, _content.Generation);
        var cube = (MeshDefinition)builtin.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId).Definition;
        var scale = _small ? .5f : 1f;
        var mesh = new MeshDefinition(cube.Vertices.Select(vertex => vertex * scale).ToArray(), cube.Indices, cube.Slots,
            new(new Vector3(-.5f * scale), new Vector3(.5f * scale)));
        var material = new OpaqueUnlitMaterialDefinition(new Vector3(_grey ? .5f : 1f));
        var meshKey = mesh.CreateKey(_content.ProjectId, RenderContentProfile.CubeAssetId);
        var materialKey = material.CreateKey(_content.ProjectId, RenderContentProfile.WhiteMaterialAssetId);
        var bindings = new MaterialBindingSetDefinition(meshKey, [new(RenderContentProfile.DefaultSlotId, materialKey)]);
        _content = new(_content.ProjectId, _content.BuildId, _content.Generation,
            [new(meshKey, mesh), new(materialKey, material), new(bindings.CreateKey(_content.ProjectId, RenderContentProfile.CubeBindingSetAssetId), bindings)]);
    }
    public RenderSceneSource ReadRenderScene() => new(_world ?? throw new InvalidOperationException("Load first."), _content,
        new(new Guid("458ddc79-8803-4dc0-b24b-9445695d980a")), Revision, RenderCameraSource.External(Matrix4x4.CreateTranslation(0, 0, 6), PerspectiveCamera.Default));

    public GameScenarioSnapshot? ReadScenarioObservation()
    {
        if (_world is not {} world || !world.IsAlive(_first)) return null;
        var renderer = world.GetComponent<MeshRenderer>(_first);
        return new(TickNumber, 1,
            [KeyValuePair.Create("first-cube.red", new GameScenarioNumber(renderer.TintEnabled ? renderer.TintLinear.X : 1, "linear-channel"))]);
    }

    public void Dispose() { _world?.Dispose(); _world = null; }
}
