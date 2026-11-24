using System.Text.Json;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.Hierarchy;
using TrueMoon.Aether.Editor.Tests.Inspector;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Editor.UI.Hierarchy;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Runtime.Semantics;
using TrueMoon.Argentis;
using Xunit;
using Xunit.Abstractions;

namespace TrueMoon.Aether.Editor.Tests.DocumentsUi;

public sealed class EditorSemanticSnapshotTests(ITestOutputHelper output)
{
    [Fact]
    public void EditorSnapshot_KeepsTargetIdsAndReportsRoutedFocusAndCommandState()
    {
        using var hierarchyModel = new HierarchyFixture();
        using var inspectorModel = new InspectorFixture();
        var hierarchy = EditorHierarchyView.Create(hierarchyModel.Model, new EditorHierarchyViewStyle()).Width(370);
        var inspector = new EditorInspectorView(inspectorModel.Model, new EditorInspectorViewStyle()).Fill();
        using var ui = InspectorFixture.Ui(HStack.Create(hierarchy, inspector));
        Assert.Throws<InvalidOperationException>(() => ui.CaptureSemantics());
        ui.Update();

        const string searchId = "editor.hierarchy.search", treeId = "editor.hierarchy.tree";
        const string applyId = "editor.inspector.apply", undoId = "editor.inspector.undo", redoId = "editor.inspector.redo";
        var xId = $"editor.inspector.transform.{DocumentData.Transform.Value:N}.x";
        var initial = ui.CaptureSemantics();
        Assert.False(initial.IsTruncated);
        Assert.All(initial.Nodes, node => Assert.Null(node.AccessibleName));
        Assert.Equal(UiSemanticRole.Tree, Node(initial, treeId).Role);
        Assert.True(Node(initial, treeId).IsSelected);
        Assert.True(Node(initial, "editor.hierarchy.create").IsEnabled);
        Assert.False(Node(initial, applyId).IsEnabled);
        Assert.False(Node(initial, undoId).IsEnabled);
        Assert.False(Node(initial, redoId).IsEnabled);

        const string moveToRootId = "editor.hierarchy.move-to-root";
        Assert.True(Node(initial, moveToRootId).IsEnabled);
        Click(ui, hierarchy.Tree.GetItemBounds(HierarchyFixture.Parent)!.Value);
        ui.Update();
        var selected = ui.CaptureSemantics();
        Assert.Equal(treeId, Assert.Single(selected.Nodes.Where(node => node.IsFocused)).Id);
        Assert.False(Node(selected, moveToRootId).IsEnabled);
        Assert.Equal(Targets(initial), Targets(selected));

        Click(ui, Node(initial, searchId).VisibleBounds);
        ui.Update();
        var searching = ui.CaptureSemantics(includeAccessibleNames: true);
        var search = Assert.Single(searching.Nodes.Where(node => node.IsFocused));
        Assert.Equal(searchId, search.Id);
        Assert.Equal(UiSemanticRole.TextInput, search.Role);
        Assert.False(string.IsNullOrWhiteSpace(search.AccessibleName));
        ui.HandleInput(new(InputKind.Text, Text: "sample-private-query"));
        ui.Update();
        Assert.Equal("sample-private-query", hierarchy.Search.Value);
        Assert.DoesNotContain("sample-private-query", JsonSerializer.Serialize(ui.CaptureSemantics(includeAccessibleNames: true)));

        ui.SetTheme(Theme.Light);
        ui.Resize(new(1500, 900, 1.25f));
        Assert.Throws<InvalidOperationException>(() => ui.CaptureSemantics());
        ui.Update();
        var resized = ui.CaptureSemantics();
        Assert.Equal(new UiViewport(1500, 900, 1.25f), resized.Viewport);
        Assert.True(resized.LayoutGeneration > searching.LayoutGeneration);
        Assert.Equal(Targets(initial), Targets(resized));
        Assert.Equal(searchId, Assert.Single(resized.Nodes.Where(node => node.IsFocused)).Id);

        inspector.Scroll.BringIntoView(Node(resized, xId).Bounds);
        ui.Update();
        Click(ui, Node(ui.CaptureSemantics(), xId).VisibleBounds);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        Assert.True(ui.HandleInput(new(InputKind.Text, Text: "9")).Handled);
        ui.Update();
        var editing = ui.CaptureSemantics(includeAccessibleNames: true);
        Assert.Equal(xId, Assert.Single(editing.Nodes.Where(node => node.IsFocused)).Id);
        Assert.True(Node(editing, applyId).IsEnabled);
        Assert.True(Node(editing, "editor.inspector.cancel").IsEnabled);
        Assert.False(Node(initial, applyId).IsEnabled);
        Assert.Equal(Targets(initial), Targets(editing));

        inspector.Scroll.BringIntoView(Node(editing, applyId).Bounds);
        ui.Update();
        Click(ui, Node(ui.CaptureSemantics(), applyId).VisibleBounds);
        ui.Update();
        var applied = ui.CaptureSemantics();
        Assert.False(Node(applied, applyId).IsEnabled);
        Assert.True(Node(applied, undoId).IsEnabled);
        Assert.False(Node(applied, redoId).IsEnabled);
        var transform = inspectorModel.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Transform];
        Assert.Equal(9, transform.Properties["x"].Value.GetNumber());

