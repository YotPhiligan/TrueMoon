using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Hierarchy;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Hierarchy;

public sealed class EditorHierarchyViewModelTests
{
    [Fact]
    public void Hierarchy_MapsResolvedParentsAndComponentKindsAndCachesUnchangedObservations()
    {
        var source = DocumentData.Scene(); var third = new EntityId(DocumentData.Id(3));
        var scene = new SceneDocument(source.Id, source.Revision,
            source.Entities.Append(new(third, DocumentData.Base, new(removedComponents: [DocumentData.Interaction]))),
            source.Definitions, source.Schemas);
        using var test = new HierarchyFixture(scene);
        var items = test.Model.Items; var cached = test.Workspace.HierarchyItems;
        Assert.Equal(3, items.Count);
        Assert.Equal(HierarchyItemKind.Entity, items.Single(item => item.Id == HierarchyFixture.Parent).Kind);
        var child = items.Single(item => item.Id == HierarchyFixture.Child);
        Assert.Equal(HierarchyFixture.Parent, child.ParentId); Assert.Equal(HierarchyItemKind.Interactive, child.Kind);
        Assert.Equal(HierarchyItemKind.Renderable, items.Single(item => item.Id == third.Value.ToString("D")).Kind);
        Assert.Equal(EditorScene.Name(HierarchyFixture.Child), child.Name);
        Assert.Equal(source.Id.Value.ToString("D"), test.Model.Caption); Assert.Equal(HierarchyFixture.Child, test.Model.SelectedId);
        Assert.True(test.Model.CanMove); Assert.True(test.Model.CanMoveToRoot);
        var notices = new List<string?>(); test.Model.PropertyChanged += (_, change) => notices.Add(change.PropertyName);
        var version = test.Model.SnapshotVersion;
        test.Model.Refresh(); test.Workspace.SetDocument(test.Owner.Document.Read().Content); test.Model.Refresh();
        Assert.Same(items, test.Model.Items); Assert.Same(cached, test.Workspace.HierarchyItems);
        Assert.Equal(version, test.Model.SnapshotVersion); Assert.Empty(notices);
    }

    [Fact]
    public void Select_SynchronizesWorkspaceAndTransformWhileUnknownKeyPreservesAndNullClearsSelection()
    {
        using var test = new HierarchyFixture(); test.Transform.Fields[0].Value = "17";
        Assert.True(test.Transform.HasDraft);
        test.Model.Select(HierarchyFixture.Parent);
        Assert.Equal(HierarchyFixture.Parent, test.Workspace.SelectedId); Assert.Equal(HierarchyFixture.Parent, test.Model.SelectedId);
        Assert.Equal("2", test.Transform.Fields[0].Value); Assert.False(test.Transform.HasDraft); Assert.True(test.Model.CanMove);
        Assert.False(test.Model.CanMoveToRoot);
        test.Model.Select("missing");
        Assert.Equal(HierarchyFixture.Parent, test.Workspace.SelectedId); Assert.Equal("2", test.Transform.Fields[0].Value);
        test.Model.Select(null);
        Assert.Null(test.Workspace.SelectedId); Assert.Null(test.Model.SelectedId); Assert.Empty(test.Transform.Components);
        Assert.False(test.Transform.CanEditProperties); Assert.False(test.Model.CanMoveToRoot);
        test.Workspace.Select(HierarchyFixture.Child); test.Model.Select(HierarchyFixture.Child);
        Assert.Equal(HierarchyFixture.Child, test.Model.SelectedId); Assert.Equal("4", test.Transform.Fields[0].Value);
    }

