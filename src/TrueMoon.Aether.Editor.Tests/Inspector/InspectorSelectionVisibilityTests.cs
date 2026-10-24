using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Hierarchy;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Scenes;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class InspectorSelectionVisibilityTests
{
    [Fact]
    public void Workspace_ClearSelectionIsIdempotentAndSurvivesSameSceneRevisionUntilAnotherSceneIsOpened()
    {
        using var test = new InspectorFixture();
        var workspace = new EditorWorkspace(EditorScene.Demo()); workspace.SetDocument(test.Owner.Document.Read().Content);
        var scene = workspace.Scene; var ids = workspace.EntityIds; var notices = 0;
        workspace.SelectionChanged += () => notices++;
        workspace.ClearSelection(); workspace.ClearSelection();
        Assert.Null(workspace.SelectedId); Assert.Empty(workspace.Properties()); Assert.Same(scene, workspace.Scene);
        Assert.Same(ids, workspace.EntityIds); Assert.Equal(2, workspace.EntityCount); Assert.Equal(1, notices);
        Assert.True(test.Owner.Document.Apply(DocumentData.Edit(7, 25)).Succeeded);
        workspace.SetDocument(test.Owner.Document.Read().Content);
        Assert.Null(workspace.SelectedId); Assert.Empty(workspace.Properties()); Assert.Equal(8, workspace.Scene.Revision);
        var document = test.Owner.Document.Read().Content!.Document;
        var other = new SceneDocument(new(DocumentData.Id(101)), document.Revision, document.Entities, document.Definitions, document.Schemas);
        using var sessions = new DocumentData.Sessions(); using var session = sessions.Create(other);
        workspace.SetDocument(session.Read());
        Assert.Equal(DocumentData.Parent.Value.ToString("D"), workspace.SelectedId); Assert.Equal(2, workspace.EntityCount);
        Assert.NotEmpty(workspace.Properties()); Assert.Equal(other.Id.Value.ToString("D"), workspace.Scene.Id);
    }

    [Fact]
    public void ViewModel_InspectorVisibilityChangesOnlyAcrossEmptyAndSelectedContexts()
    {
        var workspace = new EditorWorkspace(EditorScene.Demo());
        using var model = new EditorViewModel(workspace, new((_, _) => Task.CompletedTask, CancellationToken.None));
        var changes = new List<string?>(); model.PropertyChanged += (_, change) => changes.Add(change.PropertyName);
        Assert.True(model.IsInspectorVisible);
        model.ClearSelection(); model.ClearSelection();
        Assert.Null(model.SelectedId); Assert.False(model.IsInspectorVisible); Assert.Empty(model.Properties);
        Assert.Equal(1, changes.Count(name => name == nameof(EditorViewModel.IsInspectorVisible)));
        model.Select("green-cube"); model.Select("switch");
        Assert.Equal("switch", model.SelectedId); Assert.True(model.IsInspectorVisible);
        Assert.Equal(2, changes.Count(name => name == nameof(EditorViewModel.IsInspectorVisible)));
        Assert.Throws<ArgumentException>(() => model.Select("missing"));
        Assert.True(model.IsInspectorVisible); Assert.Equal("switch", model.SelectedId);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(3f)]
    [InlineData(800f)]
    [InlineData(1280f)]
    public void Layout_ViewportFillsSameBodyWhenInspectorIsShownHiddenAndRestored(float width)
    {
        var inspector = new Panel();
        using var layout = new EditorLayout(new Panel(), new Panel(), new Panel(), inspector, new Panel());
        layout.Measure(new(width, 720), new TextLayout()); layout.Arrange(new(0, 0, width, 720));
        var before = layout.Viewport.Bounds;
        Assert.Equal(new Rect(0, 52, width, 640), before);
        inspector.IsVisible = false;
        layout.Measure(new(width, 720), new TextLayout()); layout.Arrange(new(0, 0, width, 720));
        var after = layout.Viewport.Bounds;
        Assert.Equal(0f, inspector.Bounds.Width); Assert.Equal(width, after.X + after.Width, 3);
        Assert.Equal(before, after);
        inspector.IsVisible = true;
        layout.Measure(new(width, 720), new TextLayout()); layout.Arrange(new(0, 0, width, 720));
        Assert.Equal(before, layout.Viewport.Bounds);
    }

    [Fact]
    public void Shell_ClearSelectionFadesIslandOverInvariantViewportAndRestoresSameChromeWithoutCommittingDraftOrRetainingFocus()
    {
        using var test = new InspectorFixture();
        var workspace = new EditorWorkspace(EditorScene.Demo()); workspace.SetDocument(test.Owner.Document.Read().Content); workspace.Select(InspectorFixture.Child);
        using var model = new EditorViewModel(workspace, new((_, _) => Task.CompletedTask, CancellationToken.None), transform: test.Model);
        var view = new EditorView(model, new EditorViewStyle(), null, null, false, null, null,
            new Factory<EditorInspectorView>(() => new(test.Model, new EditorInspectorViewStyle())));
        var clock = new HeaderClock();
        using var ui = InspectorFixture.Ui(view, clock); Drain();
        var inspector = view.Inspector!; var form = inspector.Form; var scroll = inspector.Scroll; var apply = inspector.Apply;
        var input = inspector.Properties[(DocumentData.Custom, "text")].Input;
        ui.Focus(input); test.Property(DocumentData.Custom, "text").Value = "uncommitted draft"; Drain();
        var document = test.Owner.Document.Read().Content; var viewport = view.ViewportFrame.Bounds;
        Assert.Same(view.InspectorIsland, view.InspectorPanel);
        Assert.True(view.InspectorPanel.IsVisible); Assert.True(model.IsInspectorVisible); Assert.True(test.Model.HasDraft);
        model.ClearSelection(); Drain();
        Assert.False(model.IsInspectorVisible); Assert.False(view.InspectorIsland.IsOpen);
        Assert.True(view.InspectorPanel.IsVisible); Assert.False(inspector.IsEffectivelyEnabled);
        Assert.Equal(viewport, view.ViewportFrame.Bounds);
        clock.Advance(160); Drain();
        Assert.False(view.InspectorPanel.IsVisible); Assert.Equal(0f, view.InspectorPanel.Bounds.Width);
        Assert.Equal(viewport, view.ViewportFrame.Bounds);
        Assert.True(input.IsDisposed); Assert.False(input.IsFocused); Assert.False(test.Model.HasDraft);
        Assert.Same(document, test.Owner.Document.Read().Content); Assert.False(test.Owner.Document.Read().IsDirty);
        ui.SetTheme(Theme.Light); Drain(); model.Select(InspectorFixture.Child); Drain(); clock.Advance(160); Drain();
        Assert.True(view.InspectorPanel.IsVisible); Assert.Equal(viewport, view.ViewportFrame.Bounds);
        Assert.Same(inspector, view.Inspector); Assert.Same(form, inspector.Form); Assert.Same(scroll, inspector.Scroll); Assert.Same(apply, inspector.Apply);
        Assert.False(inspector.IsDisposed); Assert.False(apply.IsDisposed);
        Assert.Equal(DocumentData.Text, Assert.IsType<RichTextEditor>(inspector.Properties[(DocumentData.Custom, "text")].Input).Text);
        Assert.False(inspector.Properties[(DocumentData.Custom, "text")].Input.IsFocused);
        Assert.Equal(26f, apply.Width); Assert.Equal(0, test.Owner.Document.Read().Content!.UndoCount);

        void Drain() { ui.Update(); ui.Update(); }
    }

    [Fact]
    public void Shell_EmptyLaunchStartsWithFullViewportAndClosedInspectorWithoutInventedEntityContext()
    {
        using var test = new InspectorFixture(); test.Owner.Run(DocumentCommand.Close); test.Model.Refresh(null);
        var workspace = new EditorWorkspace(new("empty", 0, []), false);
        using var model = new EditorViewModel(workspace, new((_, _) => Task.CompletedTask, CancellationToken.None), transform: test.Model);
        var view = new EditorView(model, new EditorViewStyle(), null, null, false, null, null,
            new Factory<EditorInspectorView>(() => new(test.Model, new EditorInspectorViewStyle())));
        using var ui = InspectorFixture.Ui(view); ui.Update(); ui.Update();
        Assert.Null(model.SelectedId); Assert.False(model.IsInspectorVisible); Assert.False(view.InspectorPanel.IsVisible);
        Assert.False(view.InspectorIsland.IsOpen); Assert.Equal(0f, view.InspectorIsland.Reveal);
        Assert.False(view.InspectorIsland.NeedsFrame); Assert.Equal(0f, view.InspectorPanel.Bounds.Width); Assert.Empty(view.Inspector!.Components);
        Assert.Equal(view.Bounds.X + view.Bounds.Width, view.ViewportFrame.Bounds.X + view.ViewportFrame.Bounds.Width, 3);
        Assert.False(test.Model.HasSelection); Assert.False(test.Model.CanChangeComponents); Assert.Equal(0, model.EntityCount);
    }

    [Fact]
    public void HierarchyEscape_ClearsSharedSelectionAndHidesInspectorWithoutChangingDocument()
    {
        using var test = new InspectorFixture();
        var workspace = new EditorWorkspace(EditorScene.Demo()); workspace.SetDocument(test.Owner.Document.Read().Content); workspace.Select(InspectorFixture.Child);
        using var hierarchy = new EditorHierarchyViewModel(workspace, test.Owner.Commands, test.Model);
        using var model = new EditorViewModel(workspace, new((_, _) => Task.CompletedTask, CancellationToken.None), transform: test.Model, hierarchy: hierarchy);
        var view = new EditorView(model, new EditorViewStyle(), null, null, false, null,
            new Factory<EditorHierarchyView>(() => new(hierarchy, new EditorHierarchyViewStyle())),
            new Factory<EditorInspectorView>(() => new(test.Model, new EditorInspectorViewStyle())));
        var clock = new HeaderClock();
        using var ui = InspectorFixture.Ui(view, clock); ui.Update(); ui.Update();
        var tree = view.Hierarchy!.Tree; var before = test.Owner.Document.Read().Content;
        ui.Focus(tree); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); ui.Update(); ui.Update();
        Assert.Null(workspace.SelectedId); Assert.Null(tree.SelectedId); Assert.False(model.IsInspectorVisible);
        Assert.False(view.InspectorIsland.IsOpen); Assert.False(view.Inspector!.IsEffectivelyEnabled);
        clock.Advance(160); ui.Update(); ui.Update();
        Assert.False(view.InspectorPanel.IsVisible); Assert.Empty(test.Model.Components);
        Assert.Same(before, test.Owner.Document.Read().Content); Assert.False(test.Owner.Document.Read().IsDirty);
    }

    private sealed class TextLayout : ITextLayoutService
    { public Size Measure(string text, float fontSize, string fontFamily) => new(text.Length * fontSize / 2, fontSize); }
}
