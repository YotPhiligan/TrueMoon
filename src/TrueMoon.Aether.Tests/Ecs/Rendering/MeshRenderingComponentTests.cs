using System.Numerics;
using System.Runtime.CompilerServices;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Rendering;

public sealed class MeshRenderingComponentTests
{
    [Fact]
    public void Schemas_UseReservedIdsExactDefaultsAndRequiredTransformWithoutChangingLegacyCatalog()
    {
        Assert.Equal(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1101"), MeshRenderingComponents.MeshRendererId.Value);
        Assert.Equal(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1102"), MeshRenderingComponents.CameraId.Value);
        Assert.Equal(new[] { MeshRenderingComponents.MeshRendererId, MeshRenderingComponents.CameraId }, MeshRenderingComponents.Schemas.Select(schema => schema.TypeId));
        Assert.Equal(3, BuiltInComponents.Schemas.Length);
        Assert.Equal(new[] { "6fc9fce0-9547-42f5-b4df-f05f306f1001", "6fc9fce0-9547-42f5-b4df-f05f306f1002", "6fc9fce0-9547-42f5-b4df-f05f306f1003" },
            BuiltInComponents.Schemas.Select(schema => schema.TypeId.Value.ToString("D")));
        foreach (var schema in MeshRenderingComponents.Schemas)
        {
            Assert.Equal(1, schema.Version);
            Assert.False(schema.AllowMultiple);
            Assert.Equal(new[] { BuiltInComponents.TransformId }, schema.RequiredTypes);
            Assert.All(schema.Properties.Values, property => Assert.False(property.IsNullable));
        }
        AssertSchema(MeshRenderingComponents.MeshRendererSchema,
            ("mesh", PropertyValueKind.String, PropertyValue.From("6fc9fce0-9547-42f5-b4df-f05f306f1401")),
            ("bindings", PropertyValueKind.String, PropertyValue.From("6fc9fce0-9547-42f5-b4df-f05f306f1403")),
            ("enabled", PropertyValueKind.Boolean, PropertyValue.From(true)),
            ("layers", PropertyValueKind.Integer, PropertyValue.From(4294967295L)),
            ("tintEnabled", PropertyValueKind.Boolean, PropertyValue.From(false)),
            ("tintR", PropertyValueKind.Number, PropertyValue.From(1d)),
            ("tintG", PropertyValueKind.Number, PropertyValue.From(1d)),
            ("tintB", PropertyValueKind.Number, PropertyValue.From(1d)));
        AssertSchema(MeshRenderingComponents.CameraSchema,
            ("fov", PropertyValueKind.Number, PropertyValue.From(Math.PI / 3)),
            ("near", PropertyValueKind.Number, PropertyValue.From(.1d)),
            ("far", PropertyValueKind.Number, PropertyValue.From(100d)),
            ("layers", PropertyValueKind.Integer, PropertyValue.From(4294967295L)),
            ("clearR", PropertyValueKind.Number, PropertyValue.From(.04d)),
            ("clearG", PropertyValueKind.Number, PropertyValue.From(.06d)),
            ("clearB", PropertyValueKind.Number, PropertyValue.From(.10d)));
    }

    [Fact]
    public void Register_AddsOnlyTwoExplicitBindingsAndRequiresAnExplicitTransformRegistration()
    {
        var content = MeshRenderingFixture.Content();
        var builder = new ComponentRegistryBuilder();
        Assert.Same(builder, MeshRenderingComponents.Register(builder, content));
        Assert.Equal(2, builder.Build().Count);
        Assert.Equal(6, MeshRenderingFixture.Registry(content).Count);
        Assert.Throws<ArgumentNullException>(() => MeshRenderingComponents.Register(null!, content));
        Assert.Throws<ArgumentNullException>(() => MeshRenderingComponents.Register(new(), null!));
        Assert.Throws<ArgumentException>(() => MeshRenderingComponents.Register(builder, content));
        Assert.Equal(2, builder.Build().Count);
    }

    [Fact]
    public void Load_DefaultsProducePinnedUnmanagedComponentsWithoutNativeOrDerivedRoles()
    {
        var content = MeshRenderingFixture.Content();
        using var scene = MeshRenderingFixture.Loader(content).Load(MeshRenderingFixture.Document(includeCamera: true));
        var entity = scene.Entities[MeshRenderingFixture.Entity];
        var renderer = scene.World.GetComponent<MeshRenderer>(entity);
        Assert.Equal(content.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId).Key, renderer.Mesh);
        Assert.Equal(content.Resolve(RenderContentProfile.CubeBindingSetAssetId, RenderContentProfile.BindingSetKindId).Key, renderer.Bindings);
        Assert.True(renderer.Enabled);
        Assert.Equal(uint.MaxValue, renderer.Layers);
        Assert.False(renderer.TintEnabled);
        Assert.Equal(Vector3.One, renderer.TintLinear);
        Assert.Equal(Vector3.One, renderer.EffectiveTint);
        Assert.Equal(PerspectiveCamera.Default, scene.World.GetComponent<PerspectiveCamera>(entity));
        Assert.Equal(new LocalTransform(Vector3.Zero, Quaternion.Identity, Vector3.One), scene.World.GetComponent<LocalTransform>(entity));
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<MeshRenderer>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<PerspectiveCamera>());
        Assert.False(scene.World.HasComponent<WorldTransform>(entity));
        Assert.False(scene.World.HasComponent<PrimitiveAppearance>(entity));
        Assert.Equal(3, scene.World.ComponentTypeCount);
        Assert.Equal(17L, scene.Scene.Revision);
        renderer.Validate(content);
    }

