using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using Xunit;
using static TrueMoon.Aether.Tests.Scenes.Authoring.AuthoringTestData;

namespace TrueMoon.Aether.Tests.Scenes.Authoring;

public sealed class ComponentPropertyEditTests
{
    [Fact]
    public void Properties_AtomicMixedScalarEditsPreserveExactKindsNullAndInheritedSources()
    {
        var source = ScalarDocument();
        using var session = Session(source);
        var before = session.Read();
        var operation = Operation(7);
        var result = session.EditComponents(new(operation,
        [
            new(First, Custom, "flag", PropertyValue.From(false)),
            new(First, Custom, "count", PropertyValue.From(long.MaxValue)),
            new(First, Custom, "amount", PropertyValue.From(-.125)),
            new(First, Custom, "text", PropertyValue.From("Изменено 🌙")),
            new(First, Custom, "optional", PropertyValue.Null),
            new(First, Transform, "x", PropertyValue.From(12d))
        ]));

        Assert.True(result.Succeeded);
        Assert.Same(operation, result.Operation);
        Assert.Equal(8, result.Revision);
        Assert.Equal(8, result.ContentRevision);
        var state = session.Read();
        Assert.Equal(1, state.UndoCount);
        Assert.Equal(0, state.RedoCount);
        var properties = state.Resolved.Entities[First].Components[Custom].Properties;
        Assert.False(properties["flag"].Value.GetBoolean());
        Assert.Equal(PropertyValueKind.Integer, properties["count"].Value.Kind);
        Assert.Equal(long.MaxValue, properties["count"].Value.GetInteger());
        Assert.Equal(PropertyValueKind.Number, properties["amount"].Value.Kind);
        Assert.Equal(-.125, properties["amount"].Value.GetNumber());
        Assert.Equal("Изменено 🌙", properties["text"].Value.GetString());
        Assert.Equal(PropertyValueKind.Null, properties["optional"].Value.Kind);
        foreach (var property in properties.Values)
        {
            Assert.Equal(First, property.Source.EntityId);
            Assert.Null(property.Source.DefinitionId);
            Assert.False(property.IsSchemaDefault);
        }
        Assert.Equal(12, Number(state, First, "x"));
        Assert.Equal(17, state.Resolved.Entities[Second].Components[Custom].Properties["count"].Value.GetInteger());
        Assert.Equal(Definition, state.Resolved.Entities[Second].Components[Custom].Properties["count"].Source.DefinitionId);
        Assert.Equal(source.Definitions.AsEnumerable(), state.Document.Definitions.AsEnumerable());
        Assert.Equal(source.Schemas.AsEnumerable(), state.Document.Schemas.AsEnumerable());
        Assert.Equal(First, state.Resolved.Entities[Second].ParentId);
        Assert.Same(source, before.Document);
        Assert.Equal(3, Number(before, First, "x"));
        Assert.Equal("instance", before.Resolved.Entities[First].Components[Custom].Properties["text"].Value.GetString());
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(0)]
    [InlineData(long.MaxValue)]
    public void Properties_IntegerExtremesNeverCoerceThroughDouble(long value)
    {
        using var session = Session(ScalarDocument());

        Assert.True(session.EditComponents(new(Operation(7), [new(First, Custom, "count", PropertyValue.From(value))])).Succeeded);

        var resolved = session.Read().Resolved.Entities[First].Components[Custom].Properties["count"];
        Assert.Equal(PropertyValueKind.Integer, resolved.Value.Kind);
        Assert.Equal(value, resolved.Value.GetInteger());
        Assert.Equal(value, Patch(session.Read()).PropertyOverrides.Single(p => p.ComponentId == Custom && p.PropertyId == "count").Value.GetInteger());
    }