    [Fact]
    public void MoveToRoot_CommitsSharedHistoryPreservesLocalTransformAndUndoRestoresCleanCheckpoint()
    {
        using var test = new HierarchyFixture(); var original = test.Owner.Document.Read().Content!;
        var beforeItems = test.Model.Items; var version = test.Model.SnapshotVersion;
        test.Model.MoveToRoot();
        var moved = test.Owner.Document.Read().Content!;
        Assert.Equal(8, moved.Document.Revision); Assert.Equal(1, moved.UndoCount); Assert.Equal(0, moved.RedoCount);
        Assert.True(test.Owner.Document.Read().IsDirty); Assert.Null(moved.Resolved.Entities[DocumentData.Child].ParentId);
        Assert.Equal(4, DocumentData.X(moved)); Assert.Equal("4", test.Transform.Fields[0].Value);
        Assert.Equal(HierarchyFixture.Child, test.Workspace.SelectedId); Assert.Equal(HierarchyFixture.Child, test.Model.SelectedId);
        Assert.False(test.Model.CanMoveToRoot); Assert.True(test.Model.SnapshotVersion > version);
        Assert.Null(test.Model.Items.Single(item => item.Id == HierarchyFixture.Child).ParentId);
        Assert.Equal(HierarchyFixture.Parent, beforeItems.Single(item => item.Id == HierarchyFixture.Child).ParentId);
        Assert.Equal(DocumentData.Parent, original.Resolved.Entities[DocumentData.Child].ParentId);
        test.Transform.Undo(); test.Synchronize();
        Assert.Equal(9, test.Owner.Document.Read().Content!.Document.Revision); Assert.False(test.Owner.Document.Read().IsDirty);
        Assert.Equal(DocumentData.Parent, test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].ParentId);
        Assert.True(test.Model.CanMoveToRoot); Assert.True(test.Transform.CanRedo);
        test.Transform.Redo(); test.Synchronize();
        Assert.Null(test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].ParentId);
        Assert.True(test.Owner.Document.Read().IsDirty); Assert.Equal(4, DocumentData.X(test.Owner.Document.Read().Content!));
    }

    [Fact]
    public void Move_CycleProducesDiagnosticWhileMissingMalformedAndSameParentRequestsPublishNothing()
    {
        using var test = new HierarchyFixture(); var before = test.Owner.Document.Read().Content!;
        Assert.False(test.Model.Move(HierarchyFixture.Parent, HierarchyFixture.Child));
        Assert.Contains(nameof(SceneAuthoringError.InvalidScene), test.Model.ErrorText);
        Assert.Same(before, test.Owner.Document.Read().Content); Assert.False(test.Owner.Document.Read().IsDirty);
        Assert.False(test.Model.Move(HierarchyFixture.Child, HierarchyFixture.Parent));
        Assert.False(test.Model.Move("malformed", null));
        Assert.False(test.Model.Move(HierarchyFixture.Child, "malformed"));
        Assert.False(test.Model.Move(HierarchyFixture.Child, DocumentData.Id(99).ToString("D")));
        Assert.False(test.Model.Move(DocumentData.Id(99).ToString("D"), null));
        Assert.Same(before, test.Owner.Document.Read().Content); Assert.Equal(0, before.UndoCount);
        Assert.True(test.Model.Move(HierarchyFixture.Child, null));
        Assert.Empty(test.Model.ErrorText); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
    }

    [Theory]
    [InlineData("runtime")]
    [InlineData("draft")]
    [InlineData("conflict")]
    [InlineData("no-document")]
    public void Move_UnavailableAdmissionPreservesDocumentAndExplainsHowToResume(string blocked)
    {
        using var test = new HierarchyFixture();
        switch (blocked)
        {
            case "runtime": test.Owner.RuntimeIdle = false; break;
            case "draft": test.Transform.Fields[0].Value = "33"; break;
            case "conflict":
                test.Transform.Fields[0].Value = "33";
                Assert.True(test.Owner.Document.Apply(DocumentData.Edit(7, 41)).Succeeded);
                test.Synchronize(); break;
            case "no-document": test.Owner.Run(DocumentCommand.Close); test.Workspace.SetDocument(null); break;
        }
        test.Model.Refresh(); var before = test.Owner.Document.Read().Content;
        Assert.False(test.Model.CanMove); Assert.False(test.Model.CanMoveToRoot); Assert.NotEmpty(test.Model.Hint);
        Assert.False(test.Model.Move(HierarchyFixture.Child, null)); test.Model.MoveToRoot();
        Assert.Same(before, test.Owner.Document.Read().Content);
        if (blocked is "draft" or "conflict") Assert.Equal("33", test.Transform.Fields[0].Value);
    }

    [Fact]
    public void Move_DuringPendingSaveIsDeniedAndBecomesAvailableAfterCompletion()
    {
        using var test = new HierarchyFixture();
        var write = new TaskCompletionSource<SceneFileStamp>(TaskCreationOptions.RunContinuationsAsynchronously);
        test.Owner.Store.Write = (_, _, _, token) => write.Task.WaitAsync(token);
        Assert.True(test.Owner.Commands.Execute(DocumentCommand.Save));
        try
        {
            test.Model.Refresh(); var before = test.Owner.Document.Read().Content;
            Assert.True(test.Owner.Commands.Read().Busy); Assert.False(test.Model.CanMove);
            Assert.False(test.Model.Move(HierarchyFixture.Child, null)); Assert.Same(before, test.Owner.Document.Read().Content);
        }
        finally { write.TrySetResult(new("saved")); test.Owner.Complete(); }
        test.Model.Refresh(); Assert.True(test.Model.CanMove); Assert.True(test.Model.Move(HierarchyFixture.Child, null));
    }

    [Fact]
    public void TransformOnlyCommitIncrementsSnapshotFenceWithoutRebuildingUnchangedHierarchyData()
    {
        using var test = new HierarchyFixture(); var items = test.Model.Items; var version = test.Model.SnapshotVersion;
        var notices = new List<string?>(); test.Model.PropertyChanged += (_, change) => notices.Add(change.PropertyName);
        test.Transform.Fields[0].Value = "16"; test.Transform.Apply(); test.Synchronize();
        Assert.Equal(16, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.Same(items, test.Model.Items);
        Assert.True(test.Model.SnapshotVersion > version); Assert.Contains(nameof(EditorHierarchyViewModel.SnapshotVersion), notices);
        Assert.DoesNotContain(nameof(EditorHierarchyViewModel.Items), notices);
        Assert.True(test.Model.CanMove); Assert.Equal(HierarchyFixture.Child, test.Model.SelectedId);
    }

    [Fact]
    public void Move_WithActivePreviewSynchronizesCommittedParentGraphAndUndoRevision()
    {
        using var test = new HierarchyFixture(); test.Transform.ShowPreview(); DocumentUiFixture.Wait(test.Preview.Pending);
        Assert.Equal(7, Assert.Single(test.Shown).Revision);
        Assert.True(test.Model.Move(HierarchyFixture.Child, null)); DocumentUiFixture.Wait(test.Preview.Pending);
        Assert.Equal(new long[] { 7, 8 }, test.Shown.Select(scene => scene.Revision));
        Assert.Null(test.Shown[^1].Entities[DocumentData.Child].ParentId); Assert.Equal(4, DocumentData.X(test.Owner.Document.Read().Content!));
        test.Transform.Undo(); test.Synchronize(); DocumentUiFixture.Wait(test.Preview.Pending);
        Assert.Equal(9, test.Shown[^1].Revision); Assert.Equal(DocumentData.Parent, test.Shown[^1].Entities[DocumentData.Child].ParentId);
        Assert.Equal(3, test.Shown.Count); Assert.False(test.Owner.Document.Read().IsDirty);
    }

    [Fact]
    public void ReparentAndTransform_SaveReopenRetainsParentAndValuesWithNewCleanHistory()
    {
        var store = new JsonSceneDocumentStore(new SceneFileCodec(DocumentData.Factory()));
        using var test = new HierarchyFixture(sceneStore: store);
        test.Transform.Fields[0].Value = "21"; test.Transform.Apply(); test.Synchronize();
        Assert.True(test.Model.Move(HierarchyFixture.Child, null)); Assert.Equal(2, test.Owner.Document.Read().Content!.UndoCount);
        test.Owner.Run(DocumentCommand.Save, "hierarchy.aetherscene"); Assert.False(test.Owner.Document.Read().IsDirty);
        test.Transform.Undo(); test.Synchronize(); Assert.True(test.Owner.Document.Read().IsDirty);
        Assert.Equal(DocumentData.Parent, test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].ParentId);
        test.Transform.Redo(); test.Synchronize(); Assert.False(test.Owner.Document.Read().IsDirty);
        test.Owner.Run(DocumentCommand.Close); test.Workspace.SetDocument(null);
        test.Owner.Run(DocumentCommand.Open, "hierarchy.aetherscene"); test.Synchronize();
        var reopened = test.Owner.Document.Read().Content!;
        Assert.Null(reopened.Resolved.Entities[DocumentData.Child].ParentId); Assert.Equal(21, DocumentData.X(reopened));
        Assert.Equal(9, reopened.Document.Revision); Assert.Equal(0, reopened.UndoCount); Assert.Equal(0, reopened.RedoCount);
        Assert.False(test.Owner.Document.Read().IsDirty); Assert.False(test.Transform.CanUndo); Assert.False(test.Model.CanMoveToRoot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Move_HeadlessCommitOrSameIdentityDocumentReplacementRejectsStaleGestureUntilFreshAttempt(bool replacement)
    {
        using var test = new HierarchyFixture(); var observed = test.Owner.Document.Read().Content!;
        var observedItems = test.Model.Items; var observedVersion = test.Model.SnapshotVersion;
        if (replacement) DocumentUiFixture.Wait(test.Owner.Document.NewAsync(DocumentData.Scene()));
        else Assert.True(test.Owner.Document.Apply(DocumentData.Edit(7, 31)).Succeeded);
        var active = test.Owner.Document.Read().Content!;
        Assert.NotSame(observed.Document, active.Document); Assert.Equal(observed.Document.Id, active.Document.Id);
        Assert.Equal(observed.Document.Entities.Select(entity => entity.Id), active.Document.Entities.Select(entity => entity.Id));
        if (replacement) Assert.Equal(observed.Document.Revision, active.Document.Revision);
        Assert.Same(observedItems, test.Model.Items); Assert.Equal(observedVersion, test.Model.SnapshotVersion);
        Assert.False(test.Model.Move(HierarchyFixture.Child, null));
        Assert.Contains("Сцена изменилась", test.Model.ErrorText); Assert.Same(active, test.Owner.Document.Read().Content);
        Assert.Equal(DocumentData.Parent, active.Resolved.Entities[DocumentData.Child].ParentId);
        Assert.Equal(replacement ? 0 : 1, active.UndoCount); Assert.Equal(replacement ? 4 : 31, DocumentData.X(active));
        Assert.Equal(7, observed.Document.Revision); Assert.Equal(4, DocumentData.X(observed));
        Assert.Equal(DocumentData.Parent, observed.Resolved.Entities[DocumentData.Child].ParentId);
        Assert.Equal(0, observed.UndoCount);
        test.Synchronize(); Assert.True(test.Model.Move(HierarchyFixture.Child, null));
        var moved = test.Owner.Document.Read().Content!;
        Assert.Null(moved.Resolved.Entities[DocumentData.Child].ParentId); Assert.Equal(active.Document.Revision + 1, moved.Document.Revision);
        Assert.Equal(active.UndoCount + 1, moved.UndoCount); Assert.Equal(DocumentData.X(active), DocumentData.X(moved));
        Assert.Empty(test.Model.ErrorText);
        if (replacement) { Assert.Equal(1, test.Owner.Authoring.Products[0].Disposals); Assert.Equal(0, test.Owner.Authoring.Products[1].Disposals); }
    }

    [Fact]
    public void CommandReparent_StaleDocumentIdentityAndRuntimeAdmissionCannotMutateActiveContent()
    {
        using var test = new HierarchyFixture(); var captured = test.Owner.Document.Read().Content!.Document;
        Assert.True(test.Owner.Document.Apply(DocumentData.Edit(7, 19)).Succeeded); var current = test.Owner.Document.Read().Content!;
        var batch = new ReparentBatch(DocumentData.Operation(8), [new(DocumentData.Child, null)]);
        Assert.Throws<InvalidOperationException>(() => test.Owner.Commands.Reparent(captured, batch));
        Assert.Same(current, test.Owner.Document.Read().Content); Assert.Equal(DocumentData.Parent, current.Resolved.Entities[DocumentData.Child].ParentId);
        test.Owner.RuntimeIdle = false;
        Assert.Throws<InvalidOperationException>(() => test.Owner.Commands.Reparent(current.Document, batch));
        Assert.Same(current, test.Owner.Document.Read().Content); Assert.Equal(1, current.UndoCount);
    }

    [Fact]
    public void OwnerThreadAndDispose_FencePresentationButKeepBorrowedServicesUsable()
    {
        using var test = new HierarchyFixture();
        Exception? wrongThread = null;
        var worker = new Thread(() => wrongThread = Record.Exception(test.Model.Refresh)); worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        Assert.IsType<InvalidOperationException>(wrongThread);
        test.Model.Dispose(); test.Workspace.Select(HierarchyFixture.Parent);
        Assert.Throws<ObjectDisposedException>(test.Model.Refresh);
        Assert.Throws<ObjectDisposedException>(() => test.Model.Select(HierarchyFixture.Child));
        Assert.False(test.Owner.Commands.Read().Closed); Assert.Equal(0, test.Owner.Authoring.Products[0].Disposals);
        test.Transform.Refresh(HierarchyFixture.Child); test.Transform.Fields[0].Value = "29"; test.Transform.Apply();
        Assert.Equal(29, DocumentData.X(test.Owner.Document.Read().Content!));
    }
}
