using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Editor.Tests;

public sealed class EditorScreenTests
{
    [Fact]
    public void ShellGeometry_ReservesToolbarInspectorAndStatusAroundCentralViewport()
    {
        using var layout = new EditorLayout(new Box(), new Box(), new Box(), new Box(), new Box());
        layout.Measure(new(1280, 720), new TextLayout()); layout.Arrange(new(0, 0, 1280, 720));
        Assert.Equal(new Rect(0, 0, 1280, 52), layout.Children[0].Bounds);
        Assert.Equal(new Rect(0, 52, 224, 638), layout.Children[1].Bounds);
        Assert.Equal(new Rect(230, 52, 752, 638), layout.Children[2].Bounds);
        Assert.Equal(new Rect(988, 52, 292, 638), layout.Children[3].Bounds);
        Assert.Equal(new Rect(0, 690, 1280, 30), layout.Children[4].Bounds);
    }
    [Theory] [InlineData(0f, 0f)] [InlineData(3f, 20f)] [InlineData(1100f, 700f)]
    public void ShellGeometry_SmallBoundsStayFiniteNonnegativeAndClipped(float width, float height)
    {
        using var layout = new EditorLayout(new Box(), new Box(), new Box(), new Box(), new Box());
        layout.Measure(new(width, height), new TextLayout()); layout.Arrange(new(0, 0, width, height));
        Assert.All(layout.Children, child => { var b = child.Bounds; Assert.True(float.IsFinite(b.X + b.Y + b.Width + b.Height)); Assert.True(b.Width >= 0 && b.Height >= 0); Assert.InRange(b.X + b.Width, 0, width); Assert.InRange(b.Y + b.Height, 0, height); });
    }
    [Fact]
    public void InvalidShellChildren_AreRejectedBeforeAdoption()
    {
        using var first = new Box(); using var second = new Box(); using var third = new Box(); using var fourth = new Box();
        Assert.Throws<ArgumentException>(() => new EditorLayout(first, second, third, fourth, first)); Assert.Null(first.Parent); Assert.Null(second.Parent);
        Assert.Throws<ArgumentNullException>(() => new EditorLayout(first, second, third, fourth, null!)); Assert.Null(first.Parent);
        using var owner = new Panel(); owner.Add(first);
        Assert.Throws<ArgumentException>(() => new EditorLayout(first, second, third, fourth, new Box())); Assert.Same(owner, first.Parent); Assert.Null(second.Parent);
    }
    [Fact]
    public void HierarchyInput_UpdatesReadOnlyGridAndThemePreservesSelectionAndFocus()
    {
        var workspace = new EditorWorkspace(EditorScene.Demo()); var view = new EditorView(workspace);
        using var ui = Create(view); ui.Update(); Click(ui, view.Entities["green-cube"]); ui.Update();
        Assert.Equal("green-cube", workspace.SelectedId); Assert.Equal(ControlTone.Accent, view.Entities["green-cube"].Tone); Assert.Equal(ControlTone.Neutral, view.Entities["red-cube"].Tone);
        var field = Descendants(view).OfType<TextBox>().Single(x => x.AccessibleName == "X"); Assert.True(field.IsReadOnly); Assert.Equal("1.5", field.Value);
        ui.Focus(field); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true)); var selected = field.SelectionLength;
        ui.HandleInput(new(InputKind.Text, Text: "99")); Assert.Equal("1.5", field.Value);
        ui.SetTheme(Theme.Light); ui.Update(); Assert.True(field.IsFocused); Assert.Equal(selected, field.SelectionLength); Assert.Equal("green-cube", workspace.SelectedId); Assert.Equal(Theme.Light.ReadOnly, field.Background);
        workspace.Select("switch"); ui.Update(); Assert.True(field.IsDisposed); Assert.Equal("0", Descendants(view).OfType<TextBox>().Single(x => x.AccessibleName == "X").Value);
    }
    [Fact]
    public void CommandButtons_DispatchOnlyEnabledCommandsAndBusyStateCanStopOrCancel()
    {
        var workspace = new EditorWorkspace(EditorScene.Demo()); var view = new EditorView(workspace); var commands = new List<EditorCommand>(); view.CommandRequested += commands.Add;
        using var ui = Create(view); ui.Update(); Click(ui, view.Commands[EditorCommand.Start]); Assert.Empty(commands);
        Click(ui, view.Commands[EditorCommand.Build]); Assert.Equal(new[] { EditorCommand.Build }, commands);
        workspace.Refresh(new(SessionState.Paused), EditorWorkspaceTests.Status(GameEditorOperation.Build, false)); view.Refresh(); ui.Update();
        Assert.False(view.Commands[EditorCommand.Play].IsEnabled); Assert.True(view.Commands[EditorCommand.Cancel].IsEnabled);
        Click(ui, view.Commands[EditorCommand.Play]); Click(ui, view.Commands[EditorCommand.Stop]); Click(ui, view.Commands[EditorCommand.Cancel]);
        Assert.Equal(new[] { EditorCommand.Build, EditorCommand.Stop, EditorCommand.Cancel }, commands);
    }
    [Theory] [InlineData(1f)] [InlineData(1.5f)] [InlineData(2f)]
    public void DefaultFrame_LayoutUsesLogicalUnitsAndSettlesWithoutRedraw(float scale)
    {
        var view = new EditorView(new(EditorScene.Demo())); var frame = WindowFrame.CreateDefault("Editor", view); frame.ApplyWindowState(new(true, false, true, true, true));
        using var ui = new UiSession(frame, new Surface(), new UiViewport((int)(1280 * scale), (int)(800 * scale), scale));
        ui.Update(); Assert.Equal(new Rect(1, 37, 1278, 746), view.Bounds); Assert.Equal(0, view.CornerRadius); Assert.True(view.Canvas.Bounds.Width > 700); Assert.True(view.Canvas.Bounds.Height > 600);
        Assert.True(view.Canvas.Bounds.Y > view.Bounds.Y + 52); Assert.True(view.Canvas.Bounds.X + view.Canvas.Bounds.Width < view.Bounds.X + view.Bounds.Width - 280);
        view.Refresh(); Assert.False(ui.Update());
    }
    [Fact]
    public void Disposal_UnsubscribesWorkspaceAndKeepsTheSharedSceneUsable()
    {
        var workspace = new EditorWorkspace(EditorScene.Demo()); var view = new EditorView(workspace); using (var ui = Create(view)) ui.Update();
        Assert.True(view.IsDisposed); workspace.Select("green-cube"); Assert.Equal("green-cube", workspace.SelectedId); Assert.Equal(7, workspace.EntityCount);
    }
    private static UiSession Create(Element root) => new(root, new Surface(), new UiViewport(1280, 800));
    private static IEnumerable<Element> Descendants(Element root) { foreach (var child in root.Children) { yield return child; foreach (var next in Descendants(child)) yield return next; } }
    private static void Click(UiSession ui, Button button) { var b = button.Bounds; ui.HandleInput(new(InputKind.PointerDown, b.X + 8, b.Y + 8)); ui.HandleInput(new(InputKind.PointerUp, b.X + 8, b.Y + 8)); }
    private sealed class Box : Element { protected override Size MeasureCore(Size available, ITextLayoutService text) => new(10, 10); }
    private sealed class TextLayout : ITextLayoutService { public Size Measure(string text, float fontSize, string fontFamily) => new(text.Length * fontSize / 2, fontSize); }
    private sealed class Surface : IUiRenderSurface
    { public ITextLayoutService TextLayout { get; } = new TextLayout(); public void VerifyAvailable() { } public void Resize(UiViewport viewport) { } public void Render(Element root, UiViewport viewport) { } public void Dispose() { } }
}
