using System.Globalization;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorTransformViewModelTests
{
    [Fact]
    public void Selection_LoadsNineStableFieldsAndAtomicCommitUsesOneSharedHistoryEntry()
    {
        using var test = new EditingFixture(); var model = test.Model; var fields = model.Fields.ToArray();
        Assert.Equal(new[] { "x", "y", "z", "pitch", "yaw", "roll", "sx", "sy", "sz" }, fields.Select(f => f.Key));
        Assert.Equal(new[] { "4", "0", "0", "0", "0", "0", "1", "1", "1" }, fields.Select(f => f.Value));
        Assert.True(model.CanEdit); Assert.False(model.CanApply); Assert.False(model.CanCancel); Assert.False(model.CanUndo); Assert.False(model.CanRedo);
        var values = new[] { "12.5", "-2", "3e1", "0.1", "-0.2", "0.3", "2", "3", "4" };
        for (var i = 0; i < fields.Length; i++) fields[i].Value = values[i];
        Assert.True(model.HasDraft); Assert.True(model.CanApply); Assert.False(model.CanUndo);
        model.Apply(); var state = test.Owner.Document.Read();
        Assert.Equal(8, state.Content!.Document.Revision); Assert.Equal(1, state.Content.UndoCount); Assert.Equal(0, state.Content.RedoCount); Assert.True(state.IsDirty);
        var committed = state.Content.Resolved.Entities[DocumentData.Child].Components[DocumentData.Transform];
        for (var i = 0; i < fields.Length; i++) Assert.Equal(double.Parse(values[i], CultureInfo.InvariantCulture), committed.Properties[fields[i].Key].Value.GetNumber());
        Assert.Equal(fields, model.Fields); Assert.False(model.HasDraft); Assert.False(model.CanApply); Assert.True(model.CanUndo); Assert.Empty(model.ErrorText);
        model.Undo(); Assert.Equal(4, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.False(test.Owner.Document.Read().IsDirty);
        Assert.Equal(0, test.Owner.Document.Read().Content!.UndoCount); Assert.Equal(1, test.Owner.Document.Read().Content!.RedoCount);
        model.Redo(); Assert.Equal(12.5, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
    }

    [Theory]
    [InlineData("x", "")] [InlineData("x", "text")] [InlineData("x", "1,5")]
    [InlineData("x", "NaN")] [InlineData("y", "Infinity")] [InlineData("yaw", "-Infinity")]
    [InlineData("roll", "3.5e38")] [InlineData("pitch", "1e309")]
    [InlineData("sx", "0")] [InlineData("sy", "-1")] [InlineData("sz", "1e-46")]
    public void InvalidField_ReportsLocalErrorAndNeverCommitsPartialBatch(string key, string input)
    {
        using var test = new EditingFixture(); var before = test.Owner.Document.Read().Content;
        test.Field("z").Value = "42"; var field = test.Field(key); field.Value = input;
        Assert.NotEmpty(field.Error); Assert.True(test.Model.HasDraft); Assert.False(test.Model.CanApply); Assert.True(test.Model.CanCancel);
        test.Model.Apply(); Assert.Same(before, test.Owner.Document.Read().Content); Assert.False(test.Owner.Document.Read().IsDirty);
        Assert.Equal(0, before!.UndoCount); Assert.Equal(input, field.Value); Assert.Equal("42", test.Field("z").Value);
        test.Model.Cancel(); Assert.Empty(field.Error); Assert.False(test.Model.HasDraft); Assert.False(test.Model.CanCancel); Assert.Equal("0", test.Field("z").Value);
    }

    [Theory]
    [InlineData("x", "-125.25")] [InlineData("yaw", " -1.25e2 ")]
    [InlineData("sx", "1.401298464324817e-45")] [InlineData("z", "3.4028234663852886e38")]
    public void FiniteFloatRangeAndPositiveScaleBoundary_CommitWithoutChangingOtherProperties(string key, string input)
    {
        using var test = new EditingFixture(); test.Field(key).Value = input;
        Assert.Empty(test.Field(key).Error); Assert.True(test.Model.CanApply); test.Model.Apply();
        var state = test.Owner.Document.Read().Content!; var component = state.Resolved.Entities[DocumentData.Child].Components[DocumentData.Transform];
        Assert.Equal(double.Parse(input, CultureInfo.InvariantCulture), component.Properties[key].Value.GetNumber());
        Assert.Equal(1, state.UndoCount); Assert.Empty(test.Model.ErrorText);
        Assert.Equal(DocumentData.Text, state.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["text"].Value.GetString());
    }

    [Fact]
    public void Refresh_PreservesDraftAndExternalRevisionRequiresExplicitCancelBeforeCommit()
    {
        using var test = new EditingFixture(); var fields = test.Model.Fields.ToArray(); test.Field("x").Value = "73.25";
        test.Model.Refresh(DocumentData.Child.Value.ToString()); Assert.Equal("73.25", test.Field("x").Value); Assert.True(test.Model.CanApply);
        Assert.True(test.Owner.Document.Apply(DocumentData.Edit(7, 19)).Succeeded);
        test.Model.Refresh(DocumentData.Child.Value.ToString());
        Assert.Equal(fields, test.Model.Fields); Assert.Equal("73.25", test.Field("x").Value); Assert.True(test.Model.HasDraft);
        Assert.False(test.Model.CanApply); Assert.False(test.Model.CanUndo); Assert.True(test.Model.CanCancel); Assert.NotEmpty(test.Model.ErrorText);
        test.Model.Apply(); Assert.Equal(19, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        test.Model.Cancel(); Assert.Equal("19", test.Field("x").Value); Assert.False(test.Model.HasDraft); Assert.Empty(test.Model.ErrorText); Assert.True(test.Model.CanUndo);
        test.Field("x").Value = "21"; test.Model.Apply(); Assert.Equal(21, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.Equal(2, test.Owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void LocallyFiniteScaleWithUnrepresentableWorldMatrix_ReportsEngineFailureAndPreservesDraftHistoryAndDocument()
    {
        using var test = new EditingFixture(); var before = test.Owner.Document.Read().Content;
        test.Field("sx").Value = "1e30"; test.Field("sy").Value = "1e30"; Assert.True(test.Model.CanApply); test.Model.Apply();
        Assert.Same(before, test.Owner.Document.Read().Content); Assert.Equal(0, before!.UndoCount); Assert.False(test.Owner.Document.Read().IsDirty);
        Assert.True(test.Model.HasDraft); Assert.Equal("1e30", test.Field("sx").Value); Assert.Equal("1e30", test.Field("sy").Value); Assert.NotEmpty(test.Model.ErrorText);
        test.Model.Cancel(); Assert.Equal("1", test.Field("sx").Value); Assert.Equal("1", test.Field("sy").Value); Assert.Empty(test.Model.ErrorText);
    }

    [Fact]
    public void Apply_RechecksRevisionWithoutRefreshAndDoesNotOverwriteExternalCommit()
    {
        using var test = new EditingFixture(); test.Field("x").Value = "73";
        Assert.True(test.Owner.Document.Apply(DocumentData.Edit(7, 19)).Succeeded); test.Model.Apply();
        Assert.Equal(19, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.Equal("73", test.Field("x").Value);
        Assert.True(test.Model.HasDraft); Assert.False(test.Model.CanApply); Assert.NotEmpty(test.Model.ErrorText);
    }

    [Fact]
    public void UndoRedo_UsesSavedContentCheckpointAndDraftCommandsCannotDiscardText()
    {
        using var test = new EditingFixture(); test.Field("x").Value = "22"; test.Model.Apply();
        test.Owner.Run(DocumentCommand.Save); Assert.False(test.Owner.Document.Read().IsDirty);
        test.Model.Refresh(DocumentData.Child.Value.ToString()); test.Field("x").Value = "33";
        var before = test.Owner.Document.Read().Content; test.Model.Undo(); test.Model.Redo();
        Assert.Same(before, test.Owner.Document.Read().Content); Assert.Equal("33", test.Field("x").Value);
        test.Model.Cancel(); test.Model.Undo(); Assert.Equal("4", test.Field("x").Value); Assert.True(test.Owner.Document.Read().IsDirty);
        test.Model.Redo(); Assert.Equal("22", test.Field("x").Value); Assert.False(test.Owner.Document.Read().IsDirty);
        test.Model.Undo(); test.Field("x").Value = "44"; test.Model.Apply(); Assert.False(test.Model.CanRedo); Assert.True(test.Owner.Document.Read().IsDirty);
        Assert.Equal(44, DocumentData.X(test.Owner.Document.Read().Content!));
    }

    [Fact]
    public void SelectionChangeAndMissingSelection_ResetDraftWithoutCommittingAndDisableEditing()
    {
        using var test = new EditingFixture(); var before = test.Owner.Document.Read().Content;
        test.Field("x").Value = "77"; test.Model.Refresh(DocumentData.Parent.Value.ToString());
        Assert.Equal("2", test.Field("x").Value); Assert.False(test.Model.HasDraft); Assert.True(test.Model.CanEdit); Assert.Same(before, test.Owner.Document.Read().Content);
        foreach (var selection in new[] { null, "invalid", Guid.NewGuid().ToString() })
        {
            test.Model.Refresh(selection); Assert.False(test.Model.CanEdit); Assert.False(test.Model.CanApply); Assert.False(test.Model.HasDraft);
            Assert.All(test.Model.Fields, field => { Assert.Empty(field.Value); Assert.Empty(field.Error); });
            test.Model.Apply(); Assert.Same(before, test.Owner.Document.Read().Content);
        }
    }

    [Fact]
    public void ApplyAndHistory_RecheckLiveRuntimeAdmissionAndPreserveDraftUntilRuntimeBecomesIdle()
    {
        using var test = new EditingFixture(); test.Field("x").Value = "87"; var before = test.Owner.Document.Read().Content;
        test.Owner.RuntimeIdle = false; test.Model.Apply();
        Assert.Same(before, test.Owner.Document.Read().Content); Assert.Equal("87", test.Field("x").Value);
        Assert.False(test.Model.CanEdit); Assert.False(test.Model.CanApply); Assert.False(test.Model.CanPreview);
        test.Owner.RuntimeIdle = true; test.Model.Refresh(DocumentData.Child.Value.ToString()); Assert.True(test.Model.CanApply); test.Model.Apply();
        test.Owner.RuntimeIdle = false; test.Model.Undo(); Assert.Equal(87, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.False(test.Model.CanUndo);
        test.Owner.RuntimeIdle = true; test.Model.Undo(); Assert.Equal(4, DocumentData.X(test.Owner.Document.Read().Content!));
        test.Owner.RuntimeIdle = false; test.Model.Redo(); Assert.Equal(4, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.False(test.Model.CanRedo);
    }

    [Fact]
    public void BusySaveAndExitDecision_BlockMutationWithoutLosingDraft()
    {
        using var test = new EditingFixture(); var started = DocumentData.Signal(); var release = DocumentData.Signal();
        test.Owner.Store.Write = async (_, _, _, _) => { started.TrySetResult(); await release.Task; return new("saved"); };
        test.Field("x").Value = "97"; var before = test.Owner.Document.Read().Content;
        try
        {
            Assert.True(test.Owner.Commands.Execute(DocumentCommand.Save)); DocumentUiFixture.Wait(started.Task);
            test.Model.Apply(); Assert.Same(before, test.Owner.Document.Read().Content); Assert.Equal("97", test.Field("x").Value); Assert.False(test.Model.CanEdit);
        }
        finally { release.TrySetResult(); }
        test.Owner.Complete(); test.Model.Apply(); Assert.Equal(97, DocumentData.X(test.Owner.Document.Read().Content!));
        test.Field("x").Value = "98"; var exit = test.Owner.Commands.RequestExitAsync(); test.Owner.Decision(); test.Model.Apply();
        Assert.Equal(97, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.Equal("98", test.Field("x").Value); Assert.False(test.Model.CanEdit);
        Assert.True(test.Owner.Commands.Choose(DocumentCloseChoice.Cancel)); DocumentUiFixture.Wait(test.Owner.CompleteTransition(exit, false));
        test.Model.Refresh(DocumentData.Child.Value.ToString()); Assert.True(test.Model.CanApply);
    }

    [Fact]
    public void ModelAndField_RejectForeignThreadAndDisposedModelDoesNotOwnBorrowedServices()
    {
        using var test = new EditingFixture();
        Exception? refresh = null, edit = null;
        var thread = new Thread(() => { refresh = Record.Exception(() => test.Model.Refresh(DocumentData.Child.Value.ToString())); edit = Record.Exception(() => test.Field("x").Value = "30"); });
        thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.IsType<InvalidOperationException>(refresh); Assert.IsType<InvalidOperationException>(edit); Assert.Equal("4", test.Field("x").Value);
        test.Model.Dispose(); Assert.Throws<ObjectDisposedException>(() => test.Model.Refresh(null)); Assert.Throws<ObjectDisposedException>(() => test.Field("x").Value = "31");
        Assert.False(test.Owner.Commands.Read().Closed); Assert.False(test.Owner.Projects.IsClosed); Assert.Equal(0, test.Owner.RuntimeStops);
        Assert.True(test.Preview.Show(test.Owner.Document.Read().Content!.Resolved)); DocumentUiFixture.Wait(test.Preview.Pending);
        Assert.NotNull(test.Preview.Read().Presented);
    }
}
