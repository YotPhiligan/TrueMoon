using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class ThemeSpacingTests
{
    [Fact]
    public void DefaultTokens_PreserveExistingControlPaddingAndMeasuredSizes()
    {
        Assert.Equal(new Thickness(12, 8, 12, 8), Theme.Dark.ButtonPadding);
        Assert.Equal(new Thickness(8), Theme.Dark.EditorPadding);
        Assert.Equal(0, Theme.Dark.StackSpacing);
        Assert.Equal(Theme.Dark.ButtonPadding, Theme.Light.ButtonPadding);
        Assert.Equal(Theme.Dark.EditorPadding, Theme.Light.EditorPadding);
        Assert.Equal(Theme.Dark.StackSpacing, Theme.Light.StackSpacing);
        using var button = new Button("AB"); using var check = new CheckBox("AB"); using var editor = new TextBox(); using var stack = new VStack(); using var plain = new Text("AB");
        Assert.Equal(new Thickness(12, 8, 12, 8), button.Padding); Assert.Equal(new Thickness(36, 8, 12, 8), check.Padding);
        Assert.Equal(new Thickness(8), editor.Padding); Assert.Equal(default, plain.Padding); Assert.Equal(0, stack.Spacing);
        button.Measure(new Size(240, 140), new FixedTextLayout()); check.Measure(new Size(240, 140), new FixedTextLayout()); editor.Measure(new Size(240, 140), new FixedTextLayout());
        Assert.Equal(new Size(40, 32), button.DesiredSize); Assert.Equal(new Size(64, 32), check.DesiredSize); Assert.Equal(new Size(176, 32), editor.DesiredSize);
    }

    [Fact]
    public void ThemeTokens_UpdateExistingAndNewControlsWithoutOverridingLocalSpacing()
    {
        var theme = Theme.Light with { ButtonPadding = new Thickness(2, 3, 4, 5), EditorPadding = new Thickness(1, 2, 3, 4), StackSpacing = 7 };
        var button = new Button("AB"); var editor = new TextBox(); var check = new CheckBox("AB");
        var root = new VStack().WithChildren(button, editor, check);
        using var ui = new UiSession(root, new Surface(), new UiViewport(240, 140)); ui.Update(); var oldY = editor.Bounds.Y;
        ui.SetTheme(theme); Assert.True(ui.Update());
        Assert.Equal(theme.ButtonPadding, button.Padding); Assert.Equal(theme.EditorPadding, editor.Padding); Assert.Equal(new Thickness(26, 3, 4, 5), check.Padding); Assert.Equal(7, root.Spacing);
        Assert.Equal(new Size(22, 24), button.DesiredSize); Assert.Equal(31, editor.Bounds.Y); Assert.NotEqual(oldY, editor.Bounds.Y);
        var added = new HStack().WithChildren(new Button("later"), new TextBox()); root.Items.Add(added); ui.Update();
        Assert.Same(theme, added.Theme); Assert.Equal(7, added.Spacing); Assert.Equal(theme.ButtonPadding, added.Items[0].Padding); Assert.Equal(theme.EditorPadding, added.Items[1].Padding);
        added.Spacing = 11; ui.SetTheme(Theme.Dark); ui.Update(); Assert.Equal(11, added.Spacing); Assert.Equal(0, root.Spacing);
    }

    [Fact]
    public void TypedPaddingAndSpacing_LocalStyleThemeClearFollowEffectivePriority()
    {
        var theme = Theme.Dark with { ButtonPadding = new Thickness(3), StackSpacing = 6 };
        var baseStyle = new Style(); var styled = baseStyle.With(UiProperties.Padding, new Thickness(5)).With(UiProperties.Spacing, 8f);
        using var button = new Button().Configure(x => x.Style = styled); using var stack = new VStack().Configure(x => x.Style = styled);
        button.ApplyTheme(theme); stack.ApplyTheme(theme);
        Assert.Equal(new Thickness(5), button.Padding); Assert.Equal(8, stack.Spacing);
        button.Set(UiProperties.Padding, new Thickness(9)); stack.Set(UiProperties.Spacing, 12f);
        Assert.Equal(new Thickness(9), button.Padding); Assert.Equal(12, stack.Spacing);
        button.ApplyTheme(Theme.Light); stack.ApplyTheme(Theme.Light); Assert.Equal(new Thickness(9), button.Padding); Assert.Equal(12, stack.Spacing);
        button.Clear(UiProperties.Padding); stack.Clear(UiProperties.Spacing); Assert.Equal(new Thickness(5), button.Padding); Assert.Equal(8, stack.Spacing);
        button.Style = baseStyle; stack.Style = baseStyle; Assert.Equal(Theme.Light.ButtonPadding, button.Padding); Assert.Equal(Theme.Light.StackSpacing, stack.Spacing);
        Assert.Equal(new Thickness(5), styledValue(styled)); Assert.Equal(default, styledValue(baseStyle));
        static Thickness styledValue(Style style) { using var element = new Panel { Style = style }; return element.Padding; }
    }

    [Fact]
    public void TypedSetAndClear_InvalidateLayoutOnceAndEqualAssignmentsKeepFrameStatic()
    {
        var child = new SizedElement(30, 20); var root = new VStack().WithChildren(child, new SizedElement(30, 20));
        using var ui = new UiSession(root, new Surface(), new UiViewport(240, 140)); ui.Update(); var changes = new List<object>(); root.PropertyChanged += (_, p) => changes.Add(p);
        root.Spacing = 9; Assert.True(ui.Update()); Assert.Equal(29, root.Items[1].Bounds.Y); Assert.Same(UiProperties.Spacing, Assert.Single(changes));
        root.Spacing = 9; Assert.False(ui.Update()); Assert.Single(changes);
        root.Padding = new Thickness(2, 3, 4, 5); ui.Update(); Assert.Equal(new Rect(2, 3, 234, 20), child.Bounds);
        Assert.Same(UiProperties.Padding, changes[1]); Assert.Equal(new Thickness(2, 3, 4, 5), root.Get(UiProperties.Padding));
        root.Padding = root.Padding; Assert.False(ui.Update()); Assert.Equal(2, changes.Count);
        root.Clear(UiProperties.Spacing); root.Clear(UiProperties.Padding); ui.Update(); Assert.Equal(20, root.Items[1].Bounds.Y); Assert.Equal(default, root.Padding); Assert.Equal(4, changes.Count);
        root.Clear(UiProperties.Padding); Assert.False(ui.Update()); Assert.Equal(4, changes.Count);
    }

    public static IEnumerable<object[]> InvalidInsets()
    {
        foreach (var edge in Enumerable.Range(0, 4)) foreach (var value in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity }) yield return [edge, value];
    }

    [Theory]
    [MemberData(nameof(InvalidInsets))]
    public void InvalidPaddingOnEveryEdge_RejectsLocalStyleAndThemeTokensBeforeChanges(int edge, float value)
    {
        var values = new[] { 1f, 2f, 3f, 4f }; values[edge] = value; var invalid = new Thickness(values[0], values[1], values[2], values[3]);
        using var button = new Button { Padding = new Thickness(6) }; var changes = 0; button.PropertyChanged += (_, _) => changes++;
        Assert.Throws<ArgumentOutOfRangeException>(() => button.Padding = invalid);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Style().With(UiProperties.Padding, invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => Theme.Dark with { ButtonPadding = invalid });
        Assert.Throws<ArgumentOutOfRangeException>(() => Theme.Dark with { EditorPadding = invalid });
        Assert.Equal(new Thickness(6), button.Padding); Assert.Equal(new Thickness(12, 8, 12, 8), Theme.Dark.ButtonPadding); Assert.Equal(new Thickness(8), Theme.Dark.EditorPadding); Assert.Equal(0, changes);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void InvalidSpacing_RejectsLocalStyleAndThemeTokenBeforeChanges(float value)
    {
        using var stack = new VStack { Spacing = 5 }; var changes = 0; stack.PropertyChanged += (_, _) => changes++;
        Assert.Throws<ArgumentOutOfRangeException>(() => stack.Spacing = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Style().With(UiProperties.Spacing, value));
        Assert.Throws<ArgumentOutOfRangeException>(() => Theme.Light with { StackSpacing = value });
        Assert.Equal(5, stack.Spacing); Assert.Equal(0, Theme.Light.StackSpacing); Assert.Equal(0, changes);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(float.Epsilon)]
    public void ZeroAndNearestPositiveTokens_AreAcceptedAndInherited(float value)
    {
        var theme = Theme.Dark with { ButtonPadding = new Thickness(value), EditorPadding = new Thickness(value), StackSpacing = value };
        using var root = new VStack().WithChildren(new Button(), new TextBox()); root.ApplyTheme(theme);
        Assert.Equal(value, root.Spacing); Assert.All(root.Items, child => Assert.Equal(new Thickness(value), child.Padding));
        root.Set(UiProperties.Padding, new Thickness(value)); root.Set(UiProperties.Spacing, value); Assert.Equal(new Thickness(value), root.Padding); Assert.Equal(value, root.Spacing);
    }

    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) => root.Draw(new RecordingDrawingContext());
        public void Dispose() { }
    }
}
