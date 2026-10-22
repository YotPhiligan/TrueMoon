using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Tests.Scenes;

public sealed class SceneResolverTests
{
    [Fact]
    public void Forest_ResolvesInheritanceIndependentlyFromParentsAndPinsReferencedVersions()
    {
        var scene = SceneFixture.Forest(); var result = SceneResolver.Resolve(scene);
        Assert.Equal(scene.Id, result.Id); Assert.Equal(7, result.Revision);
        Assert.Equal(new[] { ModelFixture.Second, ModelFixture.Parent }, result.Roots);
        Assert.Equal(new[] { ModelFixture.Second, ModelFixture.First, SceneFixture.Grandchild, SceneFixture.Sibling, ModelFixture.Parent }, result.HierarchyOrder);
        Assert.Equal(new[] { ModelFixture.First, SceneFixture.Sibling }, result.Children[ModelFixture.Second]);
        Assert.Equal(new[] { SceneFixture.Grandchild }, result.Children[ModelFixture.First]);
        Assert.Empty(result.Children[SceneFixture.Grandchild]); Assert.Empty(result.Children[ModelFixture.Parent]);
        Assert.Equal(5, result.Children.Count); Assert.Equal(5, result.Entities.Count);
        Assert.Equal(ModelFixture.Second, result.Entities[ModelFixture.First].ParentId);
        Assert.Equal("red", ModelFixture.Color(result.Entities[ModelFixture.First]));
        Assert.Equal("blue", ModelFixture.Color(result.Entities[ModelFixture.Second]));
        Assert.Equal(5, ModelFixture.Property(result.Entities[ModelFixture.First], ModelFixture.Transform, "x").Value.GetNumber());
        Assert.Equal(1, ModelFixture.Property(result.Entities[SceneFixture.Grandchild], ModelFixture.Transform, "x").Value.GetNumber());
        Assert.Equal(new[] { ModelFixture.Door, ModelFixture.Red }, result.Entities[ModelFixture.First].DefinitionChain);
        Assert.Equal(ModelFixture.Red, ModelFixture.Property(result.Entities[ModelFixture.First], ModelFixture.Appearance, "color").Source.DefinitionId);
        Assert.Equal(1, result.DefinitionRevisions[ModelFixture.Door]); Assert.Equal(4, result.DefinitionRevisions[ModelFixture.Red]);
        Assert.Equal(new[] { ModelFixture.TransformType, ModelFixture.AppearanceType }, result.ComponentSchemas.Select(s => s.TypeId));
        Assert.All(result.ComponentSchemas, s => Assert.Equal(1, s.Version));
        Assert.DoesNotContain(ModelFixture.TagType, result.ComponentSchemas.Select(s => s.TypeId));
    }

