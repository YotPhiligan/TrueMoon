using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class CheckBoxEditorStateTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void ActualIndicator_UsesSemanticFillAndOrderedStateOutlineForCheckedAndUncheckedValues(bool light, bool check)
    {
        var theme = light ? Theme.Light : Theme.Dark; using var parent = new Panel { ColorRole = EditorColorRole.Boolean };
        var box = new CheckBox("Да") { IsChecked = check, HasError = true, IsModified = true, IsFocused = true, IsHovered = true }; parent.Add(box);
        parent.ApplyTheme(theme); parent.Measure(new(120, 48), new FixedTextLayout()); parent.Arrange(new(0, 0, 120, 48));
        Check(theme.Error, theme.BooleanAccent);
        parent.IsEnabled = false; Check(theme.Border, theme.Disabled);
        parent.IsEnabled = true; box.HasError = false; Check(theme.Focus, theme.BooleanAccent);
        box.IsFocused = false; Check(theme.Modified, theme.BooleanAccent);
        box.IsModified = false; Check(theme.BorderHover, theme.BooleanAccent);
        box.IsHovered = false; box.IsReadOnly = true; Check(box.BorderColor, theme.Border); Assert.Equal(theme.ReadOnly, box.Background);
        box.BorderColor = new(21, 43, 65); Check(new(21, 43, 65), theme.Border);
        Assert.Equal(check, box.IsChecked); Assert.Equal(EditorColorRole.Boolean, box.ColorRole);

        void Check(Color outline, Color fill)
        {
            var drawing = new OutlineDrawing(); box.Draw(drawing);
            Assert.Contains(drawing.Strokes, stroke => stroke == (new Rect(10.5f, 16.5f, 15, 15), outline, 1f));
            if (check) Assert.Contains(drawing.Fills, item => item == (new Rect(10, 16, 16, 16), fill));
            else Assert.DoesNotContain(drawing.Fills, item => item.Bounds == new Rect(10, 16, 16, 16));
            Assert.Equal(drawing.Saves, drawing.Restores);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ReadOnly_PreservesFocusAndValueButSuppressesPointerKeyboardAndClickUntilCleared(bool initial)
    {
        var calls = 0; var box = new CheckBox("Value") { IsChecked = initial, IsReadOnly = true }.OnClick(() => calls++);
        using var ui = CompactControlHarness.Ui(box, 120, 48); ui.Update(); ui.Focus(box);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Space)); ui.HandleInput(new(InputKind.KeyUp, Key: UiKey.Space));
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter));
        ui.HandleInput(new(InputKind.PointerDown, 15, 24)); ui.HandleInput(new(InputKind.PointerUp, 15, 24));
        Assert.True(box.IsFocused); Assert.Equal(initial, box.IsChecked); Assert.Equal(0, calls);
        box.Clear(CheckBox.IsReadOnlyProperty); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Space)); ui.HandleInput(new(InputKind.KeyUp, Key: UiKey.Space));
        Assert.Equal(!initial, box.IsChecked); Assert.Equal(1, calls); Assert.True(box.IsFocused);
        box.IsEnabled = false; ui.Update(); ui.HandleInput(new(InputKind.PointerDown, 15, 24)); ui.HandleInput(new(InputKind.PointerUp, 15, 24));
        Assert.Equal(1, calls); Assert.Equal(!initial, box.IsChecked); Assert.False(box.IsFocused);
    }

    [Fact]
    public void StyledIndicatorBorderAndSemanticGlyphColorSurviveThemeAndTextUsesMeasuredVerticalCenter()
    {
        var box = new CheckBox("Да") { IsChecked = true, ColorRole = EditorColorRole.Boolean, FontSize = 12, Padding = new(28, 2, 4, 2), Style = new Style().With(UiProperties.BorderColor, new Color(12, 34, 56)) };
        using var ui = CompactControlHarness.Ui(box, 96, 26); ui.SetTheme(Theme.Light); ui.Update(); var drawing = new OutlineDrawing(); box.Draw(drawing);
        Assert.Contains(drawing.Strokes, item => item == (new Rect(10.5f, 5.5f, 15, 15), new Color(12, 34, 56), 1f));
        Assert.Equal(("Да", 28f, 7f), Assert.Single(drawing.Texts));
        Assert.Contains(drawing.Fills, item => item.Color == Theme.Light.OnEditorAccent);
        Assert.Contains(drawing.Fills, item => item == (new Rect(10, 5, 16, 16), Theme.Light.BooleanAccent));
        Assert.Equal(new Color(255, 255, 255), Theme.Light.OnEditorAccent); Assert.Equal(Theme.Dark.Surface, Theme.Dark.OnEditorAccent);
        ui.SetTheme(Theme.Dark); ui.Update(); drawing = new(); box.Draw(drawing);
        Assert.Contains(drawing.Strokes, item => item.Color == new Color(12, 34, 56));
        Assert.Contains(drawing.Fills, item => item == (new Rect(10, 5, 16, 16), Theme.Dark.BooleanAccent));
        Assert.True(box.IsChecked);
    }
}
