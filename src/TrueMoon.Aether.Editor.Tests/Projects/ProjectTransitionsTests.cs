using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Scenes.Authoring;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Projects;

public sealed class ProjectTransitionsTests
{
    [Fact]
    public async Task SwitchDiscard_CommitsPreparedCandidateAndDisposesOnlyPreviousOwnedDocument()
    {
        await using var h = await Harness.CreateAsync(); h.Edit(17);
        var old = h.Projects.Active!; var oldDocument = h.State.Document;
        var selected = await h.SwitchAsync(DocumentCloseChoice.Discard);
        Assert.Same(selected, h.Projects.Active); Assert.Equal(h.Next.Resolved, selected.Project);
        Assert.True(old.IsClosed); Assert.True(old.Documents!.Read().IsClosed);
        Assert.Equal(1, Assert.Single(h.Authoring.Products).Disposals);
        Assert.Equal(17, oldDocument.Entities.Single(e => e.Id == DocumentData.Child).Changes.PropertyOverrides
            .Single(p => p.PropertyId == "x").Value.GetNumber());
        Assert.Null(selected.Documents!.Read().Content); Assert.False(selected.IsClosed);
        Assert.Empty(h.Documents.Writes); Assert.Equal(0, h.Authoring.Disposals);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SwitchSave_PersistsExactPreviousSnapshotInItsOwnRootBeforePublishing(bool alreadySaved)
    {
        await using var h = await Harness.CreateAsync();
        if (alreadySaved) await h.Current.SaveAsAsync("original.aetherscene");
        h.Edit(19); var expected = h.State.Document; h.Documents.Writes.Clear();
        var old = h.Projects.Active!;
        var selected = await h.SwitchAsync(DocumentCloseChoice.Save, "untitled.aetherscene");
        var write = Assert.Single(h.Documents.Writes);
        Assert.Same(expected, write.Document);
        Assert.Equal(Path.Combine(h.First.Resolved.ScenesDirectory, alreadySaved ? "original.aetherscene" : "untitled.aetherscene"), write.Path);
        Assert.Equal(alreadySaved ? "written-1" : null, write.Expected?.Sha256);
        Assert.True(old.IsClosed); Assert.Same(selected, h.Projects.Active);
    }

    [Theory]
    [InlineData(DocumentCloseChoice.Save)] [InlineData(DocumentCloseChoice.Discard)]
    public async Task InvalidCandidate_PreservesDirtyDocumentHistoryAndDoesNotAttemptSave(DocumentCloseChoice choice)
    {
        await using var h = await Harness.CreateAsync(); h.Edit(23);
        var old = h.Projects.Active!; var state = h.State; var error = new InvalidDataException("SDK is unavailable");
        h.Toolchain.Probe = (_, _) => Task.FromException<ProjectToolchainInfo>(error);
        Assert.Same(error, await Assert.ThrowsAsync<InvalidDataException>(() => h.SwitchAsync(choice)));
        Assert.Same(old, h.Projects.Active); Assert.Same(state.Document, h.State.Document);
        Assert.True(h.State.UndoCount > 0); Assert.True(h.Current.Read().IsDirty); Assert.False(old.IsClosed);
        Assert.Empty(h.Documents.Writes); Assert.Single(h.Sessions.Products);
        h.Current.Undo(DocumentData.Operation(h.State.Document.Revision)); Assert.Equal(4, DocumentData.X(h.State));
    }

    [Fact]
    public async Task SwitchCancelAndFailedSave_PreserveOwnerAndHistoryAndDisposeRejectedCandidates()
    {
        await using var h = await Harness.CreateAsync(); h.Edit(29);
        var old = h.Projects.Active!; var state = h.State;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.SwitchAsync(DocumentCloseChoice.Cancel));
        Assert.Same(old, h.Projects.Active); Assert.Same(state.Document, h.State.Document);
        Assert.True(h.Sessions.Products[1].IsClosed); Assert.Equal(0, h.Authoring.Products[0].Disposals);
        var error = new IOException("Save denied");
        h.Documents.Write = (_, _, _, _) => Task.FromException<TrueMoon.Aether.Scenes.SceneFileStamp>(error);
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => h.SwitchAsync(DocumentCloseChoice.Save, "new.aetherscene")));
        Assert.Same(old, h.Projects.Active); Assert.Same(state.Document, h.State.Document);
        Assert.True(h.State.UndoCount > 0); Assert.True(h.Current.Read().IsDirty); Assert.False(old.IsClosed);
        Assert.True(h.Sessions.Products[2].IsClosed); Assert.Null(h.Current.Read().Path);
    }

    [Fact]
    public async Task CancelAfterSaveCommit_RetainsOldProjectAndTruthfulSavedCheckpointWithUndo()
    {
        await using var h = await Harness.CreateAsync(); h.Edit(31);
        using var cancel = new CancellationTokenSource(); var old = h.Projects.Active!; var expected = h.State.Document;
        h.Documents.Write = (_, _, _, _) => { cancel.Cancel(); return Task.FromResult(new TrueMoon.Aether.Scenes.SceneFileStamp("committed")); };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.SwitchAsync(DocumentCloseChoice.Save, "saved.aetherscene", cancel.Token));
        Assert.Same(old, h.Projects.Active); Assert.False(old.IsClosed); Assert.Same(expected, h.State.Document);
        Assert.False(h.Current.Read().IsDirty); Assert.Equal("committed", h.Current.Read().Stamp!.Sha256);
        Assert.True(h.State.UndoCount > 0); Assert.True(h.Sessions.Products[1].IsClosed);
        h.Current.Undo(DocumentData.Operation(h.State.Document.Revision)); Assert.Equal(4, DocumentData.X(h.State));
        Assert.True(h.Current.Read().IsDirty);
    }

    [Theory]
    [InlineData(DocumentCloseChoice.Save)] [InlineData(DocumentCloseChoice.Discard)]
    public async Task LaterEditDuringPreflight_VetoesStaleDecisionWithoutLosingNewHistory(DocumentCloseChoice choice)
    {
        await using var h = await Harness.CreateAsync(); var old = h.Projects.Active!;
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal();
        h.Toolchain.Probe = async (_, token) => { entered.SetResult(); await finish.Task.WaitAsync(token); return new("10.0.403"); };
        var switching = h.SwitchAsync(choice, "saved.aetherscene"); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        h.Edit(37); var expected = h.State.Document;
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Projects.OpenAsync(h.Next.Manifest));
        finish.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => switching);
        Assert.Same(old, h.Projects.Active); Assert.Same(expected, h.State.Document);
        Assert.True(h.State.UndoCount > 0); Assert.Equal(37, DocumentData.X(h.State)); Assert.Empty(h.Documents.Writes);
        Assert.True(h.Sessions.Products[1].IsClosed);
    }

    [Fact]
    public async Task EditDuringCapturedSave_VetoesPublicationAndPreservesPersistedCheckpoint()
    {
        await using var h = await Harness.CreateAsync(); h.Edit(41); var saved = h.State.Document;
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal();
        h.Documents.Write = async (_, _, _, token) => { entered.SetResult(); await finish.Task.WaitAsync(token); return new("older-revision"); };
        var switching = h.SwitchAsync(DocumentCloseChoice.Save, "saved.aetherscene");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); h.Edit(43); var latest = h.State.Document;
        finish.SetResult(); await Assert.ThrowsAsync<InvalidOperationException>(() => switching);
        Assert.Same(latest, h.State.Document); Assert.Same(saved, Assert.Single(h.Documents.Writes).Document);
        Assert.Equal(43, DocumentData.X(h.State)); Assert.True(h.Current.Read().IsDirty);
        Assert.Equal("older-revision", h.Current.Read().Stamp!.Sha256); Assert.False(h.Projects.Active!.IsClosed);
        h.Current.Undo(DocumentData.Operation(h.State.Document.Revision)); Assert.Equal(41, DocumentData.X(h.State));
        Assert.False(h.Current.Read().IsDirty);
    }

    [Fact]
    public async Task CloseDrainsUncooperativeSwitch_RejectsLatePublicationAndThenDiscardsOldDocument()
    {
        await using var h = await Harness.CreateAsync(); var old = h.Projects.Active!;
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal();
        h.Toolchain.Probe = async (_, _) => { entered.SetResult(); await finish.Task; return new("10.0.403"); };
        var switching = h.SwitchAsync(DocumentCloseChoice.Discard); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var closing = h.Projects.CloseAsync(DocumentCloseChoice.Discard);
        Assert.False(closing.IsCompleted); Assert.Same(old, h.Projects.Active); Assert.False(old.IsClosed);
        finish.SetResult(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => switching); await closing;
        Assert.Null(h.Projects.Active); Assert.True(old.IsClosed); Assert.Single(h.Sessions.Products);
        Assert.Equal(1, h.Authoring.Products[0].Disposals);
    }

    [Theory]
    [InlineData(DocumentCloseChoice.Save)] [InlineData(DocumentCloseChoice.Discard)] [InlineData(DocumentCloseChoice.Cancel)]
    public async Task Close_RespectsExplicitDirtyPolicyAndCanReopen(DocumentCloseChoice choice)
    {
        await using var h = await Harness.CreateAsync(); h.Edit(47);
        var old = h.Projects.Active!; var expected = h.State.Document;
        if (choice == DocumentCloseChoice.Cancel)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Projects.CloseAsync(choice));
            Assert.Same(old, h.Projects.Active); Assert.Same(expected, h.State.Document); Assert.True(h.State.UndoCount > 0);
            Assert.False(old.IsClosed); Assert.Empty(h.Documents.Writes);
            await h.Projects.CloseAsync(DocumentCloseChoice.Discard);
        }
        else
        {
            await h.Projects.CloseAsync(choice, "closed.aetherscene");
            if (choice == DocumentCloseChoice.Save) Assert.Same(expected, Assert.Single(h.Documents.Writes).Document);
            else Assert.Empty(h.Documents.Writes);
        }
        Assert.Null(h.Projects.Active); Assert.True(old.IsClosed); Assert.False(h.Projects.IsClosed);
        Assert.Equal(1, h.Authoring.Products[0].Disposals);
        var reopened = await h.Projects.OpenAsync(h.First.Manifest); Assert.False(reopened.IsClosed);
        Assert.Null(reopened.Documents!.Read().Content);
    }

    [Fact]
    public async Task FailedCloseSave_RetainsEditableDocumentAndSupportsRetry()
    {
        await using var h = await Harness.CreateAsync(); h.Edit(53); var old = h.Projects.Active!;
        h.Documents.Write = (_, _, _, _) => Task.FromException<TrueMoon.Aether.Scenes.SceneFileStamp>(new IOException("Disk full"));
        await Assert.ThrowsAsync<IOException>(() => h.Projects.CloseAsync(DocumentCloseChoice.Save, "retry.aetherscene"));
        Assert.Same(old, h.Projects.Active); Assert.False(old.IsClosed); Assert.True(h.State.UndoCount > 0);
        h.Edit(59); Assert.Equal(59, DocumentData.X(h.State)); var expected = h.State.Document;
        h.Documents.Write = null;
        await h.Projects.CloseAsync(DocumentCloseChoice.Save, "retry.aetherscene");
        Assert.Null(h.Projects.Active); Assert.Same(expected, h.Documents.Writes.Last().Document); Assert.True(old.IsClosed);
    }

    [Fact]
    public async Task InvalidChoiceAndPrecancelledOperations_DoNotStartPreparationOrCloseOwner()
    {
        await using var h = await Harness.CreateAsync(); var reads = h.Store.ReadCalls; var old = h.Projects.Active!;
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => h.SwitchAsync((DocumentCloseChoice)99));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => h.Projects.CloseAsync((DocumentCloseChoice)99));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.SwitchAsync(DocumentCloseChoice.Discard, token: cancel.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Projects.CloseAsync(DocumentCloseChoice.Discard, cancellationToken: cancel.Token));
        Assert.Equal(reads, h.Store.ReadCalls); Assert.Same(old, h.Projects.Active); Assert.False(old.IsClosed);
    }

    [Fact]
    public async Task CallerCancellationDuringSdkProbe_PreservesOldDocumentAndDoesNotCreateCandidate()
    {
        await using var h = await Harness.CreateAsync(); h.Edit(61); var expected = h.State.Document;
        using var cancel = new CancellationTokenSource(); var entered = DocumentData.Signal();
        h.Toolchain.Probe = async (_, token) => { entered.SetResult(); await Task.Delay(Timeout.Infinite, token); return new("10.0.403"); };
        var switching = h.SwitchAsync(DocumentCloseChoice.Discard, token: cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => switching);
        Assert.Same(expected, h.State.Document); Assert.Equal(61, DocumentData.X(h.State));
        Assert.Single(h.Sessions.Products); Assert.False(h.Projects.Active!.IsClosed); Assert.True(h.State.UndoCount > 0);
    }

    [Fact]
    public async Task SameIdAndRevisionReplacementDuringPreflight_VetoesDiscardOfUnseenDocument()
    {
        await using var h = await Harness.CreateAsync(); var old = h.State.Document;
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal();
        h.Toolchain.Probe = async (_, token) => { entered.SetResult(); await finish.Task.WaitAsync(token); return new("10.0.403"); };
        var switching = h.SwitchAsync(DocumentCloseChoice.Discard);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await h.Current.NewAsync(DocumentData.Scene(), DocumentCloseChoice.Discard); var replacement = h.State.Document;
        Assert.Equal(old.Id, replacement.Id); Assert.Equal(old.Revision, replacement.Revision); Assert.NotSame(old, replacement);
        finish.SetResult(); await Assert.ThrowsAsync<InvalidOperationException>(() => switching);
        Assert.Same(replacement, h.State.Document); Assert.True(h.Current.Read().IsDirty);
        Assert.Equal(1, h.Authoring.Products[0].Disposals); Assert.Equal(0, h.Authoring.Products[1].Disposals);
    }

    [Fact]
    public async Task PendingDocumentIo_VetoesDiscardWithoutCancellingSaveAndAllowsRetryAfterDrain()
    {
        await using var h = await Harness.CreateAsync(); var expected = h.State.Document;
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal();
        h.Documents.Write = async (_, _, _, token) => { entered.SetResult(); await finish.Task.WaitAsync(token); return new("saved"); };
        var saving = h.Current.SaveAsAsync("busy.aetherscene"); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.SwitchAsync(DocumentCloseChoice.Discard));
        Assert.False(saving.IsCompleted); Assert.Same(expected, h.State.Document); Assert.False(h.Projects.Active!.IsClosed);
        finish.SetResult(); await saving; Assert.False(h.Current.Read().IsDirty);
        var selected = await h.SwitchAsync(DocumentCloseChoice.Discard); Assert.Same(selected, h.Projects.Active);
    }

    [Fact]
    public async Task PostCommitCleanupFailure_RetainsNewActiveAndRetiredOwnerUntilCloseRetry()
    {
        await using var h = await Harness.CreateAsync(); var old = h.Projects.Active!;
        var error = new IOException("Owned authoring cleanup failed");
        h.Authoring.Products[0].BeforeDispose = () => throw error;
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => h.SwitchAsync(DocumentCloseChoice.Discard)));
        Assert.Same(h.Sessions.Products[1], h.Projects.Active); Assert.True(old.IsClosed); Assert.False(h.Projects.Active!.IsClosed);
        Assert.Same(error, h.Projects.Error);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Projects.OpenAsync(h.First.Manifest));
        h.Authoring.Products[0].BeforeDispose = null; await h.Projects.CloseAsync();
        Assert.Null(h.Projects.Active); Assert.Null(h.Projects.Error); Assert.Equal(2, h.Authoring.Products[0].Disposals);
        Assert.True(h.Sessions.Products.All(p => p.IsClosed)); Assert.Equal(0, h.Authoring.Disposals);
    }

    private sealed class Harness : IAsyncDisposable
    {
        public ProjectFixture First { get; } = new();
        public ProjectFixture Next { get; } = new();
        public DocumentStore Documents { get; } = new();
        public DocumentData.Sessions Authoring { get; } = new();
        public EditorProjectsTests.Toolchain Toolchain { get; } = new();
        public EditorProjectsTests.Store Store { get; }
        public EditorProjectsTests.Sessions Sessions { get; }
        public EditorProjects Projects { get; }
        public SceneDocumentSession Current => Projects.Active!.Documents!;
        public SceneAuthoringState State => Current.Read().Content!;
        private Harness()
        {
            Store = new(First.Resolved);
            Sessions = new() { Activate = args => new(args, new(args.Project, Documents, Authoring)) };
            Projects = new(Store, Toolchain, Sessions);
        }
        public static async Task<Harness> CreateAsync()
        {
            var h = new Harness();
            await h.Projects.OpenAsync(h.First.Manifest);
            await h.Current.NewAsync(DocumentData.Scene()); return h;
        }
        public void Edit(double x) => Current.Apply(DocumentData.Edit(State.Document.Revision, x));
        public Task<ProjectSession> SwitchAsync(DocumentCloseChoice choice, string? saveAs = null, CancellationToken token = default)
        { Store.Project = Next.Resolved; return Projects.SwitchAsync(Next.Manifest, choice, saveAs, token); }
        public async ValueTask DisposeAsync()
        { await Projects.DisposeAsync(); First.Dispose(); Next.Dispose(); }
    }
}
