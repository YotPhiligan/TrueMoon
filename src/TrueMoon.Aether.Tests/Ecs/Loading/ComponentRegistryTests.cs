using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Loading;

public sealed class ComponentRegistryTests
{
    private static readonly ComponentTypeId First = new(new Guid("39b05992-1999-44a2-9779-000000000001"));
    private static readonly ComponentTypeId Second = new(new Guid("39b05992-1999-44a2-9779-000000000002"));
    private static readonly ComponentTypeId Third = new(new Guid("39b05992-1999-44a2-9779-000000000003"));
    private static readonly SceneId Scene = new(new Guid("39b05992-1999-44a2-9779-000000000010"));

    [Fact]
    public void Build_ProducesIndependentFrozenRegistriesWithoutInvokingCallbacks()
    {
        var builder = new ComponentRegistryBuilder();
        var empty = builder.Build();
        var imports = 0; var validations = 0;
        var later = Schema(Second);
        var versionTwo = Schema(First, 2);
        Assert.Same(builder, builder.Register<ValueB>(later, _ => { imports++; return new(7); }, validate: _ => validations++));
        var first = builder.Build();
        builder.Register<ValueA>(versionTwo, _ => { imports++; return new(11); });
        builder.Register<ValueA>(Schema(First), _ => { imports++; return new(13); });
        var complete = builder.Build();

        Assert.Equal((0, 1, 3), (empty.Count, first.Count, complete.Count));
        Assert.Empty(empty.Schemas);
        Assert.Equal(Second, Assert.Single(first.Schemas).TypeId);
        Assert.Equal(new[] { (First, 1), (First, 2), (Second, 1) }, complete.Schemas.Select(s => (s.TypeId, s.Version)));
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, complete.Id);
        Assert.NotEqual(empty.Id, first.Id);
        Assert.Equal((0, 0), (imports, validations));
        Assert.Equal(SceneWorldLoadError.UnknownComponent,
            Assert.Throws<SceneWorldLoadException>(() => first.Binding(versionTwo, Scene)).Error);
        Assert.Equal(typeof(ValueA), complete.Binding(versionTwo, Scene).RuntimeType);
    }

    [Fact]
    public void Registration_RejectsAliasesAndDuplicatesWithoutReservingRejectedMappings()
    {
        var builder = new ComponentRegistryBuilder().Register<ValueA>(Schema(First), _ => new(17));
        Assert.Throws<ArgumentException>(() => builder.Register<ValueA>(Schema(First), _ => new(19)));
        Assert.Throws<ArgumentException>(() => builder.Register<ValueA>(Schema(Second), _ => new(23)));
        Assert.Throws<ArgumentException>(() => builder.Register<ValueB>(Schema(First, 2), _ => new(29)));
        builder.Register<ValueB>(Schema(Second), _ => new(31));
        builder.Register<ValueA>(Schema(First, 2), _ => new(37));

        var registry = builder.Build();
        Assert.Equal(3, registry.Count);
        Assert.Equal(typeof(ValueA), registry.Binding(Schema(First, 2), Scene).RuntimeType);
        Assert.Equal(typeof(ValueB), registry.Binding(Schema(Second), Scene).RuntimeType);
        Assert.Equal(new[] { (First, 1), (First, 2), (Second, 1) }, registry.Schemas.Select(s => (s.TypeId, s.Version)));
    }

    [Fact]
    public void Registration_RejectsNullManagedAndMultipleInputsBeforeChangingRegistry()
    {
        var builder = new ComponentRegistryBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.Register<ValueA>(null!, _ => new(41)));
        Assert.Throws<ArgumentNullException>(() => builder.Register<ValueA>(Schema(First), null!));
        Assert.Throws<NotSupportedException>(() => builder.Register<ManagedValue>(Schema(First), _ => new("managed")));
        Assert.Throws<NotSupportedException>(() => builder.Register<NestedManaged>(Schema(First), _ => new(new("nested"))));
        Assert.Throws<NotSupportedException>(() => builder.Register<ValueA>(new(First, 1, [], allowMultiple: true), _ => new(43)));
        Assert.Equal(0, builder.Build().Count);
        builder.Register<ValueA>(Schema(First), _ => new(47));
        Assert.Equal(1, builder.Build().Count);
    }

    [Fact]
    public void Registration_AcceptsNestedUnmanagedEnumsAndTags()
    {
        var builder = new ComponentRegistryBuilder()
            .Register<NestedValue>(Schema(First), _ => new(new(53), 59))
            .Register<Mode>(Schema(Second), _ => Mode.Active)
            .Register<Tag>(Schema(Third), _ => new());
        var registry = builder.Build();
        Assert.Equal(3, registry.Count);
        Assert.Equal(typeof(NestedValue), registry.Binding(Schema(First), Scene).RuntimeType);
        Assert.Equal(typeof(Mode), registry.Binding(Schema(Second), Scene).RuntimeType);
        Assert.Equal(typeof(Tag), registry.Binding(Schema(Third), Scene).RuntimeType);
    }

    [Fact]
    public void Binding_RequiresExactVersionAndReportsStableSceneContext()
    {
        var registry = new ComponentRegistryBuilder().Register<ValueA>(Schema(First, 2), _ => new(61)).Build();
        var unknown = Assert.Throws<SceneWorldLoadException>(() => registry.Binding(Schema(Second), Scene));
        var earlier = Assert.Throws<SceneWorldLoadException>(() => registry.Binding(Schema(First), Scene));
        var later = Assert.Throws<SceneWorldLoadException>(() => registry.Binding(Schema(First, 3), Scene));
        Assert.Equal(SceneWorldLoadError.UnknownComponent, unknown.Error);
        Assert.Equal(SceneWorldLoadError.UnsupportedSchemaVersion, earlier.Error);
        Assert.Equal(SceneWorldLoadError.UnsupportedSchemaVersion, later.Error);
        Assert.Equal(Scene, earlier.SceneId);
        Assert.Null(earlier.EntityId);
        Assert.Null(earlier.CleanupOwner);
        Assert.Equal(2, registry.Binding(Schema(First, 2), Scene).Schema.Version);
        Assert.Equal(1, registry.Count);
    }

    [Theory]
    [InlineData("kind")]
    [InlineData("nullable-kind")]
    [InlineData("default")]
    [InlineData("nullable")]
    [InlineData("missing-property")]
    [InlineData("added-property")]
    [InlineData("property-case")]
    [InlineData("required-types")]
    public void Binding_RejectsSchemaDriftUnderSameStableVersion(string kind)
    {
        var expected = kind == "nullable-kind" ? new ComponentSchema(First, 1,
            [new("x", PropertyValueKind.Number, PropertyValue.From(0d)), new("label", PropertyValueKind.String, PropertyValue.Null, true)]) : Schema(First);
        var properties = expected.Properties.Values.ToList();
        var actual = kind switch
        {
            "kind" => new ComponentSchema(First, 1,
                [new("x", PropertyValueKind.Integer, PropertyValue.From(0L)), properties.Single(p => p.Id == "label")]),
            "nullable-kind" => new(First, 1,
                [properties.Single(p => p.Id == "x"), new("label", PropertyValueKind.Number, PropertyValue.Null, true)]),
            "default" => new(First, 1,
                [new("x", PropertyValueKind.Number, PropertyValue.From(1d)), properties.Single(p => p.Id == "label")]),
            "nullable" => new(First, 1,
                [properties.Single(p => p.Id == "x"), new("label", PropertyValueKind.String, PropertyValue.From("fallback"), false)]),
            "missing-property" => new(First, 1, [properties.Single(p => p.Id == "x")]),
            "added-property" => new(First, 1, [.. properties, new("extra", PropertyValueKind.Boolean, PropertyValue.From(true))]),
            "property-case" => new(First, 1,
                [new("X", PropertyValueKind.Number, PropertyValue.From(0d)), properties.Single(p => p.Id == "label")]),
            "required-types" => new(First, 1, properties, requiredTypes: [Second]),
            _ => throw new ArgumentException(nameof(kind))
        };
        var registry = new ComponentRegistryBuilder().Register<ValueA>(expected, _ => new(67)).Build();
        var error = Assert.Throws<SceneWorldLoadException>(() => registry.Binding(actual, Scene));
        Assert.Equal(SceneWorldLoadError.SchemaMismatch, error.Error);
        Assert.Equal(Scene, error.SceneId);
        Assert.Equal(expected, registry.Binding(expected, Scene).Schema);
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void Binding_MatchesUnorderedMetadataAndRejectsMultiplicityExplicitly()
    {
        var expected = new ComponentSchema(First, 1, Schema(First).Properties.Values, requiredTypes: [Second, Third]);
        var actual = new ComponentSchema(First, 1, Schema(First).Properties.Values.Reverse(), requiredTypes: [Third, Second]);
        var registry = new ComponentRegistryBuilder().Register<ValueA>(expected, _ => new(71)).Build();
        Assert.Equal(typeof(ValueA), registry.Binding(actual, Scene).RuntimeType);
        var multiple = new ComponentSchema(First, 1, actual.Properties.Values, allowMultiple: true, requiredTypes: [Second, Third]);
        var error = Assert.Throws<SceneWorldLoadException>(() => registry.Binding(multiple, Scene));
        Assert.Equal(SceneWorldLoadError.UnsupportedMultiplicity, error.Error);
        Assert.Equal(Scene, error.SceneId);
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void ReferenceMetadata_IsCopiedWithOrdinalKeysAndNullableStringSemantics()
    {
        var schema = new ComponentSchema(First, 1,
        [new("target", PropertyValueKind.String, PropertyValue.Null, true), new("Target", PropertyValueKind.String, PropertyValue.From("asset"))]);
        var references = new List<ComponentReference>
        { new("target", ComponentReferenceKind.Entity), new("Target", ComponentReferenceKind.Asset) };
        var builder = new ComponentRegistryBuilder().Register<ValueA>(schema, _ => new(73), references);
        references.Clear(); references.Add(new("target", ComponentReferenceKind.Asset));
        var registry = builder.Build();
        var binding = registry.Binding(schema, Scene);
        Assert.Equal(2, binding.References.Count);
        Assert.Equal(ComponentReferenceKind.Entity, binding.References["target"]);
        Assert.Equal(ComponentReferenceKind.Asset, binding.References["Target"]);
        Assert.Equal(PropertyValue.Null, Assert.Single(registry.Schemas).Properties["target"].DefaultValue);
        Assert.True(schema.Properties["target"].IsNullable);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong-kind")]
    [InlineData("duplicate")]
    [InlineData("null-entry")]
    public void ReferenceMetadata_RejectsInvalidDeclarationsWithoutReservingBinding(string invalid)
    {
        var schema = Schema(First);
        ComponentReference[] references = invalid switch
        {
            "missing" => [new("absent", ComponentReferenceKind.Entity)],
            "wrong-kind" => [new("x", ComponentReferenceKind.Entity)],
            "duplicate" => [new("label", ComponentReferenceKind.Entity), new("label", ComponentReferenceKind.Asset)],
            "null-entry" => [null!],
            _ => throw new ArgumentException(nameof(invalid))
        };
        var builder = new ComponentRegistryBuilder();
        Assert.ThrowsAny<ArgumentException>(() => builder.Register<ValueA>(schema, _ => new(79), references));
        Assert.Equal(0, builder.Build().Count);
        builder.Register<ValueA>(schema, _ => new(83), [new("label", ComponentReferenceKind.Entity)]);
        Assert.Equal(ComponentReferenceKind.Entity, builder.Build().Binding(schema, Scene).References["label"]);
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")] [InlineData("a/b")] [InlineData("цвет")]
    public void ReferenceDescriptor_RejectsInvalidPropertyKeys(string? property)
        => Assert.ThrowsAny<ArgumentException>(() => new ComponentReference(property!, ComponentReferenceKind.Entity));

    [Fact]
    public void ReferenceDescriptor_RejectsUnknownKindsAndPreservesAcceptedMetadata()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ComponentReference("target", (ComponentReferenceKind)(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ComponentReference("target", (ComponentReferenceKind)2));
        var entity = new ComponentReference("target_id.v1", ComponentReferenceKind.Entity);
        var asset = new ComponentReference("Asset", ComponentReferenceKind.Asset);
        Assert.Equal(("target_id.v1", ComponentReferenceKind.Entity), (entity.PropertyId, entity.Kind));
        Assert.Equal(("Asset", ComponentReferenceKind.Asset), (asset.PropertyId, asset.Kind));
    }

    [Fact]
    public void AssetValidator_IsBorrowedAndFrozenPerBuildWithoutAutomaticInvocation()
    {
        var oldCalls = 0; var newCalls = 0; var asset = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var builder = new ComponentRegistryBuilder();
        Assert.Null(builder.Build().AssetValidator);
        Assert.Same(builder, builder.WithAssetValidator((id, token) =>
        { Assert.Equal(asset, id); Assert.Equal(cancellation.Token, token); oldCalls++; }));
        var first = builder.Build();
        Assert.Throws<ArgumentNullException>(() => builder.WithAssetValidator(null!));
        builder.WithAssetValidator((id, token) => { Assert.Equal(asset, id); newCalls++; });
        var second = builder.Build();
        Assert.Equal((0, 0), (oldCalls, newCalls));
        first.AssetValidator!(asset, cancellation.Token);
        second.AssetValidator!(asset, cancellation.Token);
        Assert.Equal((1, 1), (oldCalls, newCalls));
        Assert.Equal((0, 0), (first.Count, second.Count));
    }

    [Fact]
    public void BindingBudget_Admits1024ExactVersionsAndRejectsOneBeyondWithoutChangingSnapshot()
    {
        var builder = new ComponentRegistryBuilder();
        for (var version = 1; version <= 1024; version++) builder.Register<ValueA>(Schema(First, version), _ => new(89));
        var full = builder.Build();
        Assert.Equal(1024, full.Count);
        Assert.Equal((1, 1024), (full.Schemas[0].Version, full.Schemas[^1].Version));
        Assert.Equal(512, full.Binding(Schema(First, 512), Scene).Schema.Version);
        Assert.Throws<InvalidOperationException>(() => builder.Register<ValueA>(Schema(First, 1025), _ => new(97)));
        Assert.Equal((1024, 1024), (full.Count, builder.Build().Count));
        Assert.Equal(SceneWorldLoadError.UnsupportedSchemaVersion,
            Assert.Throws<SceneWorldLoadException>(() => full.Binding(Schema(First, 1025), Scene)).Error);
    }

    private static ComponentSchema Schema(ComponentTypeId id, int version = 1) => new(id, version,
        [new("x", PropertyValueKind.Number, PropertyValue.From(0d)), new("label", PropertyValueKind.String, PropertyValue.From("fallback"), true)]);
    private readonly record struct ValueA(int Value);
    private readonly record struct ValueB(int Value);
    private readonly record struct NestedValue(ValueA Value, int Layer);
    private readonly record struct ManagedValue(string Value);
    private readonly record struct NestedManaged(ManagedValue Value);
    private readonly struct Tag { }
    private enum Mode { Idle, Active }
}
