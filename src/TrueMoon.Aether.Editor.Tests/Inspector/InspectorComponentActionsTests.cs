using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class InspectorComponentActionsTests
{
    [Fact]
    public void AddRemoveCustomOccurrence_UsesExactSchemaFreshIdentityAndOneSharedHistoryPerAction()
    {
        using var test = new InspectorFixture(); var model = test.Model;
        Assert.True(model.AddableTypes.Single(item => item.Schema.TypeId == DocumentData.CustomType && item.Schema.Version == 2).CanAdd);
        Assert.False(model.AddableTypes.Single(item => item.Schema.TypeId == BuiltInComponents.TransformId).CanAdd);
        var before = model.Components.Select(item => item.Id).ToHashSet();
        var unchanged = test.Property(DocumentData.Custom, "text");
        model.AddComponent(DocumentData.CustomType, 2);
        var added = Assert.Single(model.Components, item => !before.Contains(item.Id));
        Assert.Equal(DocumentData.CustomType, added.TypeId); Assert.Equal(2, added.SchemaVersion);
        Assert.Equal(long.MinValue.ToString(System.Globalization.CultureInfo.InvariantCulture), added.Properties.Single(item => item.Key == "integer").Value);
        Assert.True(added.Properties.Single(item => item.Key == "nullable").IsNull);
        Assert.Same(unchanged, test.Property(DocumentData.Custom, "text"));
        Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount); Assert.False(model.HasDraft);
        model.RemoveComponent(added.Id);
        Assert.DoesNotContain(model.Components, item => item.Id == added.Id);
        Assert.DoesNotContain(model.RemovedComponents, item => item.Id == added.Id);
        Assert.Equal(2, test.Owner.Document.Read().Content!.UndoCount);
        Assert.DoesNotContain(test.Owner.Document.Read().Content!.Document.Entities.Single(item => item.Id == DocumentData.Child).Changes.AddedComponents, item => item.Id == added.Id);
        model.Undo(); Assert.Contains(model.Components, item => item.Id == added.Id);
        model.Undo(); Assert.DoesNotContain(model.Components, item => item.Id == added.Id); Assert.False(test.Owner.Document.Read().IsDirty);
        model.Redo(); Assert.Contains(model.Components, item => item.Id == added.Id);
    }

    [Fact]
    public void RemoveAndRestoreInheritedComponent_PreservesOccurrenceAndExposesHiddenProvenance()
    {
        using var test = new InspectorFixture(); var model = test.Model;
        model.RemoveComponent(DocumentData.Interaction);
        Assert.DoesNotContain(model.Components, item => item.Id == DocumentData.Interaction);
        Assert.Contains(model.RemovedComponents, item => item.Id == DocumentData.Interaction && item.LocalMask);
        Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        model.RestoreComponent(DocumentData.Interaction);
        Assert.Contains(model.Components, item => item.Id == DocumentData.Interaction);
        Assert.DoesNotContain(model.RemovedComponents, item => item.Id == DocumentData.Interaction);
        Assert.True(test.Property(DocumentData.Interaction, "enabled").BooleanValue); Assert.Equal(2, test.Owner.Document.Read().Content!.UndoCount);
        Assert.Empty(test.Owner.Document.Read().Content!.Document.Entities.Single(item => item.Id == DocumentData.Child).Changes.RemovedComponents);
        model.Refresh(InspectorFixture.Parent);
        Assert.Contains(model.RemovedComponents, item => item.Id == DocumentData.Interaction && !item.LocalMask);
        model.RestoreComponent(DocumentData.Interaction);
        var restored = test.Owner.Document.Read().Content!;
        Assert.Contains(DocumentData.Interaction, restored.Document.Entities.Single(item => item.Id == DocumentData.Parent).Changes.RestoredComponents);
        Assert.Contains(model.Components, item => item.Id == DocumentData.Interaction);
        Assert.Equal(3, restored.UndoCount); Assert.True(test.Property(DocumentData.Interaction, "enabled").BooleanValue);
    }

    [Fact]
    public void ResetLocalRemoval_UnmasksInheritedComponentAndLeavesOtherAncestorMaskEffective()
    {
        using var test = new InspectorFixture(); test.Model.Refresh(InspectorFixture.Parent);
        Assert.Contains(test.Model.RemovedComponents, item => item.Id == DocumentData.Appearance && item.LocalMask);
        test.Model.ResetRemoval(DocumentData.Appearance);
        Assert.Contains(test.Model.Components, item => item.Id == DocumentData.Appearance);
        Assert.Contains(test.Model.RemovedComponents, item => item.Id == DocumentData.Interaction && !item.LocalMask);
        Assert.DoesNotContain(DocumentData.Appearance, test.Owner.Document.Read().Content!.Document.Entities.Single(item => item.Id == DocumentData.Parent).Changes.RemovedComponents);
        Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount); Assert.Empty(test.Model.ErrorText);
        test.Model.Undo();
        Assert.DoesNotContain(test.Model.Components, item => item.Id == DocumentData.Appearance);
        Assert.Contains(test.Model.RemovedComponents, item => item.Id == DocumentData.Appearance && item.LocalMask);
        Assert.False(test.Owner.Document.Read().IsDirty);
    }

    [Fact]
    public void MissingDependency_DisablesSchemaAdditionUntilTransformIsExplicitlyAdded()
    {
        using var test = new InspectorFixture(InspectorFixture.WithIndependentEntity(false));
        test.Model.Refresh(DocumentData.Id(3).ToString("D"));
        Assert.False(test.Model.AddableTypes.Single(item => item.Schema.TypeId == BuiltInComponents.AppearanceId).CanAdd);
        Assert.False(test.Model.AddableTypes.Single(item => item.Schema.TypeId == DocumentData.CustomType).CanAdd);
        Assert.True(test.Model.AddableTypes.Single(item => item.Schema.TypeId == BuiltInComponents.TransformId).CanAdd);
        var before = test.Owner.Document.Read().Content;
        test.Model.AddComponent(BuiltInComponents.AppearanceId, 1);
        Assert.Same(before, test.Owner.Document.Read().Content); Assert.NotEmpty(test.Model.ErrorText);
        test.Model.AddComponent(BuiltInComponents.TransformId, 1);
        Assert.True(test.Model.CanEdit); Assert.Equal(2, test.Model.Components.Count);
        Assert.Equal(9, test.Model.Components.Single(item => item.TypeId == BuiltInComponents.TransformId).Properties.Count);
        Assert.True(test.Model.AddableTypes.Single(item => item.Schema.TypeId == BuiltInComponents.AppearanceId).CanAdd);
        Assert.Equal("independent", Assert.Single(test.Model.Components.Single(item => item.TypeId != BuiltInComponents.TransformId).Properties).Value);
        Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount); Assert.Empty(test.Model.ErrorText);
    }

    [Fact]
    public void StructuralAction_DraftGateAndInvalidDependencyNeverDiscardTextOrPublishInvalidEntity()
    {
        using var test = new InspectorFixture(); var before = test.Owner.Document.Read().Content!;
        var field = test.Property(DocumentData.Custom, "text"); field.Value = "keep my draft";
        test.Model.RemoveComponent(DocumentData.Local);
        test.Model.AddComponent(DocumentData.CustomType, 2);
        Assert.Same(before, test.Owner.Document.Read().Content); Assert.Equal("keep my draft", field.Value);
        Assert.True(test.Model.HasDraft); Assert.False(test.Model.CanChangeComponents);
        test.Model.Cancel();
        test.Model.RemoveComponent(DocumentData.Transform);
        Assert.Same(before, test.Owner.Document.Read().Content);
        Assert.Equal(0, before.UndoCount); Assert.NotEmpty(test.Model.ErrorText);
        Assert.Equal("4", test.Property(DocumentData.Transform, "x").Value);
        Assert.Equal(5, test.Model.Components.Count); Assert.False(test.Model.HasDraft);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StructuralAction_HeadlessCommitOrSameIdentityReplacementRejectsStaleActionUntilFreshAttempt(bool replacement)
    {
        using var test = new InspectorFixture(); var before = test.Owner.Document.Read().Content!;
        if (replacement)
        {
            test.Owner.Store.File = new(DocumentData.Scene(), new("replacement"));
            test.Owner.Run(DocumentCommand.Open, "replacement.aetherscene");
        }
        else Assert.True(test.Owner.Document.Apply(DocumentData.Edit(7, 57)).Succeeded);
        var current = test.Owner.Document.Read().Content!;
        test.Model.RemoveComponent(DocumentData.Local);
        Assert.Same(current, test.Owner.Document.Read().Content); Assert.NotEmpty(test.Model.ErrorText);
        Assert.Contains(test.Model.Components, item => item.Id == DocumentData.Local);
        Assert.NotSame(before.Document, current.Document);
        test.Model.RemoveComponent(DocumentData.Local);
        Assert.DoesNotContain(test.Model.Components, item => item.Id == DocumentData.Local);
        Assert.Equal(current.UndoCount + 1, test.Owner.Document.Read().Content!.UndoCount); Assert.Empty(test.Model.ErrorText);
    }

    [Fact]
    public void CommittedGenericChanges_UpdatePreviewWhileUnfinishedDraftNeverBecomesPresented()
    {
        using var test = new InspectorFixture(); var model = test.Model;
        model.ShowPreview(); DocumentUiFixture.Wait(test.Preview.Pending); test.Refresh();
        Assert.Equal(new long[] { 7 }, test.Shown.Select(item => item.Revision));
        test.Property(DocumentData.Custom, "text").Value = "uncommitted";
        model.ShowPreview(); DocumentUiFixture.Wait(test.Preview.Pending);
        Assert.Equal(DocumentData.Text, test.Shown.Last().Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["text"].Value.GetString());
        model.Apply(); DocumentUiFixture.Wait(test.Preview.Pending); test.Refresh();
        Assert.Equal("uncommitted", test.Shown.Last().Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["text"].Value.GetString());
        Assert.Equal(8, test.Preview.Read().Presented!.Revision);
        model.RemoveComponent(DocumentData.Local); DocumentUiFixture.Wait(test.Preview.Pending); test.Refresh();
        Assert.False(test.Shown.Last().Entities[DocumentData.Child].Components.ContainsKey(DocumentData.Local));
        Assert.Equal(new long[] { 7, 7, 8, 9 }, test.Shown.Select(item => item.Revision));
        Assert.Equal(2, test.Owner.Document.Read().Content!.UndoCount);
        DocumentUiFixture.Wait(model.StopPreviewAsync()); test.Refresh();
        Assert.False(test.Preview.Read().Active); Assert.False(model.HasDraft);
    }
}
