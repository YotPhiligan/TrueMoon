using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Documents;

public sealed class SceneDocumentSessionTests
{
    [Fact]
    public async Task NewSaveEditUndoRedoAndReload_UseContentCheckpointRatherThanMonotonicDocumentRevision()
    {
        using var files = new DocumentFiles(); var store = new DocumentStore(); var factory = new DocumentData.Sessions();
        await using var session = new SceneDocumentSession(files.Project, store, factory);
        var empty = session.Read(); Assert.Null(empty.Content); Assert.False(empty.IsDirty); Assert.False(empty.IsClosed);
        Assert.Equal(DocumentOperation.None, empty.Operation); Assert.Null(empty.Path); Assert.Null(empty.Stamp); Assert.Null(empty.SavedContentRevision);
        Assert.True(await session.NewAsync(DocumentData.Scene()));
        var created = session.Read(); Assert.True(created.IsDirty); Assert.Null(created.Path); Assert.Null(created.Stamp); Assert.Null(created.SavedContentRevision);
        DocumentData.AssertScene(created.Content!.Document);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SaveAsync()); Assert.Empty(store.Writes);
        var saved = await session.SaveAsAsync("room.aetherscene");
        Assert.Equal((files.ScenePath, 7L, 7L), (saved.Path, saved.Revision, saved.ContentRevision));
        Assert.Equal(saved.Stamp, session.Read().Stamp); Assert.Equal(7, session.Read().SavedContentRevision); Assert.False(session.Read().IsDirty);
        Assert.Null(Assert.Single(store.Writes).Expected); Assert.Same(created.Content.Document, store.Writes[0].Document);
        var edit = session.Apply(DocumentData.Edit(7, 20)); Assert.True(edit.Succeeded); Assert.True(session.Read().IsDirty);
        Assert.Equal(20d, DocumentData.X(session.Read().Content!)); Assert.Equal(7, session.Read().SavedContentRevision);
        Assert.True(session.Undo(DocumentData.Operation(8)).Succeeded);
        var undone = session.Read(); Assert.Equal(9, undone.Content!.Document.Revision); Assert.Equal(7, undone.Content.ContentRevision);
        Assert.Equal(4d, DocumentData.X(undone.Content)); Assert.False(undone.IsDirty);
        Assert.True(session.Redo(DocumentData.Operation(9)).Succeeded); Assert.True(session.Read().IsDirty);
        Assert.Equal(20d, DocumentData.X(session.Read().Content!));
        var second = await session.SaveAsync(); Assert.Equal((10L, 8L), (second.Revision, second.ContentRevision));
        Assert.Equal(saved.Stamp, store.Writes[1].Expected); Assert.Equal(8, session.Read().SavedContentRevision); Assert.False(session.Read().IsDirty);
        var retained = session.Read().Content!; Assert.True(await session.LoadAsync(files.ScenePath));
        Assert.Equal((7L, 7L, 0, 0), (session.Read().Content!.Document.Revision, session.Read().Content!.ContentRevision, session.Read().Content!.UndoCount, session.Read().Content!.RedoCount));
        Assert.False(session.Read().IsDirty); Assert.Equal(store.File.Stamp, session.Read().Stamp);
        Assert.Equal(1, factory.Products[0].Disposals); Assert.Equal(20d, DocumentData.X(retained));
        Assert.Equal(0, factory.Disposals); Assert.Equal(0, store.Disposals);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Save_CapturesImmutableSnapshotAllowsEditsAndDoesNotMarkNewerContentClean(bool saveAs)
    {
        using var files = new DocumentFiles(); var store = new DocumentStore(); var factory = new DocumentData.Sessions();
        await using var session = new SceneDocumentSession(files.Project, store, factory);
        Assert.True(await session.LoadAsync(files.ScenePath));
        Assert.True(session.Apply(DocumentData.Edit(7, 20)).Succeeded);
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal(); var committedStamp = new SceneFileStamp("captured-eight");
        store.Write = async (_, _, _, _) => { entered.SetResult(); await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return committedStamp; };
        var saving = saveAs ? session.SaveAsAsync("copy.aetherscene") : session.SaveAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.Equal(DocumentOperation.Saving, session.Read().Operation);
        Assert.True(session.Apply(DocumentData.Edit(8, 30)).Succeeded);
        Assert.Throws<InvalidOperationException>(() => { _ = session.SaveAsync(); });
        Assert.Throws<InvalidOperationException>(() => { _ = session.NewAsync(DocumentData.Empty()); });
        var write = Assert.Single(store.Writes); Assert.Equal(8, write.Document.Revision);
        Assert.Equal(20d, write.Document.Entities.Single(e => e.Id == DocumentData.Child).Changes.PropertyOverrides.Single(p => p.ComponentId == DocumentData.Transform && p.PropertyId == "x").Value.GetNumber());
        Assert.Equal(saveAs ? null : store.File.Stamp, write.Expected);
        finish.SetResult(); var receipt = await saving;
        Assert.Equal((8L, 8L), (receipt.Revision, receipt.ContentRevision)); Assert.Equal(committedStamp, receipt.Stamp);
        var after = session.Read(); Assert.Equal(9, after.Content!.Document.Revision); Assert.Equal(9, after.Content.ContentRevision);
        Assert.Equal(30d, DocumentData.X(after.Content)); Assert.Equal(8, after.SavedContentRevision); Assert.True(after.IsDirty);
        Assert.Equal(saveAs ? Path.Combine(files.Project.ScenesDirectory, "copy.aetherscene") : files.ScenePath, after.Path);
        Assert.Equal(committedStamp, after.Stamp); Assert.Equal(DocumentOperation.None, after.Operation);
        Assert.True(session.Undo(DocumentData.Operation(9)).Succeeded); Assert.False(session.Read().IsDirty);
        Assert.Equal(20d, DocumentData.X(session.Read().Content!));
        Assert.Equal(4d, store.File.Document.Entities[1].Changes.PropertyOverrides[0].Value.GetNumber());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FailedSaveOrSaveAs_PreservesOriginalPathStampAndCheckpointAndAllowsRetry(bool saveAs)
    {
        using var files = new DocumentFiles(); var store = new DocumentStore(); var factory = new DocumentData.Sessions();
        await using var session = new SceneDocumentSession(files.Project, store, factory); Assert.True(await session.LoadAsync(files.ScenePath));
        Assert.True(session.Apply(DocumentData.Edit(7, 20)).Succeeded); var before = session.Read(); var original = new IOException("disk unavailable");
        store.Write = (_, _, _, _) => Task.FromException<SceneFileStamp>(original);
        var failed = await Assert.ThrowsAsync<IOException>(() => saveAs ? session.SaveAsAsync("copy.aetherscene") : session.SaveAsync());
        Assert.Same(original, failed); var after = session.Read();
        Assert.Same(before.Content, after.Content); Assert.Equal(before.Path, after.Path); Assert.Equal(before.Stamp, after.Stamp);
        Assert.Equal(before.SavedContentRevision, after.SavedContentRevision); Assert.True(after.IsDirty); Assert.Same(original, after.Error);
        Assert.Equal(DocumentOperation.None, after.Operation); Assert.Equal(0, factory.Products[0].Disposals);
        store.Write = null; var saved = await session.SaveAsync(); Assert.False(session.Read().IsDirty); Assert.Null(session.Read().Error);
        Assert.Equal(before.Path, saved.Path); Assert.Equal(before.Stamp, store.Writes[1].Expected);
    }

    [Fact]
    public async Task Save_ResultAfterCallerCancellationIsAnActualCommitAndStillUpdatesCheckpoint()
    {
        using var files = new DocumentFiles(); var store = new DocumentStore();
        await using var session = new SceneDocumentSession(files.Project, store, DocumentData.Factory()); Assert.True(await session.NewAsync(DocumentData.Scene()));
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal(); var cancelled = DocumentData.Signal();
        store.Write = async (_, _, _, token) =>
        {
            using var registration = token.Register(() => cancelled.TrySetResult()); entered.SetResult();
            await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return new("actually-committed");
        };
        using var cancellation = new CancellationTokenSource(); var saving = session.SaveAsAsync("room.aetherscene", cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancellation.Cancel(); await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(saving.IsCompleted); finish.SetResult(); var saved = await saving;
        Assert.Equal(new SceneFileStamp("actually-committed"), saved.Stamp); Assert.Equal(files.ScenePath, saved.Path);
        Assert.False(session.Read().IsDirty); Assert.Equal(7, session.Read().SavedContentRevision); Assert.Null(session.Read().Error);
    }

    [Theory]
    [InlineData("read")] [InlineData("factory")] [InlineData("cancel")]
    public async Task FailedLoad_PreservesOriginalSessionAndImmutableSnapshotsAndAllowsLaterCandidate(string phase)
    {
        using var files = new DocumentFiles(); var store = new DocumentStore(); var factory = new DocumentData.Sessions();
        await using var session = new SceneDocumentSession(files.Project, store, factory); Assert.True(await session.LoadAsync(files.ScenePath));
        Assert.True(session.Apply(DocumentData.Edit(7, 12)).Succeeded); var before = session.Read();
        using var cancellation = new CancellationTokenSource(); var original = new IOException("bad candidate");
        if (phase == "read") store.Read = (_, _) => Task.FromException<SceneFile>(original);
        else if (phase == "factory") factory.CreateSession = _ => throw original;
        else store.Read = (_, _) => { cancellation.Cancel(); return Task.FromResult(store.File); };
        if (phase == "cancel") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.LoadAsync("other.aetherscene", DocumentCloseChoice.Discard, cancellationToken: cancellation.Token));
        else Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => session.LoadAsync("other.aetherscene", DocumentCloseChoice.Discard)));
        var after = session.Read(); Assert.Same(before.Content, after.Content); Assert.Equal(before.Path, after.Path); Assert.Equal(before.Stamp, after.Stamp);
        Assert.Equal(before.SavedContentRevision, after.SavedContentRevision); Assert.True(after.IsDirty); Assert.Equal(12d, DocumentData.X(after.Content!));
        Assert.Single(factory.Products); Assert.Equal(0, factory.Products[0].Disposals);
        store.Read = null; factory.CreateSession = null;
        Assert.True(await session.LoadAsync("other.aetherscene", DocumentCloseChoice.Discard));
        Assert.False(session.Read().IsDirty); Assert.Null(session.Read().Error); Assert.Equal(4d, DocumentData.X(session.Read().Content!));
        Assert.Equal(1, factory.Products[0].Disposals); Assert.Equal(12d, DocumentData.X(before.Content!));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CandidateLoad_BlocksEditingAndOtherFileOperationsUntilComplete(bool newDocument)
    {
        using var files = new DocumentFiles(); var store = new DocumentStore(); var factory = new DocumentData.Sessions();
        await using var session = new SceneDocumentSession(files.Project, store, factory); Assert.True(await session.LoadAsync(files.ScenePath));
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal();
        if (newDocument) factory.CreateSession = document =>
        {
            entered.SetResult(); finish.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); return DocumentData.Factory().Create(document);
        };
        else store.Read = async (_, _) => { entered.SetResult(); await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return store.File; };
        var loading = newDocument ? session.NewAsync(DocumentData.Empty()) : session.LoadAsync("other.aetherscene");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.Equal(DocumentOperation.Loading, session.Read().Operation);
        Assert.Throws<InvalidOperationException>(() => session.Apply(DocumentData.Edit(7, 99)));
        Assert.Throws<InvalidOperationException>(() => session.Undo(DocumentData.Operation(7)));
        Assert.Throws<InvalidOperationException>(() => session.Redo(DocumentData.Operation(7)));
        Assert.Throws<InvalidOperationException>(() => { _ = session.LoadAsync(files.ScenePath); });
        Assert.Throws<InvalidOperationException>(() => { _ = session.SaveAsAsync("copy.aetherscene"); });
        Assert.Equal(4d, DocumentData.X(session.Read().Content!)); Assert.Empty(store.Writes);
        finish.SetResult(); Assert.True(await loading); Assert.Equal(DocumentOperation.None, session.Read().Operation);
        Assert.Equal(newDocument, session.Read().IsDirty); Assert.Equal(1, factory.Products[0].Disposals);
    }

    [Theory]
    [InlineData("new")] [InlineData("load")] [InlineData("close")]
    public async Task CancelChoice_PreservesDirtyDocumentAndDoesNotWriteOrDisposeItsProduct(string transition)
    {
        using var files = new DocumentFiles(); var store = new DocumentStore(); var factory = new DocumentData.Sessions();
        await using var session = new SceneDocumentSession(files.Project, store, factory); Assert.True(await session.NewAsync(DocumentData.Scene()));
        var before = session.Read();
        var allowed = transition switch { "new" => await session.NewAsync(DocumentData.Empty()), "load" => await session.LoadAsync(files.ScenePath), _ => await session.CloseAsync() };
        Assert.False(allowed); Assert.Same(before.Content, session.Read().Content); Assert.True(session.Read().IsDirty); Assert.Null(session.Read().Path);
        Assert.Equal(0, factory.Products[0].Disposals); Assert.Empty(store.Writes); Assert.False(session.Read().IsClosed);
        Assert.All(factory.Products.Skip(1), p => Assert.Equal(1, p.Disposals));
        Assert.True(session.Apply(DocumentData.Edit(7, 15)).Succeeded); Assert.Equal(15d, DocumentData.X(session.Read().Content!));
    }

    [Theory]
    [InlineData("new")] [InlineData("load")] [InlineData("close")]
    public async Task SaveChoice_PersistsUntitledOriginalBeforeReplacingOrClosingAndDisposeNeverOwnsDependencies(string transition)
    {
        using var files = new DocumentFiles(); var store = new DocumentStore(); var factory = new DocumentData.Sessions();
        var session = new SceneDocumentSession(files.Project, store, factory); Assert.True(await session.NewAsync(DocumentData.Scene()));
        Assert.True(session.Apply(DocumentData.Edit(7, 18)).Succeeded); var retained = session.Read().Content!;
        var allowed = transition switch
        {
            "new" => await session.NewAsync(DocumentData.Empty(), DocumentCloseChoice.Save, "saved.aetherscene"),
            "load" => await session.LoadAsync(files.ScenePath, DocumentCloseChoice.Save, "saved.aetherscene"),
            _ => await session.CloseAsync(DocumentCloseChoice.Save, "saved.aetherscene")
        };
        Assert.True(allowed); var write = Assert.Single(store.Writes); Assert.Equal(Path.Combine(files.Project.ScenesDirectory, "saved.aetherscene"), write.Path);
        Assert.Same(retained.Document, write.Document); Assert.Null(write.Expected); Assert.Equal(1, factory.Products[0].Disposals);
        Assert.Equal(18d, DocumentData.X(retained)); Assert.Equal(0, store.Disposals); Assert.Equal(0, factory.Disposals);
        if (transition == "close") { Assert.Null(session.Read().Content); Assert.Null(session.Read().Path); Assert.False(session.Read().IsDirty); }
        else Assert.Equal(transition == "new", session.Read().IsDirty);
        await session.DisposeAsync(); await session.DisposeAsync();
        Assert.True(session.Read().IsClosed); Assert.Null(session.Read().Content); Assert.All(factory.Products, p => Assert.Equal(1, p.Disposals));
        Assert.Equal(0, store.Disposals); Assert.Equal(0, factory.Disposals);
    }

    [Theory]
    [InlineData("new")] [InlineData("load")] [InlineData("close")]
    public async Task SaveChoice_FailedPersistenceVetoesTransitionAndRetainsRecoverableDirtyDocument(string transition)
    {
        using var files = new DocumentFiles(); var store = new DocumentStore(); var factory = new DocumentData.Sessions();
        await using var session = new SceneDocumentSession(files.Project, store, factory); Assert.True(await session.NewAsync(DocumentData.Scene()));
        var before = session.Read(); var original = new IOException("cannot save"); store.Write = (_, _, _, _) => Task.FromException<SceneFileStamp>(original);
        Task<bool> Action() => transition switch
        {
            "new" => session.NewAsync(DocumentData.Empty(), DocumentCloseChoice.Save, "save.aetherscene"),
            "load" => session.LoadAsync(files.ScenePath, DocumentCloseChoice.Save, "save.aetherscene"),
            _ => session.CloseAsync(DocumentCloseChoice.Save, "save.aetherscene")
        };
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(Action));
        Assert.Same(before.Content, session.Read().Content); Assert.Null(session.Read().Path); Assert.True(session.Read().IsDirty);
        Assert.Same(original, session.Read().Error); Assert.Equal(0, factory.Products[0].Disposals);
        Assert.All(factory.Products.Skip(1), p => Assert.Equal(1, p.Disposals));
        store.Write = null; Assert.True(await session.CloseAsync(DocumentCloseChoice.Discard)); Assert.Null(session.Read().Content);
    }

    [Fact]
    public async Task CloseDiscard_CancelsAndDrainsIgnoredLoadBeforeDisposingActiveAndNeverSelectsLateCandidate()
    {
        using var files = new DocumentFiles(); var store = new DocumentStore(); var factory = new DocumentData.Sessions();
        await using var session = new SceneDocumentSession(files.Project, store, factory); Assert.True(await session.LoadAsync(files.ScenePath));
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal(); var cancelled = DocumentData.Signal();
        store.Read = async (_, token) =>
        {
            using var registration = token.Register(() => cancelled.TrySetResult()); entered.SetResult();
            await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return store.File;
        };
        var loading = session.LoadAsync("other.aetherscene"); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var closing = session.CloseAsync(DocumentCloseChoice.Discard); await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(closing, session.CloseAsync(DocumentCloseChoice.Discard)); Assert.False(closing.IsCompleted);
        Assert.Equal(DocumentOperation.Closing, session.Read().Operation); Assert.Equal(0, factory.Products[0].Disposals);
        Assert.Throws<InvalidOperationException>(() => session.Apply(DocumentData.Edit(7, 8)));
        Assert.Throws<InvalidOperationException>(() => { _ = session.NewAsync(DocumentData.Empty()); });
        finish.SetResult(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loading); Assert.True(await closing);
        Assert.Single(factory.Products); Assert.Equal(1, factory.Products[0].Disposals); Assert.Null(session.Read().Content); Assert.Null(session.Read().Path);
        Assert.False(session.Read().IsClosed); Assert.Null(session.Read().Error);
        Assert.True(await session.NewAsync(DocumentData.Empty())); Assert.True(session.Read().IsDirty);
    }

    [Theory]
    [InlineData("new")] [InlineData("load")] [InlineData("close")]
    public async Task DiscardChoice_ReleasesUnsavedOriginalWithoutWritingAndAllowsAReusableOwner(string transition)
    {
        using var files = new DocumentFiles(); var store = new DocumentStore(); var factory = new DocumentData.Sessions();
        await using var session = new SceneDocumentSession(files.Project, store, factory); Assert.True(await session.NewAsync(DocumentData.Scene()));
        Assert.True(session.Apply(DocumentData.Edit(7, 25)).Succeeded); var old = session.Read().Content!;
        var allowed = transition switch
        {
            "new" => await session.NewAsync(DocumentData.Empty(15), DocumentCloseChoice.Discard),
            "load" => await session.LoadAsync(files.ScenePath, DocumentCloseChoice.Discard),
            _ => await session.CloseAsync(DocumentCloseChoice.Discard)
        };
        Assert.True(allowed); Assert.Empty(store.Writes); Assert.Equal(1, factory.Products[0].Disposals);
        Assert.Equal(25d, DocumentData.X(old)); Assert.False(session.Read().IsClosed);
        if (transition == "close")
        {
            Assert.Null(session.Read().Content); Assert.False(session.Read().IsDirty);
            Assert.True(await session.NewAsync(DocumentData.Empty())); Assert.True(session.Read().IsDirty);
        }
        else
        {
            Assert.Equal(transition == "new" ? 15 : 7, session.Read().Content!.Document.Revision);
            Assert.Equal(transition == "new", session.Read().IsDirty);
            Assert.Equal((0, 0), (session.Read().Content!.UndoCount, session.Read().Content!.RedoCount));
        }
    }

    [Fact]
    public async Task Close_CallerCancellationDuringLoadDrainPreservesOriginalAndStillWaitsForOwnedWork()
    {
        using var files = new DocumentFiles(); var store = new DocumentStore(); var factory = new DocumentData.Sessions();
        await using var session = new SceneDocumentSession(files.Project, store, factory); Assert.True(await session.LoadAsync(files.ScenePath));
        var before = session.Read(); var entered = DocumentData.Signal(); var finish = DocumentData.Signal(); var cancelled = DocumentData.Signal();
        store.Read = async (_, token) =>
        {
            using var registration = token.Register(() => cancelled.TrySetResult()); entered.SetResult();
            await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return store.File;
        };
        var loading = session.LoadAsync("late.aetherscene"); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource(); var closing = session.CloseAsync(DocumentCloseChoice.Discard, cancellationToken: cancellation.Token);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancellation.Cancel(); Assert.False(closing.IsCompleted);
        Assert.Equal(0, factory.Products[0].Disposals); finish.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loading); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => closing);
        Assert.Same(before.Content, session.Read().Content); Assert.Equal(before.Path, session.Read().Path); Assert.Equal(0, factory.Products[0].Disposals);
        Assert.True(await session.CloseAsync(DocumentCloseChoice.Discard)); Assert.Equal(1, factory.Products[0].Disposals);
    }

    [Fact]
    public async Task New_FailedPreviousCleanupRetainsBothProductsAndBlocksFurtherWorkUntilCloseRetry()
    {
        using var files = new DocumentFiles(); var factory = new DocumentData.Sessions();
        await using var session = new SceneDocumentSession(files.Project, new DocumentStore(), factory); Assert.True(await session.NewAsync(DocumentData.Scene()));
        var first = factory.Products[0]; var failed = new IOException("old product retained"); var attempts = 0;
        first.BeforeDispose = () => { if (++attempts == 1) throw failed; };
        Assert.Same(failed, await Assert.ThrowsAsync<IOException>(() => session.NewAsync(DocumentData.Empty(), DocumentCloseChoice.Discard)));
        Assert.Equal(2, factory.Products.Count); Assert.Equal(1, first.Disposals); Assert.Equal(0, factory.Products[1].Disposals);
        Assert.Empty(session.Read().Content!.Document.Entities); Assert.True(session.Read().IsDirty); Assert.Same(failed, session.Read().Error);
        Assert.Throws<InvalidOperationException>(() => { _ = session.NewAsync(DocumentData.Scene(), DocumentCloseChoice.Discard); });
        Assert.Throws<InvalidOperationException>(() => session.Apply(DocumentData.Edit(0, 3)));
        Assert.True(await session.CloseAsync(DocumentCloseChoice.Discard)); Assert.Equal(2, first.Disposals); Assert.Equal(1, factory.Products[1].Disposals);
        Assert.Null(session.Read().Content); Assert.Null(session.Read().Error);
    }

    [Fact]
    public async Task InvalidChoiceAndPreCancelledOperation_DoNotAcquireAdmissionOrChangeActiveContent()
    {
        using var files = new DocumentFiles(); var store = new DocumentStore(); var factory = new DocumentData.Sessions();
        await using var session = new SceneDocumentSession(files.Project, store, factory); Assert.True(await session.LoadAsync(files.ScenePath)); var before = session.Read();
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = session.NewAsync(DocumentData.Empty(), (DocumentCloseChoice)99); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = session.LoadAsync(files.ScenePath, (DocumentCloseChoice)99); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = session.CloseAsync((DocumentCloseChoice)99); });
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => { _ = session.NewAsync(DocumentData.Empty(), cancellationToken: cancellation.Token); });
        Assert.ThrowsAny<OperationCanceledException>(() => { _ = session.SaveAsync(cancellation.Token); });
        Assert.ThrowsAny<OperationCanceledException>(() => { _ = session.CloseAsync(DocumentCloseChoice.Discard, cancellationToken: cancellation.Token); });
        Assert.Single(store.Reads); Assert.Empty(store.Writes); Assert.Single(factory.Products);
        Assert.Same(before.Content, session.Read().Content); Assert.Null(session.Read().Error); Assert.Equal(DocumentOperation.None, session.Read().Operation);
    }

    [Fact]
    public async Task Dispose_FencesAndDrainsIgnoredSaveWithoutLateCheckpointOrDoubleRelease()
    {
        using var files = new DocumentFiles(); var store = new DocumentStore(); var factory = new DocumentData.Sessions();
        var session = new SceneDocumentSession(files.Project, store, factory); Assert.True(await session.NewAsync(DocumentData.Scene()));
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal(); var cancelled = DocumentData.Signal();
        store.Write = async (_, _, _, token) =>
        {
            using var registration = token.Register(() => cancelled.TrySetResult()); entered.SetResult();
            await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return new("late-commit");
        };
        var saving = session.SaveAsAsync("room.aetherscene"); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var disposal = session.DisposeAsync().AsTask(); await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(session.Read().IsClosed); Assert.False(disposal.IsCompleted); Assert.Equal(0, factory.Products[0].Disposals);
        Assert.Throws<ObjectDisposedException>(() => { _ = session.NewAsync(DocumentData.Empty()); });
        Assert.Throws<ObjectDisposedException>(() => { _ = session.SaveAsync(); });
        finish.SetResult(); var receipt = await saving; Assert.Equal(new SceneFileStamp("late-commit"), receipt.Stamp); await disposal;
        var after = session.Read(); Assert.Null(after.Content); Assert.Null(after.Path); Assert.Null(after.Stamp); Assert.Null(after.SavedContentRevision);
        Assert.False(after.IsDirty); Assert.Equal(DocumentOperation.None, after.Operation);
        await session.DisposeAsync(); Assert.Equal(1, factory.Products[0].Disposals); Assert.Equal(0, store.Disposals); Assert.Equal(0, factory.Disposals);
    }

    [Fact]
    public async Task CloseSave_WaitsForExistingSaveAllowsItsCommitAndDoesNotWriteTwice()
    {
        using var files = new DocumentFiles(); var store = new DocumentStore(); var factory = new DocumentData.Sessions();
        await using var session = new SceneDocumentSession(files.Project, store, factory); Assert.True(await session.NewAsync(DocumentData.Scene()));
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal(); var cancellationObserved = false;
        store.Write = async (_, _, _, token) =>
        {
            using var registration = token.Register(() => cancellationObserved = true); entered.SetResult();
            await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return new("committed");
        };
        var saving = session.SaveAsAsync("room.aetherscene"); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var closing = session.CloseAsync(DocumentCloseChoice.Save); Assert.False(closing.IsCompleted); Assert.False(cancellationObserved);
        finish.SetResult(); await saving; Assert.True(await closing);
        Assert.Single(store.Writes); Assert.Null(session.Read().Content); Assert.Equal(1, factory.Products[0].Disposals);
    }

    [Fact]
    public async Task CloseSave_AfterEditDuringPendingSavePersistsTheNewerSnapshotBeforeReleasingDocument()
    {
        using var files = new DocumentFiles(); var store = new DocumentStore(); var factory = new DocumentData.Sessions();
        await using var session = new SceneDocumentSession(files.Project, store, factory); Assert.True(await session.NewAsync(DocumentData.Scene()));
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal(); var firstStamp = new SceneFileStamp("first-seven");
        store.Write = async (_, _, _, _) =>
        {
            if (store.Writes.Count == 1) { entered.SetResult(); await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return firstStamp; }
            Assert.Equal(0, factory.Products[0].Disposals); return new("newer-eight");
        };
        var saving = session.SaveAsAsync("room.aetherscene"); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(session.Apply(DocumentData.Edit(7, 44)).Succeeded);
        var closing = session.CloseAsync(DocumentCloseChoice.Save); Assert.False(closing.IsCompleted);
        finish.SetResult(); var first = await saving; Assert.Equal((7L, 7L), (first.Revision, first.ContentRevision)); Assert.True(await closing);
        Assert.Equal(2, store.Writes.Count); Assert.Null(store.Writes[0].Expected); Assert.Equal(firstStamp, store.Writes[1].Expected);
        Assert.Equal((7L, 8L), (store.Writes[0].Document.Revision, store.Writes[1].Document.Revision));
        Assert.Equal(44d, store.Writes[1].Document.Entities[1].Changes.PropertyOverrides.Single(p => p.ComponentId == DocumentData.Transform && p.PropertyId == "x").Value.GetNumber());
        Assert.Null(session.Read().Content); Assert.Equal(1, factory.Products[0].Disposals); Assert.Null(session.Read().Error);
    }

    [Theory]
    [InlineData("../outside.aetherscene")] [InlineData("room.json")] [InlineData("nested/../../outside.aetherscene")]
    [InlineData("../Scenes-other/room.aetherscene")]
    public async Task Paths_RejectTraversalWrongExtensionAndSiblingPrefixWithoutFileOrFactoryActivation(string path)
    {
        using var files = new DocumentFiles(); var store = new DocumentStore(); var factory = new DocumentData.Sessions();
        await using var session = new SceneDocumentSession(files.Project, store, factory);
        Assert.Throws<ArgumentException>(() => { _ = session.LoadAsync(path); }); Assert.Throws<ArgumentException>(() => { _ = session.SaveAsAsync(path); });
        Assert.Empty(store.Reads); Assert.Empty(store.Writes); Assert.Empty(factory.Products); Assert.Null(session.Read().Content);
        Assert.True(await session.LoadAsync("folder/../room.aetherscene")); Assert.Equal(files.ScenePath, Assert.Single(store.Reads));
        Assert.Equal(files.ScenePath, session.Read().Path);
    }

    [Fact]
    public async Task Close_CleanupFailureRetainsOwnershipForExplicitRetryWithoutDoubleReleaseAfterSuccess()
    {
        using var files = new DocumentFiles(); var factory = new DocumentData.Sessions(); var store = new DocumentStore();
        var session = new SceneDocumentSession(files.Project, store, factory); Assert.True(await session.NewAsync(DocumentData.Scene()));
        var failure = new IOException("cleanup failed"); var product = Assert.Single(factory.Products); var attempts = 0;
        product.BeforeDispose = () => { if (++attempts == 1) throw failure; };
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => session.CloseAsync(DocumentCloseChoice.Discard)));
        Assert.Same(failure, session.Read().Error); Assert.True(session.Read().IsDirty); Assert.Equal(1, product.Disposals);
        Assert.True(await session.CloseAsync(DocumentCloseChoice.Discard)); Assert.Equal(2, product.Disposals); Assert.Null(session.Read().Content);
        await session.DisposeAsync(); Assert.Equal(2, product.Disposals); Assert.Equal(0, factory.Disposals);
    }
}
