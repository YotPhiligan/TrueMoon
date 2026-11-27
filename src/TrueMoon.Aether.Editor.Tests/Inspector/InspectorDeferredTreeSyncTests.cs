using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Scenes;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class InspectorDeferredTreeSyncTests
{
    private static string Independent => DocumentData.Id(3).ToString("D");
    private static ComponentId Note => new(DocumentData.Id(41));
    private static ComponentId[] ChildComponents =>
        [DocumentData.Transform, DocumentData.Appearance, DocumentData.Interaction, DocumentData.Custom, DocumentData.Local];

    [Fact]
    public void ContextReplacementsBeforeUpdate_BuildSectionsOnlyForTheCurrentContext()
    {
        using var first = new InspectorFixture(InspectorFixture.WithIndependentEntity(false));
        using var second = new InspectorFixture(InspectorFixture.WithIndependentEntity(false));
        using var current = new InspectorFixture();
        first.Refresh(Independent);
        second.Refresh(Independent);
        var view = EditorInspectorView.Create(first.Model, new EditorInspectorViewStyle());
        var host = new Panel().WithChildren(view);
        using var ui = InspectorFixture.Ui(host);
        ui.Update();
        Assert.Equal([Note], view.Components.Keys);
        Assert.Single(view.Properties);

        view.DataContext = second.Model;
        view.DataContext = current.Model;
        // One window covers both replacements: no section of a superseded context is built while it waits.
        Assert.True(view.ComponentListPart.IsInputBlocked);
        Assert.Equal([Note], view.Components.Keys);
        Assert.Single(view.Properties);

        ui.Update();
        Assert.False(view.ComponentListPart.IsInputBlocked);
        Assert.Equal(ChildComponents.ToHashSet(), view.Components.Keys.ToHashSet());
        Assert.DoesNotContain(Note, view.Components.Keys);
        Assert.Equal(23, view.Properties.Count);
    }

    [Fact]
    public void DetachWhilePassIsQueued_BuildsNoNodesAndRedrawsTheCurrentContextOnReattach()
    {
        using var previous = new InspectorFixture();
        using var current = new InspectorFixture(InspectorFixture.WithIndependentEntity(false));
        current.Refresh(Independent);
        var view = EditorInspectorView.Create(previous.Model, new EditorInspectorViewStyle());
        var host = new Panel().WithChildren(view);
        using var ui = InspectorFixture.Ui(host);
        ui.Update();
        Assert.Equal(5, view.Components.Count);

        view.DataContext = current.Model;
        Assert.True(view.ComponentListPart.IsInputBlocked);
        host.Items.Remove(view);
        Assert.False(view.ComponentListPart.IsInputBlocked);
        Assert.Equal(5, view.Components.Count);
        ui.Update();
        Assert.Equal(5, view.Components.Count);

        host.Add(view);
        ui.Update();
        Assert.Equal([Note], view.Components.Keys);
        Assert.Equal([(Note, "note")], view.Properties.Keys);
        Assert.False(view.ComponentListPart.IsInputBlocked);
    }
}
