using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Tests.Scenes;

public sealed class AuthoringModelTests
{
    [Fact]
    public void Identifiers_RejectEmptyAndDefaultIdsAtEveryModelBoundary()
    {
        Assert.Throws<ArgumentException>(() => new EntityId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new EntityDefinitionId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new ComponentId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new ComponentTypeId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new EntityDefinition(default, EntityPatch.Empty));
        Assert.Throws<ArgumentException>(() => new EntityDefinition(ModelFixture.Door, EntityPatch.Empty, default(EntityDefinitionId)));
        Assert.Throws<ArgumentException>(() => new EntityInstance(default, ModelFixture.Door));
        Assert.Throws<ArgumentException>(() => new EntityInstance(ModelFixture.First, default));
        Assert.Throws<ArgumentException>(() => new EntityInstance(ModelFixture.First, ModelFixture.Door, parentId: default(EntityId)));
        Assert.Throws<ArgumentException>(() => new ComponentData(default, ModelFixture.TransformType, 1));
        Assert.Throws<ArgumentException>(() => new ComponentData(ModelFixture.Transform, default, 1));
        Assert.Throws<ArgumentException>(() => new ComponentSchema(default, 1, []));
        Assert.Throws<ArgumentException>(() => new ComponentPropertyOverride(default, "x", PropertyValue.Null));
        Assert.Throws<ArgumentException>(() => new EntityPatch(removedComponents: [default]));
        Assert.Throws<ArgumentException>(() => new EntityPatch(restoredComponents: [default]));
        Assert.Throws<ArgumentException>(() => new ComponentSchema(ModelFixture.AppearanceType, 1, [], requiredTypes: [default]));
        Assert.Throws<ArgumentException>(() => EntityPatch.Empty.WithoutComponentRemoval(default));
        Assert.Throws<ArgumentException>(() => EntityPatch.Empty.WithoutPropertyOverride(default, "x"));
    }

