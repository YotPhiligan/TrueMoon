using System.Numerics;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Tests.Scenes;

public sealed class BuiltInSceneTests
{
    [Fact]
    public void StableRoles_DefaultsAndDependenciesAreExplicit()
    {
        Assert.Equal(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1001"), BuiltInComponents.TransformId.Value);
        Assert.Equal(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1002"), BuiltInComponents.AppearanceId.Value);
        Assert.Equal(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1003"), BuiltInComponents.InteractionId.Value);
        Assert.All(BuiltInComponents.Schemas, s => { Assert.Equal(1, s.Version); Assert.False(s.AllowMultiple); });
        var result = Project([Entity(1)], [Definition()]).CopyEntities(); var entity = Assert.Single(result);
        Assert.Equal(Matrix4x4.Identity, entity.Primitive!.ToPrimitive().World);
        Assert.Equal(Vector3.One, entity.Primitive.ToPrimitive().Color); Assert.True(entity.Interactive);
        Assert.Equal("00000001-0000-0000-0000-000000000000", entity.Id);
    }

    [Fact]
    public void ParentScaleRotationTranslation_ComposesInOrderAndRetainsShear()
    {
        var parent = Definition(1, new Dictionary<string, double> { ["x"] = 10, ["sx"] = 2, ["sy"] = 3, ["sz"] = 4, ["yaw"] = Math.PI / 2 });
        var child = Definition(2, new Dictionary<string, double> { ["x"] = 1, ["y"] = 2, ["z"] = 3, ["yaw"] = Math.PI / 4 });
        var scene = Project([Entity(2, 2, 1), Entity(1)], [child, parent]);
        var entities = scene.CopyEntities(); Assert.Equal(new[] { E(1).Value.ToString("D"), E(2).Value.ToString("D") }, entities.Select(e => e.Id));
        var second = entities[1]; Assert.Equal(22, second.X, 4); Assert.Equal(6, second.Y, 4); Assert.Equal(-2, second.Z, 4);
        var world = second.Primitive!.ToPrimitive().World;
        var x = Vector3.TransformNormal(Vector3.UnitX, world); var z = Vector3.TransformNormal(Vector3.UnitZ, world);
        Assert.Equal(-6, Vector3.Dot(x, z), 4); // Nonuniform parent scaling creates shear; no lossy SRT decomposition.
        Assert.Equal(new Vector3(22, 6, -2), new Vector3(MathF.Round(world.M41, 4), world.M42, MathF.Round(world.M43, 4)));
        Assert.Equal(7, scene.Revision);
    }

    [Fact]
    public void VariantOverridesAndComponentRemoval_DriveActualDrawAndInteractionData()
    {
        var variant = new EntityDefinition(D(2), new(propertyOverrides:
            [new(Appearance, "r", PropertyValue.From(.25)), new(Transform, "x", PropertyValue.From(4d))], removedComponents: [Interaction]), D(1), 2);
        var value = Assert.Single(Project([Entity(1, 2)], [Definition(), variant]).CopyEntities());
        Assert.Equal(4, value.X); Assert.False(value.Interactive); Assert.Equal(new(.25f, 1, 1), value.Primitive!.ToPrimitive().Color);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InteractionEnabledAndBoundaryColors_AreNotReplacedByDefaults(bool enabled)
    {
        var instance = new EntityInstance(E(1), D(1), new(propertyOverrides:
            [new(Interaction, "enabled", PropertyValue.From(enabled))]));
        var value = Assert.Single(Project([instance], [Definition(color: new() { ["r"] = 0, ["g"] = 1, ["b"] = 0 })]).CopyEntities());
        Assert.Equal(enabled, value.Interactive); Assert.Equal(Vector3.UnitY, value.Primitive!.ToPrimitive().Color);
    }

    [Fact]
    public void MissingRoles_AreIdentityGroupsAndNonRenderingMarkers()
    {
        var group = new EntityDefinition(D(1), EntityPatch.Empty);
        var marker = new EntityDefinition(D(2), new(addedComponents: [new(Transform, BuiltInComponents.TransformId, 1)]));
        var entities = Project([Entity(1), Entity(2, 2, 1)], [group, marker]).CopyEntities();
        Assert.All(entities, e => { Assert.Null(e.Primitive); Assert.False(e.Interactive); Assert.Equal(0, e.X); });
    }

    [Theory]
    [InlineData("sx", 0)] [InlineData("sy", -1)] [InlineData("sz", 1e-100)] [InlineData("x", double.MaxValue)]
    [InlineData("r", -.001)] [InlineData("g", 1.001)] [InlineData("b", double.MaxValue)]
    public void InvalidGeometryOrMaterial_FailsAtomicallyWithEntityPath(string key, double value)
    {
        var definition = key is "r" or "g" or "b" ? Definition(color: new Dictionary<string, double> { [key] = value }) :
            Definition(transform: new Dictionary<string, double> { [key] = value });
        var source = Resolve([Entity(1)], [definition]);
        var error = Assert.Throws<SceneResolutionException>(() => PrimitiveSceneAdapter.Project(source));
        Assert.Equal(SceneResolutionError.ProjectionFailed, error.Code); Assert.Equal(E(1), error.EntityId);
        Assert.EndsWith($"/entities/{E(1).Value:D}/primitive", error.Path); Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Single(source.Entities); Assert.Single(Project([Entity(1)], [Definition()]).CopyEntities());
    }

    [Fact]
    public void WorldMultiplicationOverflow_IsRejectedEvenWhenLocalTransformsAreFinite()
    {
        var definition = Definition(transform: new Dictionary<string, double> { ["x"] = float.MaxValue });
        var error = Assert.Throws<SceneResolutionException>(() => Project([Entity(1), Entity(2, 1, 1)], [definition]));
        Assert.Equal(E(2), error.EntityId); Assert.IsType<InvalidDataException>(error.InnerException);
    }

    [Theory]
    [InlineData("version")] [InlineData("default")] [InlineData("multiplicity")] [InlineData("dependency")]
    public void BuiltInSchemaChanges_RequireExplicitMigration(string change)
    {
        var schemas = BuiltInComponents.Schemas.ToArray(); var original = schemas[1];
        schemas[1] = new(original.TypeId, change == "version" ? 2 : 1,
            change == "default" ? [new("r", PropertyValueKind.Number, PropertyValue.From(.5)), .. original.Properties.Values.Where(p => p.Id != "r")] : original.Properties.Values,
            change == "multiplicity", change == "dependency" ? [] : original.RequiredTypes);
        var definition = new EntityDefinition(D(1), new(addedComponents:
            [new(Transform, BuiltInComponents.TransformId, 1), new(Appearance, BuiltInComponents.AppearanceId, schemas[1].Version)]));
        var source = Resolve([Entity(1)], [definition], schemas);
        Assert.Throws<InvalidDataException>(() => PrimitiveSceneAdapter.Project(source));
    }

    [Theory]
    [InlineData(0)] [InlineData(64)] [InlineData(65)]
    public void RuntimeBound_IsExplicitAndAuthoringRemainsUnbounded(int count)
    {
        var source = Resolve(Enumerable.Range(1, count).Select(i => Entity(i)), [Definition()]);
        Assert.Equal(count, source.Entities.Count);
        if (count > 64) Assert.Equal(SceneResolutionError.LegacyEntityLimitExceeded,
            Assert.Throws<SceneResolutionException>(() => PrimitiveSceneAdapter.Project(source)).Code);
        else Assert.Equal(count, PrimitiveSceneAdapter.Project(source).CopyEntities().Count);
    }

    [Fact]
    public void CancellationAndRepeatedProjection_DoNotPublishSharedMutablePayloads()
    {
        var source = Resolve([Entity(1)], [Definition()]); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => PrimitiveSceneAdapter.Project(source, cancel.Token));
        var first = PrimitiveSceneAdapter.Project(source); first.CopyEntities()[0].Primitive!.Transform[0] = 999;
        Assert.Equal(1, PrimitiveSceneAdapter.Project(source).CopyEntities()[0].Primitive!.Transform[0]);
        Assert.Equal(1, first.CopyEntities()[0].Primitive!.Transform[0]);
    }

    internal static readonly ComponentId Transform = new(Id(101)), Appearance = new(Id(102)), Interaction = new(Id(103));
    internal static EntityId E(int id) => new(Id(id));
    internal static EntityDefinitionId D(int id) => new(Id(id + 1000));
    private static Guid Id(int id) => new(id, 0, 0, new byte[8]);
    internal static EntityInstance Entity(int id, int definition = 1, int? parent = null) => new(E(id), D(definition), parentId: parent is {} p ? E(p) : null);
    internal static EntityDefinition Definition(int id = 1, Dictionary<string, double>? transform = null, Dictionary<string, double>? color = null)
        => new(D(id), new(addedComponents: [new(Transform, BuiltInComponents.TransformId, 1, Values(transform)),
            new(Appearance, BuiltInComponents.AppearanceId, 1, Values(color)), new(Interaction, BuiltInComponents.InteractionId, 1)]));
    private static IEnumerable<KeyValuePair<string, PropertyValue>> Values(Dictionary<string, double>? values)
        => (values ?? []).Select(p => KeyValuePair.Create(p.Key, PropertyValue.From(p.Value)));
    internal static ResolvedSceneSnapshot Resolve(IEnumerable<EntityInstance> entities, IEnumerable<EntityDefinition> definitions, IEnumerable<ComponentSchema>? schemas = null)
        => SceneResolver.Resolve(new(new(Id(2000)), 7, entities, definitions, schemas ?? BuiltInComponents.Schemas));
    internal static SceneSnapshot Project(IEnumerable<EntityInstance> entities, IEnumerable<EntityDefinition> definitions) => PrimitiveSceneAdapter.Project(Resolve(entities, definitions));
}
