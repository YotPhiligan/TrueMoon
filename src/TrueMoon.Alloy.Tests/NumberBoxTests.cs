using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class NumberBoxTests
{
    [Theory]
    [InlineData("")] [InlineData("-")] [InlineData("1e")] [InlineData("1e-")]
    [InlineData("NaN")] [InlineData("01.50")] [InlineData("2,5")]
    public void Editing_PreservesExactStringDraftAndWritesThroughExistingTextBox(string draft)
    {
        var model = new DraftModel(); var box = new NumberBox("X");
        box.Editor.BindTwoWay(TextBox.ValueProperty, model, value => value.Value);
        using var ui = CompactControlHarness.Ui(box); ui.Update(); ui.Focus(box.Editor);
        var editor = box.Editor; var prefix = box.Prefix;
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        ui.HandleInput(draft.Length == 0 ? new(InputKind.KeyDown, Key: UiKey.Backspace) : new(InputKind.Text, Text: draft));
        ui.Update(); ui.SetTheme(Theme.Light); ui.Update();

        Assert.Equal(draft, model.Value); Assert.Equal(draft, editor.Value); Assert.Equal(1, model.Writes);
        Assert.Equal(draft.Length, editor.CaretIndex); Assert.Equal(0, editor.SelectionLength); Assert.True(editor.IsFocused);
        Assert.Same(editor, box.Editor); Assert.Same(prefix, box.Prefix);
        Assert.Equal(new Element[] { prefix, editor }, box.Children); Assert.Equal("X", prefix.Value);
        box.ColorRole = EditorColorRole.AxisX;
        Assert.Equal(Theme.Light.AxisX, prefix.Foreground);
        Assert.Equal(EditorColorRole.AxisX, editor.ColorRole);
    }

    [Theory]
    [InlineData("", 0f)] [InlineData("X", 13f)] [InlineData("XYZ", 29f)]
    public void Layout_ReservesOnlyMeasuredPrefixAndGapInsteadOfTextBoxPreferredWidth(string prefix, float offset)
    {
        using var box = new NumberBox(prefix);
        box.Measure(new(120, 40), new FixedTextLayout()); box.Arrange(new(10, 20, 120, 40));
        Assert.Equal(new Rect(10 + offset, 20, 120 - offset, 40), box.Editor.Bounds);
        Assert.Equal(0, box.Editor.MinWidth); Assert.Same(box, box.Editor.Parent); Assert.Same(box, box.Prefix.Parent);
        Assert.Equal(80, box.DesiredSize.Width);
    }

    [Theory]
    [InlineData(0f)] [InlineData(1f)] [InlineData(7f)] [InlineData(12f)]
    public void Layout_ExhaustedWidthKeepsPrefixAndEditorInsideParent(float width)
    {
        using var box = new NumberBox("XYZ"); box.Measure(new(width, 32), new FixedTextLayout()); box.Arrange(new(5, 7, width, 32));
        Assert.All(box.Children, child =>
        {
            Assert.True(float.IsFinite(child.Bounds.Width)); Assert.InRange(child.Bounds.Width, 0, width);
            Assert.InRange(child.Bounds.X, 5, 5 + width); Assert.True(child.Bounds.X + child.Bounds.Width <= 5 + width);
        });
        Assert.Equal(0, box.Editor.Bounds.Width); Assert.Equal("XYZ", box.Prefix.Value);
    }

    [Fact]
    public void Disposal_OwnsIndependentPartsAndReleasesTheirBindingWithoutOwningDraftModel()
    {
        Assert.Throws<ArgumentNullException>(() => new NumberBox(null!));
        var model = new DraftModel(); var first = new NumberBox(); using var second = new NumberBox();
        first.Editor.BindTwoWay(TextBox.ValueProperty, model, value => value.Value);
        var resource = new DisposalCounter(); first.Editor.Own(resource);
        var ui = CompactControlHarness.Ui(first); ui.Update(); ui.Dispose(); first.Dispose();
        Assert.True(first.Prefix.IsDisposed); Assert.True(first.Editor.IsDisposed); Assert.Equal(1, resource.Count);
        Assert.False(second.Editor.IsDisposed); Assert.NotSame(first.Editor, second.Editor); Assert.NotSame(first.Prefix, second.Prefix);
        model.Value = "after disposal"; Assert.Equal("12", first.Editor.Value); Assert.Equal("after disposal", model.Value);
    }

    [Fact]
    public void ColorRole_InheritsEffectiveStyleAndClearWithoutOverwritingExplicitChildAccent()
    {
        using var box = new NumberBox("X");
        Assert.Equal(EditorColorRole.Number, box.Editor.ColorRole); Assert.Equal(Theme.Dark.NumberAccent, box.Prefix.Foreground);
        box.Style = new Style().With(UiProperties.ColorRole, EditorColorRole.AxisY);
        Assert.Equal(EditorColorRole.AxisY, box.Editor.ColorRole); Assert.Equal(Theme.Dark.AxisY, box.Prefix.Foreground);
        box.Editor.ColorRole = EditorColorRole.Reference; box.ColorRole = EditorColorRole.AxisX;
        box.ApplyTheme(Theme.Light);
        Assert.Equal(EditorColorRole.Reference, box.Editor.ColorRole); Assert.Equal(Theme.Light.AxisX, box.Prefix.Foreground);
        box.Clear(UiProperties.ColorRole); Assert.Equal(Theme.Light.AxisY, box.Prefix.Foreground);
        box.Style = null; Assert.Equal(Theme.Light.NumberAccent, box.Prefix.Foreground);
        box.Editor.Clear(UiProperties.ColorRole); Assert.Equal(EditorColorRole.Number, box.Editor.ColorRole);
        Assert.Equal(new Element[] { box.Prefix, box.Editor }, box.Children);
    }

    private sealed class DraftModel : Bindable
    {
        private string _value = "12";
        public int Writes { get; private set; }
        public string Value { get => _value; set { _value = value; Writes++; OnPropertyChanged(); } }
    }
}

internal static class CompactControlHarness
{
    public static UiSession Ui(Element root, int width = 400, int height = 120) => new(root, new Surface(), new UiViewport(width, height));
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) => root.Draw(new RecordingDrawingContext());
        public void Dispose() { }
    }
    public static IEnumerable<Element> Tree(Element element) => new[] { element }.Concat(element.Children.SelectMany(Tree));
}
