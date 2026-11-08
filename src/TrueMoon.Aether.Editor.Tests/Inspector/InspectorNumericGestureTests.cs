using System.Globalization;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class InspectorNumericGestureTests
{
    [Fact]
    public void GroupReset_DuringCapturedDragIsDisabledAndCannotBeOverwrittenByLateCancellation()
    {
        using var test = new NumericInspectorFixture(); test.Owner.Model.ScaleLinked = true;
        test.Field("sx").Value = "4"; test.Owner.Model.Fields[0].Value = "2"; test.Drain();
        Assert.True(test.Transform.ResetScale.IsEnabled); Assert.True(test.Transform.ResetPosition.IsEnabled);
        var box = test.Transform.Scale.First; var start = test.Start(box); test.Drain();
        Assert.True(box.IsDragging); Assert.False(test.Transform.ResetScale.IsEnabled); Assert.False(test.Transform.ResetPosition.IsEnabled);
        test.Owner.Model.ResetTransformGroup(TransformGroup.Scale);
        Assert.False(test.Field("sx").IsReset); Assert.True(box.IsDragging);
        test.Ui.HandleInput(new(InputKind.PointerMove, start.X + 4, start.Y));
        test.Ui.HandleInput(new(InputKind.PointerUp, start.X + 4, start.Y)); test.Drain();
        Assert.False(box.IsDragging); Assert.True(test.Transform.ResetScale.IsEnabled);
        test.Click(test.Transform.ResetScale);
        Assert.All(test.Fields, field => Assert.True(field.IsReset));
        box.CancelGesture(); test.Drain(); Assert.All(test.Fields, field => Assert.True(field.IsReset));
        Assert.Equal(0, test.Owner.Owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void Inspector_UsesDeclaredUnitsTypedIntegersAndOneBorrowedOptInLinkToggle()
    {
        using var owner = new InspectorFixture(); var view = new EditorInspectorView(owner.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view); ui.Update(); ui.Update();
        var transform = view.TransformEditors[DocumentData.Transform];
        Assert.False(owner.Model.ScaleLinked); Assert.False(transform.LinkScale.IsChecked); Assert.True(transform.LinkScale.IsEnabled);
        Assert.Equal(new Element[] { transform.Position.Parent!, transform.Rotation.Parent!, transform.Degrees, transform.Scale.Parent!, transform.LinkScale }, transform.Children);
        Assert.Equal(new Element[] { transform.Position, transform.ResetPosition }, transform.Position.Parent!.Children);
        Assert.Equal(new Element[] { transform.Rotation, transform.ResetRotation }, transform.Rotation.Parent!.Children);
        Assert.Equal(new Element[] { transform.Scale, transform.ResetScale }, transform.Scale.Parent!.Children);
        Assert.Equal(.1, transform.Position.First.Step); Assert.Equal(.01, transform.Rotation.First.Step); Assert.Equal(.01, transform.Scale.First.Step);
        Assert.Equal(float.Epsilon, transform.Scale.First.Minimum); Assert.Equal(float.MaxValue, transform.Position.First.Maximum);
        var integer = Assert.IsType<NumberBox>(view.GenericEditors[(DocumentData.Custom, "integer")].Editor);
        Assert.Equal(NumericValueKind.Integer, integer.NumericKind); Assert.Equal(1, integer.Step); Assert.True(integer.AdjustmentEnabled);
        Assert.True(transform.Position.First.AdjustmentEnabled); Assert.True(transform.Rotation.First.AdjustmentEnabled); Assert.True(transform.Scale.First.AdjustmentEnabled);
        owner.Model.ScaleLinked = true; ui.Update(); ui.Update(); Assert.True(transform.LinkScale.IsChecked);
        Assert.Equal(0, owner.Owner.Document.Read().Content!.UndoCount); Assert.False(owner.Model.HasDraft);
        var tree = DocumentUiFixture.Tree(view).ToArray(); ui.SetTheme(Theme.Light); view.WithViewStyle(new EditorInspectorViewStyle()); ui.Update();
        Assert.Equal(tree, DocumentUiFixture.Tree(view)); Assert.True(transform.LinkScale.IsChecked); Assert.Equal(.01, transform.Scale.First.Step);
    }

    [Theory]
    [InlineData("sx", "4")] [InlineData("sy", "6")] [InlineData("sz", "10")]
    public void LinkedTyping_PreservesNontrivialRatiosFromEachSourceAxisAndAppliesOneHistoryEntry(string key, string value)
    {
        using var test = new NumericInspectorFixture();
        Assert.False(test.Owner.Model.ScaleLinked); test.Field(key).Value = value;
        Assert.Equal(value, test.Field(key).Value);
        Assert.Equal(key == "sx" ? "4" : "2", test.Field("sx").Value);
        test.Owner.Model.Cancel(); test.Owner.Model.ScaleLinked = true; test.Field(key).Value = value;
        test.AssertScales(4, 6, 10); Assert.True(test.Owner.Model.CanApply); Assert.Equal(0, test.Owner.Owner.Document.Read().Content!.UndoCount);
        test.Owner.Model.Apply(); Assert.Equal(1, test.Owner.Owner.Document.Read().Content!.UndoCount);
        var committed = test.Owner.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Transform];
        Assert.Equal(4, committed.Properties["sx"].Value.GetNumber()); Assert.Equal(6, committed.Properties["sy"].Value.GetNumber()); Assert.Equal(10, committed.Properties["sz"].Value.GetNumber());
        test.Owner.Model.Undo(); test.AssertScales(2, 3, 5); Assert.False(test.Owner.Owner.Document.Read().IsDirty);
        test.Owner.Model.Redo(); test.AssertScales(4, 6, 10); Assert.Equal(1, test.Owner.Owner.Document.Read().Content!.UndoCount);
    }

    [Theory]
    [InlineData("0")] [InlineData("-")] [InlineData("NaN")] [InlineData("1e-50")] [InlineData("1e40")]
    public void InvalidLinkedSource_RemainsLocalAndNeverChangesTargetsOrCommits(string value)
    {
        using var test = new NumericInspectorFixture(); test.Owner.Model.ScaleLinked = true; var before = test.Owner.Owner.Document.Read().Content;
        test.Field("sx").Value = value;
        Assert.Equal(value, test.Field("sx").Value); Assert.Equal("3", test.Field("sy").Value); Assert.Equal("5", test.Field("sz").Value);
        Assert.True(test.Field("sx").HasError); Assert.False(test.Owner.Model.CanApply); Assert.True(test.Owner.Model.HasDiagnostics);
        test.Owner.Model.Apply(); Assert.Same(before, test.Owner.Owner.Document.Read().Content); Assert.Equal(0, before!.UndoCount);
        test.Field("sx").Value = "4"; test.AssertScales(4, 6, 10); Assert.True(test.Owner.Model.CanApply);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LinkedTargetOverflowOrPendingReset_IsValidatedBeforeAnyTargetWrite(bool pendingReset)
    {
        using var test = new NumericInspectorFixture(pendingReset ? ScaleScene() : ScaleScene(1, 2, 1e10));
        Assert.True(test.Owner.Model.HasSelection); Assert.NotNull(test.Owner.Owner.Document.Read().Content);
        if (pendingReset) { test.Field("sy").Value = "03.0"; test.Owner.Model.ResetProperty(test.Field("sy")); Assert.True(test.Field("sy").IsReset); }
        test.Owner.Model.ScaleLinked = true; var second = test.Field("sy").CaptureDraft(); var third = test.Field("sz").CaptureDraft();
        var targetChanges = 0; test.Field("sy").PropertyChanged += (_, change) => { if (change.PropertyName == nameof(InspectorProperty.Value)) targetChanges++; };
        test.Field("sx").Value = pendingReset ? "4" : "1e30";
        Assert.Equal(pendingReset ? "4" : "1e30", test.Field("sx").Value); Assert.Equal(second, test.Field("sy").CaptureDraft()); Assert.Equal(third, test.Field("sz").CaptureDraft());
        Assert.Equal(0, targetChanges); Assert.False(test.Owner.Model.CanApply); Assert.True(test.Field("sx").HasError);
        Assert.Contains("Связанный масштаб", test.Owner.Model.DiagnosticsText); Assert.Equal(0, test.Owner.Owner.Document.Read().Content!.UndoCount);
        test.Owner.Model.Cancel(); Assert.False(test.Owner.Model.HasDraft); Assert.False(test.Owner.Model.HasDiagnostics);
    }

    [Fact]
    public void InvalidPeerDraft_IsNeverOverwrittenFromCachedRatiosWhenAnotherAxisChanges()
    {
        using var test = new NumericInspectorFixture(); test.Owner.Model.ScaleLinked = true;
        var before = test.Owner.Owner.Document.Read().Content; test.Field("sy").Value = "-"; test.Field("sx").Value = "4";
        Assert.Equal("4", test.Field("sx").Value); Assert.Equal("-", test.Field("sy").Value); Assert.Equal("5", test.Field("sz").Value);
        Assert.True(test.Field("sy").HasError); Assert.True(test.Field("sx").HasError); Assert.False(test.Owner.Model.CanApply);
        Assert.Contains("Связанный масштаб", test.Owner.Model.DiagnosticsText); Assert.Same(before, test.Owner.Owner.Document.Read().Content); Assert.Equal(0, before!.UndoCount);
        test.Owner.Model.Apply(); Assert.Same(before, test.Owner.Owner.Document.Read().Content);
        test.Owner.Model.Cancel(); test.Drain(); test.AssertScales(2, 3, 5);
        Assert.False(test.Owner.Model.HasDraft); Assert.False(test.Owner.Model.HasDiagnostics); Assert.Equal(0, test.Owner.Owner.Document.Read().Content!.UndoCount);
    }

    [Theory]
    [InlineData("typing")] [InlineData("step")] [InlineData("drag")]
    public void ActualTypingStepAndDrag_PropagateThroughTypedToggleWithoutAutoApply(string operation)
    {
        using var test = new NumericInspectorFixture(); test.Click(test.Transform.LinkScale); Assert.True(test.Owner.Model.ScaleLinked);
        var box = test.Transform.Scale.First; var before = test.Owner.Owner.Document.Read().Content;
        if (operation == "typing") test.Enter(box.Editor, "4");
        else if (operation == "step") { test.Ui.Focus(box.Editor); Assert.True(test.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Up)).Handled); test.Drain(); }
        else
        {
            var start = test.Start(box); Assert.True(test.Ui.HandleInput(new(InputKind.PointerMove, start.X + 800, start.Y)).PointerCaptured); test.Drain();
            Assert.True(box.IsDragging); Assert.False(test.Ui.HandleInput(new(InputKind.PointerUp, start.X + 800, start.Y)).PointerCaptured);
        }
        test.Drain(); test.AssertScales(operation == "step" ? 2.01 : 4, operation == "step" ? 3.015 : 6, operation == "step" ? 5.025 : 10);
        if (operation == "step") Assert.Equal(new[] { "2.01", "3.015", "5.025" }, test.Fields.Select(item => item.Value));
        Assert.Same(before, test.Owner.Owner.Document.Read().Content); Assert.Equal(0, before!.UndoCount); Assert.True(test.View.Apply.IsEnabled);
        Assert.True(test.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true)).Handled); test.Drain();
        Assert.Equal(1, test.Owner.Owner.Document.Read().Content!.UndoCount); Assert.False(test.Owner.Model.HasDraft); Assert.False(box.IsDragging);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EscapeDuringLinkedDrag_RestoresExactThreeAxisDraftStatesInsteadOfGlobalCancel(bool pendingReset)
    {
        using var test = new NumericInspectorFixture();
        test.Field("sx").Value = "02.0"; test.Field("sy").Value = "03.0"; test.Field("sz").Value = "05.0";
        if (pendingReset) test.Owner.Model.ResetProperty(test.Field("sy"));
        test.Owner.Model.ScaleLinked = true; test.Drain(); var drafts = test.Fields.Select(field => field.CaptureDraft()).ToArray();
        var box = test.Transform.Scale.First; var results = new List<NumericGestureResult>(); box.GestureEnded += results.Add;
        var start = test.Start(box); test.Ui.HandleInput(new(InputKind.PointerMove, start.X + 8, start.Y)); test.Drain();
        Assert.Equal("2.02", box.Editor.Value); Assert.True(box.IsDragging);
        if (!pendingReset)
        {
            test.Ui.HandleInput(new(InputKind.PointerMove, start.X + 400, start.Y)); test.Drain(); test.AssertScales(3, 4.5, 7.5);
            test.Ui.HandleInput(new(InputKind.PointerMove, start.X, start.Y)); test.Drain();
            Assert.Equal(drafts, test.Fields.Select(field => field.CaptureDraft())); Assert.True(box.IsDragging);
            test.Ui.HandleInput(new(InputKind.PointerMove, start.X + 8, start.Y)); test.Drain(); Assert.Equal("2.02", box.Editor.Value);
        }
        var escaped = test.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); test.Drain();
        Assert.True(escaped.Handled); Assert.False(escaped.PointerCaptured); Assert.False(box.IsDragging);
        Assert.Equal(drafts, test.Fields.Select(field => field.CaptureDraft())); Assert.Equal(new[] { "02.0", "03.0", "05.0" }, test.Fields.Select(field => field.Value));
        Assert.True(test.Owner.Model.HasDraft); Assert.Equal(pendingReset, test.Field("sy").IsReset); Assert.Equal(0, test.Owner.Owner.Document.Read().Content!.UndoCount);
        Assert.Equal(new[] { NumericGestureResult.Cancelled }, results);
    }

    [Fact]
    public void CtrlEnterDuringDrag_CompletesCaptureAndExplicitlyAppliesExactlyOneTransaction()
    {
        using var test = new NumericInspectorFixture(); test.Owner.Model.ScaleLinked = true; test.Drain();
        var box = test.Transform.Scale.First; var results = new List<NumericGestureResult>(); box.GestureEnded += results.Add;
        var start = test.Start(box); test.Ui.HandleInput(new(InputKind.PointerMove, start.X + 800, start.Y)); test.Drain();
        Assert.Equal(0, test.Owner.Owner.Document.Read().Content!.UndoCount);
        var apply = test.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true)); test.Drain();
        Assert.True(apply.Handled); Assert.False(apply.PointerCaptured); Assert.False(box.IsDragging); Assert.False(test.Owner.Model.HasDraft);
        Assert.Equal(new[] { NumericGestureResult.Completed }, results); Assert.Equal(1, test.Owner.Owner.Document.Read().Content!.UndoCount);
        test.AssertScales(4, 6, 10); test.Ui.HandleInput(new(InputKind.PointerUp, start.X, start.Y)); Assert.Equal(1, test.Owner.Owner.Document.Read().Content!.UndoCount);
    }

    [Theory]
    [InlineData("selection")] [InlineData("revision")] [InlineData("admission")] [InlineData("detach")]
    public void GestureContextChanges_FenceOldRollbackAndCaptureWithoutAddingHistory(string change)
    {
        using var test = new NumericInspectorFixture(); test.Field("sx").Value = "02.0"; test.Drain();
        var box = test.Transform.Scale.First; var results = new List<NumericGestureResult>(); box.GestureEnded += results.Add;
        var start = test.Start(box); test.Ui.HandleInput(new(InputKind.PointerMove, start.X + 4, start.Y)); test.Drain();
        Assert.Equal("2.01", box.Editor.Value);
        switch (change)
        {
            case "selection": test.Owner.Model.Refresh(InspectorFixture.Parent); break;
            case "revision":
                var baseline = test.Owner.Owner.Document.Read().Content!.Document;
                Assert.True(test.Owner.Owner.Commands.EditComponents(baseline, new(new(Guid.NewGuid(), baseline.Revision, "external"), [new(DocumentData.Child, DocumentData.Transform, "sx", PropertyValue.From(9d))])).Succeeded);
                test.Owner.Refresh(); break;
            case "admission": test.Owner.Owner.RuntimeIdle = false; test.Owner.Refresh(); break;
            case "detach": test.Root.Items.Remove(test.View); break;
        }
        test.Drain(); Assert.False(box.IsDragging); Assert.False(test.Ui.HandleInput(new(InputKind.PointerUp, start.X, start.Y)).PointerCaptured); Assert.Single(results);
        Assert.Equal(change == "revision" ? 1 : 0, test.Owner.Owner.Document.Read().Content!.UndoCount);
        Assert.Equal(change == "selection" ? "2" : change == "revision" ? "2.01" : "02.0", test.Field("sx").Value);
        if (change is "selection" or "revision") Assert.Equal(NumericGestureResult.Interrupted, results[0]);
        else Assert.Equal(NumericGestureResult.Cancelled, results[0]);
        if (change == "selection") Assert.False(test.Owner.Model.HasDraft);
        if (change == "revision") { Assert.False(test.Owner.Model.CanApply); test.Owner.Model.Cancel(); Assert.Equal("9", test.Field("sx").Value); }
        if (change == "admission") { Assert.False(box.Editor.IsEnabled); Assert.True(test.Owner.Model.HasDraft); }
    }

    [Fact]
    public void DisposalDuringLinkedDrag_RollsBackOwnedGestureAndLeavesBorrowedModelUsable()
    {
        using var test = new NumericInspectorFixture(); test.Field("sx").Value = "02.0"; test.Owner.Model.ScaleLinked = true; test.Drain();
        var box = test.Transform.Scale.First; var start = test.Start(box); test.Ui.HandleInput(new(InputKind.PointerMove, start.X + 8, start.Y)); test.Drain();
        test.Root.Items.Remove(test.View); test.View.Dispose(); Assert.True(box.IsDisposed); Assert.False(box.IsDragging);
        Assert.Equal("02.0", test.Field("sx").Value); test.Owner.Model.ScaleLinked = false; test.Field("sx").Value = "4"; test.Owner.Model.Apply();
        Assert.Equal(1, test.Owner.Owner.Document.Read().Content!.UndoCount); Assert.Equal(0, test.Owner.Owner.Authoring.Products[0].Disposals);
        Assert.False(test.Ui.HandleInput(new(InputKind.PointerMove, 1000, 700)).PointerCaptured); Assert.False(test.Root.IsDisposed);
    }

    [Fact]
    public void SharedModelTwoViews_OldGestureCannotRollbackOrEndNewGestureOnSameProperty()
    {
        using var test = new NumericInspectorFixture(); test.Owner.Model.ScaleLinked = true; test.Drain();
        var second = new EditorInspectorView(test.Owner.Model, new EditorInspectorViewStyle()); using var secondUi = InspectorFixture.Ui(second); secondUi.Update(); secondUi.Update();
        var firstBox = test.Transform.Scale.First; var secondBox = second.TransformEditors[DocumentData.Transform].Scale.First;
        var firstResults = new List<NumericGestureResult>(); var secondResults = new List<NumericGestureResult>();
        firstBox.GestureEnded += firstResults.Add; secondBox.GestureEnded += secondResults.Add;
        var firstStart = test.Start(firstBox); test.Ui.HandleInput(new(InputKind.PointerMove, firstStart.X + 4, firstStart.Y)); test.Drain();
        secondUi.Update(); secondUi.Update(); Assert.Equal("2.01", secondBox.Editor.Value);
        var secondBaseline = test.Fields.Select(field => field.CaptureDraft()).ToArray();
        var bounds = secondBox.DragBounds; var secondX = bounds.X + bounds.Width / 2; var secondY = bounds.Y + bounds.Height / 2;
        Assert.True(secondUi.HandleInput(new(InputKind.PointerDown, secondX, secondY)).PointerCaptured);
        Assert.True(firstBox.IsDragging); Assert.True(secondBox.IsDragging);
        firstBox.CancelGesture(); Assert.False(firstBox.IsDragging); Assert.Equal(new[] { NumericGestureResult.Interrupted }, firstResults);
        Assert.Equal(secondBaseline, test.Fields.Select(field => field.CaptureDraft()));
        Assert.True(secondUi.HandleInput(new(InputKind.PointerMove, secondX + 4, secondY)).PointerCaptured); secondUi.Update(); secondUi.Update();
        Assert.True(secondBox.IsDragging); Assert.Equal("2.02", test.Field("sx").Value);
        secondBox.CancelGesture(); secondUi.Update(); secondUi.Update(); test.Drain();
        Assert.Equal(secondBaseline, test.Fields.Select(field => field.CaptureDraft())); Assert.Equal("2.01", test.Field("sx").Value);
        Assert.Equal(new[] { NumericGestureResult.Cancelled }, secondResults); Assert.Equal(0, test.Owner.Owner.Document.Read().Content!.UndoCount);
        Assert.False(test.Ui.HandleInput(new(InputKind.PointerUp, firstStart.X, firstStart.Y)).PointerCaptured);
        Assert.False(secondUi.HandleInput(new(InputKind.PointerUp, secondX, secondY)).PointerCaptured);
    }

    private static SceneDocument ScaleScene(double x = 2, double y = 3, double z = 5)
    {
        var definition = new EntityDefinition(DocumentData.Restored, new(addedComponents: [new(DocumentData.Transform, BuiltInComponents.TransformId, 1,
            [KeyValuePair.Create("sx", PropertyValue.From(x)), KeyValuePair.Create("sy", PropertyValue.From(y)), KeyValuePair.Create("sz", PropertyValue.From(z))])]));
        return new(new(DocumentData.Id(100)), 7, [new(DocumentData.Parent, DocumentData.Restored), new(DocumentData.Child, DocumentData.Restored, parentId: DocumentData.Parent)],
            [definition], [BuiltInComponents.Schemas.Single(schema => schema.TypeId == BuiltInComponents.TransformId)]);
    }

    private sealed class NumericInspectorFixture : IDisposable
    {
        public InspectorFixture Owner { get; }
        public EditorInspectorView View { get; }
        public Panel Root { get; } = new();
        public UiSession Ui { get; }
        public TransformEditor Transform => View.TransformEditors[DocumentData.Transform];
        public InspectorProperty[] Fields => new[] { Field("sx"), Field("sy"), Field("sz") };
        public NumericInspectorFixture(SceneDocument? scene = null)
        { Owner = new(scene ?? ScaleScene()); View = new(Owner.Model, new EditorInspectorViewStyle()); Root.Add(View); Ui = InspectorFixture.Ui(Root); Drain(); }
        public InspectorProperty Field(string key) => Owner.Property(DocumentData.Transform, key);
        public void Drain() { Ui.Update(); Ui.Update(); }
        public void AssertScales(double x, double y, double z)
        {
            var values = Fields.Select(field => double.Parse(field.Value, CultureInfo.InvariantCulture)).ToArray();
            Assert.Equal(x, values[0], 6); Assert.Equal(y, values[1], 6); Assert.Equal(z, values[2], 6);
        }
        public void Click(Button button) { Drain(); View.Scroll.BringIntoView(button.Bounds); Drain(); DocumentUiFixture.Click(Ui, button); Drain(); }
        public void Enter(TextBox input, string value)
        { Drain(); View.Scroll.BringIntoView(input.Bounds); Drain(); Ui.Focus(input); Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true)); Ui.HandleInput(new(InputKind.Text, Text: value)); Drain(); }
        public (float X, float Y) Start(NumberBox box)
        {
            Drain(); View.Scroll.BringIntoView(box.Bounds); Drain(); var bounds = box.DragBounds;
            var point = (X: bounds.X + bounds.Width / 2, Y: bounds.Y + bounds.Height / 2);
            Assert.True(Ui.HandleInput(new(InputKind.PointerDown, point.X, point.Y)).PointerCaptured); Assert.True(box.IsDragging); return point;
        }
        public void Dispose() { Ui.Dispose(); if (!View.IsDisposed) View.Dispose(); Owner.Dispose(); }
    }
}