    [Fact]
    public void Properties_GenericFiniteNumberAndMaximumStringAreStoredWithoutBuiltInTransformLimits()
    {
        using var session = Session(ScalarDocument());
        var text = new string('Ж', PropertyValue.MaximumStringLength);

        Assert.True(session.EditComponents(new(Operation(7),
            [new(First, Custom, "amount", PropertyValue.From(double.MaxValue)), new(First, Custom, "text", PropertyValue.From(text))])).Succeeded);

        var properties = session.Read().Resolved.Entities[First].Components[Custom].Properties;
        Assert.Equal(double.MaxValue, properties["amount"].Value.GetNumber());
        Assert.Equal(text, properties["text"].Value.GetString());
        Assert.Equal(PropertyValue.MaximumStringLength, properties["text"].Value.GetString().Length);
        Assert.Equal(3, Number(session.Read(), First, "x"));
    }

    [Theory]
    [InlineData("integer-as-number")]
    [InlineData("number-as-integer")]
    [InlineData("boolean-as-string")]
    [InlineData("string-as-boolean")]
    [InlineData("null-nonnullable")]
    [InlineData("unknown-property")]
    [InlineData("case-sensitive-property")]
    [InlineData("reset-unknown-property")]
    [InlineData("missing-component")]
    [InlineData("hidden-component")]
    [InlineData("missing-entity")]
    [InlineData("duplicate-property")]
    public void Properties_InvalidAddressOrExactSchemaTypeRejectsWholeTransactionAndCachesReceipt(string invalid)
    {
        using var session = Session(ScalarDocument());
        var before = session.Read();
        var bad = invalid switch
        {
            "integer-as-number" => new PropertyEdit(First, Custom, "amount", PropertyValue.From(2L)),
            "number-as-integer" => new(First, Custom, "count", PropertyValue.From(2d)),
            "boolean-as-string" => new(First, Custom, "text", PropertyValue.From(true)),
            "string-as-boolean" => new(First, Custom, "flag", PropertyValue.From("false")),
            "null-nonnullable" => new(First, Custom, "text", PropertyValue.Null),
            "unknown-property" => new(First, Custom, "missing", PropertyValue.From("bad")),
            "case-sensitive-property" => new(First, Custom, "Text", PropertyValue.From("bad")),
            "reset-unknown-property" => new(First, Custom, "missing", Reset: true),
            "missing-component" => new(First, new ComponentId(Id(999)), "text", PropertyValue.From("bad")),
            "hidden-component" => new(First, Interaction, "enabled", PropertyValue.From(false)),
            "missing-entity" => new(Entity(999), Custom, "text", PropertyValue.From("bad")),
            _ => new(First, Transform, "x", PropertyValue.From(20d))
        };
        var batch = new ComponentEditBatch(Operation(7), [new(First, Transform, "x", PropertyValue.From(9d)), bad]);

        var result = session.EditComponents(batch);

        Assert.False(result.Succeeded);
        var expected = invalid switch
        {
            "missing-component" or "hidden-component" => SceneAuthoringError.MissingComponent,
            "missing-entity" => SceneAuthoringError.MissingEntity,
            "duplicate-property" => SceneAuthoringError.DuplicateTarget,
            _ => SceneAuthoringError.InvalidScene
        };
        Assert.Equal(expected, result.Error);
        Assert.NotEmpty(result.Path);
        Assert.NotEmpty(result.Message);
        Assert.Equal(7, result.Revision);
        Assert.Same(before, session.Read());
        Assert.Equal(3, Number(session.Read(), First, "x"));
        Assert.Equal(0, session.Read().UndoCount);
        Assert.Same(result, session.EditComponents(batch));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("text\n")]
    [InlineData("text key")]
    [InlineData("a/b")]
    [InlineData("имя")]
    public void Properties_InvalidPropertyKeyRejectsWithoutMutation(string? key)
    {
        using var session = Session(ScalarDocument());
        var before = session.Read();

        var result = session.EditComponents(new(Operation(7), [new(First, Custom, key!, PropertyValue.From("bad"))]));

        Assert.Equal(SceneAuthoringError.InvalidScene, result.Error);
        Assert.Same(before, session.Read());
    }

