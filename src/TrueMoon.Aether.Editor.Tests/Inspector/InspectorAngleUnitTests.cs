using System.Globalization;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Scenes;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class InspectorAngleUnitTests
{
    [Theory]
    [InlineData(0d)] [InlineData(Math.PI)] [InlineData(-Math.PI / 2)]
    [InlineData(.12345678901234567)] [InlineData(1e-40)] [InlineData((double)float.MaxValue)]
    public void UnitSwitch_PreservesCanonicalBitsAndHistory(double radians)
    {
        using var test = new InspectorFixture(Scene(radians, -radians, radians / 2)); var model = test.Model;
        var before = test.Owner.Document.Read().Content!; var canonical = model.Fields.Select(field => field.Value).ToArray();
        model.ScaleLinked = true;
        for (var i = 0; i < 100; i++)
        {
            model.AnglesInDegrees = true;
            Assert.True(model.AnglesInDegrees); Assert.True(model.CanChangeAngleUnits);
            Near(radians / Math.PI * 180, Number(model.Fields[3].DisplayValue));
            Near(-radians / Math.PI * 180, Number(model.Fields[4].DisplayValue));
            Near(radians / 2 / Math.PI * 180, Number(model.Fields[5].DisplayValue));
            model.AnglesInDegrees = false;
            Assert.Equal(canonical, model.Fields.Select(field => field.Value));
            Assert.Equal(canonical, model.Fields.Select(field => field.DisplayValue));
        }
        Assert.Same(before, test.Owner.Document.Read().Content); Assert.Equal(0, before.UndoCount);
        Assert.False(model.HasDraft); Assert.False(test.Owner.Document.Read().IsDirty); Assert.True(model.ScaleLinked);
        Assert.Equal(.01, model.RotationStep); Assert.Equal("Поворот, рад", model.RotationLabel);
    }

    [Theory]
    [InlineData("pitch", "9.000e1", Math.PI / 2)]
    [InlineData("yaw", " -180 ", -Math.PI)]
    [InlineData("roll", "720", Math.PI * 4)]
    public void DegreeDraft_CommitsRadiansAndRoundTripsExactText(string key, string text, double expected)
    {
        using var test = new InspectorFixture(Scene(.2, .3, .4)); var model = test.Model;
        var field = model.Fields.Single(field => field.Key == key); var original = field.Value;
        model.AnglesInDegrees = true; field.DisplayValue = text;
        Assert.Equal(text, field.DisplayValue); Near(expected, Number(field.Value)); Assert.True(model.CanApply);
        var canonicalDraft = field.Value;
        for (var i = 0; i < 20; i++)
        {
            model.AnglesInDegrees = false; Assert.Equal(canonicalDraft, field.DisplayValue);
            model.AnglesInDegrees = true; Assert.Equal(text, field.DisplayValue); Assert.Equal(canonicalDraft, field.Value);
        }
        model.Apply(); var committed = test.Owner.Document.Read().Content!;
        Near(expected, Committed(test, key)); Assert.Equal(1, committed.UndoCount); Assert.True(model.AnglesInDegrees);
        Assert.False(model.HasDraft); model.Undo(); Near(Number(original), Committed(test, key)); Assert.False(test.Owner.Document.Read().IsDirty);
        model.Redo(); Near(expected, Committed(test, key)); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        test.Owner.Run(DocumentCommand.Save);
        var saved = Assert.Single(test.Owner.Store.Writes).Document;
        var stored = saved.Entities.Single(entity => entity.Id == DocumentData.Child).Changes.PropertyOverrides.Single(item => item.PropertyId == key);
        Near(expected, stored.Value.GetNumber());
        test.Owner.Store.File = new(saved, new("angle-save")); test.Owner.Run(DocumentCommand.Close);
        test.Owner.Run(DocumentCommand.Open, "room.aetherscene"); test.Refresh();
        Near(expected, Committed(test, key)); Assert.True(model.AnglesInDegrees); Assert.Equal(0, test.Owner.Document.Read().Content!.UndoCount);
        Assert.False(test.Owner.Document.Read().IsDirty);
    }

    [Fact]
    public void EquivalentDegreeFormatting_DoesNotCreateDraftOrLoseCanonicalPrecision()
    {
        using var test = new InspectorFixture(Scene(.12345678901234567, 0, 0)); var field = test.Model.Fields[3];
        var original = field.Value; test.Model.AnglesInDegrees = true;
        var formatted = "  " + field.DisplayValue + "e0 "; field.DisplayValue = formatted;
        Assert.Equal(formatted, field.DisplayValue); Assert.Equal(original, field.Value); Assert.False(test.Model.HasDraft);
        test.Model.AnglesInDegrees = false; Assert.Equal(original, field.DisplayValue);
        test.Model.AnglesInDegrees = true; Assert.Equal(formatted, field.DisplayValue); Assert.Equal(0, test.Owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void SignedZeroAndInvariantCulture_SurvivePresentationAndCommit()
    {
        using var test = new InspectorFixture(); var model = test.Model; var field = model.Fields[3];
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            field.Value = "-0"; var bits = BitConverter.DoubleToInt64Bits(Number(field.Value)); Assert.True(bits < 0);
            model.AnglesInDegrees = true; Assert.Equal("-0", field.DisplayValue);
            model.AnglesInDegrees = false; Assert.Equal(bits, BitConverter.DoubleToInt64Bits(Number(field.Value)));
            model.AnglesInDegrees = true; field.DisplayValue = "90.5"; Assert.True(model.CanApply);
            model.Apply(); Near(Math.PI * 90.5 / 180, Committed(test, "pitch")); Assert.DoesNotContain(',', field.Value);
            field.DisplayValue = "90,5"; Assert.True(field.HasError); Assert.False(model.CanChangeAngleUnits);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData(0, "")] [InlineData(1, "-")] [InlineData(2, "1e-")]
    [InlineData(0, "NaN")] [InlineData(1, "Infinity")] [InlineData(2, "1e50")]
    public void InvalidAngle_BlocksSwitchAndPreservesPendingResetText(int axis, string text)
    {
        using var test = new InspectorFixture(Scene(.2, .3, .4)); var model = test.Model; model.AnglesInDegrees = true;
        var before = test.Owner.Document.Read().Content; var field = model.Fields[axis + 3]; field.DisplayValue = text;
        Assert.True(field.HasError); Assert.False(model.CanChangeAngleUnits); Assert.False(model.CanApply);
        model.AnglesInDegrees = false; Assert.True(model.AnglesInDegrees); Assert.Equal(text, field.DisplayValue);
        model.ResetTransformGroup(TransformGroup.Rotation); Assert.True(field.IsReset); Assert.True(model.CanApply);
        Assert.False(model.CanChangeAngleUnits); model.AnglesInDegrees = false; Assert.True(model.AnglesInDegrees);
        Assert.Equal(text, field.DisplayValue); Assert.Same(before, test.Owner.Document.Read().Content);
        model.Apply(); Assert.True(model.CanChangeAngleUnits); Assert.True(model.AnglesInDegrees);
        Assert.False(model.HasDraft); model.AnglesInDegrees = false; Assert.False(model.AnglesInDegrees);
    }

    [Fact]
    public void UnrelatedDraftAndCancel_KeepUnitsAndExactRadianDraft()
    {
        using var test = new InspectorFixture(Scene(.2, .3, .4)); var model = test.Model;
        model.Fields[3].Value = " +1.234e-2 "; model.Fields[0].Value = "-";
        var rotation = model.Fields[3].Value; var before = test.Owner.Document.Read().Content;
        Assert.True(model.CanChangeAngleUnits); model.AnglesInDegrees = true;
        Assert.False(model.CanApply); Assert.Equal("-", model.Fields[0].Value); Assert.Equal(rotation, model.Fields[3].Value);
        model.AnglesInDegrees = false; Assert.Equal(rotation, model.Fields[3].DisplayValue);
        model.AnglesInDegrees = true; model.Cancel();
        Assert.True(model.AnglesInDegrees); Assert.True(model.CanChangeAngleUnits); Assert.False(model.HasDraft);
        Assert.Same(before, test.Owner.Document.Read().Content); Near(.2 / Math.PI * 180, Number(model.Fields[3].DisplayValue));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BusyOrConflictingState_RejectsUnitChange(bool conflict)
    {
        using var test = new InspectorFixture(); var model = test.Model;
        model.Fields[3].Value = "1";
        if (conflict) Assert.True(test.Owner.Document.Apply(DocumentData.Edit(7, 56)).Succeeded);
        else test.Owner.RuntimeIdle = false;
        var drafts = model.Fields.Select(field => field.CaptureDraft()).ToArray(); model.AnglesInDegrees = true;
        Assert.False(model.AnglesInDegrees); Assert.False(model.CanChangeAngleUnits); Assert.Equal(drafts, model.Fields.Select(field => field.CaptureDraft()));
        test.Owner.RuntimeIdle = true; model.Cancel(); Assert.True(model.CanChangeAngleUnits);
    }

    [Fact]
    public void SavingOrMissingTransform_DisablesUnitChangesWithoutLosingPreference()
    {
        using var test = new InspectorFixture(InspectorFixture.WithIndependentEntity(true)); var model = test.Model;
        model.AnglesInDegrees = true; var gate = DocumentData.Signal();
        test.Owner.Store.Write = async (_, _, _, _) => { await gate.Task; return new("saved"); };
        Assert.True(test.Owner.Commands.Execute(DocumentCommand.SaveAs, "angle-saving.aetherscene"));
        try
        {
            model.AnglesInDegrees = false; Assert.True(model.AnglesInDegrees); Assert.False(model.CanChangeAngleUnits);
        }
        finally { gate.SetResult(); test.Owner.Complete(); }
        foreach (var selected in new[] { DocumentData.Id(3).ToString("D"), "missing" })
        {
            model.Refresh(selected); Assert.False(model.CanChangeAngleUnits); model.AnglesInDegrees = false; Assert.True(model.AnglesInDegrees);
        }
        model.Refresh(InspectorFixture.Parent); Assert.True(model.CanChangeAngleUnits); Assert.True(model.AnglesInDegrees); Assert.False(model.HasDraft);
    }

    [Fact]
    public void ValidPendingReset_CanSwitchWithoutChangingFlagsOrUnrelatedGroups()
    {
        using var test = new InspectorFixture(Scene(.2, .3, .4, localAngles: true)); var model = test.Model;
        model.Fields[0].Value = "15"; model.ScaleLinked = true; model.Fields[6].Value = "2";
        model.ResetTransformGroup(TransformGroup.Rotation); var before = model.Fields.Select(field => field.CaptureDraft()).ToArray();
        model.AnglesInDegrees = true; Assert.True(model.AnglesInDegrees); Assert.Equal(before, model.Fields.Select(field => field.CaptureDraft()));
        Assert.All(model.Fields.Skip(3).Take(3), field => Assert.True(field.IsReset)); model.Apply();
        Near(.2, Committed(test, "pitch")); Near(.3, Committed(test, "yaw")); Near(.4, Committed(test, "roll"));
        Assert.Equal("15", model.Fields[0].Value); Assert.All(model.Fields.Skip(6), field => Assert.Equal("2", field.Value));
        Assert.True(model.ScaleLinked); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FloatLimits_AreAppliedToCanonicalRadians(bool negative)
    {
        using var test = new InspectorFixture(); var model = test.Model; var field = model.Fields[3]; model.AnglesInDegrees = true;
        var sign = negative ? -1d : 1d;
        field.DisplayValue = (sign * 1e40).ToString("R", CultureInfo.InvariantCulture);
        Assert.False(field.HasError); Assert.True(model.CanApply); Assert.True(model.CanChangeAngleUnits);
        Assert.True(Math.Abs(Number(field.Value)) < float.MaxValue);
        field.DisplayValue = (sign * model.RotationMaximum).ToString("R", CultureInfo.InvariantCulture);
        Assert.False(field.HasError); Assert.True(model.CanApply); Assert.True(float.IsFinite((float)Number(field.Value)));
        field.DisplayValue = (sign * model.RotationMaximum * 2).ToString("R", CultureInfo.InvariantCulture);
        Assert.True(field.HasError); Assert.False(model.CanApply); Assert.False(model.CanChangeAngleUnits);
    }

    [Theory]
    [InlineData(230)] [InlineData(330)]
    public void UnitControl_RetainsTreeFocusDraftAndTypedUnits(int width)
    {
        using var test = new UiFixture(Scene(Math.PI / 2, -Math.PI, Math.PI * 4), width);
        var field = test.Owner.Model.Fields[3]; var transform = test.Transform; var tree = DocumentUiFixture.Tree(test.View).ToArray();
        Assert.False(transform.Degrees.IsChecked); Assert.True(transform.Degrees.IsEnabled); Assert.Equal(26, transform.Degrees.Bounds.Height);
        test.Ui.Focus(transform.Degrees); test.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Space)); test.Ui.HandleInput(new(InputKind.KeyUp, Key: UiKey.Space)); test.Drain();
        Assert.True(transform.Degrees.IsChecked); Assert.True(test.Owner.Model.AnglesInDegrees);
        Assert.Equal("Поворот, °", transform.Rotation.Label.Value); Assert.Equal("90", transform.Rotation.First.Editor.Value);
        Assert.Equal("-180", transform.Rotation.Second.Editor.Value); Assert.Equal("720", transform.Rotation.Third.Editor.Value);
        Assert.Equal(1, transform.Rotation.First.Step); Assert.Equal(.1, transform.Position.First.Step); Assert.Equal(.01, transform.Scale.First.Step);
        Assert.Contains("°", transform.Rotation.First.Editor.AccessibleName); Assert.Contains("°", transform.Rotation.First.AccessibleName);
        Assert.Contains("°", test.View.Properties[(DocumentData.Transform, "pitch")].Reset.AccessibleName);
        var input = transform.Rotation.First.Editor; test.Enter(input, "9.000e1"); test.Ui.Focus(input);
        test.Ui.SetTheme(Theme.Light); test.View.WithViewStyle(new EditorInspectorViewStyle()); test.Ui.Resize(new(width + 80, 800)); test.Drain();
        Assert.True(input.IsFocused); Assert.Equal("9.000e1", input.Value); Assert.Equal(tree, DocumentUiFixture.Tree(test.View));
        Assert.False(test.Owner.Model.HasDraft); Assert.Equal(Math.PI / 2, Number(field.Value));
        test.Owner.Model.AnglesInDegrees = false; test.Drain(); Assert.Equal(.01, transform.Rotation.First.Step); Assert.Equal("Поворот, рад", transform.Rotation.Label.Value);
        test.Owner.Model.AnglesInDegrees = true; test.Drain(); Assert.Equal("9.000e1", input.Value); Assert.True(input.IsFocused);
        test.Enter(input, "-"); Assert.False(transform.Degrees.IsEnabled); Assert.Contains("Pitch, °", test.Owner.Model.DiagnosticsText);
        var before = test.Owner.Model.AnglesInDegrees; transform.Degrees.IsChecked = !before; test.Drain();
        Assert.Equal(before, transform.Degrees.IsChecked); Assert.Equal("-", input.Value);
        test.Owner.Model.Cancel(); test.Drain(); Assert.True(transform.Degrees.IsEnabled); Assert.Equal(tree, DocumentUiFixture.Tree(test.View));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DegreeDrag_CancelsExactTextOrAppliesOneRadianTransaction(bool cancel)
    {
        using var test = new UiFixture(Scene(.12345678901234567, 0, 0)); var model = test.Owner.Model;
        model.AnglesInDegrees = true; test.Drain(); var field = model.Fields[3]; var box = test.Transform.Rotation.First;
        var originalText = "  " + field.DisplayValue + "e0 "; test.Enter(box.Editor, originalText);
        var canonical = field.Value; var bounds = box.DragBounds; var x = bounds.X + bounds.Width / 2; var y = bounds.Y + bounds.Height / 2;
        Assert.True(test.Ui.HandleInput(new(InputKind.PointerDown, x, y)).PointerCaptured); test.Drain();
        Assert.True(box.IsDragging); Assert.False(test.Transform.Degrees.IsEnabled); model.AnglesInDegrees = false; Assert.True(model.AnglesInDegrees);
        test.Ui.HandleInput(new(InputKind.PointerMove, x + 4, y)); test.Drain();
        Near(Number(originalText) + 1, Number(field.DisplayValue));
        if (cancel)
        {
            test.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); test.Drain();
            Assert.Equal(canonical, field.Value); Assert.Equal(originalText, field.DisplayValue); Assert.Equal(originalText, box.Editor.Value);
            Assert.False(model.HasDraft); Assert.Equal(0, test.Owner.Owner.Document.Read().Content!.UndoCount);
        }
        else
        {
            test.Ui.HandleInput(new(InputKind.PointerUp, x + 4, y)); test.Drain(); Assert.True(test.Transform.Degrees.IsEnabled);
            test.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true)); test.Drain();
            Assert.Equal(1, test.Owner.Owner.Document.Read().Content!.UndoCount); Near(Number(canonical) + Math.PI / 180, Committed(test.Owner, "pitch"));
        }
        Assert.False(box.IsDragging); Assert.True(test.Transform.Degrees.IsEnabled); Assert.True(model.AnglesInDegrees);
    }

    [Fact]
    public void DegreeArrowStep_UsesOneDegreeAndSupportsModifiers()
    {
        using var test = new UiFixture(); var model = test.Owner.Model; model.AnglesInDegrees = true; test.Drain();
        var box = test.Transform.Rotation.Second; test.Ui.Focus(box.Editor);
        test.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Up)); test.Drain(); Assert.Equal("1", box.Editor.Value);
        test.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Up, Shift: true)); test.Drain(); Assert.Equal("11", box.Editor.Value);
        test.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Down, Control: true)); test.Drain(); Assert.Equal("10.9", box.Editor.Value);
        Near(10.9 / 180 * Math.PI, Number(model.Fields[4].Value)); Assert.Equal(0, test.Owner.Owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void LegacyView_UsesSharedDisplayWithoutChangingCanonicalContract()
    {
        using var owner = new InspectorFixture(Scene(Math.PI, 0, 0));
        var view = new EditorTransformView(owner.Model, new EditorTransformViewStyle()); using var ui = InspectorFixture.Ui(view);
        ui.Update(); owner.Model.AnglesInDegrees = true; ui.Update(); ui.Update();
        Assert.Equal("180", view.Fields["pitch"].Value); Assert.Contains("°", view.Fields["pitch"].AccessibleName);
        view.Fields["pitch"].Value = "90"; ui.Update(); Near(Math.PI / 2, Number(owner.Model.Fields[3].Value));
        owner.Model.Apply(); Near(Math.PI / 2, Committed(owner, "pitch")); Assert.Equal(1, owner.Owner.Document.Read().Content!.UndoCount);
    }

    private static double Number(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static void Near(double expected, double actual) => Assert.InRange(actual, expected - Math.Max(1e-300, Math.Abs(expected) * 1e-14), expected + Math.Max(1e-300, Math.Abs(expected) * 1e-14));
    private static double Committed(InspectorFixture test, string key) => test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Transform].Properties[key].Value.GetNumber();
    private static SceneDocument Scene(double pitch = 0, double yaw = 0, double roll = 0, bool localAngles = false)
    {
        var definition = new EntityDefinition(DocumentData.Base, new(addedComponents: [new(DocumentData.Transform, BuiltInComponents.TransformId, 1,
            [KeyValuePair.Create("pitch", PropertyValue.From(pitch)), KeyValuePair.Create("yaw", PropertyValue.From(yaw)), KeyValuePair.Create("roll", PropertyValue.From(roll))])]));
        var local = localAngles ? new[] { new ComponentPropertyOverride(DocumentData.Transform, "pitch", PropertyValue.From(pitch + 1)),
            new(DocumentData.Transform, "yaw", PropertyValue.From(yaw + 1)), new(DocumentData.Transform, "roll", PropertyValue.From(roll + 1)) } : [];
        return new(new(DocumentData.Id(100)), 7, [new(DocumentData.Parent, DocumentData.Base), new(DocumentData.Child, DocumentData.Base,
            new(propertyOverrides: local), DocumentData.Parent)], [definition], [BuiltInComponents.Schemas.Single(schema => schema.TypeId == BuiltInComponents.TransformId)]);
    }

    private sealed class UiFixture : IDisposable
    {
        public InspectorFixture Owner { get; }
        public EditorInspectorView View { get; }
        public TrueMoon.Alloy.Runtime.UiSession Ui { get; }
        public TransformEditor Transform => View.TransformEditors[DocumentData.Transform];
        public UiFixture(SceneDocument? scene = null, int width = 330)
        { Owner = new(scene ?? Scene()); View = new(Owner.Model, new EditorInspectorViewStyle()); Ui = InspectorFixture.Ui(View, width: width); Drain(); }
        public void Drain() { Ui.Update(); Ui.Update(); }
        public void Enter(TextBox input, string value)
        { View.Scroll.BringIntoView(input.Bounds); Drain(); Ui.Focus(input); Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true)); Ui.HandleInput(new(InputKind.Text, Text: value)); Drain(); }
        public void Dispose() { Ui.Dispose(); Owner.Dispose(); }
    }
}
