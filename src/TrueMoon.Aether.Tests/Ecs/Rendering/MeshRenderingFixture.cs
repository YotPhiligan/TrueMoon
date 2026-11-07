using System.Numerics;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Tests.Ecs.Rendering;

internal static class MeshRenderingFixture
{
    internal static Guid Id(int value) => Guid.Parse($"17a54ad2-dd33-4708-bd1e-{value:000000000000}");
    internal static readonly Guid Project = Id(1);
    internal static readonly RenderContentHash Build = RenderContentHash.Parse(new string('a', 64));
    internal static readonly SceneId Scene = new(Id(2));
    internal static readonly EntityDefinitionId Definition = new(Id(3));
    internal static readonly EntityId Entity = new(Id(4));
    internal static readonly ComponentId Transform = new(Id(5));
    internal static readonly ComponentId Renderer = new(Id(6));
    internal static readonly ComponentId Camera = new(Id(7));
    internal static readonly ComponentId Appearance = new(Id(8));

    internal static RenderContentManifest Content() => BuiltinRenderContent.Create(Project, Build, Id(9));
    internal static RenderContentManifest ColoredContent(RenderContentManifest original)
    {
        var cube = original.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId);
        var material = new OpaqueUnlitMaterialDefinition(new(.5f, .25f, .75f));
        var key = material.CreateKey(Project, RenderContentProfile.WhiteMaterialAssetId);
        var binding = new MaterialBindingSetDefinition(cube.Key, [new(RenderContentProfile.DefaultSlotId, key)]);
        return new(Project, Build, Id(10), [cube, new(key, material), new(binding.CreateKey(Project, RenderContentProfile.CubeBindingSetAssetId), binding)]);
    }
    internal static ComponentRegistry Registry(RenderContentManifest content)
        => MeshRenderingComponents.Register(PrimitiveEcsComponents.Register(new()), content).Build();
    internal static SceneWorldLoader Loader(RenderContentManifest content, IFactory<World>? worlds = null, ComponentRegistry? registry = null)
        => new(registry ?? Registry(content), worlds ?? new Factory<World>(() => new()),
            new(new Factory<EntityResolver, SceneDocument>(source => new(source.Definitions, source.Schemas))));
    internal static SceneDocument Document(IEnumerable<KeyValuePair<string, PropertyValue>>? renderer = null,
        IEnumerable<KeyValuePair<string, PropertyValue>>? camera = null, bool includeCamera = false, bool appearance = false,
        bool transform = true)
    {
        var components = new List<ComponentData>();
        if (transform) components.Add(new(Transform, BuiltInComponents.TransformId, 1));
        components.Add(new(Renderer, MeshRenderingComponents.MeshRendererId, 1, renderer));
        if (includeCamera) components.Add(new(Camera, MeshRenderingComponents.CameraId, 1, camera));
        if (appearance) components.Add(new(Appearance, BuiltInComponents.AppearanceId, 1));
        return new(Scene, 17, [new(Entity, Definition)], [new(Definition, new(components))],
            BuiltInComponents.Schemas.Concat([PrimitiveEcsComponents.VelocitySchema]).Concat(MeshRenderingComponents.Schemas));
    }
    internal static KeyValuePair<string, PropertyValue> Number(string key, double value) => new(key, PropertyValue.From(value));
    internal static KeyValuePair<string, PropertyValue> Integer(string key, long value) => new(key, PropertyValue.From(value));
    internal static KeyValuePair<string, PropertyValue> Boolean(string key, bool value) => new(key, PropertyValue.From(value));
    internal static KeyValuePair<string, PropertyValue> Reference(string key, Guid value) => new(key, PropertyValue.From(value.ToString("D")));
}