    [Fact]
    public void Properties_KeyAcceptsExact128AsciiCharactersAndRejectsImmediatelyLongerAddress()
    {
        var source = Document();
        var key = new string('a', 128);
        var schema = new ComponentSchema(CustomType, 1,
            [new("note", PropertyValueKind.String, PropertyValue.From("")), new(key, PropertyValueKind.Number, PropertyValue.From(0d))]);
        using var session = Session(new(source.Id, 7, source.Entities, source.Definitions, [.. BuiltInComponents.Schemas, schema]));
        Assert.True(session.EditComponents(new(Operation(7), [new(First, Custom, key, PropertyValue.From(9d))])).Succeeded);
        var before = session.Read();
        Assert.Equal(9, before.Resolved.Entities[First].Components[Custom].Properties[key].Value.GetNumber());

        var result = session.EditComponents(new(Operation(8), [new(First, Custom, key + "a", PropertyValue.From(10d))]));

        Assert.Equal(SceneAuthoringError.InvalidScene, result.Error);
        Assert.Same(before, session.Read());
    }

    [Theory]
    [InlineData("sx", 0d)]
    [InlineData("sy", -1d)]
    [InlineData("sz", double.Epsilon)]
    [InlineData("x", double.MaxValue)]
    public void Properties_BuiltInTransformConstraintsRejectFiniteButUnrenderableValuesAtomically(string key, double value)
    {
        using var session = Session(ScalarDocument());
        var before = session.Read();

        var result = session.EditComponents(new(Operation(7),
            [new(First, Custom, "text", PropertyValue.From("must not commit")), new(First, Transform, key, PropertyValue.From(value))]));

        Assert.False(result.Succeeded);
        Assert.Equal(SceneAuthoringError.InvalidScene, result.Error);
        Assert.NotEmpty(result.Message);
        Assert.Same(before, session.Read());
        Assert.Equal("instance", session.Read().Resolved.Entities[First].Components[Custom].Properties["text"].Value.GetString());
    }

    [Fact]
    public void Reset_InheritedPropertyClearsOnlyLocalOverrideAndRestoresDefinitionProvenance()
    {
        using var session = Session(ScalarDocument());
        var before = session.Read();

        Assert.True(session.EditComponents(new(Operation(7), [new(First, Custom, "text", PropertyValue.From("ignored"), Reset: true)])).Succeeded);

        var state = session.Read();
        var text = state.Resolved.Entities[First].Components[Custom].Properties["text"];
        Assert.Equal("inherited", text.Value.GetString());
        Assert.Equal(Definition, text.Source.DefinitionId);
        Assert.Null(text.Source.EntityId);
        Assert.False(text.IsSchemaDefault);
        Assert.DoesNotContain(Patch(state).PropertyOverrides, p => p.ComponentId == Custom && p.PropertyId == "text");
        Assert.Equal(3, Number(state, First, "x"));
        Assert.Equal(.25, state.Resolved.Entities[First].Components[Appearance].Properties["g"].Value.GetNumber());
        Assert.Contains(Interaction, Patch(state).RemovedComponents);
        Assert.Equal("instance", before.Resolved.Entities[First].Components[Custom].Properties["text"].Value.GetString());
    }

