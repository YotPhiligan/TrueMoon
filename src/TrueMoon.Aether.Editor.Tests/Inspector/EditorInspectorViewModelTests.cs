using System.Globalization;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class EditorInspectorViewModelTests
{
    [Fact]
    public void Selection_ProjectsOnlyEffectiveComponentsWithExactSchemasKindsAndProvenance()
    {
        using var test = new InspectorFixture(); var model = test.Model;
        Assert.True(model.HasSelection); Assert.True(model.CanEditProperties); Assert.True(model.CanChangeComponents);
        Assert.Equal(new[] { DocumentData.Transform, DocumentData.Appearance, DocumentData.Interaction, DocumentData.Custom, DocumentData.Local }.OrderBy(id => id.Value),
            model.Components.Select(item => item.Id).OrderBy(id => id.Value));
        var custom = model.Components.Single(item => item.Id == DocumentData.Custom);
        Assert.Equal(DocumentData.CustomType, custom.TypeId); Assert.Equal(2, custom.SchemaVersion);
        Assert.Equal(new[] { "flag", "integer", "nullable", "number", "text" }, custom.Properties.Select(item => item.Key).Order());
        Assert.Equal(PropertyValueKind.Boolean, test.Property(DocumentData.Custom, "flag").Kind);
        Assert.False(test.Property(DocumentData.Custom, "flag").BooleanValue);
        Assert.Equal(long.MaxValue.ToString(CultureInfo.InvariantCulture), test.Property(DocumentData.Custom, "integer").Value);
        Assert.True(test.Property(DocumentData.Custom, "nullable").IsNull);
        Assert.True(test.Property(DocumentData.Custom, "nullable").IsNullable);
        Assert.False(test.Property(DocumentData.Custom, "text").IsNullable);
        Assert.Equal(DocumentData.Text, test.Property(DocumentData.Custom, "text").Value);
        Assert.True(test.Property(DocumentData.Custom, "nullable").CanReset);
        Assert.False(test.Property(DocumentData.Custom, "flag").CanReset);
        Assert.Equal("Наследуется · a846e4a2 r19", custom.SourceText);
        Assert.Equal("Наследуется · a846e4a2 r21", test.Property(DocumentData.Custom, "flag").SourceText);
        Assert.Equal("Локальное значение", test.Property(DocumentData.Custom, "nullable").SourceText);
        Assert.Equal("По умолчанию из схемы", test.Property(DocumentData.Local, "integer").SourceText);

        model.Refresh(InspectorFixture.Parent);
        Assert.Equal(new[] { DocumentData.Transform, DocumentData.Custom }.OrderBy(id => id.Value),
            model.Components.Select(item => item.Id).OrderBy(id => id.Value));
        Assert.Contains(model.RemovedComponents, item => item.Id == DocumentData.Appearance && item.LocalMask);
        Assert.Contains(model.RemovedComponents, item => item.Id == DocumentData.Interaction && !item.LocalMask);
        Assert.False(model.HasDraft); Assert.False(model.CanApply);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IndependentOrEmptyEntity_DoesNotInventTransformAndUnknownSelectionClearsEditors(bool empty)
    {
        using var test = new InspectorFixture(InspectorFixture.WithIndependentEntity(empty));
        test.Model.Refresh(DocumentData.Id(3).ToString("D"));
        Assert.True(test.Model.HasSelection); Assert.True(test.Model.CanEditProperties); Assert.True(test.Model.CanChangeComponents);
        Assert.False(test.Model.CanEdit);
        if (empty) Assert.Empty(test.Model.Components);
        else
        {
            var component = Assert.Single(test.Model.Components); var field = Assert.Single(component.Properties);
            Assert.Equal("note", field.Key); Assert.Equal("independent", field.Value); Assert.Equal(PropertyValueKind.String, field.Kind);
        }
        Assert.DoesNotContain(test.Model.Components, item => item.TypeId == BuiltInComponents.TransformId);
        test.Model.Refresh("missing");
        Assert.False(test.Model.HasSelection); Assert.Empty(test.Model.Components); Assert.Empty(test.Model.RemovedComponents);
        Assert.False(test.Model.CanApply); Assert.False(test.Model.CanChangeComponents);
        test.Model.Refresh(null); Assert.Empty(test.Model.Components); Assert.False(test.Model.HasDraft);
    }

    [Fact]
    public void MixedScalarDraft_CommitsOnlyChangedPropertiesAtomicallyAndUndoRestoresInheritedValues()
    {
        using var test = new InspectorFixture(); var model = test.Model;
        test.Property(DocumentData.Transform, "x").Value = "12.5";
        test.Property(DocumentData.Custom, "flag").BooleanValue = true;
        test.Property(DocumentData.Custom, "integer").Value = long.MinValue.ToString(CultureInfo.InvariantCulture);
        test.Property(DocumentData.Custom, "number").Value = "-1.25e2";
        test.Property(DocumentData.Custom, "nullable").IsNull = false;
        test.Property(DocumentData.Custom, "nullable").Value = "";
        const string text = "edited\nМир 🛰️";
        test.Property(DocumentData.Custom, "text").Value = text;
        Assert.True(model.HasDraft); Assert.True(model.CanApply); Assert.False(model.CanChangeComponents); Assert.False(model.CanUndo);
        model.Apply();
        var committed = test.Owner.Document.Read().Content!; var custom = committed.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom];
        Assert.Equal(8, committed.Document.Revision); Assert.Equal(1, committed.UndoCount); Assert.Equal(0, committed.RedoCount);
        Assert.Equal(12.5d, DocumentData.X(committed));
        Assert.True(custom.Properties["flag"].Value.GetBoolean());
        Assert.Equal(long.MinValue, custom.Properties["integer"].Value.GetInteger());
        Assert.Equal(-125d, custom.Properties["number"].Value.GetNumber());
        Assert.Equal("", custom.Properties["nullable"].Value.GetString()); Assert.Equal(text, custom.Properties["text"].Value.GetString());
        var patch = committed.Document.Entities.Single(item => item.Id == DocumentData.Child).Changes;
        Assert.DoesNotContain(patch.PropertyOverrides, item => item.ComponentId == DocumentData.Transform && item.PropertyId == "y");
        Assert.False(model.HasDraft); Assert.True(model.CanUndo); Assert.Empty(model.ErrorText);
        model.Undo();
        Assert.False(test.Owner.Document.Read().IsDirty); Assert.Equal(4d, DocumentData.X(test.Owner.Document.Read().Content!));
        Assert.False(test.Property(DocumentData.Custom, "flag").BooleanValue); Assert.True(test.Property(DocumentData.Custom, "nullable").IsNull);
        Assert.Equal(DocumentData.Text, test.Property(DocumentData.Custom, "text").Value);
        model.Redo(); Assert.Equal(text, test.Property(DocumentData.Custom, "text").Value); Assert.True(test.Owner.Document.Read().IsDirty);
    }

    [Theory]
    [InlineData("integer", "9223372036854775808")]
    [InlineData("integer", "-9223372036854775809")]
    [InlineData("integer", "1.5")]
    [InlineData("number", "NaN")]
    [InlineData("number", "Infinity")]
    [InlineData("number", "1e309")]
    [InlineData("number", "1,5")]
    [InlineData("number", "")]
    [InlineData("flag", "yes")]
    public void InvalidTypedField_PreservesEveryDraftAndNeverPublishesPartialTransaction(string key, string input)
    {
        using var test = new InspectorFixture(); var before = test.Owner.Document.Read().Content!;
        test.Property(DocumentData.Transform, "x").Value = "35";
        var field = test.Property(DocumentData.Custom, key); field.Value = input;
        Assert.NotEmpty(field.Error); Assert.True(field.HasError); Assert.True(test.Model.HasDraft); Assert.False(test.Model.CanApply);
        test.Model.Apply();
        Assert.Same(before, test.Owner.Document.Read().Content); Assert.Equal(0, before.UndoCount); Assert.False(test.Owner.Document.Read().IsDirty);
        Assert.Equal(input, field.Value); Assert.Equal("35", test.Property(DocumentData.Transform, "x").Value);
        test.Model.Cancel(); Assert.Empty(field.Error); Assert.False(test.Model.HasDraft); Assert.Equal("4", test.Property(DocumentData.Transform, "x").Value);
    }

    [Theory]
    [InlineData(4096, true)]
    [InlineData(4097, false)]
    public void StringLength_ExactMaximumAndImmediatelyAdjacentValueAreDistinguished(int length, bool accepted)
    {
        using var test = new InspectorFixture(); var text = new string('Ж', length); var field = test.Property(DocumentData.Custom, "text");
        field.Value = text; Assert.Equal(accepted, test.Model.CanApply); Assert.Equal(!accepted, field.HasError);
        var before = test.Owner.Document.Read().Content; test.Model.Apply();
        if (accepted)
        {
            Assert.Equal(text, test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["text"].Value.GetString());
            Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        }
        else { Assert.Same(before, test.Owner.Document.Read().Content); Assert.Equal(text, field.Value); }
    }

    [Fact]
    public void GenericNumber_AcceptsFiniteDoubleRangeWithoutApplyingTransformFloatLimits()
    {
        using var test = new InspectorFixture(); var field = test.Property(DocumentData.Custom, "number");
        field.Value = double.MaxValue.ToString("R", CultureInfo.InvariantCulture);
        Assert.True(test.Model.CanApply); Assert.Empty(field.Error); test.Model.Apply();
        Assert.Equal(double.MaxValue, test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["number"].Value.GetNumber());
        Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount); Assert.Equal(4d, DocumentData.X(test.Owner.Document.Read().Content!));
    }

    [Fact]
    public void UntouchedMultilineString_PresentationNormalizationDoesNotRewriteCommittedLineEndings()
    {
        var source = DocumentData.Scene(); var child = source.Entities.Single(item => item.Id == DocumentData.Child);
        const string stored = "stored\r\nМир 🛰️\r\n";
        var patch = new EntityPatch(child.Changes.AddedComponents, child.Changes.RemovedComponents,
            child.Changes.PropertyOverrides.Add(new(DocumentData.Custom, "text", PropertyValue.From(stored))), child.Changes.RestoredComponents);
        var scene = new SceneDocument(source.Id, source.Revision,
            source.Entities.Select(item => item.Id == child.Id ? new EntityInstance(item.Id, item.DefinitionId, patch, item.ParentId) : item), source.Definitions, source.Schemas);
        using var test = new InspectorFixture(scene);
        Assert.Equal("stored\nМир 🛰️\n", test.Property(DocumentData.Custom, "text").Value); Assert.False(test.Model.HasDraft);
        test.Property(DocumentData.Custom, "number").Value = "2.5"; test.Model.Apply();
        var custom = test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom];
        Assert.Equal(stored, custom.Properties["text"].Value.GetString()); Assert.Equal(2.5d, custom.Properties["number"].Value.GetNumber());
        Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount); Assert.False(test.Model.HasDraft);
    }

    [Theory]
    [InlineData(0xd800)]
    [InlineData(0xdc00)]
    public void InvalidUnicodeString_IsDiagnosedBeforeCommitAndPreservesTheDraft(int codeUnit)
    {
        var invalid = new string((char)codeUnit, 1);
        using var test = new InspectorFixture(); var before = test.Owner.Document.Read().Content;
        var field = test.Property(DocumentData.Custom, "text"); field.Value = invalid;
        Assert.True(field.HasError); Assert.False(test.Model.CanApply); test.Model.Apply();
        Assert.Same(before, test.Owner.Document.Read().Content); Assert.Equal(invalid, field.Value); Assert.True(test.Model.HasDraft);
    }

    [Fact]
    public void NullAndReset_AreDifferentDraftsAndInheritedResetRemovesOnlyTheInstanceOverride()
    {
        using var test = new InspectorFixture(); var field = test.Property(DocumentData.Custom, "nullable");
        Assert.True(field.IsNull); Assert.True(field.CanReset);
        test.Model.ResetProperty(field);
        Assert.True(field.IsReset); Assert.True(field.HasDraft); Assert.True(test.Model.CanApply); Assert.False(field.CanWrite);
        test.Model.Apply();
        Assert.False(field.IsNull); Assert.Equal("inherited", field.Value); Assert.False(field.CanReset);
        var content = test.Owner.Document.Read().Content!;
        Assert.DoesNotContain(content.Document.Entities.Single(item => item.Id == DocumentData.Child).Changes.PropertyOverrides,
            item => item.ComponentId == DocumentData.Custom && item.PropertyId == "nullable");
        field.IsNull = true; Assert.True(test.Model.CanApply); test.Model.Apply();
        Assert.Equal(PropertyValueKind.Null, test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["nullable"].Value.Kind);
        Assert.Equal(2, test.Owner.Document.Read().Content!.UndoCount); Assert.True(field.CanReset);
        Assert.Throws<InvalidOperationException>(() => test.Property(DocumentData.Custom, "text").IsNull = true);
        Assert.Throws<InvalidOperationException>(() => test.Property(DocumentData.Custom, "text").BooleanValue = true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExternalCommitOrSameIdentityReplacement_PreservesDraftAndRequiresExplicitCancel(bool replacement)
    {
        using var test = new InspectorFixture(); var property = test.Property(DocumentData.Custom, "text");
        property.Value = "my draft"; var baseline = test.Owner.Document.Read().Content!.Document;
        if (replacement)
        {
            test.Owner.Store.File = new(DocumentData.Scene(), new("replacement"));
            test.Owner.Run(DocumentCommand.Open, "replacement.aetherscene");
        }
        else Assert.True(test.Owner.Document.Apply(DocumentData.Edit(7, 56)).Succeeded);
        test.Model.Apply();
        Assert.Same(property, test.Property(DocumentData.Custom, "text")); Assert.Equal("my draft", property.Value);
        Assert.True(test.Model.HasDraft); Assert.False(test.Model.CanApply); Assert.True(test.Model.CanCancel);
        Assert.NotEmpty(test.Model.ErrorText); Assert.False(test.Model.CanChangeComponents);
        Assert.NotSame(baseline, test.Owner.Document.Read().Content!.Document);
        Assert.Equal(DocumentData.Text, test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["text"].Value.GetString());
        test.Model.Cancel();
        Assert.False(test.Model.HasDraft); Assert.Empty(test.Model.ErrorText); Assert.True(test.Model.CanChangeComponents);
        Assert.Equal(DocumentData.Text, test.Property(DocumentData.Custom, "text").Value);
    }

    [Fact]
    public void UnchangedRefresh_RetainsComponentAndPropertyModelsAndPublishesNoNotifications()
    {
        using var test = new InspectorFixture(); var components = test.Model.Components; var fields = components.SelectMany(item => item.Properties).ToArray();
        var notices = 0; test.Model.PropertyChanged += (_, _) => notices++;
        foreach (var field in fields) field.PropertyChanged += (_, _) => notices++;
        test.Refresh(); test.Refresh();
        Assert.Same(components, test.Model.Components); Assert.Equal(fields, test.Model.Components.SelectMany(item => item.Properties));
        Assert.Equal(0, notices); Assert.False(test.Model.HasDraft);
    }

    [Fact]
    public void RuntimeAndDocumentClose_DisableAllEditorsAndComponentActionsWithoutCommittingDraft()
    {
        using var test = new InspectorFixture(); var before = test.Owner.Document.Read().Content!;
        test.Property(DocumentData.Custom, "text").Value = "pending";
        test.Owner.RuntimeIdle = false; test.Refresh();
        Assert.False(test.Model.CanEditProperties); Assert.False(test.Model.CanChangeComponents); Assert.False(test.Model.CanApply);
        Assert.All(test.Model.Components.SelectMany(item => item.Properties), field => { Assert.False(field.CanWrite); Assert.False(field.CanChangeNull); });
        test.Model.RemoveComponent(DocumentData.Local); test.Model.Apply();
        Assert.Same(before, test.Owner.Document.Read().Content); Assert.Equal("pending", test.Property(DocumentData.Custom, "text").Value);
        test.Owner.RuntimeIdle = true; test.Model.Cancel(); test.Owner.Run(DocumentCommand.Close); test.Refresh();
        Assert.False(test.Model.HasSelection); Assert.Empty(test.Model.Components); Assert.False(test.Model.CanEditProperties); Assert.False(test.Model.CanChangeComponents);
    }
}
