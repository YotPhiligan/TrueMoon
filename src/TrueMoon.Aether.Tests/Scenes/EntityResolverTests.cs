using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Tests.Scenes;

public sealed class EntityResolverTests
{
    [Fact]
    public void DoorVariant_TwoInstances_ResolveIndependentValuesAndOrigins()
    {
        var resolver = ModelFixture.Resolver();
        var first = resolver.Resolve(new(ModelFixture.First, ModelFixture.Red,
            new(propertyOverrides: [new(ModelFixture.Transform, "x", PropertyValue.From(10d))]), ModelFixture.Parent));
        var second = resolver.Resolve(new(ModelFixture.Second, ModelFixture.Red,
            new(propertyOverrides: [new(ModelFixture.Appearance, "color", PropertyValue.From("green"))])));

        Assert.Equal(new[] { ModelFixture.Door, ModelFixture.Red }, first.DefinitionChain);
        Assert.Equal(ModelFixture.Parent, first.ParentId); Assert.Null(second.ParentId);
        Assert.Equal(10, ModelFixture.Property(first, ModelFixture.Transform, "x").Value.GetNumber());
        Assert.Equal(1, ModelFixture.Property(second, ModelFixture.Transform, "x").Value.GetNumber());
        Assert.Equal("red", ModelFixture.Color(first)); Assert.Equal("green", ModelFixture.Color(second));
        Assert.Equal(ModelFixture.Red, ModelFixture.Property(first, ModelFixture.Appearance, "color").Source.DefinitionId);
        Assert.Equal(4, ModelFixture.Property(first, ModelFixture.Appearance, "color").Source.DefinitionRevision);
        Assert.Equal(ModelFixture.First, ModelFixture.Property(first, ModelFixture.Transform, "x").Source.EntityId);
        Assert.Equal(ModelFixture.Second, ModelFixture.Property(second, ModelFixture.Appearance, "color").Source.EntityId);
        Assert.Equal("base", ModelFixture.Property(second, ModelFixture.Appearance, "label").Value.GetString());
        Assert.True(ModelFixture.Property(first, ModelFixture.Appearance, "visible").Value.GetBoolean());
        Assert.True(ModelFixture.Property(first, ModelFixture.Appearance, "visible").IsSchemaDefault);
        Assert.Equal(ModelFixture.Door, ModelFixture.Property(first, ModelFixture.Appearance, "visible").Source.DefinitionId);
        Assert.Equal(ModelFixture.First, first.Id); Assert.Equal(ModelFixture.Second, second.Id);
        Assert.Equal("red", ModelFixture.Color(resolver.Resolve(new(ModelFixture.First, ModelFixture.Red))));
    }

    [Fact]
    public void BaseRevision_UpdatesInheritedValuesWithoutOverwritingLocalOverridesOrOldResults()
    {
        var instance = new EntityInstance(ModelFixture.First, ModelFixture.Red);
        var old = ModelFixture.Resolver().Resolve(instance);
        var updated = ModelFixture.Resolver(ModelFixture.Base(label: "updated", revision: 9)).Resolve(instance);
        Assert.Equal("base", ModelFixture.Property(old, ModelFixture.Appearance, "label").Value.GetString());
        Assert.Equal("updated", ModelFixture.Property(updated, ModelFixture.Appearance, "label").Value.GetString());
        Assert.Equal(9, ModelFixture.Property(updated, ModelFixture.Appearance, "label").Source.DefinitionRevision);
        Assert.Equal("red", ModelFixture.Color(updated));
        Assert.Equal(4, ModelFixture.Property(updated, ModelFixture.Appearance, "color").Source.DefinitionRevision);
    }

