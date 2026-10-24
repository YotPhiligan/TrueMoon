using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using Xunit;
using static TrueMoon.Aether.Tests.Scenes.Authoring.AuthoringTestData;

namespace TrueMoon.Aether.Tests.Scenes.Authoring;

public sealed class ReparentTests
{
    [Fact]
    public void Reparent_CombinedParentSwapMovesWholeSubtreeWithoutChangingLocalDataOrInheritance()
    {
        var source = HierarchyDocument();
        using var session = Session(source);
        var before = session.Read();
        var operation = Operation(7);

        // The first edit alone creates a cycle; the second breaks it in the same atomic candidate.
        var result = session.Reparent(new(operation, [new(First, Second), new(Second, null)]));

        Assert.True(result.Succeeded);
        Assert.Equal(SceneAuthoringError.None, result.Error);
        Assert.Same(operation, result.Operation);
        Assert.Equal(8, result.Revision);
        Assert.Equal(8, result.ContentRevision);
        Assert.Empty(result.Path);
        Assert.Empty(result.Message);
        var after = session.Read();
        AssertState(after, 8, 8, 1, 0);
        Assert.Equal(source.Id, after.Document.Id);
        Assert.Equal(new[] { Second }, after.Resolved.Roots);
        Assert.Equal(new[] { Second, First, Entity(3), Entity(4) }, after.Resolved.HierarchyOrder);
        Assert.Equal(new[] { First }, after.Resolved.Children[Second]);
        Assert.Equal(new[] { Entity(3) }, after.Resolved.Children[First]);
        Assert.Equal(new[] { Entity(4) }, after.Resolved.Children[Entity(3)]);
        Assert.Empty(after.Resolved.Children[Entity(4)]);
        AssertParent(after, First, Second);
        AssertParent(after, Second, null);
        AssertParent(after, Entity(3), First);
        AssertParent(after, Entity(4), Entity(3));
        Assert.Equal(source.Entities.Select(entity => entity.Id), after.Document.Entities.Select(entity => entity.Id));
        Assert.Equal(source.Definitions.AsEnumerable(), after.Document.Definitions.AsEnumerable());
        Assert.Equal(source.Schemas.AsEnumerable(), after.Document.Schemas.AsEnumerable());
        foreach (var entity in source.Entities) AssertLocalDataUnchanged(before, after, entity.Id);
        Assert.Equal(new[] { Definition, new EntityDefinitionId(Id(11)) }, after.Resolved.Entities[Second].DefinitionChain);
        Assert.Equal(8, Number(after, Second, "x"));
        Assert.Equal(.75, after.Resolved.Entities[Second].Components[Appearance].Properties["g"].Value.GetNumber());
        Assert.Equal(Second, after.Resolved.Entities[Second].Components[Interaction].PresenceSource.EntityId);
        Assert.Equal("keep", after.Resolved.Entities[First].Components[Custom].Properties["note"].Value.GetString());
        Assert.False(after.Resolved.Entities[First].Components.ContainsKey(Interaction));
        Assert.Same(source, before.Document);
        AssertState(before, 7, 7, 0, 0);
        AssertParent(before, First, null);
        AssertParent(before, Second, First);
        Assert.Equal(new[] { First }, before.Resolved.Roots);
    }

    [Fact]
    public void Reparent_MovesToExistingParentAndThenRootWithoutLosingDescendants()
    {
        using var session = Session(Document(count: 4));
        Assert.True(session.Reparent(new(Operation(7), [new(Entity(3), Second), new(Entity(4), Entity(3))])).Succeeded);
        var parented = session.Read();
        AssertParent(parented, Entity(3), Second);
        AssertParent(parented, Entity(4), Entity(3));
        Assert.Equal(new[] { Entity(3) }, parented.Resolved.Children[Second]);

        Assert.True(session.Reparent(new(Operation(8), [new(Entity(3), null)])).Succeeded);

        var rooted = session.Read();
        AssertState(rooted, 9, 9, 2, 0);
        Assert.Equal(new[] { First, Entity(3) }, rooted.Resolved.Roots);
        AssertParent(rooted, Entity(3), null);
        AssertParent(rooted, Entity(4), Entity(3));
        Assert.Empty(rooted.Resolved.Children[Second]);
        Assert.Equal(new[] { Entity(4) }, rooted.Resolved.Children[Entity(3)]);
        AssertParent(parented, Entity(3), Second);
    }

