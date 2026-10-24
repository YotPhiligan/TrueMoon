using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Hierarchy;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class InspectorIslandStyleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PanelMaterials_AreScopedSeparateFromMenuAndThemeChangeRetainsInputsDraftFocusAndScroll(bool light)
    {
        using var test = new InspectorFixture();
        var workspace = new EditorWorkspace(EditorScene.Demo());
        workspace.SetDocument(test.Owner.Document.Read().Content); workspace.Select(InspectorFixture.Child);
        using var hierarchy = new EditorHierarchyViewModel(workspace, test.Owner.Commands, test.Model);
        using var model = new EditorViewModel(workspace, new((_, _) => Task.CompletedTask, CancellationToken.None),
            transform: test.Model, hierarchy: hierarchy);
        var view = new EditorView(model, new EditorViewStyle(), null, null, false, null,
            new Factory<EditorHierarchyView>(() => new(hierarchy, new EditorHierarchyViewStyle())),
            new Factory<EditorInspectorView>(() => new(test.Model, new EditorInspectorViewStyle())));
        using var ui = InspectorFixture.Ui(view);
        var theme = light ? Theme.Light : Theme.Dark; ui.SetTheme(theme); Drain();
        var inspector = view.Inspector!;
        var surface = Assert.IsType<Border>(view.InspectorIsland.Child);
        var tree = view.Hierarchy!.Tree;
        var field = inspector.Properties[(DocumentData.Transform, "x")];
        var input = field.Input; var section = inspector.Components[DocumentData.Transform].Section;
        Assert.Equal(new Thickness(8), view.HierarchyIsland.Padding);
        Assert.Equal(new Thickness(8), view.InspectorIsland.Padding);
        Assert.Equal(theme.PanelBackdrop, surface.Backdrop); Assert.Equal(theme.PanelBackdrop, view.Hierarchy.Backdrop);
        Assert.Equal(Color.Transparent, surface.Background); Assert.Equal(Color.Transparent, section.Background);
        Assert.Equal(8f, surface.CornerRadius); Assert.Equal(1f, surface.BorderWidth);
        Assert.Equal(8f, view.Hierarchy.CornerRadius); Assert.Equal(1f, view.Hierarchy.BorderWidth);
        Assert.Equal(255, input.Background.A);
        Assert.Equal(view.Bounds.X + 16, view.Hierarchy.Bounds.X, 3);
        Assert.Equal(view.Bounds.X + view.Bounds.Width - 16, surface.Bounds.X + surface.Bounds.Width, 3);
        Assert.False(view.ViewportTitle.IsVisible);
        var viewport = view.ViewportFrame.Bounds;
        field.Property.Value = "15";
        inspector.Scroll.Offset = 35; Drain(); var offset = inspector.Scroll.Offset;
        Assert.True(offset > 0); ui.Focus(input);
        var fallback = (light ? Theme.Dark : Theme.Light) with { PanelBackdrop = null };
        ui.SetTheme(fallback); Drain();
        Assert.Same(inspector, view.Inspector); Assert.Same(tree, view.Hierarchy.Tree);
        Assert.Same(input, inspector.Properties[(DocumentData.Transform, "x")].Input);
        Assert.Same(section, inspector.Components[DocumentData.Transform].Section);
        Assert.True(input.IsFocused); Assert.Equal("15", field.Property.Value); Assert.True(test.Model.HasDraft);
        Assert.Equal(offset, inspector.Scroll.Offset); Assert.Equal(viewport, view.ViewportFrame.Bounds);
        Assert.Null(surface.Backdrop); Assert.Null(view.Hierarchy.Backdrop);
        Assert.Equal(fallback.Surface, surface.Background); Assert.Equal(fallback.Surface, view.Hierarchy.Background);
        Assert.Equal(Color.Transparent, section.Background); Assert.Equal(255, input.Background.A);
        Assert.NotNull(fallback.MenuBackdrop); Assert.False(test.Owner.Document.Read().IsDirty);
        Assert.Equal(0, test.Owner.Document.Read().Content!.UndoCount);

        void Drain() { ui.Update(); ui.Update(); }
    }
}