    [Fact]
    public void PropertyReset_DistinguishesMissingNullAndExplicitDefaultAndRetainsOtherChanges()
    {
        var patch = new EntityPatch(propertyOverrides:
        [new(ModelFixture.Appearance, "label", PropertyValue.Null), new(ModelFixture.Appearance, "order", PropertyValue.From(0L)),
         new(ModelFixture.Appearance, "visible", PropertyValue.From(true)), new(ModelFixture.Transform, "x", PropertyValue.From(5d))]);
        var resolver = ModelFixture.Resolver();
        var explicitValues = resolver.Resolve(new(ModelFixture.First, ModelFixture.Red, patch));
        var reset = patch.WithoutPropertyOverride(ModelFixture.Appearance, "label")
            .WithoutPropertyOverride(ModelFixture.Appearance, "order").WithoutPropertyOverride(ModelFixture.Appearance, "visible");
        var inherited = resolver.Resolve(new(ModelFixture.First, ModelFixture.Red, reset));
        Assert.Equal(PropertyValueKind.Null, ModelFixture.Property(explicitValues, ModelFixture.Appearance, "label").Value.Kind);
        Assert.Equal(0, ModelFixture.Property(explicitValues, ModelFixture.Appearance, "order").Value.GetInteger());
        Assert.False(ModelFixture.Property(explicitValues, ModelFixture.Appearance, "visible").IsSchemaDefault);
        Assert.Equal("base", ModelFixture.Property(inherited, ModelFixture.Appearance, "label").Value.GetString());
        Assert.Equal(7, ModelFixture.Property(inherited, ModelFixture.Appearance, "order").Value.GetInteger());
        Assert.True(ModelFixture.Property(inherited, ModelFixture.Appearance, "visible").IsSchemaDefault);
        Assert.Equal(5, ModelFixture.Property(inherited, ModelFixture.Transform, "x").Value.GetNumber());
        Assert.Equal(4, patch.PropertyOverrides.Length); Assert.Single(reset.PropertyOverrides);
        Assert.Equal(PropertyValueKind.Null, ModelFixture.Property(resolver.Resolve(new(ModelFixture.Second, ModelFixture.Red, patch)), ModelFixture.Appearance, "label").Value.Kind);
    }

    [Fact]
    public void Components_AddRemoveRestoreAndResetMasks_PreserveValuesAndPresenceOrigins()
    {
        var tag = new ComponentData(ModelFixture.Extra, ModelFixture.TagType, 1, [new("name", PropertyValue.From("variant"))]);
        var changes = new EntityPatch([tag], [ModelFixture.Appearance]);
        var hidden = new EntityDefinition(ModelFixture.Hidden, changes, ModelFixture.Red);
        var resolver = new EntityResolver([ModelFixture.Base(), ModelFixture.Variant(), hidden], ModelFixture.Schemas);
        var masked = resolver.Resolve(new(ModelFixture.First, ModelFixture.Hidden));
        Assert.False(masked.Components.ContainsKey(ModelFixture.Appearance));
        Assert.Equal("variant", ModelFixture.Property(masked, ModelFixture.Extra, "name").Value.GetString());
        var restored = resolver.Resolve(new(ModelFixture.Second, ModelFixture.Hidden,
            new(propertyOverrides: [new(ModelFixture.Appearance, "color", PropertyValue.From("orange"))], restoredComponents: [ModelFixture.Appearance])));
        Assert.Equal("orange", ModelFixture.Color(restored));
        Assert.Equal("base", ModelFixture.Property(restored, ModelFixture.Appearance, "label").Value.GetString());
        Assert.Equal(ModelFixture.Door, restored.Components[ModelFixture.Appearance].Source.DefinitionId);
        Assert.Equal(ModelFixture.Second, restored.Components[ModelFixture.Appearance].PresenceSource.EntityId);
        Assert.Equal(ModelFixture.Hidden, restored.Components[ModelFixture.Extra].Source.DefinitionId);
        var resetDefinition = new EntityDefinition(ModelFixture.Hidden, changes.WithoutComponentRemoval(ModelFixture.Appearance), ModelFixture.Red);
        var reset = new EntityResolver([ModelFixture.Base(), ModelFixture.Variant(), resetDefinition], ModelFixture.Schemas)
            .Resolve(new(ModelFixture.First, ModelFixture.Hidden));
        Assert.Equal("red", ModelFixture.Color(reset)); Assert.True(reset.Components.ContainsKey(ModelFixture.Extra));
        Assert.Single(changes.RemovedComponents);
        var noLocalMask = EntityPatch.Empty.WithoutComponentRemoval(ModelFixture.Appearance);
        Assert.False(resolver.Resolve(new(ModelFixture.First, ModelFixture.Hidden, noLocalMask)).Components.ContainsKey(ModelFixture.Appearance));
    }

