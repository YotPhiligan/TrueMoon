using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using Xunit;
using static TrueMoon.Aether.Tests.Scenes.Authoring.AuthoringTestData;
using static TrueMoon.Aether.Tests.Scenes.Authoring.ComponentPropertyEditTests;

namespace TrueMoon.Aether.Tests.Scenes.Authoring;

public sealed class ComponentPresenceEditTests
{
    private static ComponentId Added => new(Id(104));
    private static ComponentId Dependent => new(Id(105));
    private static ComponentTypeId DependentType => new(Id(201));

    [Fact]
    public void Add_AndPropertiesAreOneAtomicTransactionWithInstancePresenceAndPropertyProvenance()
    {
        var source = Document();
        using var session = Session(source);
        var addition = new ComponentData(Added, CustomType, 1, [new("note", PropertyValue.From("introduced"))]);

        var result = session.EditComponents(new(Operation(7),
            [new(First, Added, "note", PropertyValue.From("edited"))],
            [new(First, Custom, ComponentChangeKind.Remove), new(First, Added, ComponentChangeKind.Add, addition)]));

        Assert.True(result.Succeeded);
        var state = session.Read();
        Assert.Equal(8, state.Document.Revision);
        Assert.Equal(1, state.UndoCount);
        Assert.False(state.Resolved.Entities[First].Components.ContainsKey(Custom));
        var component = state.Resolved.Entities[First].Components[Added];
        Assert.Equal(CustomType, component.TypeId);
        Assert.Equal(1, component.SchemaVersion);
        Assert.Equal(First, component.Source.EntityId);
        Assert.Equal(First, component.PresenceSource.EntityId);
        Assert.Equal("edited", component.Properties["note"].Value.GetString());
        Assert.Equal(First, component.Properties["note"].Source.EntityId);
        Assert.False(component.Properties["note"].IsSchemaDefault);
        Assert.Equal("introduced", Assert.Single(Patch(state).AddedComponents).Properties["note"].GetString());
        Assert.Equal("edited", Patch(state).PropertyOverrides.Single(p => p.ComponentId == Added).Value.GetString());
        Assert.Equal(source.Definitions.AsEnumerable(), state.Document.Definitions.AsEnumerable());
        Assert.Equal(source.Schemas.AsEnumerable(), state.Document.Schemas.AsEnumerable());
        Assert.Equal(3, Number(state, First, "x"));
        Assert.Equal(.25, state.Resolved.Entities[First].Components[Appearance].Properties["g"].Value.GetNumber());
        Assert.Equal(First, state.Resolved.Entities[Second].ParentId);
    }

    [Fact]
    public void Add_AndResetDiscardExplicitNewDataBeforeResolutionAndUseSchemaDefault()
    {
        using var session = Session();

        Assert.True(session.EditComponents(new(Operation(7), [new(Second, Added, "note", Reset: true)],
            [new(Second, Added, ComponentChangeKind.Add, new(Added, CustomType, 1, [new("note", PropertyValue.From("ignored"))]))])).Succeeded);

        var property = session.Read().Resolved.Entities[Second].Components[Added].Properties["note"];
        Assert.Equal("", property.Value.GetString());
        Assert.True(property.IsSchemaDefault);
        Assert.Equal(Second, property.Source.EntityId);
        Assert.Empty(Patch(session.Read(), Second).AddedComponents.Single(c => c.Id == Added).Properties);
    }

    [Fact]
    public void Remove_LocalAdditionDeletesOccurrenceAndAllItsOverridesWithoutMaskingDefinition()
    {
        using var session = Session();
        Assert.True(session.EditComponents(new(Operation(7), [new(First, Custom, "note", PropertyValue.From("changed"))])).Succeeded);
        var before = session.Read();

        Assert.True(session.EditComponents(new(Operation(8), [], [new(First, Custom, ComponentChangeKind.Remove)])).Succeeded);

        var state = session.Read();
        Assert.False(state.Resolved.Entities[First].Components.ContainsKey(Custom));
        Assert.DoesNotContain(Patch(state).AddedComponents, c => c.Id == Custom);
        Assert.DoesNotContain(Custom, Patch(state).RemovedComponents);
        Assert.DoesNotContain(Custom, Patch(state).RestoredComponents);
        Assert.DoesNotContain(Patch(state).PropertyOverrides, p => p.ComponentId == Custom);
        Assert.Equal(3, Number(state, First, "x"));
        Assert.True(session.Undo(Operation(9)).Succeeded);
        Assert.Equal("changed", session.Read().Resolved.Entities[First].Components[Custom].Properties["note"].Value.GetString());
        Assert.Equal("keep", Patch(session.Read()).AddedComponents.Single(c => c.Id == Custom).Properties["note"].GetString());
        Assert.Equal("changed", before.Resolved.Entities[First].Components[Custom].Properties["note"].Value.GetString());
        Assert.True(session.Redo(Operation(10)).Succeeded);
        Assert.False(session.Read().Resolved.Entities[First].Components.ContainsKey(Custom));
    }