    [Fact]
    public void ScalarValues_PreserveKindsBoundariesAndRejectCrossKindReads()
    {
        Assert.True(PropertyValue.From(true).GetBoolean()); Assert.False(PropertyValue.From(false).GetBoolean());
        Assert.Equal(long.MinValue, PropertyValue.From(long.MinValue).GetInteger());
        Assert.Equal(long.MaxValue, PropertyValue.From(long.MaxValue).GetInteger());
        Assert.Equal(double.MaxValue, PropertyValue.From(double.MaxValue).GetNumber());
        Assert.Equal(-double.MaxValue, PropertyValue.From(-double.MaxValue).GetNumber());
        Assert.Equal("", PropertyValue.From("").GetString());
        Assert.Equal("Дверь 🚪", PropertyValue.From("Дверь 🚪").GetString());
        var atBound = new string('x', PropertyValue.MaximumStringLength);
        Assert.Equal(atBound, PropertyValue.From(atBound).GetString());
        Assert.Equal(PropertyValue.MaximumStringLength - 1, PropertyValue.From(atBound[..^1]).GetString().Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => PropertyValue.From(atBound + "x"));
        Assert.Throws<ArgumentNullException>(() => PropertyValue.From((string)null!));
        Assert.Throws<InvalidOperationException>(() => PropertyValue.Null.GetString());
        Assert.Throws<InvalidOperationException>(() => PropertyValue.From(1L).GetNumber());
        Assert.Throws<InvalidOperationException>(() => PropertyValue.From(1d).GetInteger());
        Assert.Throws<InvalidOperationException>(() => PropertyValue.From("true").GetBoolean());
        Assert.Equal(PropertyValueKind.Null, default(PropertyValue).Kind);
        Assert.NotEqual(PropertyValue.From(1L), PropertyValue.From(1d));
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.NegativeInfinity)] [InlineData(double.PositiveInfinity)]
    public void ScalarNumbers_RejectNonFiniteValues(double value)
        => Assert.Throws<ArgumentOutOfRangeException>(() => PropertyValue.From(value));

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")] [InlineData("a/b")] [InlineData("цвет")]
    public void PropertyIds_RejectInvalidKeysAtSchemaDataAndOverrideBoundaries(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ComponentPropertySchema(id!, PropertyValueKind.String, PropertyValue.From("")));
        Assert.ThrowsAny<ArgumentException>(() => new ComponentPropertyOverride(ModelFixture.Appearance, id!, PropertyValue.Null));
        Assert.ThrowsAny<ArgumentException>(() => new ComponentData(ModelFixture.Appearance, ModelFixture.AppearanceType, 1, [new(id!, PropertyValue.Null)]));
        Assert.ThrowsAny<ArgumentException>(() => EntityPatch.Empty.WithoutPropertyOverride(ModelFixture.Appearance, id!));
    }

    [Fact]
    public void PropertyIds_KeepOrdinalCaseAndEnforceExactLengthBoundary()
    {
        var key = new string('a', 128);
        var schema = new ComponentSchema(ModelFixture.TagType, 1,
            [new("A", PropertyValueKind.Boolean, PropertyValue.From(true)), new("a", PropertyValueKind.Boolean, PropertyValue.From(false)),
             new(key, PropertyValueKind.Integer, PropertyValue.From(5L)), new("position.x-1_2", PropertyValueKind.Number, PropertyValue.From(0d))]);
        var definition = new EntityDefinition(ModelFixture.Door, new([new(ModelFixture.Extra, ModelFixture.TagType, 1,
            [new("A", PropertyValue.From(false)), new("a", PropertyValue.From(true))])]));
        var result = new EntityResolver([definition], [schema]).Resolve(new(ModelFixture.First, ModelFixture.Door));
        Assert.False(ModelFixture.Property(result, ModelFixture.Extra, "A").Value.GetBoolean());
        Assert.True(ModelFixture.Property(result, ModelFixture.Extra, "a").Value.GetBoolean());
        Assert.Equal(5, ModelFixture.Property(result, ModelFixture.Extra, key).Value.GetInteger());
        Assert.Throws<ArgumentException>(() => new ComponentPropertySchema(key + "a", PropertyValueKind.Integer, PropertyValue.From(0L)));
    }

    [Theory]
    [InlineData("unknown-kind")] [InlineData("null-kind")] [InlineData("wrong-default")]
    [InlineData("null-required")] [InlineData("number-coercion")]
    public void PropertySchemas_RejectInvalidKindsAndDefaults(string invalid)
    {
        Assert.ThrowsAny<ArgumentException>(() => invalid switch
        {
            "unknown-kind" => new ComponentPropertySchema("x", (PropertyValueKind)99, PropertyValue.From(true)),
            "null-kind" => new("x", PropertyValueKind.Null, PropertyValue.Null, true),
            "wrong-default" => new("x", PropertyValueKind.Boolean, PropertyValue.From("true")),
            "number-coercion" => new("x", PropertyValueKind.Number, PropertyValue.From(0L)),
            _ => new("x", PropertyValueKind.String, PropertyValue.Null)
        });
    }

    [Fact]
    public void NullableSchema_ResolvesNullDefaultAndAcceptsExplicitNonNullValue()
    {
        var schema = new ComponentSchema(ModelFixture.TagType, 1, [new("name", PropertyValueKind.String, PropertyValue.Null, true)]);
        var definition = new EntityDefinition(ModelFixture.Door, new([new(ModelFixture.Extra, ModelFixture.TagType, 1)]));
        var resolver = new EntityResolver([definition], [schema]);
        var inherited = resolver.Resolve(new(ModelFixture.First, ModelFixture.Door));
        Assert.Equal(PropertyValueKind.Null, ModelFixture.Property(inherited, ModelFixture.Extra, "name").Value.Kind);
        Assert.True(ModelFixture.Property(inherited, ModelFixture.Extra, "name").IsSchemaDefault);
        var changed = resolver.Resolve(new(ModelFixture.Second, ModelFixture.Door,
            new(propertyOverrides: [new(ModelFixture.Extra, "name", PropertyValue.From("named"))])));
        Assert.Equal("named", ModelFixture.Property(changed, ModelFixture.Extra, "name").Value.GetString());
        Assert.False(ModelFixture.Property(changed, ModelFixture.Extra, "name").IsSchemaDefault);
    }

    [Theory]
    [InlineData("definition-revision")] [InlineData("component-version-zero")] [InlineData("component-version-negative")]
    [InlineData("schema-version-zero")] [InlineData("schema-version-negative")]
    [InlineData("same-parent")] [InlineData("null-definition-patch")] [InlineData("null-properties")]
    [InlineData("null-definition-catalog")] [InlineData("null-schema-catalog")] [InlineData("null-definition-entry")]
    [InlineData("null-schema-entry")] [InlineData("null-schema-property")] [InlineData("null-component-entry")]
    [InlineData("null-override-entry")] [InlineData("self-dependency")] [InlineData("duplicate-dependency")]
    [InlineData("duplicate-schema-property")] [InlineData("duplicate-data-property")] [InlineData("duplicate-add")]
    [InlineData("duplicate-remove")] [InlineData("duplicate-restore")] [InlineData("duplicate-override")]
    [InlineData("add-remove")] [InlineData("add-restore")] [InlineData("remove-restore")]
    public void AuthoringInputs_RejectInvalidLocalContractsBeforeResolution(string invalid)
    {
        var component = new ComponentData(ModelFixture.Extra, ModelFixture.TagType, 1);
        var property = new ComponentPropertySchema("name", PropertyValueKind.String, PropertyValue.From(""));
        var patchProperty = new ComponentPropertyOverride(ModelFixture.Extra, "name", PropertyValue.From("x"));
        Assert.ThrowsAny<ArgumentException>(() => invalid switch
        {
            "definition-revision" => (object)new EntityDefinition(ModelFixture.Door, EntityPatch.Empty, revision: -1),
            "component-version-zero" => new ComponentData(ModelFixture.Extra, ModelFixture.TagType, 0),
            "component-version-negative" => new ComponentData(ModelFixture.Extra, ModelFixture.TagType, -1),
            "schema-version-zero" => new ComponentSchema(ModelFixture.TagType, 0, []),
            "schema-version-negative" => new ComponentSchema(ModelFixture.TagType, -1, []),
            "same-parent" => new EntityInstance(ModelFixture.First, ModelFixture.Door, parentId: ModelFixture.First),
            "null-definition-patch" => new EntityDefinition(ModelFixture.Door, null!),
            "null-properties" => new ComponentSchema(ModelFixture.TagType, 1, null!),
            "null-definition-catalog" => new EntityResolver(null!, []),
            "null-schema-catalog" => new EntityResolver([], null!),
            "null-definition-entry" => new EntityResolver([null!], []),
            "null-schema-entry" => new EntityResolver([], [null!]),
            "null-schema-property" => new ComponentSchema(ModelFixture.TagType, 1, [null!]),
            "null-component-entry" => new EntityPatch([null!]),
            "null-override-entry" => new EntityPatch(propertyOverrides: [null!]),
            "self-dependency" => new ComponentSchema(ModelFixture.TagType, 1, [], requiredTypes: [ModelFixture.TagType]),
            "duplicate-dependency" => new ComponentSchema(ModelFixture.TagType, 1, [], requiredTypes: [ModelFixture.TransformType, ModelFixture.TransformType]),
            "duplicate-schema-property" => new ComponentSchema(ModelFixture.TagType, 1, [property, property]),
            "duplicate-data-property" => new ComponentData(ModelFixture.Extra, ModelFixture.TagType, 1, [new("name", PropertyValue.From("x")), new("name", PropertyValue.From("x"))]),
            "duplicate-add" => new EntityPatch([component, component]),
            "duplicate-remove" => new EntityPatch(removedComponents: [ModelFixture.Extra, ModelFixture.Extra]),
            "duplicate-restore" => new EntityPatch(restoredComponents: [ModelFixture.Extra, ModelFixture.Extra]),
            "duplicate-override" => new EntityPatch(propertyOverrides: [patchProperty, patchProperty]),
            "add-remove" => new EntityPatch([component], [ModelFixture.Extra]),
            "add-restore" => new EntityPatch([component], restoredComponents: [ModelFixture.Extra]),
            _ => new EntityPatch(removedComponents: [ModelFixture.Extra], restoredComponents: [ModelFixture.Extra])
        });
    }

    [Fact]
    public void Collections_AreFrozenTransitivelyBeforeCallerMutatesEveryInputCollection()
    {
        var propertySchemas = new List<ComponentPropertySchema> { new("name", PropertyValueKind.String, PropertyValue.From("default")) };
        var requirements = new List<ComponentTypeId> { ModelFixture.TransformType };
        var schema = new ComponentSchema(ModelFixture.TagType, 1, propertySchemas, true, requirements);
        var values = new Dictionary<string, PropertyValue> { ["name"] = PropertyValue.From("captured") };
        var data = new ComponentData(ModelFixture.Extra, ModelFixture.TagType, 1, values);
        var additions = new List<ComponentData> { data };
        var overrides = new List<ComponentPropertyOverride> { new(ModelFixture.Transform, "x", PropertyValue.From(12d)) };
        var removals = new List<ComponentId> { ModelFixture.Appearance };
        var patch = new EntityPatch(additions, removals, overrides);
        propertySchemas.Clear(); requirements.Clear(); values["name"] = PropertyValue.From("mutated");
        additions.Clear(); overrides.Clear(); removals.Clear();
        var resolver = new EntityResolver([ModelFixture.Base()], [ModelFixture.TransformSchema, ModelFixture.AppearanceSchema, schema]);
        var resolved = resolver.Resolve(new(ModelFixture.First, ModelFixture.Door, patch));
        Assert.Equal("captured", ModelFixture.Property(resolved, ModelFixture.Extra, "name").Value.GetString());
        Assert.Equal(12, ModelFixture.Property(resolved, ModelFixture.Transform, "x").Value.GetNumber());
        Assert.False(resolved.Components.ContainsKey(ModelFixture.Appearance));
        Assert.Contains(ModelFixture.TransformType, schema.RequiredTypes);
        var dependencyError = Assert.Throws<EntityResolutionException>(() => resolver.Resolve(new(ModelFixture.First, ModelFixture.Door,
            new([data], [ModelFixture.Appearance, ModelFixture.Transform]))));
        Assert.Equal(EntityResolutionError.MissingComponentDependency, dependencyError.Code);
        var restorations = new List<ComponentId> { ModelFixture.Appearance };
        var restore = new EntityPatch(restoredComponents: restorations); restorations.Clear();
        var hidden = new EntityDefinition(ModelFixture.Hidden, new(removedComponents: [ModelFixture.Appearance]), ModelFixture.Door);
        Assert.Equal("blue", ModelFixture.Color(new EntityResolver([ModelFixture.Base(), hidden], ModelFixture.Schemas)
            .Resolve(new(ModelFixture.First, ModelFixture.Hidden, restore))));
    }

    [Fact]
    public void EmptyPatchResets_AreHarmlessAndResolveRejectsNullInstance()
    {
        var patch = EntityPatch.Empty.WithoutComponentRemoval(ModelFixture.Extra).WithoutPropertyOverride(ModelFixture.Extra, "unused");
        Assert.Empty(patch.AddedComponents); Assert.Empty(patch.RemovedComponents);
        Assert.Empty(patch.RestoredComponents); Assert.Empty(patch.PropertyOverrides);
        Assert.Equal("red", ModelFixture.Color(ModelFixture.Resolver().Resolve(new(ModelFixture.First, ModelFixture.Red, patch))));
        Assert.Throws<ArgumentNullException>(() => ModelFixture.Resolver().Resolve(null!));
    }

    [Fact]
    public void PatchResets_PreserveUnrelatedAddRemoveRestoreAndPropertyChanges()
    {
        var hidden = new EntityDefinition(ModelFixture.Hidden, new(removedComponents: [ModelFixture.Transform]), ModelFixture.Door);
        var resolver = new EntityResolver([ModelFixture.Base(), hidden], ModelFixture.Schemas);
        var patch = new EntityPatch([new(ModelFixture.Extra, ModelFixture.TagType, 1, [new("name", PropertyValue.From("captured"))])],
            [ModelFixture.Appearance], [new(ModelFixture.Extra, "name", PropertyValue.From("local")), new(ModelFixture.Transform, "x", PropertyValue.From(12d))],
            [ModelFixture.Transform]);
        var reset = patch.WithoutPropertyOverride(ModelFixture.Extra, "name").WithoutComponentRemoval(ModelFixture.Appearance);
        var result = resolver.Resolve(new(ModelFixture.First, ModelFixture.Hidden, reset));
        Assert.Equal("captured", ModelFixture.Property(result, ModelFixture.Extra, "name").Value.GetString());
        Assert.Equal("blue", ModelFixture.Color(result));
        Assert.Equal(12, ModelFixture.Property(result, ModelFixture.Transform, "x").Value.GetNumber());
        Assert.Equal(ModelFixture.First, result.Components[ModelFixture.Transform].PresenceSource.EntityId);
        var original = resolver.Resolve(new(ModelFixture.First, ModelFixture.Hidden, patch));
        Assert.False(original.Components.ContainsKey(ModelFixture.Appearance));
        Assert.Equal("local", ModelFixture.Property(original, ModelFixture.Extra, "name").Value.GetString());
        Assert.Equal(12, ModelFixture.Property(original, ModelFixture.Transform, "x").Value.GetNumber());
    }
}
