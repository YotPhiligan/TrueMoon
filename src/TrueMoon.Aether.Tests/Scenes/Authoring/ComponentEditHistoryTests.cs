using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using Xunit;
using static TrueMoon.Aether.Tests.Scenes.Authoring.AuthoringTestData;

namespace TrueMoon.Aether.Tests.Scenes.Authoring;

public sealed class ComponentEditHistoryTests
{
    [Fact]
    public void Receipt_ValueEquivalentAdditionReturnsOriginalReceiptAfterLaterEditAndCancellation()
    {
        using var session = Session();
        var operation = Operation(7);
        var batch = new ComponentEditBatch(operation, [],
            [new(Second, Custom, ComponentChangeKind.Add, new(Custom, CustomType, 1, [new("note", PropertyValue.From("same"))]))]);
        var receipt = session.EditComponents(batch);
        Assert.True(receipt.Succeeded);
        Assert.True(session.Apply(Batch(8, 20)).Succeeded);
        var before = session.Read();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var duplicate = session.EditComponents(new(new(operation.Id, 7, operation.Actor), [],
            [new(Second, Custom, ComponentChangeKind.Add, new(Custom, CustomType, 1, [new("note", PropertyValue.From("same"))]))]), cancelled.Token);

        Assert.Same(receipt, duplicate);
        Assert.Same(before, session.Read());
        Assert.Equal(9, before.Document.Revision);
        Assert.Equal(2, before.UndoCount);
        Assert.Equal("same", before.Resolved.Entities[Second].Components[Custom].Properties["note"].Value.GetString());
        Assert.Equal(20, Number(before, First, "x"));
    }

    [Theory]
    [InlineData("property")]
    [InlineData("addition-value")]
    [InlineData("presence-kind")]
    [InlineData("actor")]
    [InlineData("expected-revision")]
    [InlineData("transform-command")]
    [InlineData("reparent-command")]
    [InlineData("undo-command")]
    [InlineData("redo-command")]
    public void Receipt_ChangedPayloadMetadataOrCommandKindCannotReuseComponentOperationId(string conflict)
    {
        using var session = Session();
        var operation = Operation(7);
        var property = new PropertyEdit(First, Transform, "x", PropertyValue.From(9d));
        var addition = new ComponentEdit(Second, Custom, ComponentChangeKind.Add,
            new(Custom, CustomType, 1, [new("note", PropertyValue.From("original"))]));
        var batch = new ComponentEditBatch(operation, [property], [addition]);
        var receipt = session.EditComponents(batch);
        Assert.True(receipt.Succeeded);
        var before = session.Read();
        var candidate = conflict switch
        {
            "actor" => new AuthoringOperation(operation.Id, 7, "different actor"),
            "expected-revision" => new AuthoringOperation(operation.Id, 8, operation.Actor),
            _ => operation
        };

        var result = conflict switch
        {
            "transform-command" => session.Apply(new(operation, [Edit(First, 9)])),
            "reparent-command" => session.Reparent(new(operation, [new(Second, null)])),
            "undo-command" => session.Undo(operation),
            "redo-command" => session.Redo(operation),
            _ => session.EditComponents(new(candidate,
                [conflict == "property" ? property with { Value = PropertyValue.From(10d) } : property],
                [conflict == "addition-value" ? addition with { Addition = new(Custom, CustomType, 1, [new("note", PropertyValue.From("different"))]) } :
                 conflict == "presence-kind" ? new(Second, Custom, ComponentChangeKind.Remove) : addition]))
        };

        Assert.Equal(SceneAuthoringError.OperationIdConflict, result.Error);
        Assert.Same(before, session.Read());
        Assert.Equal(9, Number(before, First, "x"));
        Assert.Equal("original", before.Resolved.Entities[Second].Components[Custom].Properties["note"].Value.GetString());
        Assert.Same(receipt, session.EditComponents(batch));
    }

    [Fact]
    public void Receipt_StaleRevisionRemainsTerminalAndDoesNotOverwriteCurrentComponentValues()
    {
        using var session = Session();
        Assert.True(session.EditComponents(Note(7, "current")).Succeeded);
        var before = session.Read();
        var stale = Note(7, "stale");

        var result = session.EditComponents(stale);

        Assert.Equal(SceneAuthoringError.RevisionConflict, result.Error);
        Assert.Equal(8, result.Revision);
        Assert.Equal(8, result.ContentRevision);
        Assert.Same(before, session.Read());
        Assert.True(session.Apply(Batch(8, 10)).Succeeded);
        var later = session.Read();
        Assert.Same(result, session.EditComponents(stale));
        Assert.Same(later, session.Read());
        Assert.Equal("current", session.Read().Resolved.Entities[First].Components[Custom].Properties["note"].Value.GetString());
        Assert.Equal(SceneAuthoringError.OperationIdConflict, session.EditComponents(new(
            new(stale.Operation.Id, 9, stale.Operation.Actor), stale.Properties)).Error);
    }