    [Fact]
    public void Remove_InheritedOccurrenceClearsOverridesAndRestoreClearsMaskWithInheritedProvenance()
    {
        using var session = Session();
        var source = session.Read();

        Assert.True(session.EditComponents(new(Operation(7), [], [new(First, Appearance, ComponentChangeKind.Remove)])).Succeeded);

        var removed = session.Read();
        Assert.False(removed.Resolved.Entities[First].Components.ContainsKey(Appearance));
        Assert.Contains(Appearance, Patch(removed).RemovedComponents);
        Assert.DoesNotContain(Patch(removed).PropertyOverrides, p => p.ComponentId == Appearance);
        Assert.True(session.EditComponents(new(Operation(8), [], [new(First, Appearance, ComponentChangeKind.Restore)])).Succeeded);
        var restored = session.Read().Resolved.Entities[First].Components[Appearance];
        Assert.Equal(Definition, restored.Source.DefinitionId);
        Assert.Equal(Definition, restored.PresenceSource.DefinitionId);
        Assert.Null(restored.PresenceSource.EntityId);
        Assert.Equal(1, restored.Properties["g"].Value.GetNumber());
        Assert.True(restored.Properties["g"].IsSchemaDefault);
        Assert.DoesNotContain(Appearance, Patch(session.Read()).RemovedComponents);
        Assert.DoesNotContain(Appearance, Patch(session.Read()).RestoredComponents);
        Assert.Equal(.25, source.Resolved.Entities[First].Components[Appearance].Properties["g"].Value.GetNumber());
    }

    [Fact]
    public void Restore_AncestorMaskUsesInstanceRestorationAndRemoveReturnsToAncestorHiddenState()
    {
        var source = Document();
        var variantId = new EntityDefinitionId(Id(11));
        var variant = new EntityDefinition(variantId, new(removedComponents: [Interaction]), Definition);
        var first = source.Entities[0];
        var patch = new EntityPatch(first.Changes.AddedComponents, propertyOverrides: first.Changes.PropertyOverrides);
        source = new(source.Id, 7,
            [new EntityInstance(First, variantId, patch), new EntityInstance(Second, Definition, parentId: First)],
            source.Definitions.Add(variant), source.Schemas);
        using var session = Session(source);

        Assert.True(session.EditComponents(new(Operation(7),
            [new(First, Interaction, "enabled", PropertyValue.From(false))],
            [new(First, Interaction, ComponentChangeKind.Restore)])).Succeeded);

        var restored = session.Read().Resolved.Entities[First].Components[Interaction];
        Assert.Equal(Definition, restored.Source.DefinitionId);
        Assert.Equal(First, restored.PresenceSource.EntityId);
        Assert.False(restored.Properties["enabled"].Value.GetBoolean());
        Assert.Equal(First, restored.Properties["enabled"].Source.EntityId);
        Assert.Contains(Interaction, Patch(session.Read()).RestoredComponents);
        Assert.DoesNotContain(Interaction, Patch(session.Read()).RemovedComponents);
        Assert.True(session.EditComponents(new(Operation(8), [], [new(First, Interaction, ComponentChangeKind.Remove)])).Succeeded);
        var hidden = session.Read();
        Assert.False(hidden.Resolved.Entities[First].Components.ContainsKey(Interaction));
        Assert.DoesNotContain(Interaction, Patch(hidden).RemovedComponents);
        Assert.DoesNotContain(Interaction, Patch(hidden).RestoredComponents);
        Assert.DoesNotContain(Patch(hidden).PropertyOverrides, p => p.ComponentId == Interaction);
        Assert.True(session.Undo(Operation(9)).Succeeded);
        Assert.Contains(Interaction, Patch(session.Read()).RestoredComponents);
        Assert.False(session.Read().Resolved.Entities[First].Components[Interaction].Properties["enabled"].Value.GetBoolean());
        Assert.Equal(new[] { Definition, variantId }, session.Read().Resolved.Entities[First].DefinitionChain);
    }

