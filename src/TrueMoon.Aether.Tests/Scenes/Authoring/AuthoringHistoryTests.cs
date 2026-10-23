using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using Xunit;
using static TrueMoon.Aether.Tests.Scenes.Authoring.AuthoringTestData;

namespace TrueMoon.Aether.Tests.Scenes.Authoring;

public sealed class AuthoringHistoryTests
{
    [Fact]
    public void History_BoundsCombinedUndoRedoAndRestoresContentWithMonotonicDocumentRevisions()
    {
        using var session = Session(options: new() { MaxHistoryEntries = 2 });
        Assert.True(session.Apply(Batch(7, 10)).Succeeded);
        Assert.True(session.Apply(Batch(8, 20)).Succeeded);
        var third = Batch(9, 30); var thirdReceipt = session.Apply(third);
        Assert.True(thirdReceipt.Succeeded); AssertState(session, 10, 10, 30, 2, 0);
        Assert.True(session.Undo(Operation(10)).Succeeded); AssertState(session, 11, 9, 20, 1, 1);
        Assert.True(session.Undo(Operation(11)).Succeeded); AssertState(session, 12, 8, 10, 0, 2);
        var before = session.Read();
        Assert.Equal(SceneAuthoringError.NoUndo, session.Undo(Operation(12)).Error); Assert.Same(before, session.Read());
        Assert.True(session.Redo(Operation(12)).Succeeded); AssertState(session, 13, 9, 20, 1, 1);
        Assert.True(session.Apply(Batch(13, 40)).Succeeded); AssertState(session, 14, 14, 40, 2, 0);
        before = session.Read();
        Assert.Equal(SceneAuthoringError.NoRedo, session.Redo(Operation(14)).Error); Assert.Same(before, session.Read());
        Assert.Same(thirdReceipt, session.Apply(third)); Assert.Same(before, session.Read());
        Assert.True(session.Undo(Operation(14)).Succeeded); AssertState(session, 15, 9, 20, 1, 1);
    }

    [Fact]
    public void History_NoOpApplyIsTransactionAndClearsRedoWhileZeroLimitDisablesHistory()
    {
        using var session = Session();
        Assert.True(session.Apply(Batch(7, 10)).Succeeded);
        Assert.True(session.Undo(Operation(8)).Succeeded); AssertState(session, 9, 7, 3, 0, 1);
        var noOp = new TransformEditBatch(Operation(9), [new(First, Transform, new(3, 0, 0))]);
        Assert.True(session.Apply(noOp).Succeeded); AssertState(session, 10, 10, 3, 1, 0);
        Assert.Equal(SceneAuthoringError.NoRedo, session.Redo(Operation(10)).Error);
        using var disabled = Session(options: new() { MaxHistoryEntries = 0 });
        Assert.True(disabled.Apply(Batch(7, 10)).Succeeded); AssertState(disabled, 8, 8, 10, 0, 0);
        var before = disabled.Read();
        Assert.Equal(SceneAuthoringError.NoUndo, disabled.Undo(Operation(8)).Error);
        Assert.Equal(SceneAuthoringError.NoRedo, disabled.Redo(Operation(8)).Error); Assert.Same(before, disabled.Read());
    }

    [Fact]
    public void History_ExactUndoRedoReceiptsAreStableAcrossLaterEditsAndCancelledRetries()
    {
        using var session = Session();
        Assert.True(session.Apply(Batch(7, 10)).Succeeded);
        var undo = Operation(8); var undoReceipt = session.Undo(undo);
        var redo = Operation(9); var redoReceipt = session.Redo(redo);
        Assert.True(undoReceipt.Succeeded); Assert.True(redoReceipt.Succeeded);
        Assert.True(session.Apply(Batch(10, 20)).Succeeded); var before = session.Read();
        // Undo and Redo both carry empty edit arrays; command identity must still distinguish their receipts.
        Assert.Equal(SceneAuthoringError.OperationIdConflict, session.Redo(undo).Error);
        Assert.Equal(SceneAuthoringError.OperationIdConflict, session.Undo(redo).Error);
        Assert.Same(before, session.Read());
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Same(undoReceipt, session.Undo(new(undo.Id, undo.ExpectedRevision, undo.Actor), cancelled.Token));
        Assert.Same(redoReceipt, session.Redo(new(redo.Id, redo.ExpectedRevision, redo.Actor), cancelled.Token));
        Assert.Same(before, session.Read()); AssertState(session, 11, 11, 20, 2, 0);
    }