    [Fact]
    public void Receipt_ComponentTransformAndParentCommandsShareOneNonEvictingCapacity()
    {
        using var session = Session(options: new() { MaxOperations = 3 });
        var componentBatch = Note(7, "committed");
        var component = session.EditComponents(componentBatch);
        var transformBatch = Batch(8, 10);
        var transform = session.Apply(transformBatch);
        var parentBatch = new ReparentBatch(Operation(9), [new(Second, null)]);
        var parent = session.Reparent(parentBatch);
        Assert.True(component.Succeeded);
        Assert.True(transform.Succeeded);
        Assert.True(parent.Succeeded);
        var before = session.Read();

        Assert.Equal(SceneAuthoringError.OperationLimitReached, session.EditComponents(Note(10, "rejected")).Error);
        Assert.Equal(SceneAuthoringError.OperationLimitReached, session.Undo(Operation(10)).Error);
        Assert.Same(component, session.EditComponents(componentBatch));
        Assert.Same(transform, session.Apply(transformBatch));
        Assert.Same(parent, session.Reparent(parentBatch));
        Assert.Same(before, session.Read());
        Assert.Equal(3, before.UndoCount);
        Assert.Equal("committed", before.Resolved.Entities[First].Components[Custom].Properties["note"].Value.GetString());
    }

    [Fact]
    public void History_ComponentTransformAndParentEditsUseSingleStackAndContentCheckpoints()
    {
        using var session = Session();
        Assert.True(session.EditComponents(Note(7, "edited")).Succeeded);
        Assert.True(session.Apply(Batch(8, 10)).Succeeded);
        Assert.True(session.Reparent(new(Operation(9), [new(Second, null)])).Succeeded);
        AssertState(session, 10, 10, "edited", 10, null, 3, 0);

        Assert.True(session.Undo(Operation(10)).Succeeded);
        AssertState(session, 11, 9, "edited", 10, First, 2, 1);
        Assert.True(session.Undo(Operation(11)).Succeeded);
        AssertState(session, 12, 8, "edited", 3, First, 1, 2);
        Assert.True(session.Undo(Operation(12)).Succeeded);
        AssertState(session, 13, 7, "keep", 3, First, 0, 3);
        Assert.True(session.Redo(Operation(13)).Succeeded);
        AssertState(session, 14, 8, "edited", 3, First, 1, 2);
        Assert.True(session.Redo(Operation(14)).Succeeded);
        AssertState(session, 15, 9, "edited", 10, First, 2, 1);
        Assert.True(session.Redo(Operation(15)).Succeeded);
        AssertState(session, 16, 10, "edited", 10, null, 3, 0);
    }