    [Fact]
    public void FinalDependencies_AllowRestorationButRejectMissingRequiredTypes()
    {
        var hiddenTransform = new EntityDefinition(ModelFixture.Hidden, new(removedComponents: [ModelFixture.Transform]), ModelFixture.Door);
        var resolver = new EntityResolver([ModelFixture.Base(), hiddenTransform], ModelFixture.Schemas);
        var error = Assert.Throws<EntityResolutionException>(() => resolver.Resolve(new(ModelFixture.First, ModelFixture.Hidden)));
        Assert.Equal(EntityResolutionError.MissingComponentDependency, error.Code);
        Assert.Equal($"entities/{ModelFixture.First.Value:D}/components/{ModelFixture.Appearance.Value:D}", error.Path);
        var valid = resolver.Resolve(new(ModelFixture.First, ModelFixture.Hidden, new(restoredComponents: [ModelFixture.Transform])));
        Assert.Equal(2, valid.Components.Count); Assert.Equal(1, ModelFixture.Property(valid, ModelFixture.Transform, "x").Value.GetNumber());
    }

    [Fact]
    public void Multiplicity_IsTypeBasedAndPermitsIndependentOccurrencesOnlyWhenDeclared()
    {
        var duplicate = new ComponentData(ModelFixture.Extra, ModelFixture.AppearanceType, 1);
        var resolver = ModelFixture.Resolver();
        var error = Assert.Throws<EntityResolutionException>(() => resolver.Resolve(new(ModelFixture.First, ModelFixture.Red, new([duplicate]))));
        Assert.Equal(EntityResolutionError.ComponentMultiplicity, error.Code);
        var replaced = resolver.Resolve(new(ModelFixture.First, ModelFixture.Red, new([duplicate], [ModelFixture.Appearance])));
        Assert.False(replaced.Components.ContainsKey(ModelFixture.Appearance));
        Assert.Equal("white", ModelFixture.Property(replaced, ModelFixture.Extra, "color").Value.GetString());
        var tags = resolver.Resolve(new(ModelFixture.First, ModelFixture.Red, new([
            new(ModelFixture.Extra, ModelFixture.TagType, 1, [new("name", PropertyValue.From("first"))]),
            new(new(ModelFixture.Guid(304)), ModelFixture.TagType, 1, [new("name", PropertyValue.From("second"))])])));
        Assert.Equal(new[] { "first", "second" }, tags.Components.Values.Where(c => c.TypeId == ModelFixture.TagType)
            .Select(c => c.Properties["name"].Value.GetString()).OrderBy(x => x, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("remove-missing", EntityResolutionError.ComponentNotFound)]
    [InlineData("restore-missing", EntityResolutionError.ComponentNotFound)]
    [InlineData("override-missing", EntityResolutionError.ComponentNotFound)]
    [InlineData("restore-visible", EntityResolutionError.ComponentNotRemoved)]
    [InlineData("add-existing", EntityResolutionError.ComponentAlreadyExists)]
    [InlineData("remove-hidden", EntityResolutionError.ComponentAlreadyRemoved)]
    [InlineData("override-hidden", EntityResolutionError.ComponentAlreadyRemoved)]
    [InlineData("reuse-hidden-id", EntityResolutionError.ComponentAlreadyExists)]
    [InlineData("unknown-property", EntityResolutionError.UnknownProperty)]
    [InlineData("property-case", EntityResolutionError.UnknownProperty)]
    [InlineData("wrong-type", EntityResolutionError.PropertyTypeMismatch)]
    [InlineData("null-required", EntityResolutionError.PropertyTypeMismatch)]
    [InlineData("integer-for-number", EntityResolutionError.PropertyTypeMismatch)]
    [InlineData("number-for-integer", EntityResolutionError.PropertyTypeMismatch)]
    public void InvalidChanges_ReportExactCodeAndTargetWithoutPoisoningResolver(string kind, EntityResolutionError code)
    {
        var missing = new ComponentId(ModelFixture.Guid(999));
        var target = kind.EndsWith("missing", StringComparison.Ordinal) ? missing : ModelFixture.Appearance;
        var property = kind switch { "unknown-property" => "missing", "property-case" => "Color", "integer-for-number" => "x", "number-for-integer" => "order", _ => "color" };
        if (kind == "integer-for-number") target = ModelFixture.Transform;
        var hidden = new EntityDefinition(ModelFixture.Hidden, new(removedComponents: [ModelFixture.Appearance]), ModelFixture.Red);
        var patch = kind switch
        {
            "remove-missing" or "remove-hidden" => new EntityPatch(removedComponents: [target]),
            "restore-missing" or "restore-visible" => new(restoredComponents: [target]),
            "add-existing" or "reuse-hidden-id" => new(addedComponents: [new(target, ModelFixture.AppearanceType, 1)]),
            _ => new(propertyOverrides: [new(target, property, kind switch
            { "wrong-type" => PropertyValue.From(false), "null-required" => PropertyValue.Null,
              "integer-for-number" => PropertyValue.From(2L), "number-for-integer" => PropertyValue.From(2d), _ => PropertyValue.From("bad") })])
        };
        var resolver = new EntityResolver([ModelFixture.Base(), ModelFixture.Variant(), hidden], ModelFixture.Schemas);
        var definition = kind is "remove-hidden" or "override-hidden" or "reuse-hidden-id" ? ModelFixture.Hidden : ModelFixture.Red;
        var error = Assert.Throws<EntityResolutionException>(() => resolver.Resolve(new(ModelFixture.First, definition, patch)));
        Assert.Equal(code, error.Code);
        var expectedPath = $"entities/{ModelFixture.First.Value:D}/components/{target.Value:D}";
        if (code is EntityResolutionError.UnknownProperty or EntityResolutionError.PropertyTypeMismatch) expectedPath += $"/properties/{property}";
        Assert.Equal(expectedPath, error.Path);
        Assert.Contains(expectedPath, error.Message, StringComparison.Ordinal);
        Assert.Equal("red", ModelFixture.Color(resolver.Resolve(new(ModelFixture.Second, ModelFixture.Red))));
    }

    [Fact]
    public void SchemaVersions_AreExactAndUnknownSchemasAndInvalidInitialValuesAreRejected()
    {
        var version2 = new ComponentSchema(ModelFixture.TagType, 2, [new("name", PropertyValueKind.Integer, PropertyValue.From(42L))], true);
        var resolver = new EntityResolver([ModelFixture.Base()], ModelFixture.Schemas.Append(version2));
        var result = resolver.Resolve(new(ModelFixture.First, ModelFixture.Door, new([new(ModelFixture.Extra, ModelFixture.TagType, 2)])));
        Assert.Equal(2, result.Components[ModelFixture.Extra].SchemaVersion);
        Assert.Equal(42, ModelFixture.Property(result, ModelFixture.Extra, "name").Value.GetInteger());
        Assert.True(ModelFixture.Property(result, ModelFixture.Extra, "name").IsSchemaDefault);
        foreach (var (type, version, values, expected) in new[]
        {
            (ModelFixture.TagType, 3, Array.Empty<KeyValuePair<string, PropertyValue>>(), EntityResolutionError.SchemaVersionMismatch),
            (new ComponentTypeId(ModelFixture.Guid(999)), 1, Array.Empty<KeyValuePair<string, PropertyValue>>(), EntityResolutionError.UnknownSchema),
            (ModelFixture.TagType, 2, new[] { KeyValuePair.Create("name", PropertyValue.From("wrong-version-value")) }, EntityResolutionError.PropertyTypeMismatch),
            (ModelFixture.TagType, 1, new[] { KeyValuePair.Create("Name", PropertyValue.From("wrong-case")) }, EntityResolutionError.UnknownProperty)
        })
        {
            var error = Assert.Throws<EntityResolutionException>(() => resolver.Resolve(new(ModelFixture.First, ModelFixture.Door,
                new([new(ModelFixture.Extra, type, version, values)]))));
            Assert.Equal(expected, error.Code); Assert.Contains($"components/{ModelFixture.Extra.Value:D}", error.Path, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("missing-instance", EntityResolutionError.MissingDefinition)]
    [InlineData("missing-base", EntityResolutionError.MissingDefinition)]
    [InlineData("self-cycle", EntityResolutionError.InheritanceCycle)]
    [InlineData("two-cycle", EntityResolutionError.InheritanceCycle)]
    public void InvalidDefinitionGraphs_ReportIdsAndDoNotTraverseSceneParents(string kind, EntityResolutionError code)
    {
        EntityDefinition[] definitions = kind switch
        {
            "missing-instance" => [],
            "missing-base" => [new(ModelFixture.Red, EntityPatch.Empty, ModelFixture.Door)],
            "self-cycle" => [new(ModelFixture.Red, EntityPatch.Empty, ModelFixture.Red)],
            _ => [new(ModelFixture.Red, EntityPatch.Empty, ModelFixture.Door), new(ModelFixture.Door, EntityPatch.Empty, ModelFixture.Red)]
        };
        var error = Assert.Throws<EntityResolutionException>(() => new EntityResolver(definitions, []).Resolve(
            new(ModelFixture.First, ModelFixture.Red, parentId: ModelFixture.Parent)));
        Assert.Equal(code, error.Code);
        Assert.Contains(kind == "missing-instance" ? ModelFixture.First.Value.ToString("D") : ModelFixture.Red.Value.ToString("D"), error.Path, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)] [InlineData(63)] [InlineData(64)] [InlineData(65)]
    public void InheritanceDepth_EnforcesExactBoundWithoutRecursion(int depth)
    {
        var definitions = Enumerable.Range(0, depth).Select(i => new EntityDefinition(new(ModelFixture.Guid(1000 + i)), EntityPatch.Empty,
            i == 0 ? null : new EntityDefinitionId(ModelFixture.Guid(999 + i)))).ToArray();
        var resolver = new EntityResolver(definitions, []);
        var instance = new EntityInstance(ModelFixture.First, definitions[^1].Id);
        if (depth > EntityResolver.MaximumInheritanceDepth)
        {
            var error = Assert.Throws<EntityResolutionException>(() => resolver.Resolve(instance));
            Assert.Equal(EntityResolutionError.InheritanceDepthExceeded, error.Code);
            Assert.Contains("/base/", error.Path, StringComparison.Ordinal);
        }
        else
        {
            var result = resolver.Resolve(instance);
            Assert.Equal(definitions.Select(d => d.Id), result.DefinitionChain); Assert.Empty(result.Components);
        }
    }

    [Fact]
    public void Catalogs_AreSnapshottedAndRepeatedParallelResolutionsDoNotShareMutableState()
    {
        var definitions = new List<EntityDefinition> { ModelFixture.Base(), ModelFixture.Variant() };
        var schemas = ModelFixture.Schemas.ToList(); var resolver = new EntityResolver(definitions, schemas);
        definitions.Clear(); schemas.Clear();
        Parallel.For(0, 32, i =>
        {
            var id = new EntityId(ModelFixture.Guid(500 + i));
            var result = resolver.Resolve(new(id, ModelFixture.Red, new(propertyOverrides: [new(ModelFixture.Transform, "x", PropertyValue.From((double)i))])));
            Assert.Equal(i, ModelFixture.Property(result, ModelFixture.Transform, "x").Value.GetNumber());
            Assert.Equal(id, ModelFixture.Property(result, ModelFixture.Transform, "x").Source.EntityId);
            Assert.Equal("red", ModelFixture.Color(result));
        });
        Assert.Equal(1, ModelFixture.Property(resolver.Resolve(new(ModelFixture.First, ModelFixture.Red)), ModelFixture.Transform, "x").Value.GetNumber());
    }

    [Fact]
    public void Catalogs_RejectDuplicateDefinitionsAndExactSchemaVersionsWithPaths()
    {
        var duplicateDefinition = Assert.Throws<EntityResolutionException>(() => new EntityResolver([ModelFixture.Base(), ModelFixture.Base()], []));
        Assert.Equal(EntityResolutionError.DuplicateDefinition, duplicateDefinition.Code);
        Assert.Equal($"definitions/{ModelFixture.Door.Value:D}", duplicateDefinition.Path);
        var duplicateSchema = Assert.Throws<EntityResolutionException>(() => new EntityResolver([], [ModelFixture.TransformSchema, ModelFixture.TransformSchema]));
        Assert.Equal(EntityResolutionError.DuplicateSchema, duplicateSchema.Code);
        Assert.Equal($"schemas/{ModelFixture.TransformType.Value:D}/1", duplicateSchema.Path);
    }

    [Fact]
    public void InvalidDefinitionValues_ReportDefinitionPathWithoutPoisoningOtherInstances()
    {
        var invalid = new EntityDefinition(ModelFixture.Hidden, new([new(ModelFixture.Extra, ModelFixture.TagType, 1,
            [new("name", PropertyValue.From(false))])]), ModelFixture.Door);
        var resolver = new EntityResolver([ModelFixture.Base(), ModelFixture.Variant(), invalid], ModelFixture.Schemas);
        var error = Assert.Throws<EntityResolutionException>(() => resolver.Resolve(new(ModelFixture.First, ModelFixture.Hidden)));
        Assert.Equal(EntityResolutionError.PropertyTypeMismatch, error.Code);
        Assert.Equal($"definitions/{ModelFixture.Hidden.Value:D}/components/{ModelFixture.Extra.Value:D}/properties/name", error.Path);
        var unaffected = resolver.Resolve(new(ModelFixture.Second, ModelFixture.Red));
        Assert.Equal("red", ModelFixture.Color(unaffected));
        Assert.False(unaffected.Components.ContainsKey(ModelFixture.Extra));
    }
}

internal static class ModelFixture
{
    internal static Guid Guid(int id) => new(id, 0, 0, new byte[8]);
    internal static readonly EntityDefinitionId Door = new(Guid(1)), Red = new(Guid(2)), Hidden = new(Guid(3));
    internal static readonly EntityId First = new(Guid(101)), Second = new(Guid(102)), Parent = new(Guid(103));
    internal static readonly ComponentTypeId TransformType = new(Guid(201)), AppearanceType = new(Guid(202)), TagType = new(Guid(203));
    internal static readonly ComponentId Transform = new(Guid(301)), Appearance = new(Guid(302)), Extra = new(Guid(303));
    internal static ComponentSchema TransformSchema => new(TransformType, 1,
        [new("x", PropertyValueKind.Number, PropertyValue.From(0d)), new("y", PropertyValueKind.Number, PropertyValue.From(0d))]);
    internal static ComponentSchema AppearanceSchema => new(AppearanceType, 1,
        [new("color", PropertyValueKind.String, PropertyValue.From("white")), new("label", PropertyValueKind.String, PropertyValue.Null, true),
         new("visible", PropertyValueKind.Boolean, PropertyValue.From(true)), new("order", PropertyValueKind.Integer, PropertyValue.From(0L))], requiredTypes: [TransformType]);
    internal static ComponentSchema[] Schemas => [TransformSchema, AppearanceSchema, new(TagType, 1, [new("name", PropertyValueKind.String, PropertyValue.From(""))], true)];
    internal static EntityDefinition Base(string label = "base", long revision = 1) => new(Door, new([
        new(Transform, TransformType, 1, [new("x", PropertyValue.From(1d))]),
        new(Appearance, AppearanceType, 1, [new("color", PropertyValue.From("blue")), new("label", PropertyValue.From(label)), new("order", PropertyValue.From(7L))])]), revision: revision);
    internal static EntityDefinition Variant() => new(Red, new(propertyOverrides: [new(Appearance, "color", PropertyValue.From("red"))]), Door, 4);
    internal static EntityResolver Resolver(EntityDefinition? root = null) => new([root ?? Base(), Variant()], Schemas);
    internal static ResolvedProperty Property(ResolvedEntity entity, ComponentId component, string property) => entity.Components[component].Properties[property];
    internal static string Color(ResolvedEntity entity) => Property(entity, Appearance, "color").Value.GetString();
}
