using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Menus;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.Hierarchy;
using TrueMoon.Aether.Editor.Tests.Inspector;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Editor.UI.Hierarchy;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Runtime.Semantics;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.DocumentsUi;

public sealed class EditorSemanticInputTests
{
    [Fact]
    public void Hierarchy_RoutesSearchAndRejectsDisabledAndStaleTargets()
    {
        using var fixture = new HierarchyFixture();
        var view = EditorHierarchyView.Create(fixture.Model, new EditorHierarchyViewStyle());
        using var ui = InspectorFixture.Ui(view);
        ui.Update();

        const string search = "editor.hierarchy.search", tree = "editor.hierarchy.tree", moveToRoot = "editor.hierarchy.move-to-root";
        var initial = ui.CaptureSemantics();
        Assert.Equal(UiSemanticTargetStatus.NotFound, ui.FindSemanticTarget(initial, "editor.hierarchy.absent").Status);
        Assert.Equal(UiSemanticTargetStatus.IncompleteSnapshot, ui.FindSemanticTarget(ui.CaptureSemantics(maximumNodes: 1), search).Status);
        var text = new UiInput(InputKind.Text, Text: "branch");
        var withoutFocus = ui.HandleSemanticInput(initial, search, text);
        Assert.Equal(UiSemanticTargetStatus.NotFocused, withoutFocus.Status);
        Assert.False(withoutFocus.Input.Handled);
        Assert.Empty(view.Search.Value);

        var searchBounds = Ready(ui, initial, search).VisibleBounds;
        var popup = new Button("Cover search").Width(searchBounds.Width).Height(searchBounds.Height).SemanticId("test.search-cover");
        view.Overlays.Open(view.Search, popup, new(searchBounds.X, searchBounds.Y, 0, 0), ui);
        ui.Update();
        var covered = ui.HandleSemanticInput(ui.CaptureSemantics(), search, At(InputKind.PointerDown, searchBounds));
        Assert.Equal(UiSemanticTargetStatus.Obstructed, covered.Status);
        Assert.Equal("test.search-cover", covered.HitTargetId);
        Assert.False(covered.Input.Handled);
        Assert.False(view.Search.IsFocused);
        view.Overlays.Close(restoreFocus: false);
        ui.Update();

        Click(ui, search);
        var typed = ui.HandleSemanticInput(ui.CaptureSemantics(), search, text);
        AssertRouted(typed, search);
        Assert.Equal(search, typed.FocusedId);
        Assert.Equal("branch", fixture.Model.SearchText);
        ui.Update();

        var beforeSelection = ui.CaptureSemantics();
        Assert.Equal(UiSemanticTargetStatus.Ready, ui.FindSemanticTarget(beforeSelection, moveToRoot).Status);
        Click(ui, tree, view.Tree.GetItemBounds(HierarchyFixture.Parent)!.Value);
        Assert.Equal(HierarchyFixture.Parent, fixture.Workspace.SelectedId);
        var afterSelection = ui.CaptureSemantics();
        var unavailable = ui.FindSemanticTarget(afterSelection, moveToRoot);
        Assert.Equal(UiSemanticTargetStatus.Disabled, unavailable.Status);
        var disabled = ui.HandleSemanticInput(afterSelection, moveToRoot, At(InputKind.PointerDown, unavailable.Node!.VisibleBounds));
        Assert.Equal(UiSemanticTargetStatus.Disabled, disabled.Status);
        Assert.False(disabled.Input.Handled);
        Assert.Equal(tree, disabled.FocusedId);

        ui.Resize(new(1100, 760));
        Assert.Equal(UiSemanticTargetStatus.StaleSnapshot, ui.FindSemanticTarget(afterSelection, search).Status);
        ui.Update();
        var stale = ui.HandleSemanticInput(beforeSelection, search, text);
        Assert.Equal(UiSemanticTargetStatus.StaleSnapshot, stale.Status);
        Assert.False(stale.Input.Handled);
        Assert.Equal(tree, stale.FocusedId);
        Assert.Equal("branch", fixture.Model.SearchText);
        Assert.Equal(0, fixture.Owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void Inspector_HitTestRejectsCoveredApplyAndStaleTransformBeforeRouting()
    {
        using var fixture = new InspectorFixture();
        var view = new EditorInspectorView(fixture.Model, new EditorInspectorViewStyle());
        var host = new OverlayHost(view);
        using var ui = InspectorFixture.Ui(host);
        ui.Update();

        const string apply = "editor.inspector.apply", blocker = "test.popup";
        var x = $"editor.inspector.transform.{DocumentData.Transform.Value:N}.x";
        var initial = ui.CaptureSemantics();
        var disabled = ui.FindSemanticTarget(initial, apply);
        Assert.Equal(UiSemanticTargetStatus.Disabled, disabled.Status);
        Assert.Equal(UiSemanticTargetStatus.Disabled, ui.HandleSemanticInput(initial, apply, At(InputKind.PointerUp, disabled.Node!.VisibleBounds)).Status);

        view.ScrollPart.BringIntoView(Ready(ui, initial, x).Bounds);
        ui.Update();
        Click(ui, x);
        var focused = ui.CaptureSemantics();
        AssertRouted(ui.HandleSemanticInput(focused, x, new(InputKind.KeyDown, Key: UiKey.A, Control: true)), x);
        AssertRouted(ui.HandleSemanticInput(focused, x, new(InputKind.Text, Text: "9")), x);
        ui.Update();
        view.ScrollPart.BringIntoView(Ready(ui, ui.CaptureSemantics(), apply).Bounds);
        ui.Update();

        var coverClicks = 0;
        var targetBounds = Ready(ui, ui.CaptureSemantics(), apply).VisibleBounds;
        var popup = new Button("Cover").Width(120).Height(36).SemanticId(blocker).OnClick(() => coverClicks++);
        host.Open(view.ApplyPart, popup, new(targetBounds.X, targetBounds.Y, 0, 0), ui);
        ui.Update();
        var covered = ui.CaptureSemantics();
        Assert.True(Ready(ui, covered, blocker).IsActivePopup);
        foreach (var kind in new[] { InputKind.PointerDown, InputKind.PointerUp })
        {
            var rejected = ui.HandleSemanticInput(covered, apply, At(kind, targetBounds));
            Assert.Equal(UiSemanticTargetStatus.Obstructed, rejected.Status);
            Assert.Equal(blocker, rejected.HitTargetId);
            Assert.False(rejected.Input.Handled);
        }
        Assert.Equal(0, coverClicks);
        Assert.Equal(0, fixture.Owner.Document.Read().Content!.UndoCount);
        Assert.True(fixture.Model.HasDraft);

        host.Close(restoreFocus: false);
        ui.Update();
        Assert.Equal(UiSemanticTargetStatus.StaleSnapshot, ui.HandleSemanticInput(covered, apply, At(InputKind.PointerDown, targetBounds)).Status);
        var elsewhere = new Button("Non-overlapping popup").SemanticId("test.outside-popup");
        host.Open(view.Properties[(DocumentData.Transform, "x")].Input, elsewhere, new(750, 450, 0, 0), ui);
        ui.Update();
        var intercepted = ui.HandleSemanticInput(ui.CaptureSemantics(), apply, At(InputKind.PointerDown, targetBounds));
        Assert.Equal(UiSemanticTargetStatus.Intercepted, intercepted.Status);
        Assert.True(intercepted.Input.Handled);
        Assert.Null(host.Popup);
        Assert.True(fixture.Model.HasDraft);
        Assert.Equal(0, fixture.Owner.Document.Read().Content!.UndoCount);
        ui.Update();
        Click(ui, apply);
        var applied = fixture.Owner.Document.Read().Content!;
        Assert.Equal(1, applied.UndoCount);
        Assert.Equal(9, applied.Resolved.Entities[DocumentData.Child].Components[DocumentData.Transform].Properties["x"].Value.GetNumber());

        var beforeRecreate = ui.CaptureSemantics();
        var replaced = view.Properties[(DocumentData.Transform, "x")].Input;
        fixture.Model.Refresh(null);
        ui.Update();
        Assert.Equal(UiSemanticTargetStatus.NotFound, ui.FindSemanticTarget(ui.CaptureSemantics(), x).Status);
        fixture.Refresh();
        ui.Update();
        Assert.True(replaced.IsDisposed);
        var stale = ui.HandleSemanticInput(beforeRecreate, x, new(InputKind.Text, Text: "0"));
        Assert.Equal(UiSemanticTargetStatus.StaleSnapshot, stale.Status);
        Assert.False(stale.Input.Handled);
        Assert.Equal(x, Ready(ui, ui.CaptureSemantics(), x).Id);
        Assert.False(fixture.Model.HasDraft);

        view.ScrollPart.Offset = 0;
        ui.Update();
        var wheelSnapshot = ui.CaptureSemantics();
        var wheelPoint = Ready(ui, wheelSnapshot, x).VisibleBounds;
        var scrolled = ui.HandleSemanticInput(wheelSnapshot, "editor.inspector", At(InputKind.Wheel, wheelPoint) with { WheelDelta = -2 });
        AssertRouted(scrolled, x);
        Assert.True(view.ScrollPart.Offset > 0);
    }

    [Fact]
    public void Hierarchy_CapturedGestureCannotBeSentToAnotherIdAndCancelsOnFocusLoss()
    {
        using var fixture = new HierarchyFixture();
        var view = EditorHierarchyView.Create(fixture.Model, new EditorHierarchyViewStyle());
        using var ui = InspectorFixture.Ui(view);
        ui.Update();
        const string create = "editor.hierarchy.create", search = "editor.hierarchy.search";
        var snapshot = ui.CaptureSemantics();
        var point = Ready(ui, snapshot, search).VisibleBounds;
        var down = ui.HandleSemanticInput(snapshot, create, At(InputKind.PointerDown, Ready(ui, snapshot, create).VisibleBounds));
        AssertRouted(down, create);
        Assert.Equal(create, down.CapturedId);
        var blocked = ui.HandleSemanticInput(snapshot, search, At(InputKind.PointerMove, point));
        Assert.Equal(UiSemanticTargetStatus.PointerCaptured, blocked.Status);
        Assert.Equal(create, blocked.HitTargetId);
        Assert.False(blocked.Input.Handled);
        var continuing = ui.HandleSemanticInput(snapshot, create, At(InputKind.PointerMove, point));
        Assert.Equal(UiSemanticTargetStatus.Routed, continuing.Status);
        Assert.Equal(create, continuing.HitTargetId);
        Assert.Equal(create, continuing.CapturedId);

        ui.HandleInput(new(InputKind.FocusLost));
        var afterLoss = ui.CaptureSemantics();
        Assert.DoesNotContain(afterLoss.Nodes, node => node.IsFocused || node.HasPointerCapture);
        var up = ui.HandleSemanticInput(afterLoss, create, At(InputKind.PointerUp, point));
        Assert.Equal(UiSemanticTargetStatus.OutsideTarget, up.Status);
        Assert.Equal(0, fixture.Owner.Document.Read().Content!.UndoCount);
    }

    private static void Click(UiSession session, string id, Rect? point = null)
    {
        var snapshot = session.CaptureSemantics();
        var bounds = point ?? Ready(session, snapshot, id).VisibleBounds;
        AssertRouted(session.HandleSemanticInput(snapshot, id, At(InputKind.PointerDown, bounds)), id);
        session.Update();
        var up = session.HandleSemanticInput(session.CaptureSemantics(), id, At(InputKind.PointerUp, bounds));
        Assert.Equal(UiSemanticTargetStatus.Routed, up.Status);
        Assert.Equal(id, up.HitTargetId);
        session.Update();
    }

    private static UiSemanticNode Ready(UiSession session, UiSemanticSnapshot snapshot, string id)
    {
        var lookup = session.FindSemanticTarget(snapshot, id);
        Assert.Equal(UiSemanticTargetStatus.Ready, lookup.Status);
        return Assert.IsType<UiSemanticNode>(lookup.Node);
    }

    private static void AssertRouted(UiSemanticInputResult result, string id)
    {
        Assert.Equal(UiSemanticTargetStatus.Routed, result.Status);
        Assert.Equal(id, result.HitTargetId);
        Assert.True(result.Input.Handled);
    }

    private static UiInput At(InputKind kind, Rect bounds)
    {
        Assert.True(bounds.Width > 0 && bounds.Height > 0);
        return new(kind, bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2, Button: 0);
    }
}
