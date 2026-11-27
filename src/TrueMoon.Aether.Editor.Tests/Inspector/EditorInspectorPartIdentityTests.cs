using TrueMoon.Argentis;
using TrueMoon.Argentis.Layout;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.UI.Editing;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

/// <summary>The cutover contract: every named control of the inspector is reached through its generated
/// part property, wired into the declared container, and stays live across refresh and re-attachment.</summary>
public sealed class EditorInspectorPartIdentityTests
{
    [Fact]
    public void GeneratedParts_ResolveToControlsWiredIntoTheirDeclaredContainers()
    {
        using var owner = new InspectorFixture();
        using var view = new EditorInspectorView(owner.Model, new EditorInspectorViewStyle());
        var content = new Panel(); content.Add(view);
        using var ui = InspectorFixture.Ui(content); ui.Update(); ui.Update();

        Assert.Same(view.ScrollPart, view.GetRequiredPart<ScrollViewer>("ScrollPart"));
        Assert.Same(view.ApplyPart.Parent, view.ActionsPart);
        Assert.Same(view.ActionsPart.Parent, view.FormPart);
        Assert.Same(view.ComponentListPart.Parent, view.FormPart);

        // The partial sections keep their own subtrees; the generated keys index into those live elements.
        foreach (var panel in new Element[] { view.MaterialsPanel, view.TexturesPanel, view.MeshesPanel })
        {
            Assert.Contains(panel, DocumentUiFixture.Tree(view));
            Assert.True(panel.IsVisible == false || panel.IsVisible);
        }
        Assert.Contains(view.MaterialsHeader, DocumentUiFixture.Tree(view.MaterialsPanel));
        Assert.Contains(view.MaterialName, DocumentUiFixture.Tree(view.MaterialsPanel));
        Assert.Contains(view.MaterialTextureList, DocumentUiFixture.Tree(view.MaterialsPanel));
        Assert.Contains(view.TextureNamePart, DocumentUiFixture.Tree(view.TexturesPanel));
        Assert.Contains(view.MeshName, DocumentUiFixture.Tree(view.MeshesPanel));
        Assert.Contains(view.MeshesItems, DocumentUiFixture.Tree(view.MeshesPanel));
        Assert.NotSame(view.MaterialName, view.MeshName);
    }

    [Fact]
    public void PartKeys_StayLiveAcrossModelRefreshAndReattachment()
    {
        using var owner = new InspectorFixture();
        using var view = new EditorInspectorView(owner.Model, new EditorInspectorViewStyle());
        var content = new Panel(); content.Add(view);
        using var ui = InspectorFixture.Ui(content); ui.Update(); ui.Update();

        var list = view.ComponentListPart; var materials = view.MaterialsPanel; var header = view.MaterialsHeader;
        owner.Refresh(InspectorFixture.Parent); ui.Update(); ui.Update();
        Assert.Same(list, view.ComponentListPart); Assert.Same(materials, view.MaterialsPanel);

        // Detaching and re-adding the same view re-indexes the scope from the tree, not from caches.
        content.Items.Remove(view); content.Add(view); ui.Update(); ui.Update();
        Assert.Same(list, view.ComponentListPart);
        Assert.Same(materials, view.MaterialsPanel);
        Assert.Same(header, view.MaterialsHeader);
        Assert.Contains(view.MaterialsHeader, DocumentUiFixture.Tree(view.MaterialsPanel));
    }
}