    [Fact]
    public void DeclarationOrder_DoesNotChangeHierarchyOrResolvedValues()
    {
        var source = SceneFixture.Forest(); var before = SceneResolver.Resolve(source);
        var permuted = new SceneDocument(source.Id, source.Revision, source.Entities.Reverse(), source.Definitions.Reverse(), source.Schemas.Reverse());
        var after = SceneResolver.Resolve(permuted);
        Assert.Equal(before.Roots.AsEnumerable(), after.Roots.AsEnumerable());
        Assert.Equal(before.HierarchyOrder.AsEnumerable(), after.HierarchyOrder.AsEnumerable());
        Assert.Equal(before.DefinitionRevisions.OrderBy(p => p.Key.Value), after.DefinitionRevisions.OrderBy(p => p.Key.Value));
        Assert.Equal(before.ComponentSchemas.Select(s => (s.TypeId, s.Version)), after.ComponentSchemas.Select(s => (s.TypeId, s.Version)));
        foreach (var id in before.HierarchyOrder)
        {
            Assert.Equal(before.Children[id].AsEnumerable(), after.Children[id].AsEnumerable());
            Assert.Equal(ModelFixture.Color(before.Entities[id]), ModelFixture.Color(after.Entities[id]));
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void EmptyAndSingletonScenes_ReturnCompleteHierarchyAndOnlyUsedMetadata(int count)
    {
        var document = SceneFixture.Document(Enumerable.Range(0, count).Select(_ => new EntityInstance(ModelFixture.First, ModelFixture.Red)), revision: count);
        var result = SceneResolver.Resolve(document);
        Assert.Equal(count, result.Entities.Count); Assert.Equal(count, result.Roots.Length);
        Assert.Equal(count, result.Children.Count); Assert.Equal(count, result.HierarchyOrder.Length);
        if (count == 0) { Assert.Empty(result.DefinitionRevisions); Assert.Empty(result.ComponentSchemas); }
        else
        {
            Assert.Equal(ModelFixture.First, Assert.Single(result.Roots)); Assert.Empty(result.Children[ModelFixture.First]);
            Assert.Equal("red", ModelFixture.Color(result.Entities[ModelFixture.First]));
            Assert.Equal(2, result.DefinitionRevisions.Count); Assert.Equal(2, result.ComponentSchemas.Length);
        }
    }

    [Fact]
    public void ParentGraph_RejectsMissingParentWithStablePathAndEntityIdentity()
    {
        var document = SceneFixture.Document([new(ModelFixture.First, ModelFixture.Red, parentId: ModelFixture.Parent)]);
        var error = Assert.Throws<SceneResolutionException>(() => SceneResolver.Resolve(document));
        Assert.Equal(SceneResolutionError.MissingParent, error.Code); Assert.Equal(ModelFixture.First, error.EntityId);
        Assert.Equal($"scenes/{document.Id.Value:D}/entities/{ModelFixture.First.Value:D}/parent", error.Path);
        Assert.Contains(ModelFixture.Parent.Value.ToString("D"), error.Message, StringComparison.Ordinal); Assert.Null(error.InnerException);
    }

    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void ParentGraph_RejectsCyclesIncludingTailsEnteringACycle(int shape)
    {
        var a = ModelFixture.First; var b = ModelFixture.Second; var c = ModelFixture.Parent;
        EntityInstance[] entities = shape switch
        {
            2 => [new(a, ModelFixture.Door, parentId: b), new(b, ModelFixture.Door, parentId: a)],
            3 => [new(a, ModelFixture.Door, parentId: b), new(b, ModelFixture.Door, parentId: c), new(c, ModelFixture.Door, parentId: a)],
            _ => [new(a, ModelFixture.Door, parentId: b), new(b, ModelFixture.Door, parentId: c), new(c, ModelFixture.Door, parentId: b)]
        };
        var error = Assert.Throws<SceneResolutionException>(() => SceneResolver.Resolve(SceneFixture.Document(entities)));
        var closing = shape == 2 ? b : c;
        Assert.Equal(SceneResolutionError.ParentCycle, error.Code); Assert.Equal(closing, error.EntityId);
        Assert.Equal($"scenes/{SceneFixture.Id.Value:D}/entities/{closing.Value:D}/parent", error.Path);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public void DuplicateEntityIds_AreRejectedBeforeResolvingEitherInstance()
    {
        var document = SceneFixture.Document([new(ModelFixture.First, ModelFixture.Door), new(ModelFixture.First, ModelFixture.Red)]);
        var error = Assert.Throws<SceneResolutionException>(() => SceneResolver.Resolve(document));
        Assert.Equal(SceneResolutionError.DuplicateEntity, error.Code); Assert.Equal(ModelFixture.First, error.EntityId);
        Assert.Equal($"scenes/{document.Id.Value:D}/entities/{ModelFixture.First.Value:D}", error.Path);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void CatalogErrors_RetainOriginalCategoryAndScenePathEvenForEmptyScenes(bool duplicateDefinition)
    {
        var document = new SceneDocument(SceneFixture.Id, 1, [],
            duplicateDefinition ? [ModelFixture.Base(), ModelFixture.Base()] : [],
            duplicateDefinition ? [] : [ModelFixture.TransformSchema, ModelFixture.TransformSchema]);
        var error = Assert.Throws<SceneResolutionException>(() => SceneResolver.Resolve(document));
        var inner = Assert.IsType<EntityResolutionException>(error.InnerException);
        Assert.Equal(SceneResolutionError.InvalidCatalog, error.Code); Assert.Null(error.EntityId);
        Assert.Equal(duplicateDefinition ? EntityResolutionError.DuplicateDefinition : EntityResolutionError.DuplicateSchema, inner.Code);
        Assert.Equal($"scenes/{document.Id.Value:D}/{inner.Path}", error.Path);
    }

    [Fact]
    public void EntityFailure_IsAtomicAndRetainsInstanceAndOriginalDefinitionCause()
    {
        var good = SceneFixture.Document([new(ModelFixture.First, ModelFixture.Red)], revision: 10);
        var before = SceneResolver.Resolve(good);
        var invalidDefinition = new EntityDefinition(ModelFixture.Hidden, new(propertyOverrides:
            [new(ModelFixture.Appearance, "color", PropertyValue.From(false))]), ModelFixture.Door, 20);
        var changed = SceneFixture.Document([new(ModelFixture.First, ModelFixture.Red), new(ModelFixture.Second, ModelFixture.Hidden)],
            [ModelFixture.Base("changed", 11), ModelFixture.Variant(), invalidDefinition], revision: 12);
        ResolvedSceneSnapshot? published = null;
        var error = Assert.Throws<SceneResolutionException>(() => published = SceneResolver.Resolve(changed));
        var inner = Assert.IsType<EntityResolutionException>(error.InnerException);
        Assert.Null(published); Assert.Equal(SceneResolutionError.InvalidEntity, error.Code); Assert.Equal(ModelFixture.Second, error.EntityId);
        Assert.Equal(EntityResolutionError.PropertyTypeMismatch, inner.Code);
        Assert.Equal($"definitions/{ModelFixture.Hidden.Value:D}/components/{ModelFixture.Appearance.Value:D}/properties/color", inner.Path);
        Assert.Equal($"scenes/{changed.Id.Value:D}/{inner.Path}", error.Path);
        Assert.Equal(10, before.Revision); Assert.Equal(1, before.DefinitionRevisions[ModelFixture.Door]);
        Assert.Equal("base", ModelFixture.Property(before.Entities[ModelFixture.First], ModelFixture.Appearance, "label").Value.GetString());
        Assert.Equal("changed", changed.Definitions.Single(d => d.Id == ModelFixture.Door).Changes.AddedComponents
            .Single(c => c.Id == ModelFixture.Appearance).Properties["label"].GetString());
        var after = SceneResolver.Resolve(good); Assert.Single(after.Entities); Assert.Equal("red", ModelFixture.Color(after.Entities[ModelFixture.First]));
    }

    [Fact]
    public void RevisionMetadata_PinsUsedDefinitionsAndCoexistingExactSchemaVersions()
    {
        var version2 = new ComponentSchema(ModelFixture.AppearanceType, 2,
            [new("color", PropertyValueKind.String, PropertyValue.From("v2"))], requiredTypes: [ModelFixture.TransformType]);
        var variant = new EntityDefinition(ModelFixture.Red, new([new(ModelFixture.Extra, ModelFixture.AppearanceType, 2)],
            [ModelFixture.Appearance]), ModelFixture.Door, 19);
        var unused = new EntityDefinition(ModelFixture.Hidden, EntityPatch.Empty, new(ModelFixture.Guid(999)));
        var document = SceneFixture.Document([new(ModelFixture.First, ModelFixture.Door), new(ModelFixture.Second, ModelFixture.Red)],
            [ModelFixture.Base(), variant, unused], ModelFixture.Schemas.Append(version2), long.MaxValue);
        var result = SceneResolver.Resolve(document);
        Assert.Equal(long.MaxValue, result.Revision); Assert.Equal(19, result.DefinitionRevisions[ModelFixture.Red]);
        Assert.False(result.DefinitionRevisions.ContainsKey(ModelFixture.Hidden));
        Assert.Equal(new[] { (ModelFixture.TransformType, 1), (ModelFixture.AppearanceType, 1), (ModelFixture.AppearanceType, 2) },
            result.ComponentSchemas.Select(s => (s.TypeId, s.Version)));
        Assert.Equal("blue", ModelFixture.Color(result.Entities[ModelFixture.First]));
        Assert.Equal("v2", ModelFixture.Property(result.Entities[ModelFixture.Second], ModelFixture.Extra, "color").Value.GetString());
        Assert.Equal(2, result.Entities[ModelFixture.Second].Components[ModelFixture.Extra].SchemaVersion);
        Assert.False(result.Entities[ModelFixture.Second].Components.ContainsKey(ModelFixture.Appearance));
    }

    [Fact]
    public void FrozenDocument_RemainsConsistentAfterAllCallerCatalogsAreMutated()
    {
        var entities = new List<EntityInstance> { new(ModelFixture.First, ModelFixture.Red) };
        var definitions = new List<EntityDefinition> { ModelFixture.Base(), ModelFixture.Variant() };
        var schemas = ModelFixture.Schemas.ToList();
        var document = new SceneDocument(SceneFixture.Id, 5, entities, definitions, schemas);
        entities.Clear(); definitions[0] = ModelFixture.Base("changed", 9); definitions.Clear(); schemas.Clear();
        var before = SceneResolver.Resolve(document);
        Assert.Single(before.Entities); Assert.Equal("red", ModelFixture.Color(before.Entities[ModelFixture.First]));
        Assert.Equal("base", ModelFixture.Property(before.Entities[ModelFixture.First], ModelFixture.Appearance, "label").Value.GetString());
        Assert.Equal(1, before.DefinitionRevisions[ModelFixture.Door]); Assert.Equal(5, before.Revision);
        var next = SceneResolver.Resolve(SceneFixture.Document([new(ModelFixture.First, ModelFixture.Red)],
            [ModelFixture.Base("new", 30), ModelFixture.Variant()], revision: 31));
        Assert.Equal("new", ModelFixture.Property(next.Entities[ModelFixture.First], ModelFixture.Appearance, "label").Value.GetString());
        Assert.Equal(30, next.DefinitionRevisions[ModelFixture.Door]); Assert.Equal(31, next.Revision);
        Assert.Equal(1, before.DefinitionRevisions[ModelFixture.Door]); Assert.Equal(5, before.Revision);
    }

    [Theory]
    [InlineData("missing", EntityResolutionError.MissingDefinition)]
    [InlineData("cycle", EntityResolutionError.InheritanceCycle)]
    [InlineData("schema", EntityResolutionError.UnknownSchema)]
    public void InheritanceAndSchemaFailures_AreNotConfusedWithAValidSceneParentGraph(string kind, EntityResolutionError expected)
    {
        EntityDefinition[] definitions = kind switch
        {
            "missing" => [],
            "cycle" => [new(ModelFixture.Door, EntityPatch.Empty, ModelFixture.Door)],
            _ => [new(ModelFixture.Door, new([new(ModelFixture.Extra, new(ModelFixture.Guid(999)), 1)]))]
        };
        var error = Assert.Throws<SceneResolutionException>(() => SceneResolver.Resolve(SceneFixture.Document(
            [new(ModelFixture.First, ModelFixture.Door)], definitions)));
        Assert.Equal(SceneResolutionError.InvalidEntity, error.Code); Assert.Equal(ModelFixture.First, error.EntityId);
        var inner = Assert.IsType<EntityResolutionException>(error.InnerException); Assert.Equal(expected, inner.Code);
        Assert.Equal($"scenes/{SceneFixture.Id.Value:D}/{inner.Path}", error.Path);
    }

    [Fact]
    public void DeepHierarchy_UsesIterativeTraversalAndDoesNotInheritDefinitionDepthBound()
    {
        var ids = Enumerable.Range(0, 4096).Select(i => new EntityId(ModelFixture.Guid(10000 + i))).ToArray();
        var instances = ids.Select((id, i) => new EntityInstance(id, ModelFixture.Door, parentId: i + 1 < ids.Length ? ids[i + 1] : null));
        var result = SceneResolver.Resolve(SceneFixture.Document(instances, [new(ModelFixture.Door, EntityPatch.Empty)], []));
        Assert.Equal(ids.Reverse(), result.HierarchyOrder); Assert.Equal(ids[^1], Assert.Single(result.Roots));
        Assert.Empty(result.Children[ids[0]]); Assert.Equal(ids[^2], Assert.Single(result.Children[ids[^1]]));
        Assert.Equal(ids.Length, result.Entities.Count); Assert.Empty(result.ComponentSchemas);
    }

    [Fact]
    public void CancelledAndParallelResolutions_DoNotChangeSharedDocumentOrOtherSnapshots()
    {
        var document = SceneFixture.Forest(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => SceneResolver.Resolve(document, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Parallel.For(0, 16, _ =>
        {
            var result = SceneResolver.Resolve(document);
            Assert.Equal(5, result.Entities.Count); Assert.Equal("red", ModelFixture.Color(result.Entities[ModelFixture.First]));
            Assert.Equal(new[] { ModelFixture.Second, ModelFixture.First, SceneFixture.Grandchild, SceneFixture.Sibling, ModelFixture.Parent }, result.HierarchyOrder);
        });
        Assert.Equal(5, document.Entities.Length); Assert.Equal(7, document.Revision);
    }

    [Theory]
    [InlineData("empty-id")] [InlineData("default-id")] [InlineData("revision-negative")]
    [InlineData("null-entities")] [InlineData("null-definitions")] [InlineData("null-schemas")]
    [InlineData("null-entity-entry")] [InlineData("null-definition-entry")] [InlineData("null-schema-entry")]
    [InlineData("null-scene")] [InlineData("self-parent")]
    public void LocalContracts_RejectInvalidInputsBeforeSceneResolution(string kind)
    {
        Assert.ThrowsAny<ArgumentException>(() => kind switch
        {
            "empty-id" => (object)new SceneId(Guid.Empty),
            "default-id" => new SceneDocument(default, 0, [], [], []),
            "revision-negative" => new SceneDocument(SceneFixture.Id, -1, [], [], []),
            "null-entities" => new SceneDocument(SceneFixture.Id, 0, null!, [], []),
            "null-definitions" => new SceneDocument(SceneFixture.Id, 0, [], null!, []),
            "null-schemas" => new SceneDocument(SceneFixture.Id, 0, [], [], null!),
            "null-entity-entry" => new SceneDocument(SceneFixture.Id, 0, [null!], [], []),
            "null-definition-entry" => new SceneDocument(SceneFixture.Id, 0, [], [null!], []),
            "null-schema-entry" => new SceneDocument(SceneFixture.Id, 0, [], [], [null!]),
            "self-parent" => new EntityInstance(ModelFixture.First, ModelFixture.Door, parentId: ModelFixture.First),
            _ => SceneResolver.Resolve(null!)
        });
    }
}

internal static class SceneFixture
{
    internal static readonly SceneId Id = new(ModelFixture.Guid(700));
    internal static readonly EntityId Sibling = new(ModelFixture.Guid(104)), Grandchild = new(ModelFixture.Guid(105));
    internal static SceneDocument Document(IEnumerable<EntityInstance> entities, IEnumerable<EntityDefinition>? definitions = null,
        IEnumerable<ComponentSchema>? schemas = null, long revision = 7)
        => new(Id, revision, entities, definitions ?? [ModelFixture.Base(), ModelFixture.Variant()], schemas ?? ModelFixture.Schemas);
    internal static SceneDocument Forest() => Document([
        new(ModelFixture.Parent, ModelFixture.Door), new(Grandchild, ModelFixture.Door, parentId: ModelFixture.First),
        new(ModelFixture.First, ModelFixture.Red, new(propertyOverrides: [new(ModelFixture.Transform, "x", PropertyValue.From(5d))]), ModelFixture.Second),
        new(Sibling, ModelFixture.Door, parentId: ModelFixture.Second), new(ModelFixture.Second, ModelFixture.Door)]);
}