    [Fact]
    public void Ledger_AtCapacityRejectsNewIdsWithoutEvictingSuccessOrErrorReceipts()
    {
        using var session = Session(options: new() { MaxOperations = 2 });
        var first = Batch(7, 10); var success = session.Apply(first);
        var stale = Batch(0, 20); var failure = session.Apply(stale);
        Assert.True(success.Succeeded); Assert.Equal(SceneAuthoringError.RevisionConflict, failure.Error);
        var before = session.Read(); var next = Batch(8, 30);
        Assert.Equal(SceneAuthoringError.OperationLimitReached, session.Apply(next).Error);
        Assert.Equal(SceneAuthoringError.OperationLimitReached, session.Undo(Operation(8)).Error);
        Assert.Same(before, session.Read());
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Same(success, session.Apply(first, cancelled.Token)); Assert.Same(failure, session.Apply(stale, cancelled.Token));
        Assert.Equal(SceneAuthoringError.OperationIdConflict, session.Apply(new(first.Operation, [Edit(First, 99)])).Error);
        // Rejected over-capacity IDs consume no receipt: a different payload still hits capacity, not an ID conflict.
        Assert.Equal(SceneAuthoringError.OperationLimitReached, session.Apply(new(next.Operation, [Edit(First, 31)])).Error);
        Assert.Same(before, session.Read()); AssertState(session, 8, 8, 10, 1, 0);
    }

    [Fact]
    public void Ledger_ExpectedEmptyHistoryErrorConsumesReceiptButCancellationDoesNot()
    {
        using var session = Session(options: new() { MaxOperations = 1 });
        var cancelledBatch = Batch(7, 5);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => session.Apply(cancelledBatch, cancelled.Token));
        var undo = Operation(7); var rejected = session.Undo(undo);
        Assert.Equal(SceneAuthoringError.NoUndo, rejected.Error);
        Assert.Equal(SceneAuthoringError.OperationLimitReached, session.Apply(cancelledBatch).Error);
        Assert.Same(rejected, session.Undo(undo, cancelled.Token)); AssertState(session, 7, 7, 3, 0, 0);
    }

    [Theory]
    [InlineData("apply")] [InlineData("undo")] [InlineData("redo")]
    public void Revision_AtLongMaximumRejectsWithoutWrappingOrChangingContent(string command)
    {
        using var session = Session(Document(long.MaxValue - 1));
        Assert.True(session.Apply(Batch(long.MaxValue - 1, 10)).Succeeded);
        var before = session.Read(); var operation = Operation(long.MaxValue);
        var result = command switch
        {
            "undo" => session.Undo(operation), "redo" => session.Redo(operation),
            _ => session.Apply(new(operation, [Edit(First, 20)]))
        };
        Assert.Equal(SceneAuthoringError.RevisionExhausted, result.Error);
        Assert.Equal(long.MaxValue, result.Revision); Assert.Same(before, session.Read());
        AssertState(session, long.MaxValue, long.MaxValue, 10, 1, 0);
    }

    private static void AssertState(ISceneAuthoringSession session, long revision, long content, double x, int undo, int redo)
    {
        var state = session.Read();
        Assert.Equal(revision, state.Document.Revision); Assert.Equal(revision, state.Resolved.Revision);
        Assert.Equal(content, state.ContentRevision); Assert.Equal(x, Number(state, First, "x"));
        Assert.Equal(undo, state.UndoCount); Assert.Equal(redo, state.RedoCount);
    }
}
