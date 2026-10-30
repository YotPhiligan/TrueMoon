using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using Xunit;
using static TrueMoon.Aether.Tests.Scenes.Authoring.AuthoringTestData;

namespace TrueMoon.Aether.Tests.Scenes.Authoring;

public sealed class EntityEditTests
{
    [Fact]
    public void Create_EmptySceneReceivesCatalogAndParentedInstancesAsOneUndoableTransaction()
    {
        var sample = Document(); var empty = new SceneDocument(sample.Id, 7, [], [], []);
        using var session = Session(empty); var before = session.Read();
        var create = new CreateEntitiesBatch(Operation(7), sample.Entities, sample.Definitions, sample.Schemas);
        var receipt = session.EditEntities(create); Assert.True(receipt.Succeeded); var created = session.Read();
        var equivalent = Document();
        Assert.Same(receipt, session.EditEntities(new CreateEntitiesBatch(create.Operation, equivalent.Entities, equivalent.Definitions, equivalent.Schemas)));
        Assert.Equal(8, created.Document.Revision); Assert.Equal(1, created.UndoCount);
        Assert.Equal(new[] { First, Second }, created.Resolved.HierarchyOrder);
        Assert.Equal(First, created.Resolved.Entities[Second].ParentId); Assert.Equal(3, Number(created, First, "x"));
        Assert.True(session.Apply(Batch(8, 42)).Succeeded); Assert.Equal(2, session.Read().UndoCount);
        Assert.True(session.Undo(Operation(9)).Succeeded); Assert.Equal(3, Number(session.Read(), First, "x"));
        Assert.True(session.Undo(Operation(10)).Succeeded);
        Assert.Empty(session.Read().Document.Entities); Assert.Empty(session.Read().Document.Definitions); Assert.Empty(session.Read().Document.Schemas);
        Assert.Equal(before.ContentRevision, session.Read().ContentRevision);
        Assert.True(session.Redo(Operation(11)).Succeeded);
        Assert.Equal(new[] { First, Second }, session.Read().Resolved.HierarchyOrder); Assert.Equal(3, Number(session.Read(), First, "x"));
        Assert.Empty(before.Document.Entities);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Delete_ExplicitPolicyRejectsDanglingChildrenOrRemovesWholeBranchAndRestoresIt(bool subtree)
    {
        using var session = Session(Document(count: 4)); var before = session.Read();
        var result = session.EditEntities(new DeleteEntitiesBatch(Operation(7), [First], subtree ? EntityDeletionPolicy.Subtree : EntityDeletionPolicy.RejectChildren));
        if (!subtree) { Assert.Equal(SceneAuthoringError.InvalidScene, result.Error); Assert.Same(before, session.Read()); return; }
        Assert.True(result.Succeeded); Assert.Empty(session.Read().Document.Entities);
        Assert.Equal(before.Document.Definitions, session.Read().Document.Definitions); Assert.Equal(before.Document.Schemas, session.Read().Document.Schemas);
        Assert.True(session.Undo(Operation(8)).Succeeded); Assert.Equal(before.Document.Entities, session.Read().Document.Entities);
        Assert.Equal(3, Number(session.Read(), First, "x")); Assert.Equal(First, session.Read().Resolved.Entities[Second].ParentId);
        Assert.True(session.Redo(Operation(9)).Succeeded); Assert.Empty(session.Read().Document.Entities);
    }

    [Fact]
    public void Delete_OverlappingSubtreesAreRemovedOnceAndRejectChildrenCanRemoveAnExplicitCompleteSet()
    {
        using var session = Session(Document(count: 3));
        Assert.True(session.EditEntities(new DeleteEntitiesBatch(Operation(7), [First, Second], EntityDeletionPolicy.Subtree)).Succeeded);
        Assert.Empty(session.Read().Document.Entities); Assert.Equal(1, session.Read().UndoCount);
        Assert.True(session.Undo(Operation(8)).Succeeded);
        Assert.True(session.EditEntities(new DeleteEntitiesBatch(Operation(9), [First, Second, Entity(3)], EntityDeletionPolicy.RejectChildren)).Succeeded);
        Assert.Empty(session.Read().Document.Entities); Assert.Equal(0, session.Read().RedoCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Duplicate_RemapPreservesNamesInheritancePatchesAndSiblingOrderWithoutTouchingOriginals(bool descendants)
    {
        var source = Document(count: 3); source = new(source.Id, source.Revision, source.Entities.Select(entity =>
            new EntityInstance(entity.Id, entity.DefinitionId, entity.Changes, entity.ParentId, "Name " + entity.Id.Value, null)), source.Definitions, source.Schemas);
        using var session = Session(source); var before = session.Read();
        EntityCopyId[] map = descendants ? [new(First, Entity(100)), new(Second, Entity(102)), new(Entity(3), Entity(101))] : [new(First, Entity(100))];
        Assert.True(session.EditEntities(new DuplicateEntitiesBatch(Operation(7), First, map, null, descendants)).Succeeded);
        var copied = session.Read(); Assert.Equal(1, copied.UndoCount);
        Assert.Equal(new[] { First, Entity(100) }, copied.Resolved.Roots);
        Assert.Equal(before.Resolved.Entities[First].Name, copied.Resolved.Entities[Entity(100)].Name);
        Assert.Equal(Definition, copied.Resolved.Entities[Entity(100)].DefinitionId);
        Assert.Equal(3, Number(copied, Entity(100), "x")); Assert.Same(source.Entities[0].Changes, copied.Document.Entities.Single(entity => entity.Id == Entity(100)).Changes);
        Assert.Equal(before.Document.Definitions, copied.Document.Definitions); Assert.Equal(before.Document.Schemas, copied.Document.Schemas);
        Assert.Equal(new[] { Second, Entity(3) }, copied.Resolved.Children[First]);
        if (descendants) { Assert.Equal(new[] { Entity(102), Entity(101) }, copied.Resolved.Children[Entity(100)]); Assert.Equal(Entity(100), copied.Resolved.Entities[Entity(102)].ParentId); }
        else Assert.Empty(copied.Resolved.Children[Entity(100)]);
        Assert.True(session.EditComponents(new(Operation(8), [new(Entity(100), Transform, "x", PropertyValue.From(42d))])).Succeeded);
        Assert.Equal(3, Number(session.Read(), First, "x")); Assert.Equal(42, Number(session.Read(), Entity(100), "x"));
        Assert.True(session.Undo(Operation(9)).Succeeded); Assert.True(session.Undo(Operation(10)).Succeeded);
        Assert.Equal(before.Document.Entities, session.Read().Document.Entities); Assert.True(session.Redo(Operation(11)).Succeeded);
        Assert.Equal(descendants ? 6 : 4, session.Read().Document.Entities.Length);
    }

    [Fact]
    public void Duplicate_DifferentParentAppendsAfterExistingChildren()
    {
        using var session = Session(Document(count: 3, parented: false));
        Assert.True(session.EditEntities(new DuplicateEntitiesBatch(Operation(7), First, [new(First, Entity(100))], Second, false)).Succeeded);
        Assert.Equal(Second, session.Read().Resolved.Entities[Entity(100)].ParentId);
        Assert.Equal(Entity(100), Assert.Single(session.Read().Resolved.Children[Second]));
        Assert.Equal(new[] { First, Second, Entity(3) }, session.Read().Resolved.Roots);
    }

    [Theory]
    [InlineData("existing")] [InlineData("repeated")] [InlineData("parent")] [InlineData("definition")]
    [InlineData("schema")] [InlineData("catalog")] [InlineData("order")] [InlineData("transform")]
    public void Create_InvalidGraphCatalogOrIdentityPublishesNothing(string fault)
    {
        using var session = Session(); var before = session.Read(); var id = Entity(100);
        var entity = new EntityInstance(fault == "existing" ? First : id, fault == "definition" ? new(Id(999)) : Definition,
            fault == "transform" ? new(propertyOverrides: [new(Transform, "sx", PropertyValue.From(0d))]) : EntityPatch.Empty,
            fault == "parent" ? Entity(999) : null, "New", fault == "order" ? 1 : null);
        var result = session.EditEntities(new CreateEntitiesBatch(Operation(7), fault == "repeated" ? [entity, entity] : [entity],
            fault == "catalog" ? before.Document.Definitions : null, fault == "schema" ? before.Document.Schemas : null));
        Assert.False(result.Succeeded); Assert.Same(before, session.Read()); Assert.Equal(0, session.Read().UndoCount);
    }

    [Theory]
    [InlineData("missing-source")] [InlineData("missing-parent")] [InlineData("missing-map")] [InlineData("extra-map")]
    [InlineData("duplicate-source")] [InlineData("duplicate-copy")] [InlineData("existing-copy")] [InlineData("empty-copy")]
    public void Duplicate_InvalidMapPublishesNothing(string fault)
    {
        using var session = Session(); var before = session.Read();
        EntityCopyId[] map = fault switch
        {
            "missing-map" => [new(First, Entity(100))],
            "extra-map" => [new(First, Entity(100)), new(Second, Entity(101)), new(Entity(999), Entity(102))],
            "duplicate-source" => [new(First, Entity(100)), new(First, Entity(101))],
            "duplicate-copy" => [new(First, Entity(100)), new(Second, Entity(100))],
            "existing-copy" => [new(First, First), new(Second, Entity(101))],
            "empty-copy" => [new(First, default), new(Second, Entity(101))],
            _ => [new(First, Entity(100)), new(Second, Entity(101))]
        };
        var result = session.EditEntities(new DuplicateEntitiesBatch(Operation(7), fault == "missing-source" ? Entity(999) : First, map,
            fault == "missing-parent" ? Entity(999) : null));
        Assert.False(result.Succeeded); Assert.Same(before, session.Read());
    }

    [Fact]
    public void Receipts_EquivalentFrozenCreationReplaysAndChangedPayloadConflictsAcrossKinds()
    {
        using var session = Session(); var operation = Operation(7);
        CreateEntitiesBatch Batch(string name) => new(operation, [new(Entity(100), Definition,
            new(propertyOverrides: [new(Transform, "x", PropertyValue.From(7d))]), null, name, null)]);
        var result = session.EditEntities(Batch("New")); Assert.True(result.Succeeded);
        Assert.Same(result, session.EditEntities(Batch("New")));
        Assert.Equal(SceneAuthoringError.OperationIdConflict, session.EditEntities(Batch("Changed")).Error);
        Assert.Equal(SceneAuthoringError.OperationIdConflict, session.EditEntities(new DeleteEntitiesBatch(operation, [First], EntityDeletionPolicy.Subtree)).Error);
        Assert.Equal(SceneAuthoringError.RevisionConflict, session.EditEntities(new DeleteEntitiesBatch(Operation(7), [First], EntityDeletionPolicy.Subtree)).Error);
    }

    [Fact]
    public void ExpandedLimitsCancellationAndInvalidDeletionPolicyRetainState()
    {
        using var session = Session(Document(count: 3), new() { MaxBatchEdits = 2 }); var before = session.Read();
        Assert.Equal(SceneAuthoringError.BatchLimitExceeded, session.EditEntities(new DeleteEntitiesBatch(Operation(7), [First], EntityDeletionPolicy.Subtree)).Error);
        Assert.Equal(SceneAuthoringError.BatchLimitExceeded, session.EditEntities(new DuplicateEntitiesBatch(Operation(7), First,
            [new(First, Entity(100)), new(Second, Entity(101)), new(Entity(3), Entity(102))], null)).Error);
        Assert.Equal(SceneAuthoringError.InvalidScene, session.EditEntities(new DeleteEntitiesBatch(Operation(7), [First], (EntityDeletionPolicy)99)).Error);
        Assert.Equal(SceneAuthoringError.DuplicateTarget, session.EditEntities(new DeleteEntitiesBatch(Operation(7), [First, First], EntityDeletionPolicy.Subtree)).Error);
        Assert.Equal(SceneAuthoringError.MissingEntity, session.EditEntities(new DeleteEntitiesBatch(Operation(7), [Entity(999)], EntityDeletionPolicy.Subtree)).Error);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => session.EditEntities(new DeleteEntitiesBatch(Operation(7), [Second], EntityDeletionPolicy.Subtree), cancellation.Token));
        Assert.Same(before, session.Read());
        Assert.Throws<ArgumentOutOfRangeException>(() => new CreateEntitiesBatch(Operation(7), []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeleteEntitiesBatch(Operation(7), [], EntityDeletionPolicy.Subtree));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DuplicateEntitiesBatch(Operation(7), First, [], null));
    }

    [Fact]
    public void SceneCapacity_AllowsExactLimitAndRejectsTheNextCreationAndDuplication()
    {
        using var session = Session(Document(count: SceneFileCodec.MaximumEntities - 1, parented: false));
        Assert.True(session.EditEntities(new CreateEntitiesBatch(Operation(7), [new(Entity(10000), Definition)])).Succeeded);
        Assert.Equal(SceneFileCodec.MaximumEntities, session.Read().Document.Entities.Length);
        Assert.Equal(Entity(10000), session.Read().Resolved.Roots[^1]); var before = session.Read();
        Assert.Equal(SceneAuthoringError.BatchLimitExceeded, session.EditEntities(new CreateEntitiesBatch(Operation(8), [new(Entity(10001), Definition)])).Error);
        Assert.Equal(SceneAuthoringError.BatchLimitExceeded, session.EditEntities(new DuplicateEntitiesBatch(Operation(8), First, [new(First, Entity(10002))], null, false)).Error);
        Assert.Same(before, session.Read());
    }

    [Fact]
    public void CancellationDuringResolutionDoesNotPublishCatalogOrConsumeOperationId()
    {
        using var cancellation = new CancellationTokenSource(); var cancel = false;
        using var session = Session(beforeCreate: _ => { if (cancel) cancellation.Cancel(); }); var before = session.Read();
        var batch = new CreateEntitiesBatch(Operation(7), [new(Entity(100), Definition)]); cancel = true;
        Assert.Throws<OperationCanceledException>(() => session.EditEntities(batch, cancellation.Token)); Assert.Same(before, session.Read());
        cancel = false; Assert.True(session.EditEntities(batch).Succeeded); Assert.Equal(1, session.Read().UndoCount);
    }
}