    [Fact]
    public void Load_ResolvesInheritedAndInstanceOverridesWithUInt32BoundaryAndLinearTintIsolation()
    {
        var content = MeshRenderingFixture.Content();
        var source = MeshRenderingFixture.Document(renderer: [MeshRenderingFixture.Boolean("tintEnabled", true), MeshRenderingFixture.Number("tintR", .25)],
            camera: [MeshRenderingFixture.Number("near", 1), MeshRenderingFixture.Number("far", 11)], includeCamera: true);
        var variant = new EntityDefinitionId(MeshRenderingFixture.Id(20));
        source = new(source.Id, source.Revision,
            [new(MeshRenderingFixture.Entity, variant, new(propertyOverrides:
                [new(MeshRenderingFixture.Renderer, "enabled", PropertyValue.From(false)),
                    new(MeshRenderingFixture.Renderer, "layers", PropertyValue.From(0L)),
                    new(MeshRenderingFixture.Renderer, "tintB", PropertyValue.From(.75d)),
                    new(MeshRenderingFixture.Camera, "layers", PropertyValue.From(4294967295L)),
                    new(MeshRenderingFixture.Camera, "clearG", PropertyValue.From(.5d))]))],
            source.Definitions.Concat([new EntityDefinition(variant, new(propertyOverrides:
                [new(MeshRenderingFixture.Renderer, "tintG", PropertyValue.From(.5d))]), MeshRenderingFixture.Definition)]), source.Schemas);
        using var scene = MeshRenderingFixture.Loader(content).Load(source);
        var entity = scene.Entities[MeshRenderingFixture.Entity];
        var renderer = scene.World.GetComponent<MeshRenderer>(entity);
        Assert.False(renderer.Enabled);
        Assert.Equal(0u, renderer.Layers);
        Assert.True(renderer.TintEnabled);
        Assert.Equal(new Vector3(.25f, .5f, .75f), renderer.EffectiveTint);
        Assert.Equal(Vector3.One, (renderer with { TintEnabled = false }).EffectiveTint);
        var camera = scene.World.GetComponent<PerspectiveCamera>(entity);
        Assert.Equal(1f, camera.Near);
        Assert.Equal(11f, camera.Far);
        Assert.Equal(uint.MaxValue, camera.Layers);
        Assert.Equal(new Vector3(.04f, .5f, .10f), camera.ClearColorSrgb);
        var material = Assert.IsType<OpaqueUnlitMaterialDefinition>(content.Resolve(RenderContentProfile.WhiteMaterialAssetId, RenderContentProfile.MaterialKindId).Definition);
        Assert.Equal(Vector3.One, material.BaseColorLinear);
        Assert.Equal(PropertyValue.From(1d), Assert.Single(source.Schemas, schema => schema.TypeId == MeshRenderingComponents.MeshRendererId).Properties["tintB"].DefaultValue);
    }

