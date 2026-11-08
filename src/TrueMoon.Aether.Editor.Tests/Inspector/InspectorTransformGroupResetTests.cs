using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class InspectorTransformGroupResetTests
{
    private static readonly string[][] Keys = [["x", "y", "z"], ["pitch", "yaw", "roll"], ["sx", "sy", "sz"]];
    private static readonly double[] Inherited = [2, 3, 4, .2, .3, .4, 2, 3, 5];
    private static readonly double[] Local = [12, 13, 14, 1.2, 1.3, 1.4, 4, 6, 10];

    [Theory]
    [InlineData(TransformGroup.Position)] [InlineData(TransformGroup.Rotation)] [InlineData(TransformGroup.Scale)]
    public void GroupReset_StagesOnlyItsGroupAndApplyUndoRedoSaveReopenPreserveInheritance(TransformGroup group)
    {
        using var test = new InspectorFixture(Scene()); var model = test.Model;
        var before = test.Owner.Document.Read().Content!;
        model.ResetTransformGroup(group);
        Assert.Same(before, test.Owner.Document.Read().Content); Assert.False(test.Owner.Document.Read().IsDirty);
        Assert.Equal(0, before.UndoCount); Assert.True(model.CanApply); Assert.False(CanReset(model, group));
        foreach (var field in model.Fields) Assert.Equal(Keys[(int)group].Contains(field.Key), field.IsReset);
        var drafts = model.Fields.Select(field => field.CaptureDraft()).ToArray();
        model.ResetTransformGroup(group); Assert.Equal(drafts, model.Fields.Select(field => field.CaptureDraft()));
        model.Apply(); var committed = test.Owner.Document.Read().Content!;
        Assert.Equal(before.Document.Revision + 1, committed.Document.Revision); Assert.Equal(1, committed.UndoCount);
        var expected = Local.ToArray(); Array.Copy(Inherited, (int)group * 3, expected, (int)group * 3, 3);
        AssertValues(committed, expected);
        var overrides = committed.Document.Entities.Single().Changes.PropertyOverrides;
        Assert.Equal(6, overrides.Length); Assert.DoesNotContain(overrides, item => Keys[(int)group].Contains(item.PropertyId));
        foreach (var key in Keys[(int)group])
        { Assert.StartsWith("Наследуется", test.Property(DocumentData.Transform, key).SourceText); Assert.False(test.Property(DocumentData.Transform, key).CanReset); }
        model.Undo(); AssertValues(test.Owner.Document.Read().Content!, Local); Assert.False(test.Owner.Document.Read().IsDirty);
        model.Redo(); AssertValues(test.Owner.Document.Read().Content!, expected); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        test.Owner.Run(DocumentCommand.Save);
        var saved = Assert.Single(test.Owner.Store.Writes).Document;
        Assert.Equal(6, saved.Entities.Single().Changes.PropertyOverrides.Length);
        test.Owner.Store.File = new(saved, new("saved-reset"));
        test.Owner.Run(DocumentCommand.Close); test.Owner.Run(DocumentCommand.Open, "room.aetherscene"); test.Refresh();
        AssertValues(test.Owner.Document.Read().Content!, expected); Assert.False(test.Owner.Document.Read().IsDirty);
        Assert.Equal(0, test.Owner.Document.Read().Content!.UndoCount); Assert.False(model.HasDraft);
    }

    [Theory]
    [InlineData(TransformGroup.Position)] [InlineData(TransformGroup.Rotation)] [InlineData(TransformGroup.Scale)]
    public void CancelGroupReset_PreservesCommittedDocumentAndOtherDraftUntilExplicitCancel(TransformGroup group)
    {
        using var test = new InspectorFixture(Scene()); var before = test.Owner.Document.Read().Content!;
        var other = test.Model.Fields[((int)group + 1) % 3 * 3]; other.Value = "17";
        test.Model.ResetTransformGroup(group);
        Assert.Equal("17", other.Value); Assert.False(other.IsReset); Assert.Same(before, test.Owner.Document.Read().Content);
        test.Model.Cancel(); Assert.Same(before, test.Owner.Document.Read().Content); Assert.False(test.Model.HasDraft);
        AssertValues(before, Local); Assert.All(test.Model.Fields, field => Assert.False(field.IsReset));
    }

    [Fact]
    public void InheritedGroup_IsDisabledUntilDraftAndResetLeavesCleanPeersUntouched()
    {
        using var test = new InspectorFixture(Scene(overrides: false));
        Assert.False(test.Model.CanResetPosition); Assert.False(test.Model.CanResetRotation); Assert.False(test.Model.CanResetScale);
        test.Model.ResetTransformGroup(TransformGroup.Position); Assert.False(test.Model.HasDraft);
        var x = test.Property(DocumentData.Transform, "x"); x.Value = "-";
        Assert.True(x.HasError); Assert.True(test.Model.CanResetPosition);
        test.Model.ResetTransformGroup(TransformGroup.Position);
        Assert.True(x.IsReset); Assert.False(x.HasError); Assert.True(test.Model.CanApply);
        Assert.False(test.Property(DocumentData.Transform, "y").HasDraft); Assert.False(test.Property(DocumentData.Transform, "z").HasDraft);
        test.Model.Apply(); AssertValues(test.Owner.Document.Read().Content!, Inherited);
        Assert.Empty(test.Owner.Document.Read().Content!.Document.Entities.Single().Changes.PropertyOverrides);
    }

    [Fact]
    public void LocalComponentGroupReset_UsesSchemaDefaultsAndKeepsOtherGroups()
    {
        using var test = new InspectorFixture(Scene(localComponent: true));
        test.Model.ResetTransformGroup(TransformGroup.Scale); test.Model.Apply();
        AssertValues(test.Owner.Document.Read().Content!, [12, 13, 14, 1.2, 1.3, 1.4, 1, 1, 1]);
        var component = Assert.Single(test.Owner.Document.Read().Content!.Document.Entities.Single().Changes.AddedComponents);
        Assert.DoesNotContain(component.Properties.Keys, key => Keys[2].Contains(key)); Assert.Equal(6, component.Properties.Count);
    }

    [Fact]
    public void LinkedScaleReset_ClearsInvalidRatioDraftAsOneBatchWithoutPropagatingValues()
    {
        using var test = new InspectorFixture(Scene()); var model = test.Model; model.ScaleLinked = true;
        var sx = test.Property(DocumentData.Transform, "sx"); var sy = test.Property(DocumentData.Transform, "sy");
        sy.Value = "-"; sx.Value = "8";
        Assert.True(sx.HasError); Assert.False(model.CanApply); Assert.Equal("10", test.Property(DocumentData.Transform, "sz").Value);
        var values = model.Fields.Select(field => field.Value).ToArray();
        model.ResetTransformGroup(TransformGroup.Scale);
        Assert.True(model.ScaleLinked); Assert.True(model.CanApply); Assert.False(model.HasDiagnostics);
        Assert.Equal(values, model.Fields.Select(field => field.Value));
        Assert.All(model.Fields.Skip(6), field => Assert.True(field.IsReset));
        model.Apply(); AssertValues(test.Owner.Document.Read().Content!, [12, 13, 14, 1.2, 1.3, 1.4, 2, 3, 5]);
        Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount); Assert.True(model.ScaleLinked);
        sx.Value = "4";
        Assert.Equal("6", sy.Value); Assert.Equal("10", test.Property(DocumentData.Transform, "sz").Value);
    }

    [Fact]
    public void OtherGroupReset_PreservesLinkedScaleErrorAndBlocksWholeDraftCommit()
    {
        using var test = new InspectorFixture(Scene()); test.Model.ScaleLinked = true;
        test.Property(DocumentData.Transform, "sy").Value = "-"; test.Property(DocumentData.Transform, "sx").Value = "8";
        var before = test.Owner.Document.Read().Content;
        test.Model.ResetTransformGroup(TransformGroup.Position);
        Assert.True(test.Property(DocumentData.Transform, "sx").HasError); Assert.True(test.Model.HasDiagnostics); Assert.False(test.Model.CanApply);
        test.Model.Apply(); Assert.Same(before, test.Owner.Document.Read().Content);
        test.Model.ResetTransformGroup(TransformGroup.Scale); Assert.True(test.Model.CanApply);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RuntimeOrDocumentBusy_GroupResetDoesNotAlterDraft(bool documentBusy)
    {
        using var test = new InspectorFixture(Scene()); var before = test.Owner.Document.Read().Content;
        TaskCompletionSource? waiting = null;
        if (documentBusy)
        {
            waiting = DocumentData.Signal(); var captured = waiting;
            test.Owner.Store.Write = async (_, _, _, _) => { await captured.Task; return new("saved"); };
            Assert.True(test.Owner.Commands.Execute(DocumentCommand.SaveAs, "busy.aetherscene"));
        }
        else test.Owner.RuntimeIdle = false;
        try
        {
            test.Refresh(); Assert.False(test.Model.CanResetPosition); Assert.False(test.Model.CanResetRotation); Assert.False(test.Model.CanResetScale);
            test.Model.ResetTransformGroup(TransformGroup.Position); Assert.False(test.Model.HasDraft);
            Assert.Same(before, test.Owner.Document.Read().Content);
        }
        finally { if (waiting != null) { waiting.SetResult(); test.Owner.Complete(); } }
        test.Owner.RuntimeIdle = true; test.Refresh(); Assert.True(test.Model.CanResetPosition);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExternalRevision_GroupResetRejectsStaleActionAndPreservesConflictingDraft(bool draft)
    {
        using var test = new InspectorFixture(Scene());
        if (draft) test.Model.Fields[3].Value = "pending";
        Assert.True(test.Owner.Document.Apply(DocumentData.Edit(7, 56)).Succeeded);
        var before = test.Owner.Document.Read().Content;
        test.Model.ResetTransformGroup(TransformGroup.Position);
        Assert.Same(before, test.Owner.Document.Read().Content); Assert.False(test.Model.Fields[0].IsReset);
        Assert.NotEmpty(test.Model.ErrorText);
        if (draft) { Assert.Equal("pending", test.Model.Fields[3].Value); Assert.False(test.Model.CanResetPosition); }
        else { Assert.False(test.Model.HasDraft); test.Model.ResetTransformGroup(TransformGroup.Position); Assert.True(test.Model.Fields[0].IsReset); }
    }

    [Fact]
    public void MissingTransformOrSelection_GroupResetCannotReusePreviousFields()
    {
        using var test = new InspectorFixture(InspectorFixture.WithIndependentEntity(true));
        foreach (var selection in new[] { DocumentData.Id(3).ToString("D"), "missing" })
        {
            test.Model.Refresh(selection); Assert.False(test.Model.CanResetPosition); Assert.False(test.Model.CanResetRotation); Assert.False(test.Model.CanResetScale);
            foreach (var group in Enum.GetValues<TransformGroup>()) test.Model.ResetTransformGroup(group);
            Assert.False(test.Model.HasDraft); Assert.Equal(0, test.Owner.Document.Read().Content!.UndoCount);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => test.Model.ResetTransformGroup((TransformGroup)99));
    }

    [Fact]
    public void SelectionChange_DiscardsPendingGroupResetAndRecomputesAdmissionForTheNewEntity()
    {
        using var test = new InspectorFixture(); var before = test.Owner.Document.Read().Content;
        test.Model.ResetTransformGroup(TransformGroup.Position); Assert.True(test.Model.HasDraft);
        test.Model.Refresh(InspectorFixture.Parent);
        Assert.False(test.Model.HasDraft); Assert.False(test.Model.CanResetPosition); Assert.Equal("2", test.Model.Fields[0].Value);
        test.Model.ResetTransformGroup(TransformGroup.Position); Assert.False(test.Model.HasDraft);
        test.Model.Refresh(InspectorFixture.Child);
        Assert.True(test.Model.CanResetPosition); Assert.False(test.Model.Fields[0].IsReset); Assert.Equal("4", test.Model.Fields[0].Value);
        Assert.Same(before, test.Owner.Document.Read().Content);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ActiveNumericGesture_DisablesGroupResetUntilCompletionOrCancellation(bool cancel)
    {
        using var test = new InspectorFixture(Scene()); var field = test.Model.Fields[0];
        var id = test.Model.BeginNumericGesture(field); Assert.NotEqual(0, id);
        field.Value = "18";
        Assert.False(test.Model.CanResetPosition); Assert.False(test.Model.CanResetScale);
        test.Model.ResetTransformGroup(TransformGroup.Position); Assert.False(field.IsReset); Assert.Equal("18", field.Value);
        test.Model.FinishNumericGesture(cancel ? NumericGestureResult.Cancelled : NumericGestureResult.Completed, id);
        Assert.True(test.Model.CanResetPosition); Assert.Equal(cancel ? "12" : "18", field.Value);
        test.Model.ResetTransformGroup(TransformGroup.Position); Assert.True(field.IsReset);
        test.Model.FinishNumericGesture(NumericGestureResult.Cancelled, id); Assert.True(field.IsReset);
    }

    [Theory]
    [InlineData(330)] [InlineData(230)]
    public void GroupButtons_StayVisibleRetainControlsAndSupportKeyboardResetAcrossThemeAndResize(int width)
    {
        using var test = new InspectorFixture(Scene()); var view = new EditorInspectorView(test.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view, width: width); ui.Update(); ui.Update();
        var transform = view.TransformEditors[DocumentData.Transform]; var tree = DocumentUiFixture.Tree(view).ToArray();
        foreach (var pair in new[] { (transform.Position, transform.ResetPosition), (transform.Rotation, transform.ResetRotation), (transform.Scale, transform.ResetScale) })
        {
            var (vector, button) = pair;
            Assert.Equal(24, button.Bounds.Width); Assert.Equal(24, button.Bounds.Height); Assert.True(button.IsVisible);
            Assert.True(button.Bounds.X >= vector.Bounds.X + vector.Bounds.Width); Assert.True(button.Bounds.X + button.Bounds.Width <= view.Bounds.X + view.Bounds.Width);
            Assert.Equal(vector.First.Bounds.Width, vector.Second.Bounds.Width); Assert.Equal(vector.Second.Bounds.Width, vector.Third.Bounds.Width);
            Assert.Contains("группу", button.AccessibleName);
        }
        var input = transform.Position.First.Editor; ui.Focus(input); input.Value = "-"; ui.Update();
        ui.SetTheme(Theme.Light); view.WithViewStyle(new EditorInspectorViewStyle()); ui.Update();
        Assert.Equal(tree, DocumentUiFixture.Tree(view)); Assert.True(input.IsFocused); Assert.Equal("-", input.Value);
        ui.Resize(new(width + 80, 800)); ui.Update(); ui.Update();
        Assert.Equal(tree, DocumentUiFixture.Tree(view)); Assert.True(input.IsFocused); Assert.Equal("-", input.Value);
        ui.Focus(transform.ResetPosition); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter)); ui.HandleInput(new(InputKind.KeyUp, Key: UiKey.Enter)); ui.Update(); ui.Update();
        Assert.True(test.Model.Fields[0].IsReset); Assert.False(transform.ResetPosition.IsEnabled); Assert.True(transform.ResetScale.IsEnabled);
        Assert.Equal(tree, DocumentUiFixture.Tree(view)); Assert.False(test.Owner.Document.Read().IsDirty);
        test.Model.Cancel(); ui.Update(); ui.Update(); Assert.True(transform.ResetPosition.IsEnabled);
        ui.Focus(transform.Rotation.Third.Editor); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Tab));
        Assert.True(transform.ResetRotation.IsFocused);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Space)); ui.HandleInput(new(InputKind.KeyUp, Key: UiKey.Space)); ui.Update();
        Assert.All(test.Model.Fields.Skip(3).Take(3), field => Assert.True(field.IsReset));
        test.Model.Cancel(); ui.Update(); ui.Update();
        view.Scroll.BringIntoView(transform.ResetScale.Bounds); ui.Update();
        DocumentUiFixture.Click(ui, transform.ResetScale); ui.Update(); ui.Update();
        Assert.All(test.Model.Fields.Skip(6), field => Assert.True(field.IsReset));
    }

    private static bool CanReset(EditorInspectorViewModel model, TransformGroup group) => group switch
    { TransformGroup.Position => model.CanResetPosition, TransformGroup.Rotation => model.CanResetRotation, _ => model.CanResetScale };

    private static void AssertValues(SceneAuthoringState state, double[] expected) => Assert.Equal(expected,
        Keys.SelectMany(keys => keys).Select(key => state.Resolved.Entities[DocumentData.Child].Components[DocumentData.Transform].Properties[key].Value.GetNumber()));

    private static SceneDocument Scene(bool overrides = true, bool localComponent = false)
    {
        var values = Keys.SelectMany(keys => keys).ToArray();
        var inherited = new ComponentData(DocumentData.Transform, BuiltInComponents.TransformId, 1,
            values.Select((key, i) => KeyValuePair.Create(key, PropertyValue.From(Inherited[i]))));
        var local = new ComponentData(DocumentData.Transform, BuiltInComponents.TransformId, 1,
            values.Select((key, i) => KeyValuePair.Create(key, PropertyValue.From(Local[i]))));
        var definition = new EntityDefinition(DocumentData.Base, localComponent ? EntityPatch.Empty : new(addedComponents: [inherited]));
        var patch = localComponent ? new EntityPatch(addedComponents: [local]) : new EntityPatch(propertyOverrides:
            overrides ? values.Select((key, i) => new ComponentPropertyOverride(DocumentData.Transform, key, PropertyValue.From(Local[i]))) : []);
        return new(new(DocumentData.Id(100)), 7, [new(DocumentData.Child, DocumentData.Base, patch)], [definition],
            [BuiltInComponents.Schemas.Single(schema => schema.TypeId == BuiltInComponents.TransformId)]);
    }
}