    [Fact]
    public void Restore_AndResetHiddenPropertiesAtomicallyRetainAncestorOverrideAndSchemaDefaultProvenance()
    {
        var source = ScalarDocument();
        var valueVariantId = new EntityDefinitionId(Id(11));
        var hiddenVariantId = new EntityDefinitionId(Id(12));
        var valueVariant = new EntityDefinition(valueVariantId,
            new(propertyOverrides: [new(Custom, "text", PropertyValue.From("variant value"))]), Definition);
        var hiddenVariant = new EntityDefinition(hiddenVariantId, new(removedComponents: [Custom]), valueVariantId);
        var original = source.Entities[0];
        var patch = new EntityPatch(original.Changes.AddedComponents, original.Changes.RemovedComponents,
            original.Changes.PropertyOverrides.Where(p => p.ComponentId != Custom), original.Changes.RestoredComponents);
        source = new(source.Id, 7, [new EntityInstance(First, hiddenVariantId, patch), source.Entities[1]],
            source.Definitions.Add(valueVariant).Add(hiddenVariant), source.Schemas);
        using var session = Session(source);
        var before = session.Read();
        Assert.False(before.Resolved.Entities[First].Components.ContainsKey(Custom));

        var result = session.EditComponents(new(Operation(7),
            [new(First, Custom, "text", PropertyValue.From("ignored"), Reset: true), new(First, Custom, "flag", Reset: true)],
            [new(First, Custom, ComponentChangeKind.Restore)]));

        Assert.True(result.Succeeded);
        var state = session.Read();
        Assert.Equal(8, state.Document.Revision);
        Assert.Equal(1, state.UndoCount);
        Assert.Contains(Custom, Patch(state).RestoredComponents);
        Assert.DoesNotContain(Patch(state).PropertyOverrides, p => p.ComponentId == Custom);
        var component = state.Resolved.Entities[First].Components[Custom];
        Assert.Equal(Definition, component.Source.DefinitionId);
        Assert.Equal(First, component.PresenceSource.EntityId);
        Assert.Equal("variant value", component.Properties["text"].Value.GetString());
        Assert.Equal(valueVariantId, component.Properties["text"].Source.DefinitionId);
        Assert.False(component.Properties["text"].IsSchemaDefault);
        Assert.True(component.Properties["flag"].Value.GetBoolean());
        Assert.True(component.Properties["flag"].IsSchemaDefault);
        Assert.Equal(Definition, component.Properties["flag"].Source.DefinitionId);
        Assert.Equal(3, Number(state, First, "x"));
        Assert.False(before.Resolved.Entities[First].Components.ContainsKey(Custom));
        Assert.True(session.Undo(Operation(8)).Succeeded);
        Assert.False(session.Read().Resolved.Entities[First].Components.ContainsKey(Custom));
        Assert.Equal(7, session.Read().ContentRevision);
        Assert.True(session.Redo(Operation(9)).Succeeded);
        Assert.Equal("variant value", session.Read().Resolved.Entities[First].Components[Custom].Properties["text"].Value.GetString());
        Assert.True(session.Read().Resolved.Entities[First].Components[Custom].Properties["flag"].IsSchemaDefault);
        Assert.Equal(8, session.Read().ContentRevision);
    }

    [Fact]
    public void ResetRemoval_ClearsOnlyInstanceMaskAndRestoresOriginalComponentValues()
    {
        using var session = Session();

        Assert.True(session.EditComponents(new(Operation(7), [], [new(First, Interaction, ComponentChangeKind.ResetRemoval)])).Succeeded);

        var state = session.Read();
        Assert.DoesNotContain(Interaction, Patch(state).RemovedComponents);
        Assert.DoesNotContain(Interaction, Patch(state).RestoredComponents);
        var component = state.Resolved.Entities[First].Components[Interaction];
        Assert.Equal(Definition, component.PresenceSource.DefinitionId);
        Assert.True(component.Properties["enabled"].Value.GetBoolean());
        Assert.Equal("keep", state.Resolved.Entities[First].Components[Custom].Properties["note"].Value.GetString());
        Assert.True(session.Undo(Operation(8)).Succeeded);
        Assert.Contains(Interaction, Patch(session.Read()).RemovedComponents);
        Assert.False(session.Read().Resolved.Entities[First].Components.ContainsKey(Interaction));
    }

