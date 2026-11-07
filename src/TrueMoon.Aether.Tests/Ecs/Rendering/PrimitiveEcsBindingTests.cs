using System.Numerics;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Motion;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Rendering;

public sealed class PrimitiveEcsBindingTests
{
    [Fact]
    public void Register_PreservesExactBuiltInCatalogAndAddsOnlyOptionalVelocitySchema()
    {
        var builder = new ComponentRegistryBuilder();
        Assert.Same(builder, PrimitiveEcsComponents.Register(builder));
        var registry = builder.Build();
        Assert.Equal(4, registry.Count);
        Assert.Equal(new[] { "6fc9fce0-9547-42f5-b4df-f05f306f1001", "6fc9fce0-9547-42f5-b4df-f05f306f1002", "6fc9fce0-9547-42f5-b4df-f05f306f1003" },
            BuiltInComponents.Schemas.Select(schema => schema.TypeId.Value.ToString("D")));
        foreach (var schema in BuiltInComponents.Schemas)
        {
            Assert.Same(schema, Assert.Single(registry.Schemas, actual => actual.TypeId == schema.TypeId));
            Assert.Equal(1, schema.Version);
            Assert.False(schema.AllowMultiple);
            Assert.All(schema.Properties.Values, property => Assert.False(property.IsNullable));
        }
        var velocity = Assert.Single(registry.Schemas, schema => schema.TypeId == PrimitiveEcsComponents.VelocityId);
        Assert.Equal(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1004"), velocity.TypeId.Value);
        Assert.Equal(1, velocity.Version);
        Assert.False(velocity.AllowMultiple);
        Assert.Equal(new[] { BuiltInComponents.TransformId }, velocity.RequiredTypes);
        Assert.Equal(new[] { "x", "y", "z" }, velocity.Properties.Keys.Order(StringComparer.Ordinal));
        Assert.All(velocity.Properties.Values, property =>
        {
            Assert.Equal(PropertyValueKind.Number, property.Kind);
            Assert.Equal(PropertyValue.From(0d), property.DefaultValue);
            Assert.False(property.IsNullable);
        });
        Assert.Throws<ArgumentNullException>(() => PrimitiveEcsComponents.Register(null!));
        Assert.Throws<ArgumentException>(() => PrimitiveEcsComponents.Register(builder));
        Assert.Equal(4, registry.Count);
    }

    [Fact]
    public void Load_MapsDefaultsToExactRuntimeTypesAndKeepsVelocityOptional()
    {
        var document = PrimitivePipelineFixture.Document();
        var definition = new EntityDefinition(PrimitivePipelineFixture.Definition,
            new([new(PrimitivePipelineFixture.Transform, BuiltInComponents.TransformId, 1),
                new(PrimitivePipelineFixture.Appearance, BuiltInComponents.AppearanceId, 1),
                new(PrimitivePipelineFixture.Interaction, BuiltInComponents.InteractionId, 1)]));
        document = new(document.Id, document.Revision, document.Entities, [definition], document.Schemas);
        using var scene = PrimitivePipelineFixture.Loader().Load(document);
        var entity = scene.Entities[PrimitivePipelineFixture.Entity(1)];
        Assert.Equal(new LocalTransform(Vector3.Zero, Quaternion.Identity, Vector3.One), scene.World.GetComponent<LocalTransform>(entity));
        Assert.Equal(new PrimitiveAppearance(Vector3.One, true), scene.World.GetComponent<PrimitiveAppearance>(entity));
        Assert.True(scene.World.GetComponent<InteractionEnabled>(entity).Enabled);
        Assert.False(scene.World.HasComponent<LinearVelocity>(entity));
        Assert.False(scene.World.HasComponent<WorldTransform>(entity));
        Assert.False(scene.World.HasComponent<TransformParent>(entity));
    }

    [Fact]
    public void Load_InheritanceAndInstanceOverridesMapEulerRadiansColorVelocityAndInteraction()
    {
        var variant = new EntityDefinitionId(PrimitivePipelineFixture.Id(7));
        var components = new ComponentData[]
        {
            new(PrimitivePipelineFixture.Transform, BuiltInComponents.TransformId, 1,
                [PrimitivePipelineFixture.Number("x", 4), PrimitivePipelineFixture.Number("yaw", Math.PI / 2),
                 PrimitivePipelineFixture.Number("pitch", Math.PI / 2), PrimitivePipelineFixture.Number("roll", Math.PI / 4), PrimitivePipelineFixture.Number("sx", 2)]),
            new(PrimitivePipelineFixture.Appearance, BuiltInComponents.AppearanceId, 1, [PrimitivePipelineFixture.Number("r", .25)]),
            new(PrimitivePipelineFixture.Interaction, BuiltInComponents.InteractionId, 1),
            new(PrimitivePipelineFixture.Velocity, PrimitiveEcsComponents.VelocityId, 1, [PrimitivePipelineFixture.Number("z", -3)])
        };
        var source = new SceneDocument(PrimitivePipelineFixture.SceneId, 31,
            [new(PrimitivePipelineFixture.Entity(1), variant, new(propertyOverrides:
                [new(PrimitivePipelineFixture.Transform, "z", PropertyValue.From(7d)),
                 new(PrimitivePipelineFixture.Appearance, "b", PropertyValue.From(.5d)),
                 new(PrimitivePipelineFixture.Interaction, "enabled", PropertyValue.From(false))]))],
            [new(PrimitivePipelineFixture.Definition, new(components)),
             new(variant, new(propertyOverrides: [new(PrimitivePipelineFixture.Transform, "y", PropertyValue.From(5d))]), PrimitivePipelineFixture.Definition)],
            BuiltInComponents.Schemas.Concat([PrimitiveEcsComponents.VelocitySchema]));
        using var scene = PrimitivePipelineFixture.Loader().Load(source);
        var entity = scene.Entities[PrimitivePipelineFixture.Entity(1)];
        var local = scene.World.GetComponent<LocalTransform>(entity);
        Assert.Equal(new Vector3(4, 5, 7), local.Position);
        Assert.Equal(new Vector3(2, 1, 1), local.Scale);
        var c = MathF.Sqrt(.5f);
        PrimitivePipelineFixture.Near(new(c, 0, -c), Vector3.Transform(Vector3.UnitX, local.Rotation));
        PrimitivePipelineFixture.Near(new(c, 0, c), Vector3.Transform(Vector3.UnitY, local.Rotation));
        PrimitivePipelineFixture.Near(-Vector3.UnitY, Vector3.Transform(Vector3.UnitZ, local.Rotation));
        Assert.Equal(new PrimitiveAppearance(new(.25f, 1, .5f)), scene.World.GetComponent<PrimitiveAppearance>(entity));
        Assert.False(scene.World.GetComponent<InteractionEnabled>(entity).Enabled);
        Assert.Equal(new LinearVelocity(new(0, 0, -3)), scene.World.GetComponent<LinearVelocity>(entity));
        Assert.Equal(31L, scene.Scene.Revision);
    }

    [Theory]
    [InlineData("x", double.MaxValue)] [InlineData("pitch", double.MaxValue)]
    [InlineData("sx", 0)] [InlineData("sy", -1)] [InlineData("sz", double.Epsilon)]
    public void Load_InvalidTransformIsRejectedBeforeAnyWorldCreation(string property, double value)
    {
        var calls = 0;
        var loader = PrimitivePipelineFixture.Loader(worlds: new Factory<World>(() => { calls++; return new(); }));
        var error = Assert.Throws<SceneWorldLoadException>(() => loader.Load(PrimitivePipelineFixture.Document(transform: [PrimitivePipelineFixture.Number(property, value)])));
        Assert.Equal(SceneWorldLoadError.InvalidValue, error.Error);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(-.001)] [InlineData(1.001)] [InlineData(double.MaxValue)]
    public void Load_InvalidAppearanceIsRejectedBeforeAnyWorldCreation(double color)
    {
        var calls = 0;
        var loader = PrimitivePipelineFixture.Loader(worlds: new Factory<World>(() => { calls++; return new(); }));
        var error = Assert.Throws<SceneWorldLoadException>(() => loader.Load(PrimitivePipelineFixture.Document(color: [PrimitivePipelineFixture.Number("g", color)])));
        Assert.Equal(SceneWorldLoadError.InvalidValue, error.Error);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Load_VelocityRequiresTransformAndUsesExactSchemaVersionAndMetadata()
    {
        var calls = 0;
        var loader = PrimitivePipelineFixture.Loader(worlds: new Factory<World>(() => { calls++; return new(); }));
        SceneDocument Source(ComponentSchema schema, int version, bool transform) => new(PrimitivePipelineFixture.SceneId, 0,
            [new(PrimitivePipelineFixture.Entity(1), PrimitivePipelineFixture.Definition)],
            [new(PrimitivePipelineFixture.Definition, new(transform
                ? [new(PrimitivePipelineFixture.Transform, BuiltInComponents.TransformId, 1), new(PrimitivePipelineFixture.Velocity, schema.TypeId, version)]
                : [new(PrimitivePipelineFixture.Velocity, schema.TypeId, version)]))], BuiltInComponents.Schemas.Concat([schema]));
        Assert.Equal(SceneWorldLoadError.InvalidScene, Assert.Throws<SceneWorldLoadException>(() => loader.Load(Source(PrimitiveEcsComponents.VelocitySchema, 1, false))).Error);
        var version2 = new ComponentSchema(PrimitiveEcsComponents.VelocityId, 2, PrimitiveEcsComponents.VelocitySchema.Properties.Values,
            requiredTypes: [BuiltInComponents.TransformId]);
        Assert.Equal(SceneWorldLoadError.UnsupportedSchemaVersion, Assert.Throws<SceneWorldLoadException>(() => loader.Load(Source(version2, 2, true))).Error);
        var drift = new ComponentSchema(PrimitiveEcsComponents.VelocityId, 1,
            [new("x", PropertyValueKind.Number, PropertyValue.From(1d)), new("y", PropertyValueKind.Number, PropertyValue.From(0d)), new("z", PropertyValueKind.Number, PropertyValue.From(0d))],
            requiredTypes: [BuiltInComponents.TransformId]);
        Assert.Equal(SceneWorldLoadError.SchemaMismatch, Assert.Throws<SceneWorldLoadException>(() => loader.Load(Source(drift, 1, true))).Error);
        Assert.Equal(0, calls);
        using var scene = loader.Load(Source(PrimitiveEcsComponents.VelocitySchema, 1, true));
        Assert.Equal(new LinearVelocity(Vector3.Zero), scene.World.GetComponent<LinearVelocity>(Assert.Single(scene.Entities.Values)));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Load_InvalidVelocityAndNullBuiltInValueFailBeforeWorldCreation(bool explicitNull)
    {
        var source = PrimitivePipelineFixture.Document(velocity: true);
        var components = source.Definitions[0].Changes.AddedComponents.Select(component =>
            component.Id == (explicitNull ? PrimitivePipelineFixture.Transform : PrimitivePipelineFixture.Velocity)
                ? new ComponentData(component.Id, component.TypeId, 1,
                    [new("x", explicitNull ? PropertyValue.Null : PropertyValue.From(double.MaxValue))]) : component);
        source = new(source.Id, source.Revision, source.Entities, [new(PrimitivePipelineFixture.Definition, new(components))], source.Schemas);
        var calls = 0;
        var loader = PrimitivePipelineFixture.Loader(worlds: new Factory<World>(() => { calls++; return new(); }));
        Assert.Equal(explicitNull ? SceneWorldLoadError.InvalidScene : SceneWorldLoadError.InvalidValue,
            Assert.Throws<SceneWorldLoadException>(() => loader.Load(source)).Error);
        Assert.Equal(0, calls);
    }
}