    [Fact]
    public void Reparent_EntityWithoutTransformRetainsItsVisibleComponentsAndHiddenTransform()
    {
        var source = Document();
        var variantId = new EntityDefinitionId(Id(11));
        var variant = new EntityDefinition(variantId, new(removedComponents: [Transform, Appearance, Interaction]), Definition);
        source = new(source.Id, source.Revision,
            [source.Entities[0], new EntityInstance(Second, variantId,
                new(addedComponents: [new(Custom, CustomType, 1, [new("note", PropertyValue.From("group"))])]), First)],
            source.Definitions.Add(variant), source.Schemas);
        using var session = Session(source);
        var before = session.Read();

        var result = session.Reparent(new(Operation(7), [new(Second, null)]));

        Assert.True(result.Succeeded);
        var after = session.Read();
        AssertParent(after, Second, null);
        Assert.False(after.Resolved.Entities[Second].Components.ContainsKey(Transform));
        Assert.Equal(new[] { Custom }, after.Resolved.Entities[Second].Components.Keys);
        Assert.Equal("group", after.Resolved.Entities[Second].Components[Custom].Properties["note"].Value.GetString());
        AssertLocalDataUnchanged(before, after, Second);
        Assert.True(session.Undo(Operation(8)).Succeeded);
        AssertParent(session.Read(), Second, First);
        Assert.False(session.Read().Resolved.Entities[Second].Components.ContainsKey(Transform));
    }

    [Fact]
    public void Reparent_HeadlessHierarchyIsNotTruncatedToPreviewEntityLimit()
    {
        using var session = Session(Document(count: 65, parented: false), new() { MaxBatchEdits = 65 });
        var batch = new ReparentBatch(Operation(7), Enumerable.Range(1, 65)
            .Select(index => new ReparentEdit(Entity(index), index == 1 ? null : First)));

        Assert.True(session.Reparent(batch).Succeeded);

        var state = session.Read();
        AssertState(state, 8, 8, 1, 0);
        Assert.Equal(65, state.Resolved.Entities.Count);
        Assert.Equal(new[] { First }, state.Resolved.Roots);
        Assert.Equal(Enumerable.Range(2, 64).Select(Entity), state.Resolved.Children[First]);
        AssertParent(state, Entity(65), First);
        Assert.Equal(3, Number(state, First, "x"));
    }

    [Theory]
    [InlineData("missing-source", SceneAuthoringError.MissingEntity)]
    [InlineData("default-source", SceneAuthoringError.MissingEntity)]
    [InlineData("missing-parent", SceneAuthoringError.InvalidScene)]
    [InlineData("default-parent", SceneAuthoringError.InvalidScene)]
    [InlineData("self-parent", SceneAuthoringError.InvalidScene)]
    [InlineData("descendant-parent", SceneAuthoringError.InvalidScene)]
    [InlineData("duplicate-source", SceneAuthoringError.DuplicateTarget)]
    public void Reparent_InvalidTargetRejectsEntireBatchAndRetainsTerminalReceipt(string invalid, SceneAuthoringError error)
    {
        using var session = Session(Document(count: 3));
        var before = session.Read();
        var target = invalid switch
        {
            "missing-source" => new ReparentEdit(Entity(99), null),
            "default-source" => new(default, null),
            "missing-parent" => new(Second, Entity(99)),
            "default-parent" => new(Second, default(EntityId)),
            "self-parent" => new(Second, Second),
            "descendant-parent" => new(First, Second),
            _ => new(Entity(3), null)
        };
        var batch = new ReparentBatch(Operation(7), [new(Entity(3), Second), target]);

        var result = session.Reparent(batch);

        Assert.False(result.Succeeded);
        Assert.Equal(error, result.Error);
        Assert.Equal(7, result.Revision);
        Assert.Equal(7, result.ContentRevision);
        Assert.Contains("/entities/", result.Path);
        Assert.EndsWith("/parent", result.Path);
        Assert.NotEmpty(result.Message);
        Assert.Same(before, session.Read());
        AssertParent(before, Entity(3), First);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Same(result, session.Reparent(batch, cancellation.Token));
        Assert.True(session.Reparent(new(Operation(7), [new(Entity(3), Second)])).Succeeded);
        AssertParent(session.Read(), Entity(3), Second);
        AssertState(session.Read(), 8, 8, 1, 0);
    }