        Click(ui, Node(applied, undoId).VisibleBounds);
        ui.Update();
        var undone = ui.CaptureSemantics();
        Assert.False(Node(undone, undoId).IsEnabled);
        Assert.True(Node(undone, redoId).IsEnabled);
        Click(ui, Node(undone, redoId).VisibleBounds);
        ui.Update();
        Assert.False(Node(ui.CaptureSemantics(), redoId).IsEnabled);

        var oldField = inspector.Properties[(DocumentData.Transform, "x")].Input;
        inspectorModel.Model.Refresh(null);
        ui.Update();
        var empty = ui.CaptureSemantics();
        Assert.DoesNotContain(empty.Nodes, node => node.Id == xId);
        Assert.True(empty.TreeGeneration > applied.TreeGeneration);
        inspectorModel.Refresh();
        ui.Update();
        var recreated = ui.CaptureSemantics();
        Assert.NotSame(oldField, inspector.Properties[(DocumentData.Transform, "x")].Input);
        Assert.Equal(xId, Node(recreated, xId).Id);
        output.WriteLine("uiqa-snapshot=" + JsonSerializer.Serialize(editing));
    }

    [Fact]
    public void Snapshot_ReportsTruncationAndOverlayOrderAndRejectsDuplicateIds()
    {
        var owner = Button.Simple("Open").SemanticId("owner");
        var host = new OverlayHost(owner);
        using var ui = InspectorFixture.Ui(host);
        ui.Update();
        var popup = Button.Simple("Popup").SemanticId("popup");
        host.Open(owner, popup, owner.Bounds, ui);
        ui.Focus(popup);
        ui.Capture(popup);
        ui.Update();
        var full = ui.CaptureSemantics();
        Assert.Equal(new[] { null, "owner", "popup" }, full.Nodes.Select(node => node.Id));
        var semantics = Node(full, "popup");
        Assert.Equal(0, semantics.ParentIndex);
        Assert.Equal(UiSemanticRole.Popup, semantics.Role);
        Assert.True(semantics.IsActivePopup);
        Assert.True(semantics.IsFocused);
        Assert.True(semantics.HasPointerCapture);

        var partial = ui.CaptureSemantics(maximumNodes: 2);
        Assert.True(partial.IsTruncated);
        Assert.Equal(new[] { null, "owner" }, partial.Nodes.Select(node => node.Id));
        popup.SemanticId = "owner";
        ui.Update();
        Assert.Throws<InvalidOperationException>(() => ui.CaptureSemantics());
    }

    private static UiSemanticNode Node(UiSemanticSnapshot snapshot, string id) =>
        Assert.Single(snapshot.Nodes.Where(node => node.Id == id));

    private static string[] Targets(UiSemanticSnapshot snapshot) =>
        snapshot.Nodes.Select(node => node.Id).OfType<string>().Order(StringComparer.Ordinal).ToArray();

    private static void Click(UiSession session, Rect bounds)
    {
        Assert.True(bounds.Width > 0 && bounds.Height > 0);
        var x = bounds.X + bounds.Width / 2;
        var y = bounds.Y + bounds.Height / 2;
        Assert.True(session.HandleInput(new(InputKind.PointerDown, x, y, Button: 0)).Handled);
        session.HandleInput(new(InputKind.PointerUp, x, y, Button: 0));
    }
}