    [Fact]
    public void Remove_DependentsAndRequiredTransformTogetherValidateOnlyFinalEffectiveEntity()
    {
        using var session = Session();

        Assert.True(session.EditComponents(new(Operation(7), [],
            [new(First, Transform, ComponentChangeKind.Remove), new(First, Appearance, ComponentChangeKind.Remove)])).Succeeded);

        var state = session.Read();
        Assert.Equal(new[] { Custom }, state.Resolved.Entities[First].Components.Keys);
        Assert.Contains(Transform, Patch(state).RemovedComponents);
        Assert.Contains(Appearance, Patch(state).RemovedComponents);
        Assert.DoesNotContain(Patch(state).PropertyOverrides, p => p.ComponentId == Transform || p.ComponentId == Appearance);
        Assert.True(session.Undo(Operation(8)).Succeeded);
        Assert.Equal(3, Number(session.Read(), First, "x"));
        Assert.Equal(.25, session.Read().Resolved.Entities[First].Components[Appearance].Properties["g"].Value.GetNumber());
        Assert.True(session.Redo(Operation(9)).Succeeded);
        Assert.False(session.Read().Resolved.Entities[First].Components.ContainsKey(Transform));
    }

    [Fact]
    public void Add_DependencyAndRequiringComponentTogetherAcceptsRegardlessOfInputOrder()
    {
        var source = WithDependency();
        using var session = Session(source);

        Assert.True(session.EditComponents(new(Operation(7), [],
        [
            new(Second, Dependent, ComponentChangeKind.Add, new(Dependent, DependentType, 1)),
            new(Second, Custom, ComponentChangeKind.Add, new(Custom, CustomType, 1))
        ])).Succeeded);

        var entity = session.Read().Resolved.Entities[Second];
        Assert.Equal(DependentType, entity.Components[Dependent].TypeId);
        Assert.Equal(CustomType, entity.Components[Custom].TypeId);
        Assert.Equal(Second, entity.Components[Dependent].Source.EntityId);
        Assert.Equal(Second, entity.Components[Custom].Source.EntityId);
        Assert.Equal(1, session.Read().UndoCount);
    }

    [Fact]
    public void Add_MultipleOccurrencesRespectSchemaAllowMultipleAndKeepStableIdentities()
    {
        var source = Document();
        var schema = new ComponentSchema(CustomType, 1, [new("note", PropertyValueKind.String, PropertyValue.From(""))], allowMultiple: true);
        source = new(source.Id, 7, source.Entities, source.Definitions, [.. BuiltInComponents.Schemas, schema]);
        using var session = Session(source);

        Assert.True(session.EditComponents(new(Operation(7), [],
            [new(First, Added, ComponentChangeKind.Add, new(Added, CustomType, 1, [new("note", PropertyValue.From("second"))]))])).Succeeded);

        var components = session.Read().Resolved.Entities[First].Components;
        Assert.Equal("keep", components[Custom].Properties["note"].Value.GetString());
        Assert.Equal("second", components[Added].Properties["note"].Value.GetString());
        Assert.Equal(2, components.Values.Count(c => c.TypeId == CustomType));
        Assert.Equal(First, components[Added].Source.EntityId);
    }

