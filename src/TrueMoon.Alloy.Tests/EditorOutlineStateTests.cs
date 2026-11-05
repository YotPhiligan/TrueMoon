using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class EditorOutlineStateTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CombinedStates_OutlinePriorityIsDisabledErrorFocusModifiedHoverThenSemantic(bool light)
    {
        var theme = light ? Theme.Light : Theme.Dark;
        using var parent = new Panel(); var editor = new TextBox { ColorRole = EditorColorRole.AxisX, IsHovered = true, IsModified = true, IsFocused = true, HasError = true };
        parent.Add(editor); parent.ApplyTheme(theme);
        Assert.Equal(theme.Error, editor.BorderColor);
        parent.IsEnabled = false; Assert.Equal(theme.Border, editor.BorderColor); Assert.False(editor.IsEffectivelyEnabled);
        parent.IsEnabled = true; editor.HasError = false; Assert.Equal(theme.Focus, editor.BorderColor);
        editor.IsFocused = false; Assert.Equal(theme.Modified, editor.BorderColor);
        editor.IsModified = false; Assert.Equal(theme.BorderHover, editor.BorderColor);
        editor.IsHovered = false;
        Assert.Equal(light ? new Color(137, 96, 114) : new Color(158, 125, 143), editor.BorderColor);
        editor.ColorRole = EditorColorRole.Neutral; Assert.Equal(theme.Border, editor.BorderColor);
        Assert.Equal("", editor.Value); Assert.DoesNotContain(parent.Children, child => child.IsDisposed);
    }

    [Theory]
    [InlineData(EditorColorRole.Neutral)] [InlineData(EditorColorRole.Number)] [InlineData(EditorColorRole.Text)]
    [InlineData(EditorColorRole.Boolean)] [InlineData(EditorColorRole.Reference)] [InlineData(EditorColorRole.AxisX)]
    [InlineData(EditorColorRole.AxisY)] [InlineData(EditorColorRole.AxisZ)]
    public void SemanticPalette_UsesDedicatedThemeTokenAndUpdatesExistingInheritedPrefix(EditorColorRole role)
    {
        using var box = new NumberBox("axis") { ColorRole = role };
        var custom = Theme.Dark with
        {
            Foreground = new(1, 2, 3), NumberAccent = new(4, 5, 6), TextAccent = new(7, 8, 9), BooleanAccent = new(10, 11, 12),
            ReferenceAccent = new(13, 14, 15), AxisX = new(16, 17, 18), AxisY = new(19, 20, 21), AxisZ = new(22, 23, 24)
        };
        var first = (int)role * 3 + 1; var expected = new Color((byte)first, (byte)(first + 1), (byte)(first + 2));
        box.ApplyTheme(custom);
        Assert.Equal(expected, custom.EditorColor(role)); Assert.Equal(expected, box.Prefix.Foreground);
        Assert.Equal(role, box.Editor.ColorRole); Assert.Same(box, box.Prefix.Parent);
        box.ApplyTheme(Theme.Light); Assert.NotEqual(expected, box.Prefix.Foreground);
        Assert.Equal(Theme.Light.EditorColor(role), box.Prefix.Foreground);
    }

    [Fact]
    public void ExplicitBorders_WinOverStateAndThemeWhileFocusAndErrorKeepIndependentShapes()
    {
        var view = new OutlineView(); using var ui = CompactControlHarness.Ui(view, 100, 50); ui.Update();
        view.WithViewStyle(new OutlineStyle()); var editor = view.Editor;
        editor.HasError = true; editor.IsModified = true; ui.Focus(editor);
        Assert.Equal(new Color(11, 22, 33), editor.BorderColor);
        editor.Style = new Style().With(UiProperties.BorderColor, new Color(44, 55, 66));
        Assert.Equal(new Color(44, 55, 66), editor.BorderColor);
        editor.BorderColor = new(77, 88, 99); ui.SetTheme(Theme.Light); ui.Update();
        var drawing = new OutlineDrawing(); editor.Draw(drawing);
        Assert.Contains(drawing.Strokes, stroke => stroke == (new Rect(.5f, .5f, 99, 49), new Color(77, 88, 99), 1f));
        Assert.Contains(drawing.Strokes, stroke => stroke == (new Rect(2, 2, 96, 46), Theme.Light.Focus, 2f));
        Assert.Contains(drawing.Fills, fill => fill == (new Rect(0, 0, 3, 50), Theme.Light.Error));
        Assert.DoesNotContain(drawing.Fills, fill => fill.Color == Theme.Light.Modified);
        Assert.Equal(drawing.Saves, drawing.Restores);
        editor.Clear(UiProperties.BorderColor); Assert.Equal(new Color(44, 55, 66), editor.BorderColor);
        editor.Style = null; Assert.Equal(new Color(11, 22, 33), editor.BorderColor);
        view.WithViewStyle<OutlineView>(null); Assert.Equal(Theme.Light.Error, editor.BorderColor);
        editor.HasError = false; drawing = new(); editor.Draw(drawing);
        Assert.Contains(drawing.Fills, fill => fill == (new Rect(95, 2, 3, 3), Theme.Light.Modified));
        Assert.Equal(Theme.Light.Focus, editor.BorderColor); Assert.Same(editor, view.Child);
    }

    [Fact]
    public void ModifiedAndRoleProperties_InvalidateRenderOnlyAndRejectUndefinedRolesBeforeMutation()
    {
        var editor = new TextBox(); using var ui = CompactControlHarness.Ui(editor); ui.Update();
        var events = new List<object>(); var invalidations = new List<Invalidation>();
        editor.PropertyChanged += (_, property) => events.Add(property); editor.Invalidated += invalidations.Add;
        var bounds = editor.Bounds; editor.IsModified = true; Assert.True(ui.Update());
        Assert.Same(UiProperties.IsModified, Assert.Single(events)); Assert.Equal(Invalidation.Render, Assert.Single(invalidations));
        Assert.Equal(bounds, editor.Bounds); editor.IsModified = true; Assert.False(ui.Update()); Assert.Single(events);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.ColorRole = (EditorColorRole)99);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Style().With(UiProperties.ColorRole, (EditorColorRole)(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Theme.Dark.EditorColor((EditorColorRole)99));
        Assert.Equal(EditorColorRole.Neutral, editor.ColorRole); Assert.Single(events);
        editor.Clear(UiProperties.IsModified); Assert.True(ui.Update()); Assert.Equal(2, events.Count); Assert.False(editor.IsModified);
    }

    [Theory]
    [InlineData(48f, 16f, 13f, 16f)]
    [InlineData(20f, 16f, 3f, 8f)]
    [InlineData(60f, 12f, 21f, 12f)]
    public void TextBox_TextSelectionAndCaretShareVerticallyCenteredLineBox(float height, float fontSize, float textY, float lineHeight)
    {
        var editor = new TextBox { Value = "AB", FontSize = fontSize, Padding = new(4, 3, 8, 9) };
        using var ui = CompactControlHarness.Ui(editor, 120, (int)height); ui.Update(); ui.Focus(editor);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        var drawing = new OutlineDrawing(); editor.Draw(drawing);
        Assert.Equal(("AB", 4f, textY), Assert.Single(drawing.Texts));
        Assert.Contains(drawing.Fills, fill => fill == (new Rect(4, textY, fontSize, lineHeight), Theme.Dark.Accent with { A = 100 }));
        Assert.Contains(drawing.Fills, fill => fill == (new Rect(4 + fontSize, textY, 1, lineHeight), Theme.Dark.Foreground));
        Assert.Contains(new Rect(4, 3, 108, height - 12), drawing.Clips);
        Assert.Equal(2, editor.CaretIndex); Assert.Equal(2, editor.SelectionLength); Assert.Equal("AB", editor.Value);
        Assert.Equal(drawing.Saves, drawing.Restores);
    }

    private sealed class OutlineView : View
    {
        public TextBox Editor { get; } = new() { MinWidth = 0 };
        public OutlineView() => SetContent(Editor);
    }
    private sealed class OutlineStyle : IViewStyle<OutlineView>
    {
        public void Configure(IViewStyleBuilder<OutlineView> styles) => styles.Part(view => view.Editor).Set(UiProperties.BorderColor, new Color(11, 22, 33));
    }
}

internal sealed class OutlineDrawing : IDrawingContext
{
    public int Saves, Restores;
    public List<(Rect Bounds, Color Color)> Fills { get; } = [];
    public List<(Rect Bounds, Color Color, float Width)> Strokes { get; } = [];
    public List<(string Text, float X, float Y)> Texts { get; } = [];
    public List<Rect> Clips { get; } = [];
    public void Save() => Saves++;
    public void Restore() => Restores++;
    public void Clip(Rect rectangle) => Clips.Add(rectangle);
    public void Translate(float x, float y) { }
    public void Fill(Rect rectangle, Color color, float radius = 0) => Fills.Add((rectangle, color));
    public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) => Strokes.Add((rectangle, color, width));
    public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) => Texts.Add((text, x, y));
    public void Image(IImageSource source, Rect destination) { }
}