    [Fact]
    public void History_FreshComponentEditAfterUndoClearsRedoAndRespectsSharedHistoryBound()
    {
        using var session = Session(options: new() { MaxHistoryEntries = 2 });
        Assert.True(session.EditComponents(Note(7, "one")).Succeeded);
        Assert.True(session.Apply(Batch(8, 10)).Succeeded);
        Assert.True(session.Reparent(new(Operation(9), [new(Second, null)])).Succeeded);
        Assert.Equal(2, session.Read().UndoCount);
        Assert.True(session.Undo(Operation(10)).Succeeded);

        Assert.True(session.EditComponents(Note(11, "new branch")).Succeeded);

        AssertState(session, 12, 12, "new branch", 10, First, 2, 0);
        var before = session.Read();
        Assert.Equal(SceneAuthoringError.NoRedo, session.Redo(Operation(12)).Error);
        Assert.Same(before, session.Read());
        Assert.True(session.Undo(Operation(12)).Succeeded);
        AssertState(session, 13, 9, "one", 10, First, 1, 1);
        Assert.True(session.Undo(Operation(13)).Succeeded);
        AssertState(session, 14, 8, "one", 3, First, 0, 2);
        Assert.Equal(SceneAuthoringError.NoUndo, session.Undo(Operation(14)).Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancellation_BeforeCommitLeavesSnapshotAndReceiptIdAvailableForRetry(bool duringResolution)
    {
        using var cancellation = new CancellationTokenSource();
        var armed = false;
        using var session = Session(options: new() { MaxOperations = 1 }, beforeCreate: _ =>
        {
            if (armed && duringResolution) cancellation.Cancel();
        });
        var before = session.Read();
        var batch = Note(7, "committed after retry");
        armed = true;
        if (!duringResolution) cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => session.EditComponents(batch, cancellation.Token));

        Assert.Same(before, session.Read());
        Assert.Equal(0, before.UndoCount);
        armed = false;
        var receipt = session.EditComponents(batch);
        Assert.True(receipt.Succeeded);
        Assert.Equal("committed after retry", session.Read().Resolved.Entities[First].Components[Custom].Properties["note"].Value.GetString());
        Assert.Same(receipt, session.EditComponents(batch, cancellation.Token));
        Assert.Equal(1, session.Read().UndoCount);
    }

    [Fact]
    public void Preparation_UnexpectedResolutionFailurePreservesExceptionAndAllowsSameOperationRetry()
    {
        var expected = new IOException("component candidate failed");
        var armed = false;
        using var session = Session(options: new() { MaxOperations = 1 }, beforeCreate: _ => { if (armed) throw expected; });
        var before = session.Read();
        var batch = Note(7, "retry");
        armed = true;

        Assert.Same(expected, Assert.Throws<IOException>(() => session.EditComponents(batch)));

        Assert.Same(before, session.Read());
        armed = false;
        Assert.True(session.EditComponents(batch).Succeeded);
        Assert.Equal("retry", session.Read().Resolved.Entities[First].Components[Custom].Properties["note"].Value.GetString());
    }

    [Fact]
    public async Task ConcurrentComponentTransformAndParentCommandsAtSameRevisionCommitExactlyOneWholeIntent()
    {
        using var session = Session();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var component = Note(7, "winner");
        var transform = Batch(7, 10);
        var parent = new ReparentBatch(Operation(7), [new(Second, null)]);
        var actions = new[]
        {
            Task.Run(async () => { await start.Task; return session.EditComponents(component); }),
            Task.Run(async () => { await start.Task; return session.Apply(transform); }),
            Task.Run(async () => { await start.Task; return session.Reparent(parent); })
        };
        start.SetResult();

        var results = await Task.WhenAll(actions).WaitAsync(TimeSpan.FromSeconds(10));

        var winner = Assert.Single(results, result => result.Succeeded);
        Assert.Equal(2, results.Count(result => result.Error == SceneAuthoringError.RevisionConflict));
        AssertState(session, 8, 8, winner.Operation.Id == component.Operation.Id ? "winner" : "keep",
            winner.Operation.Id == transform.Operation.Id ? 10 : 3, winner.Operation.Id == parent.Operation.Id ? null : First, 1, 0);
        var before = session.Read();
        Assert.Same(results[0], session.EditComponents(component));
        Assert.Same(results[1], session.Apply(transform));
        Assert.Same(results[2], session.Reparent(parent));
        Assert.Same(before, session.Read());
    }

    [Fact]
    public void Revision_ComponentEditAtMaximumRetainsContentAndReturnsRepeatableExhaustionReceipt()
    {
        using var session = Session(Document(long.MaxValue - 1));
        Assert.True(session.EditComponents(Note(long.MaxValue - 1, "maximum")).Succeeded);
        var before = session.Read();
        var batch = Note(long.MaxValue, "overflow");

        var result = session.EditComponents(batch);

        Assert.Equal(SceneAuthoringError.RevisionExhausted, result.Error);
        Assert.Equal(long.MaxValue, result.Revision);
        Assert.Same(before, session.Read());
        Assert.Equal("maximum", before.Resolved.Entities[First].Components[Custom].Properties["note"].Value.GetString());
        Assert.Same(result, session.EditComponents(batch));
    }

    private static ComponentEditBatch Note(long revision, string text)
        => new(Operation(revision), [new(First, Custom, "note", PropertyValue.From(text))]);

    private static void AssertState(ISceneAuthoringSession session, long revision, long content, string note,
        double x, EntityId? parent, int undo, int redo)
    {
        var state = session.Read();
        Assert.Equal(revision, state.Document.Revision);
        Assert.Equal(revision, state.Resolved.Revision);
        Assert.Equal(content, state.ContentRevision);
        Assert.Equal(note, state.Resolved.Entities[First].Components[Custom].Properties["note"].Value.GetString());
        Assert.Equal(x, Number(state, First, "x"));
        Assert.Equal(parent, state.Resolved.Entities[Second].ParentId);
        Assert.Equal(undo, state.UndoCount);
        Assert.Equal(redo, state.RedoCount);
    }
}