    [Theory]
    [InlineData("tintR", -double.Epsilon)] [InlineData("tintG", 1.0000000000000002)] [InlineData("tintB", double.MaxValue)]
    public void Load_ValidatesDisabledMaskedAndTintOffValuesBeforeWorldCreation(string property, double value)
    {
        var calls = 0;
        var content = MeshRenderingFixture.Content();
        var source = MeshRenderingFixture.Document(renderer:
            [MeshRenderingFixture.Boolean("enabled", false), MeshRenderingFixture.Integer("layers", 0),
                MeshRenderingFixture.Boolean("tintEnabled", false), MeshRenderingFixture.Number(property, value)]);
        var error = Assert.Throws<SceneWorldLoadException>(() => MeshRenderingFixture.Loader(content,
            new Factory<World>(() => { calls++; return new(); })).Load(source));
        Assert.Equal(SceneWorldLoadError.InvalidValue, error.Error);
        Assert.Equal(MeshRenderingFixture.Renderer, error.ComponentId);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false, -1L)] [InlineData(false, 4294967296L)]
    [InlineData(true, -1L)] [InlineData(true, 4294967296L)]
    public void Load_RejectsBothRendererAndCameraMaskOverflowBeforeWorldCreation(bool camera, long value)
    {
        var calls = 0;
        var content = MeshRenderingFixture.Content();
        var source = camera ? MeshRenderingFixture.Document(camera: [MeshRenderingFixture.Integer("layers", value)], includeCamera: true)
            : MeshRenderingFixture.Document(renderer: [MeshRenderingFixture.Integer("layers", value)]);
        var error = Assert.Throws<SceneWorldLoadException>(() => MeshRenderingFixture.Loader(content,
            new Factory<World>(() => { calls++; return new(); })).Load(source));
        Assert.Equal(SceneWorldLoadError.InvalidValue, error.Error);
        Assert.Equal(camera ? MeshRenderingFixture.Camera : MeshRenderingFixture.Renderer, error.ComponentId);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("fov-zero")] [InlineData("fov-negative")] [InlineData("fov-pi")]
    [InlineData("fov-rounds-pi")] [InlineData("near-underflow")] [InlineData("far-collapses")]
    [InlineData("far-not-greater")] [InlineData("far-overflow")]
    [InlineData("clear-negative")] [InlineData("clear-rounds-one")]
    public void Load_RejectsCameraRangesBeforeAndAfterSingleConversion(string kind)
    {
        var values = kind switch
        {
            "fov-zero" => new[] { MeshRenderingFixture.Number("fov", 0) },
            "fov-negative" => [MeshRenderingFixture.Number("fov", -1)],
            "fov-pi" => [MeshRenderingFixture.Number("fov", Math.PI)],
            "fov-rounds-pi" => [MeshRenderingFixture.Number("fov", Math.BitDecrement(Math.PI))],
            "near-underflow" => [MeshRenderingFixture.Number("near", double.Epsilon)],
            "far-collapses" => [MeshRenderingFixture.Number("near", 1), MeshRenderingFixture.Number("far", Math.BitIncrement(1d))],
            "far-not-greater" => [MeshRenderingFixture.Number("near", 2), MeshRenderingFixture.Number("far", 1)],
            "far-overflow" => [MeshRenderingFixture.Number("far", double.MaxValue)],
            "clear-negative" => [MeshRenderingFixture.Number("clearB", -double.Epsilon)],
            _ => [MeshRenderingFixture.Number("clearR", Math.BitIncrement(1d))]
        };
        var calls = 0;
        var content = MeshRenderingFixture.Content();
        var error = Assert.Throws<SceneWorldLoadException>(() => MeshRenderingFixture.Loader(content,
            new Factory<World>(() => { calls++; return new(); })).Load(MeshRenderingFixture.Document(camera: values, includeCamera: true)));
        Assert.Equal(SceneWorldLoadError.InvalidValue, error.Error);
        Assert.Equal(MeshRenderingFixture.Camera, error.ComponentId);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Load_AcceptsNearestValidSingleProjectionEndpointsAndPreservesZeroCameraMask(bool maximumFov)
    {
        var fov = maximumFov ? float.BitDecrement(MathF.PI) : float.Epsilon;
        var near = float.Epsilon;
        var far = float.BitIncrement(near);
        var content = MeshRenderingFixture.Content();
        var source = MeshRenderingFixture.Document(camera:
            [MeshRenderingFixture.Number("fov", fov), MeshRenderingFixture.Number("near", near),
                MeshRenderingFixture.Number("far", far), MeshRenderingFixture.Integer("layers", 0),
                MeshRenderingFixture.Number("clearR", 0), MeshRenderingFixture.Number("clearB", 1)], includeCamera: true);
        using var scene = MeshRenderingFixture.Loader(content).Load(source);
        var camera = scene.World.GetComponent<PerspectiveCamera>(scene.Entities[MeshRenderingFixture.Entity]);
        Assert.Equal(fov, camera.FieldOfView);
        Assert.Equal(near, camera.Near);
        Assert.Equal(far, camera.Far);
        Assert.Equal(0u, camera.Layers);
        Assert.Equal(new Vector3(0, .06f, 1), camera.ClearColorSrgb);
    }

    [Theory]
    [InlineData("mesh", "missing", SceneWorldLoadError.InvalidReference)]
    [InlineData("bindings", "missing", SceneWorldLoadError.InvalidReference)]
    [InlineData("mesh", "wrong-kind", SceneWorldLoadError.InvalidValue)]
    [InlineData("bindings", "wrong-kind", SceneWorldLoadError.InvalidValue)]
    [InlineData("mesh", "empty", SceneWorldLoadError.InvalidReference)]
    public void Load_RejectsAssetSyntaxExistenceAndKindEvenWhenRendererIsInvisible(string property, string kind, SceneWorldLoadError expected)
    {
        var content = MeshRenderingFixture.Content();
        var id = kind switch { "missing" => MeshRenderingFixture.Id(101), "empty" => Guid.Empty, _ => RenderContentProfile.WhiteMaterialAssetId };
        var calls = 0;
        var source = MeshRenderingFixture.Document(renderer:
            [MeshRenderingFixture.Reference(property, id), MeshRenderingFixture.Boolean("enabled", false), MeshRenderingFixture.Integer("layers", 0)]);
        var error = Assert.Throws<SceneWorldLoadException>(() => MeshRenderingFixture.Loader(content,
            new Factory<World>(() => { calls++; return new(); })).Load(source));
        Assert.Equal(expected, error.Error);
        Assert.Equal(0, calls);
        if (kind == "wrong-kind") Assert.Equal(RenderContentError.AssetKindMismatch, Assert.IsType<RenderContentException>(error.InnerException).Error);
    }

    [Fact]
    public void Load_ReplacingGenericAssetValidatorCannotBypassFrozenTypedResolution()
    {
        var content = MeshRenderingFixture.Content();
        var builder = MeshRenderingComponents.Register(PrimitiveEcsComponents.Register(new()), content);
        builder.WithAssetValidator((_, _) => { });
        var calls = 0;
        var error = Assert.Throws<SceneWorldLoadException>(() => MeshRenderingFixture.Loader(content,
            new Factory<World>(() => { calls++; return new(); }), builder.Build()).Load(
                MeshRenderingFixture.Document(renderer: [MeshRenderingFixture.Reference("mesh", MeshRenderingFixture.Id(102))])));
        Assert.Equal(SceneWorldLoadError.InvalidValue, error.Error);
        Assert.Equal(RenderContentError.MissingAsset, Assert.IsType<RenderContentException>(error.InnerException).Error);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Load_RejectsLegacyAppearanceAmbiguityAndMissingTransformWithoutAllocatingWorld(bool missingTransform)
    {
        var content = MeshRenderingFixture.Content();
        var calls = 0;
        var source = missingTransform ? MeshRenderingFixture.Document(transform: false) : MeshRenderingFixture.Document(appearance: true);
        var error = Assert.Throws<SceneWorldLoadException>(() => MeshRenderingFixture.Loader(content,
            new Factory<World>(() => { calls++; return new(); })).Load(source));
        Assert.Equal(missingTransform ? SceneWorldLoadError.InvalidScene : SceneWorldLoadError.InvalidValue, error.Error);
        if (!missingTransform) Assert.Equal(RenderContentError.InvalidComponent, Assert.IsType<RenderContentException>(error.InnerException).Error);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Register_CapturesEachFrozenManifestAndNeverReadsAnotherRegistrysLatestRevision()
    {
        var first = MeshRenderingFixture.Content();
        var firstRegistry = MeshRenderingFixture.Registry(first);
        var second = MeshRenderingFixture.ColoredContent(first);
        var secondRegistry = MeshRenderingFixture.Registry(second);
        using var firstScene = MeshRenderingFixture.Loader(first, registry: firstRegistry).Load(MeshRenderingFixture.Document());
        using var secondScene = MeshRenderingFixture.Loader(second, registry: secondRegistry).Load(MeshRenderingFixture.Document());
        var firstValue = firstScene.World.GetComponent<MeshRenderer>(firstScene.Entities[MeshRenderingFixture.Entity]);
        var secondValue = secondScene.World.GetComponent<MeshRenderer>(secondScene.Entities[MeshRenderingFixture.Entity]);
        Assert.Equal(first.Resolve(RenderContentProfile.CubeBindingSetAssetId, RenderContentProfile.BindingSetKindId).Key, firstValue.Bindings);
        Assert.Equal(second.Resolve(RenderContentProfile.CubeBindingSetAssetId, RenderContentProfile.BindingSetKindId).Key, secondValue.Bindings);
        Assert.NotEqual(firstValue.Bindings.Revision, secondValue.Bindings.Revision);
        Assert.Equal(firstValue.Mesh, secondValue.Mesh);
        Assert.NotEqual(firstScene.World.Id, secondScene.World.Id);
        Assert.Equal(17L, firstScene.Scene.Revision);
        Assert.Equal(17L, secondScene.Scene.Revision);
    }

    [Theory]
    [InlineData("default", RenderContentError.InvalidIdentity)]
    [InlineData("foreign-project", RenderContentError.InvalidIdentity)]
    [InlineData("wrong-binding-kind", RenderContentError.AssetKindMismatch)]
    [InlineData("stale-mesh", RenderContentError.RevisionMismatch)]
    [InlineData("stale-bindings", RenderContentError.RevisionMismatch)]
    [InlineData("disabled-tint", RenderContentError.InvalidMaterial)]
    [InlineData("missing-mesh", RenderContentError.MissingAsset)]
    public void Validate_RejectsInvalidPinnedIdentitiesAndDisabledTint(string kind, RenderContentError expected)
    {
        var content = MeshRenderingFixture.Content();
        var value = ValidRenderer(content) with { Enabled = false, Layers = 0, TintEnabled = false };
        value = kind switch
        {
            "default" => default,
            "foreign-project" => value with { Mesh = value.Mesh with { ProjectId = MeshRenderingFixture.Id(201) } },
            "wrong-binding-kind" => value with { Bindings = value.Mesh },
            "stale-mesh" => value with { Mesh = value.Mesh with { Revision = RenderContentHash.Parse(new string('b', 64)) } },
            "stale-bindings" => value with { Bindings = value.Bindings with { Revision = RenderContentHash.Parse(new string('b', 64)) } },
            "disabled-tint" => value with { TintLinear = new(0, -float.Epsilon, 0) },
            _ => value with { Mesh = value.Mesh with { AssetId = MeshRenderingFixture.Id(202) } }
        };
        Assert.Equal(expected, Assert.Throws<RenderContentException>(() => value.Validate(content)).Error);
        if (kind == "disabled-tint")
            Assert.Equal(RenderContentError.InvalidMaterial, Assert.Throws<RenderContentException>(() => value.EffectiveTint).Error);
    }

    [Fact]
    public void Validate_RequiresBindingsForThatExactMeshAndResetDoesNotMutateAnotherInstance()
    {
        var content = MeshRenderingFixture.Content();
        var original = content.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId);
        var otherMesh = original.Definition.CreateKey(MeshRenderingFixture.Project, MeshRenderingFixture.Id(203));
        var expanded = new RenderContentManifest(content.ProjectId, content.BuildId, MeshRenderingFixture.Id(204),
            content.Entries.Add(new(otherMesh, original.Definition)));
        var first = ValidRenderer(content) with { TintEnabled = true, TintLinear = new(.2f, .3f, .4f) };
        var second = first with { TintLinear = new(.8f, .9f, 1) };
        first.Validate(expanded); second.Validate(expanded);
        Assert.Equal(RenderContentError.InvalidBinding,
            Assert.Throws<RenderContentException>(() => (first with { Mesh = otherMesh }).Validate(expanded)).Error);
        Assert.Equal(new Vector3(.2f, .3f, .4f), first.EffectiveTint);
        Assert.Equal(Vector3.One, (first with { TintEnabled = false }).EffectiveTint);
        Assert.Equal(new Vector3(.8f, .9f, 1), second.EffectiveTint);
        Assert.Equal(first.Bindings, second.Bindings);
        Assert.Equal(Vector3.One, Assert.IsType<OpaqueUnlitMaterialDefinition>(content.Resolve(RenderContentProfile.WhiteMaterialAssetId, RenderContentProfile.MaterialKindId).Definition).BaseColorSrgb);
        Assert.Throws<ArgumentNullException>(() => first.Validate(null!));
    }

    private static MeshRenderer ValidRenderer(RenderContentManifest content)
        => new(content.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId).Key,
            content.Resolve(RenderContentProfile.CubeBindingSetAssetId, RenderContentProfile.BindingSetKindId).Key,
            true, uint.MaxValue, false, Vector3.One);

    private static void AssertSchema(ComponentSchema schema, params (string Key, PropertyValueKind Kind, PropertyValue Value)[] expected)
    {
        Assert.Equal(expected.Length, schema.Properties.Count);
        Assert.Equal(expected.Select(item => item.Key).Order(StringComparer.Ordinal), schema.Properties.Keys.Order(StringComparer.Ordinal));
        foreach (var item in expected)
        {
            Assert.Equal(item.Kind, schema.Properties[item.Key].Kind);
            Assert.Equal(item.Value, schema.Properties[item.Key].DefaultValue);
        }
    }
}