    [Theory]
    [InlineData("dependency-remove")]
    [InlineData("dependency-add")]
    [InlineData("multiplicity")]
    [InlineData("unknown-schema")]
    [InlineData("schema-version")]
    [InlineData("duplicate-occurrence")]
    [InlineData("restore-visible")]
    [InlineData("restore-unknown")]
    [InlineData("remove-hidden")]
    [InlineData("reset-without-mask")]
    [InlineData("mismatched-addition-id")]
    [InlineData("missing-addition")]
    [InlineData("unexpected-addition")]
    [InlineData("undefined-kind")]
    [InlineData("empty-component-id")]
    [InlineData("missing-entity")]
    [InlineData("duplicate-presence")]
    public void Presence_InvalidIntentOrFinalGraphRejectsWholeBatchAndPreservesUnrelatedProperties(string invalid)
    {
        using var session = Session(WithDependency());
        var before = session.Read();
        var edit = invalid switch
        {
            "dependency-remove" => new ComponentEdit(First, Transform, ComponentChangeKind.Remove),
            "dependency-add" => new(Second, Dependent, ComponentChangeKind.Add, new(Dependent, DependentType, 1)),
            "multiplicity" => new(First, Added, ComponentChangeKind.Add, new(Added, CustomType, 1)),
            "unknown-schema" => new(First, Added, ComponentChangeKind.Add, new(Added, new ComponentTypeId(Id(999)), 1)),
            "schema-version" => new(First, Added, ComponentChangeKind.Add, new(Added, CustomType, 2)),
            "duplicate-occurrence" => new(First, Transform, ComponentChangeKind.Add, new(Transform, BuiltInComponents.TransformId, 1)),
            "restore-visible" => new(First, Appearance, ComponentChangeKind.Restore),
            "restore-unknown" => new(First, Added, ComponentChangeKind.Restore),
            "remove-hidden" => new(First, Interaction, ComponentChangeKind.Remove),
            "reset-without-mask" => new(First, Appearance, ComponentChangeKind.ResetRemoval),
            "mismatched-addition-id" => new(First, Added, ComponentChangeKind.Add, new(Custom, CustomType, 1)),
            "missing-addition" => new(First, Added, ComponentChangeKind.Add),
            "unexpected-addition" => new(First, Custom, ComponentChangeKind.Remove, new(Custom, CustomType, 1)),
            "undefined-kind" => new(First, Custom, (ComponentChangeKind)999),
            "empty-component-id" => new(First, default, ComponentChangeKind.Remove),
            "missing-entity" => new(Entity(999), Custom, ComponentChangeKind.Remove),
            _ => new(First, Custom, ComponentChangeKind.Remove)
        };
        var components = invalid == "duplicate-presence" ? new[] { edit, edit } : new[] { edit };
        var batch = new ComponentEditBatch(Operation(7), [new(Second, Transform, "y", PropertyValue.From(19d))], components);

        var result = session.EditComponents(batch);

        Assert.False(result.Succeeded);
        var expected = invalid switch
        {
            "remove-hidden" => SceneAuthoringError.MissingComponent,
            "missing-entity" => SceneAuthoringError.MissingEntity,
            "duplicate-presence" => SceneAuthoringError.DuplicateTarget,
            _ => SceneAuthoringError.InvalidScene
        };
        Assert.Equal(expected, result.Error);
        Assert.NotEmpty(result.Path);
        Assert.NotEmpty(result.Message);
        Assert.Same(before, session.Read());
        Assert.Equal(0, Number(session.Read(), Second, "y"));
        Assert.Equal("keep", session.Read().Resolved.Entities[First].Components[Custom].Properties["note"].Value.GetString());
        Assert.Same(result, session.EditComponents(batch));
    }

    [Theory]
    [InlineData(ComponentChangeKind.Remove, false)]
    [InlineData(ComponentChangeKind.Remove, true)]
    [InlineData(ComponentChangeKind.ResetRemoval, false)]
    [InlineData(ComponentChangeKind.ResetRemoval, true)]
    public void Presence_HidingOrResettingMaskCannotShareAPropertyEdit(ComponentChangeKind kind, bool reset)
    {
        using var session = Session();
        var before = session.Read();
        var component = kind == ComponentChangeKind.Remove ? Custom : Interaction;

        var result = session.EditComponents(new(Operation(7),
            [new(First, component, kind == ComponentChangeKind.Remove ? "note" : "enabled", PropertyValue.Null, reset)],
            [new(First, component, kind)]));

        Assert.Equal(SceneAuthoringError.InvalidScene, result.Error);
        Assert.Same(before, session.Read());
    }

    private static SceneDocument WithDependency()
    {
        var source = Document();
        return new(source.Id, source.Revision, source.Entities, source.Definitions,
            source.Schemas.Add(new(DependentType, 1, [], requiredTypes: [CustomType])));
    }
}