    [Fact]
    public void Reparent_CycleIntroducedOnlyByCombinedEditsRejectsWithoutPublishingAnyLink()
    {
        using var session = Session(Document(count: 3, parented: false));
        var before = session.Read();
        var batch = new ReparentBatch(Operation(7), [new(First, Second), new(Second, Entity(3)), new(Entity(3), First)]);

        var result = session.Reparent(batch);

        Assert.Equal(SceneAuthoringError.InvalidScene, result.Error);
        Assert.False(result.Succeeded);
        Assert.Same(before, session.Read());
        Assert.All(before.Document.Entities, entity => Assert.Null(entity.ParentId));
        Assert.Equal(new[] { First, Second, Entity(3) }, before.Resolved.Roots);
        Assert.Same(result, session.Reparent(batch));
        Assert.Equal(SceneAuthoringError.NoUndo, session.Undo(Operation(7)).Error);
    }

    [Fact]
    public void Reparent_WorldTransformOverflowRejectsLocallyValidTransformsAtomically()
    {
        var source = Document(parented: false);
        source = new(source.Id, source.Revision, source.Entities.Select(entity => new EntityInstance(entity.Id,
            entity.DefinitionId, new(propertyOverrides: [new(Transform, "x", PropertyValue.From((double)float.MaxValue))]))),
            source.Definitions, source.Schemas);
        using var session = Session(source);
        var before = session.Read();
        var batch = new ReparentBatch(Operation(7), [new(Second, First)]);

        var result = session.Reparent(batch);

        Assert.Equal(SceneAuthoringError.InvalidScene, result.Error);
        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Path);
        Assert.NotEmpty(result.Message);
        Assert.Same(before, session.Read());
        AssertParent(before, Second, null);
        Assert.Equal((double)float.MaxValue, Number(before, Second, "x"));
        Assert.Same(result, session.Reparent(batch));
        Assert.True(session.Apply(new(Operation(7), [Edit(Second, 0)])).Succeeded);
        Assert.True(session.Reparent(new(Operation(8), [new(Second, First)])).Succeeded);
        AssertParent(session.Read(), Second, First);
    }

    [Fact]
    public void Batch_FreezesCallerOrderAndValuesBeforeSourceListIsChanged()
    {
        var operation = Operation(7);
        var edits = new List<ReparentEdit> { new(Entity(3), Second), new(Second, null) };
        var batch = new ReparentBatch(operation, edits);
        edits[0] = new(Entity(3), null);
        edits.Clear();

        using var session = Session(Document(count: 3));
        Assert.True(session.Reparent(batch).Succeeded);

        Assert.Same(operation, batch.Operation);
        Assert.Equal(new[] { new ReparentEdit(Entity(3), Second), new ReparentEdit(Second, null) }, batch.Edits);
        AssertParent(session.Read(), Entity(3), Second);
        AssertParent(session.Read(), Second, null);
    }

    [Fact]
    public void Batch_RejectsNullAndEmptyInputsAndAcceptsExact4096Limit()
    {
        var operation = Operation(7);
        Assert.Throws<ArgumentNullException>(() => new ReparentBatch(null!, [new(First, null)]));
        Assert.Throws<ArgumentNullException>(() => new ReparentBatch(operation, null!));
        Assert.Throws<ArgumentException>(() => new ReparentBatch(operation, []));
        var batch = new ReparentBatch(operation, Enumerable.Range(1, 4096).Select(index => new ReparentEdit(Entity(index), null)));
        Assert.Equal(4096, batch.Edits.Length);
        Assert.Equal(new ReparentEdit(First, null), batch.Edits[0]);
        Assert.Equal(new ReparentEdit(Entity(4096), null), batch.Edits[^1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReparentBatch(operation,
            Enumerable.Range(1, 4097).Select(index => new ReparentEdit(Entity(index), null))));
    }

    [Fact]
    public void Reparent_ConfiguredBatchLimitRejectsBeforeResolutionButAcceptsExactLimit()
    {
        var resolutions = 0;
        using var session = Session(options: new() { MaxBatchEdits = 1 }, beforeCreate: _ => resolutions++);
        var before = session.Read();

        var rejected = session.Reparent(new(Operation(7), [new(First, Second), new(Second, null)]));

        Assert.Equal(SceneAuthoringError.BatchLimitExceeded, rejected.Error);
        Assert.Same(before, session.Read());
        Assert.Equal(1, resolutions);
        Assert.True(session.Reparent(new(Operation(7), [new(Second, null)])).Succeeded);
        Assert.Equal(2, resolutions);
        AssertParent(session.Read(), Second, null);
        AssertState(session.Read(), 8, 8, 1, 0);
    }

    [Fact]
    public void Reparent_NullBatchRejectsWithoutChangingStateOrConsumingReceipt()
    {
        using var session = Session(options: new() { MaxOperations = 1 });
        var before = session.Read();

        Assert.Throws<ArgumentNullException>(() => session.Reparent(null!));

        Assert.Same(before, session.Read());
        Assert.True(session.Reparent(new(Operation(7), [new(Second, null)])).Succeeded);
        AssertHistory(session, 8, 8, 3, 0, null, 1, 0);
    }

    [Fact]
    public void History_TransformAndReparentUseOneUndoRedoStackAndRestoreContentCheckpoints()
    {
        using var session = Session();
        Assert.True(session.Apply(Batch(7, 10)).Succeeded);
        Assert.True(session.Reparent(new(Operation(8), [new(Second, null)])).Succeeded);
        var reparented = session.Read();
        Assert.True(session.Apply(new(Operation(9), [Edit(Second, 20)])).Succeeded);
        AssertHistory(session, 10, 10, 10, 20, null, 3, 0);

        Assert.True(session.Undo(Operation(10)).Succeeded);
        AssertHistory(session, 11, 9, 10, 0, null, 2, 1);
        Assert.True(session.Undo(Operation(11)).Succeeded);
        AssertHistory(session, 12, 8, 10, 0, First, 1, 2);
        Assert.True(session.Undo(Operation(12)).Succeeded);
        AssertHistory(session, 13, 7, 3, 0, First, 0, 3);
        Assert.True(session.Redo(Operation(13)).Succeeded);
        AssertHistory(session, 14, 8, 10, 0, First, 1, 2);
        Assert.True(session.Redo(Operation(14)).Succeeded);
        AssertHistory(session, 15, 9, 10, 0, null, 2, 1);
        Assert.True(session.Redo(Operation(15)).Succeeded);
        AssertHistory(session, 16, 10, 10, 20, null, 3, 0);
        AssertState(reparented, 9, 9, 2, 0);
        AssertParent(reparented, Second, null);
        Assert.Equal(0, Number(reparented, Second, "x"));
    }

    [Fact]
    public void History_MixedTransactionsShareBoundAndFreshReparentClearsRedo()
    {
        using var session = Session(options: new() { MaxHistoryEntries = 2 });
        Assert.True(session.Apply(Batch(7, 10)).Succeeded);
        Assert.True(session.Reparent(new(Operation(8), [new(Second, null)])).Succeeded);
        Assert.True(session.Apply(Batch(9, 20)).Succeeded);
        AssertHistory(session, 10, 10, 20, 0, null, 2, 0);
        Assert.True(session.Undo(Operation(10)).Succeeded);
        AssertHistory(session, 11, 9, 10, 0, null, 1, 1);
        Assert.True(session.Undo(Operation(11)).Succeeded);
        AssertHistory(session, 12, 8, 10, 0, First, 0, 2);
        var before = session.Read();
        Assert.Equal(SceneAuthoringError.NoUndo, session.Undo(Operation(12)).Error);
        Assert.Same(before, session.Read());
        Assert.True(session.Redo(Operation(12)).Succeeded);
        AssertHistory(session, 13, 9, 10, 0, null, 1, 1);

        Assert.True(session.Reparent(new(Operation(13), [new(Second, First)])).Succeeded);

        AssertHistory(session, 14, 14, 10, 0, First, 2, 0);
        before = session.Read();
        Assert.Equal(SceneAuthoringError.NoRedo, session.Redo(Operation(14)).Error);
        Assert.Same(before, session.Read());
    }

    [Fact]
    public void History_NoOpReparentCommitsNewCheckpointAndClearsTransformRedo()
    {
        using var session = Session();
        Assert.True(session.Apply(Batch(7, 10)).Succeeded);
        Assert.True(session.Undo(Operation(8)).Succeeded);
        AssertHistory(session, 9, 7, 3, 0, First, 0, 1);

        Assert.True(session.Reparent(new(Operation(9), [new(Second, First)])).Succeeded);

        AssertHistory(session, 10, 10, 3, 0, First, 1, 0);
        Assert.Equal(SceneAuthoringError.NoRedo, session.Redo(Operation(10)).Error);
        Assert.True(session.Undo(Operation(10)).Succeeded);
        AssertHistory(session, 11, 7, 3, 0, First, 0, 1);
    }

    [Fact]
    public void History_ZeroLimitDisablesReparentHistoryButRetainsReceipt()
    {
        using var session = Session(options: new() { MaxHistoryEntries = 0 });
        var batch = new ReparentBatch(Operation(7), [new(Second, null)]);
        var result = session.Reparent(batch);

        Assert.True(result.Succeeded);
        AssertHistory(session, 8, 8, 3, 0, null, 0, 0);
        var before = session.Read();
        Assert.Equal(SceneAuthoringError.NoUndo, session.Undo(Operation(8)).Error);
        Assert.Equal(SceneAuthoringError.NoRedo, session.Redo(Operation(8)).Error);
        Assert.Same(result, session.Reparent(batch));
        Assert.Same(before, session.Read());
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("revision")]
    [InlineData("parent")]
    [InlineData("entity")]
    [InlineData("order")]
    [InlineData("transform")]
    [InlineData("undo")]
    [InlineData("redo")]
    public void Operations_ExactReparentRetryIsStableButChangedIntentConflictsAcrossCommandKinds(string changed)
    {
        using var session = Session(Document(count: 3));
        var operation = Operation(7);
        var batch = new ReparentBatch(operation, [new(Entity(3), Second), new(Second, null)]);
        var receipt = session.Reparent(batch);
        Assert.True(receipt.Succeeded);
        Assert.True(session.Apply(Batch(8, 10)).Succeeded);
        var before = session.Read();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Same(receipt, session.Reparent(new(new(operation.Id, 7, operation.Actor), batch.Edits), cancelled.Token));

        var conflict = changed switch
        {
            "actor" => session.Reparent(new(new(operation.Id, 7, "other"), batch.Edits)),
            "revision" => session.Reparent(new(new(operation.Id, 8, operation.Actor), batch.Edits)),
            "parent" => session.Reparent(new(operation, [new(Entity(3), null), new(Second, null)])),
            "entity" => session.Reparent(new(operation, [new(First, Second), new(Second, null)])),
            "order" => session.Reparent(new(operation, batch.Edits.Reverse())),
            "transform" => session.Apply(new(operation, [Edit(First, 99)])),
            "undo" => session.Undo(operation),
            _ => session.Redo(operation)
        };

        Assert.Equal(SceneAuthoringError.OperationIdConflict, conflict.Error);
        Assert.Same(before, session.Read());
        AssertState(before, 9, 9, 2, 0);
        Assert.Equal(10, Number(before, First, "x"));
        AssertParent(before, Entity(3), Second);
    }

    [Fact]
    public void Operations_TransformReceiptCannotBeReusedAsReparentIntent()
    {
        using var session = Session();
        var batch = Batch(7, 10);
        var receipt = session.Apply(batch);
        var before = session.Read();

        var result = session.Reparent(new(batch.Operation, [new(Second, null)]));

        Assert.Equal(SceneAuthoringError.OperationIdConflict, result.Error);
        Assert.Same(before, session.Read());
        Assert.Same(receipt, session.Apply(batch));
        AssertParent(before, Second, First);
        Assert.Equal(10, Number(before, First, "x"));
    }

    [Fact]
    public void Operations_RevisionConflictIsCachedAndExpectedRevisionUsesDocumentRatherThanContentCheckpoint()
    {
        using var session = Session();
        var future = new ReparentBatch(Operation(8), [new(Second, null)]);
        var rejected = session.Reparent(future);
        Assert.Equal(SceneAuthoringError.RevisionConflict, rejected.Error);
        Assert.Equal(7, rejected.Revision);
        Assert.True(session.Apply(Batch(7, 10)).Succeeded);
        var atExpectedRevision = session.Read();
        Assert.Same(rejected, session.Reparent(future));
        Assert.Same(atExpectedRevision, session.Read());
        Assert.True(session.Undo(Operation(8)).Succeeded);
        AssertHistory(session, 9, 7, 3, 0, First, 0, 1);
        var restored = session.Read();

        Assert.Equal(SceneAuthoringError.RevisionConflict, session.Reparent(new(Operation(restored.ContentRevision), [new(Second, null)])).Error);
        Assert.Same(restored, session.Read());
        Assert.True(session.Reparent(new(Operation(9), [new(Second, null)])).Succeeded);
        AssertHistory(session, 10, 10, 3, 0, null, 1, 0);
    }

    [Fact]
    public void Ledger_ReparentSharesCapacityWithTransformAndRetainsSuccessfulAndRejectedReceipts()
    {
        using var session = Session(options: new() { MaxOperations = 3 });
        var transform = Batch(7, 10);
        var transformReceipt = session.Apply(transform);
        var reparent = new ReparentBatch(Operation(8), [new(Second, null)]);
        var success = session.Reparent(reparent);
        var invalid = new ReparentBatch(Operation(9), [new(Second, Entity(99))]);
        var failure = session.Reparent(invalid);
        Assert.True(transformReceipt.Succeeded);
        Assert.True(success.Succeeded);
        Assert.Equal(SceneAuthoringError.InvalidScene, failure.Error);
        var before = session.Read();
        var next = new ReparentBatch(Operation(9), [new(Second, First)]);

        Assert.Equal(SceneAuthoringError.OperationLimitReached, session.Reparent(next).Error);
        Assert.Equal(SceneAuthoringError.OperationLimitReached, session.Apply(Batch(9, 20)).Error);
        Assert.Equal(SceneAuthoringError.OperationLimitReached, session.Undo(Operation(9)).Error);
        Assert.Equal(SceneAuthoringError.OperationLimitReached, session.Reparent(new(next.Operation, [new(Second, null)])).Error);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Same(transformReceipt, session.Apply(transform, cancelled.Token));
        Assert.Same(success, session.Reparent(reparent, cancelled.Token));
        Assert.Same(failure, session.Reparent(invalid, cancelled.Token));
        Assert.Equal(SceneAuthoringError.OperationIdConflict, session.Reparent(new(reparent.Operation, [new(Second, First)])).Error);
        Assert.Same(before, session.Read());
        AssertHistory(session, 9, 9, 10, 0, null, 2, 0);
    }

    [Fact]
    public void Cancellation_PreCancelledReparentConsumesNoReceiptAndCancelledRetryReturnsCommittedReceipt()
    {
        using var session = Session(options: new() { MaxOperations = 1 });
        var before = session.Read();
        var batch = new ReparentBatch(Operation(7), [new(Second, null)]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => session.Reparent(batch, cancellation.Token));

        Assert.Same(before, session.Read());
        var receipt = session.Reparent(batch);
        Assert.True(receipt.Succeeded);
        Assert.Same(receipt, session.Reparent(batch, cancellation.Token));
        AssertHistory(session, 8, 8, 3, 0, null, 1, 0);
    }

    [Fact]
    public void Cancellation_DuringReparentPreparationPreservesSnapshotHistoryAndRetryableOperationId()
    {
        using var cancellation = new CancellationTokenSource();
        var armed = false;
        using var session = Session(options: new() { MaxOperations = 1 }, beforeCreate: _ => { if (armed) cancellation.Cancel(); });
        var before = session.Read();
        var batch = new ReparentBatch(Operation(7), [new(Second, null)]);
        armed = true;

        Assert.ThrowsAny<OperationCanceledException>(() => session.Reparent(batch, cancellation.Token));

        Assert.Same(before, session.Read());
        AssertHistory(session, 7, 7, 3, 0, First, 0, 0);
        armed = false;
        Assert.True(session.Reparent(batch).Succeeded);
        AssertHistory(session, 8, 8, 3, 0, null, 1, 0);
    }

    [Theory]
    [InlineData("undo")]
    [InlineData("redo")]
    public void Cancellation_DuringReparentHistoryPreparationPreservesParentAndHistoryUntilSameIdRetry(string command)
    {
        using var cancellation = new CancellationTokenSource();
        var armed = false;
        using var session = Session(beforeCreate: _ => { if (armed) cancellation.Cancel(); });
        Assert.True(session.Reparent(new(Operation(7), [new(Second, null)])).Succeeded);
        if (command == "redo") Assert.True(session.Undo(Operation(8)).Succeeded);
        var before = session.Read();
        var operation = Operation(before.Document.Revision);
        SceneAuthoringResult Invoke(CancellationToken token) => command == "undo"
            ? session.Undo(operation, token) : session.Redo(operation, token);
        armed = true;

        Assert.ThrowsAny<OperationCanceledException>(() => Invoke(cancellation.Token));

        Assert.Same(before, session.Read());
        armed = false;
        Assert.True(Invoke(default).Succeeded);
        if (command == "undo") AssertHistory(session, 9, 7, 3, 0, First, 0, 1);
        else AssertHistory(session, 10, 8, 3, 0, null, 1, 0);
    }

    [Theory]
    [InlineData("scene")]
    [InlineData("invalid-data")]
    public void Cancellation_ExpectedResolutionFailureDoesNotConsumeReceiptWhenCancelled(string failure)
    {
        var source = Document();
        var invalid = new SceneDocument(source.Id, source.Revision,
            [source.Entities[0], new EntityInstance(Second, Definition, parentId: Entity(99))], source.Definitions, source.Schemas);
        Exception expected = failure == "scene"
            ? Assert.Throws<SceneResolutionException>(() => SceneResolver.Resolve(invalid))
            : new InvalidDataException("candidate transform validation failed");
        using var cancellation = new CancellationTokenSource();
        var armed = false;
        using var session = Session(options: new() { MaxOperations = 1 }, beforeCreate: _ =>
        {
            if (!armed) return;
            cancellation.Cancel();
            throw expected;
        });
        var before = session.Read();
        var batch = new ReparentBatch(Operation(7), [new(Second, null)]);
        armed = true;

        Assert.ThrowsAny<OperationCanceledException>(() => session.Reparent(batch, cancellation.Token));

        Assert.Same(before, session.Read());
        AssertHistory(session, 7, 7, 3, 0, First, 0, 0);
        armed = false;
        Assert.True(session.Reparent(batch).Succeeded);
        AssertHistory(session, 8, 8, 3, 0, null, 1, 0);
    }

    [Fact]
    public void Preparation_UnexpectedResolverFailurePreservesExceptionIdentityAndAllowsSameIdRetry()
    {
        var expected = new IOException("reparent resolver failure");
        var armed = false;
        using var session = Session(options: new() { MaxOperations = 1 }, beforeCreate: _ => { if (armed) throw expected; });
        var before = session.Read();
        var batch = new ReparentBatch(Operation(7), [new(Second, null)]);
        armed = true;

        Assert.Same(expected, Assert.Throws<IOException>(() => session.Reparent(batch)));

        Assert.Same(before, session.Read());
        armed = false;
        Assert.True(session.Reparent(batch).Succeeded);
        AssertHistory(session, 8, 8, 3, 0, null, 1, 0);
    }

    [Fact]
    public void Preparation_ReadSeesPreviousCommitAndReentrantReparentIsRejected()
    {
        ISceneAuthoringSession? session = null;
        SceneAuthoringState? before = null;
        var armed = false;
        var observations = 0;
        session = Session(beforeCreate: _ =>
        {
            if (!armed) return;
            observations++;
            Assert.Same(before, session!.Read());
            AssertParent(session.Read(), Second, First);
            Assert.Throws<InvalidOperationException>(() => session.Reparent(new(Operation(7), [new(Second, null)])));
        });
        using (session)
        {
            before = session.Read();
            armed = true;
            Assert.True(session.Reparent(new(Operation(7), [new(Second, null)])).Succeeded);
            Assert.Equal(1, observations);
            AssertHistory(session, 8, 8, 3, 0, null, 1, 0);
        }
    }

    [Fact]
    public async Task ConcurrentTransformAndReparentAtSameRevisionCommitExactlyOneWholeTransaction()
    {
        using var session = Session();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transform = Batch(7, 10);
        var reparent = new ReparentBatch(Operation(7), [new(Second, null)]);
        var first = Task.Run(async () => { await start.Task; return session.Apply(transform); });
        var second = Task.Run(async () => { await start.Task; return session.Reparent(reparent); });
        start.SetResult();

        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));

        var winner = Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => result.Error == SceneAuthoringError.RevisionConflict);
        var after = session.Read();
        AssertState(after, 8, 8, 1, 0);
        var transformed = winner.Operation.Id == transform.Operation.Id;
        Assert.Equal(transformed ? 10 : 3, Number(after, First, "x"));
        AssertParent(after, Second, transformed ? First : null);
        Assert.Same(results[0], session.Apply(transform));
        Assert.Same(results[1], session.Reparent(reparent));
        Assert.Same(after, session.Read());
    }

    [Fact]
    public void Revision_ReparentAtMaximumRejectsWithoutWrappingOrChangingParent()
    {
        using var session = Session(Document(long.MaxValue - 1));
        Assert.True(session.Reparent(new(Operation(long.MaxValue - 1), [new(Second, null)])).Succeeded);
        var before = session.Read();
        var batch = new ReparentBatch(Operation(long.MaxValue), [new(Second, First)]);

        var result = session.Reparent(batch);

        Assert.Equal(SceneAuthoringError.RevisionExhausted, result.Error);
        Assert.Equal(long.MaxValue, result.Revision);
        Assert.Same(before, session.Read());
        Assert.Same(result, session.Reparent(batch));
        AssertHistory(session, long.MaxValue, long.MaxValue, 3, 0, null, 1, 0);
    }

    private static SceneDocument HierarchyDocument()
    {
        var document = Document(count: 4);
        var variantId = new EntityDefinitionId(Id(11));
        var variant = new EntityDefinition(variantId, new(removedComponents: [Interaction], propertyOverrides:
            [new(Transform, "x", PropertyValue.From(8d)), new(Appearance, "g", PropertyValue.From(.75d))]), Definition);
        var firstPatch = document.Entities[0].Changes;
        var transform = new Dictionary<string, double>
        {
            ["x"] = 3, ["y"] = -2, ["z"] = 7, ["pitch"] = .1, ["yaw"] = .2,
            ["roll"] = -.3, ["sx"] = 1.5, ["sy"] = .75, ["sz"] = 2
        };
        var first = new EntityInstance(First, Definition, new(firstPatch.AddedComponents, firstPatch.RemovedComponents,
            firstPatch.PropertyOverrides.Where(value => value.ComponentId != Transform)
                .Concat(transform.Select(value => new ComponentPropertyOverride(Transform, value.Key, PropertyValue.From(value.Value))))));
        var entities = new[]
        {
            document.Entities[2],
            new EntityInstance(Entity(4), Definition, parentId: Entity(3)),
            first,
            new EntityInstance(Second, variantId, new(restoredComponents: [Interaction]), First)
        };
        return new(document.Id, document.Revision, entities, document.Definitions.Add(variant), document.Schemas);
    }

    private static void AssertParent(SceneAuthoringState state, EntityId entity, EntityId? parent)
    {
        Assert.Equal(parent, state.Document.Entities.Single(value => value.Id == entity).ParentId);
        Assert.Equal(parent, state.Resolved.Entities[entity].ParentId);
    }

    private static void AssertState(SceneAuthoringState state, long revision, long content, int undo, int redo)
    {
        Assert.Equal(revision, state.Document.Revision);
        Assert.Equal(revision, state.Resolved.Revision);
        Assert.Equal(content, state.ContentRevision);
        Assert.Equal(undo, state.UndoCount);
        Assert.Equal(redo, state.RedoCount);
    }

    private static void AssertHistory(ISceneAuthoringSession session, long revision, long content,
        double firstX, double secondX, EntityId? parent, int undo, int redo)
    {
        var state = session.Read();
        AssertState(state, revision, content, undo, redo);
        Assert.Equal(firstX, Number(state, First, "x"));
        Assert.Equal(secondX, Number(state, Second, "x"));
        AssertParent(state, Second, parent);
    }

    private static void AssertLocalDataUnchanged(SceneAuthoringState before, SceneAuthoringState after, EntityId id)
    {
        var source = before.Document.Entities.Single(entity => entity.Id == id);
        var candidate = after.Document.Entities.Single(entity => entity.Id == id);
        Assert.Equal(source.DefinitionId, candidate.DefinitionId);
        Assert.Equal(source.Changes.AddedComponents.Select(component => component.Id), candidate.Changes.AddedComponents.Select(component => component.Id));
        foreach (var component in source.Changes.AddedComponents)
        {
            var changed = candidate.Changes.AddedComponents.Single(value => value.Id == component.Id);
            Assert.Equal(component.TypeId, changed.TypeId);
            Assert.Equal(component.SchemaVersion, changed.SchemaVersion);
            Assert.Equal(component.Properties.OrderBy(property => property.Key), changed.Properties.OrderBy(property => property.Key));
        }
        Assert.Equal(source.Changes.RemovedComponents.OrderBy(component => component.Value), candidate.Changes.RemovedComponents.OrderBy(component => component.Value));
        Assert.Equal(source.Changes.RestoredComponents.OrderBy(component => component.Value), candidate.Changes.RestoredComponents.OrderBy(component => component.Value));
        Assert.Equal(source.Changes.PropertyOverrides.Select(value => (value.ComponentId, value.PropertyId, value.Value)),
            candidate.Changes.PropertyOverrides.Select(value => (value.ComponentId, value.PropertyId, value.Value)));
        var resolvedBefore = before.Resolved.Entities[id];
        var resolvedAfter = after.Resolved.Entities[id];
        Assert.Equal(resolvedBefore.DefinitionChain.AsEnumerable(), resolvedAfter.DefinitionChain.AsEnumerable());
        Assert.Equal(resolvedBefore.Components.Keys.OrderBy(component => component.Value), resolvedAfter.Components.Keys.OrderBy(component => component.Value));
        foreach (var (componentId, component) in resolvedBefore.Components)
        {
            var changed = resolvedAfter.Components[componentId];
            Assert.Equal(component.TypeId, changed.TypeId);
            Assert.Equal(component.SchemaVersion, changed.SchemaVersion);
            Assert.Equal(component.Source, changed.Source);
            Assert.Equal(component.PresenceSource, changed.PresenceSource);
            Assert.Equal(component.Properties.Keys.OrderBy(key => key), changed.Properties.Keys.OrderBy(key => key));
            foreach (var (key, property) in component.Properties)
            {
                Assert.Equal(property.Value, changed.Properties[key].Value);
                Assert.Equal(property.Source, changed.Properties[key].Source);
                Assert.Equal(property.IsSchemaDefault, changed.Properties[key].IsSchemaDefault);
            }
        }
    }
}