    [Fact]
    public void Reset_LocalAdditionRemovesBothExplicitDataAndOverrideReturningToSchemaDefault()
    {
        using var session = Session();
        Assert.True(session.EditComponents(new(Operation(7), [new(First, Custom, "note", PropertyValue.From("override"))])).Succeeded);

        Assert.True(session.EditComponents(new(Operation(8), [new(First, Custom, "note", Reset: true)])).Succeeded);

        var state = session.Read();
        var component = state.Resolved.Entities[First].Components[Custom];
        var property = component.Properties["note"];
        Assert.Equal("", property.Value.GetString());
        Assert.True(property.IsSchemaDefault);
        Assert.Equal(First, property.Source.EntityId);
        Assert.Equal(First, component.Source.EntityId);
        Assert.False(Patch(state).AddedComponents.Single(c => c.Id == Custom).Properties.ContainsKey("note"));
        Assert.DoesNotContain(Patch(state).PropertyOverrides, p => p.ComponentId == Custom && p.PropertyId == "note");
        Assert.True(session.Undo(Operation(9)).Succeeded);
        Assert.Equal("override", session.Read().Resolved.Entities[First].Components[Custom].Properties["note"].Value.GetString());
        Assert.Equal("keep", Patch(session.Read()).AddedComponents.Single(c => c.Id == Custom).Properties["note"].GetString());
        Assert.True(session.Redo(Operation(10)).Succeeded);
        Assert.True(session.Read().Resolved.Entities[First].Components[Custom].Properties["note"].IsSchemaDefault);
    }

    [Fact]
    public void Reset_ExplicitNullIsDistinctFromAbsentOverrideAndCanReturnToNonNullDefault()
    {
        using var session = Session(ScalarDocument());
        Assert.True(session.EditComponents(new(Operation(7), [new(First, Custom, "optional", PropertyValue.Null)])).Succeeded);
        var nullable = session.Read().Resolved.Entities[First].Components[Custom].Properties["optional"];
        Assert.Equal(PropertyValueKind.Null, nullable.Value.Kind);
        Assert.False(nullable.IsSchemaDefault);
        Assert.Equal(First, nullable.Source.EntityId);

        Assert.True(session.EditComponents(new(Operation(8), [new(First, Custom, "optional", Reset: true)])).Succeeded);

        var inherited = session.Read().Resolved.Entities[First].Components[Custom].Properties["optional"];
        Assert.Equal("schema optional", inherited.Value.GetString());
        Assert.True(inherited.IsSchemaDefault);
        Assert.Equal(Definition, inherited.Source.DefinitionId);
        Assert.DoesNotContain(Patch(session.Read()).PropertyOverrides, p => p.ComponentId == Custom && p.PropertyId == "optional");
    }

    internal static EntityPatch Patch(SceneAuthoringState state, EntityId? entity = null)
        => state.Document.Entities.Single(e => e.Id == (entity ?? First)).Changes;

    internal static SceneDocument ScalarDocument()
    {
        var source = Document();
        var schema = new ComponentSchema(CustomType, 1,
        [
            new("flag", PropertyValueKind.Boolean, PropertyValue.From(true)),
            new("count", PropertyValueKind.Integer, PropertyValue.From(42L)),
            new("amount", PropertyValueKind.Number, PropertyValue.From(.5d)),
            new("text", PropertyValueKind.String, PropertyValue.From("schema")),
            new("optional", PropertyValueKind.String, PropertyValue.From("schema optional"), isNullable: true)
        ]);
        var definition = new EntityDefinition(Definition, new(addedComponents:
        [
            new(Transform, BuiltInComponents.TransformId, 1),
            new(Appearance, BuiltInComponents.AppearanceId, 1),
            new(Interaction, BuiltInComponents.InteractionId, 1),
            new(Custom, CustomType, 1, [new("text", PropertyValue.From("inherited")), new("count", PropertyValue.From(17L))])
        ]));
        var patch = new EntityPatch(removedComponents: [Interaction], propertyOverrides:
        [
            new(Transform, "x", PropertyValue.From(3d)),
            new(Appearance, "g", PropertyValue.From(.25d)),
            new(Custom, "text", PropertyValue.From("instance"))
        ]);
        return new(source.Id, source.Revision, [new EntityInstance(First, Definition, patch), new EntityInstance(Second, Definition, parentId: First)],
            [definition], [.. BuiltInComponents.Schemas, schema]);
    }
}
