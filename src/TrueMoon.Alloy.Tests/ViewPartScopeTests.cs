using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Views;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class ViewPartScopeTests
{
    [Fact]
    public void NamedChildViewRoot_IsPartOfTheParentScope_BodyStaysPrivate()
    {
        using var shell = new ShellView();
        using var ui = new UiSession(shell, new Surface(), new(400, 480));
        Assert.Same(shell.DetailsInstance, shell.GetRequiredPart<DetailsView>("DetailsPart"));
        Assert.Equal("Inside", shell.DetailsInstance.GetRequiredPart<Text>("InsidePart").Value);
        var missing = Assert.Throws<KeyNotFoundException>(() => shell.GetRequiredPart<Text>("InsidePart"));
        Assert.Contains("InsidePart", missing.Message);
        var mismatch = Assert.Throws<InvalidOperationException>(() => shell.GetRequiredPart<Panel>("DetailsPart"));
        Assert.Contains("expected 'Panel'", mismatch.Message);
    }

    [Fact]
    public void DuplicatePartName_AcrossChildRootBoundary_IsRejectedBeforeCommit()
    {
        using var clash = new ClashView();
        using var ui = new UiSession(clash, new Surface(), new(400, 480));
        var slot = clash.GetRequiredPart<Panel>("Slot");
        var error = Assert.Throws<InvalidOperationException>(() => slot.Add(new DetailsView().Named("Duplicated")));
        Assert.Contains("already contains a part named 'Duplicated'", error.Message);
        Assert.True(clash.TryGetPart<Text>("Duplicated", out _));
        Assert.False(clash.TryGetPart<DetailsView>("Duplicated", out _));
    }

    [Fact]
    public void MovingChildWithinOneScope_ReindexesAndKeepsTheInstance()
    {
        using var shell = new ShellView();
        using var ui = new UiSession(shell, new Surface(), new(400, 480));
        var details = shell.DetailsInstance;
        shell.MoveDetails();
        Assert.Same(details, shell.GetRequiredPart<DetailsView>("DetailsPart"));
        Assert.Same(details, shell.GetRequiredPart<Panel>("Tray").Children[0]);
    }

    [Fact]
    public void MovingChildIntoAnotherView_MovesTheIndexEntry()
    {
        var shell = new ShellView();
        var host = new HostView();
        var root = new Panel();
        root.Add(shell);
        root.Add(host);
        using var ui = new UiSession(root, new Surface(), new(400, 480));
        var details = shell.DetailsInstance;
        shell.GetRequiredPart<Panel>("Body").Items.Remove(details);
        host.GetRequiredPart<Panel>("Host").Add(details);

        Assert.False(shell.TryGetPart<DetailsView>("DetailsPart", out _));
        Assert.Same(details, host.GetRequiredPart<DetailsView>("DetailsPart"));
        Assert.Equal("Inside", details.GetRequiredPart<Text>("InsidePart").Value);
    }

    [Fact]
    public void Reattach_KeepsTheIndexedChildViewInstance()
    {
        var shell = new ShellView();
        var root = new Panel();
        root.Add(shell);
        using var ui = new UiSession(root, new Surface(), new(400, 480));
        var details = shell.GetRequiredPart<DetailsView>("DetailsPart");
        root.Items.Remove(shell);
        root.Add(shell);
        Assert.Same(details, shell.GetRequiredPart<DetailsView>("DetailsPart"));
    }

    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport, UiPresenters? presenters = null) =>
            UiTreeRenderer.Draw(root, new RecordingDrawingContext(), presenters);
        public void Dispose() { }
    }
}

internal sealed class DetailsView : View
{
    public DetailsView() => Content(ui => ui.Text("Inside").Named("InsidePart"));
}

internal sealed class ShellView : View
{
    public DetailsView DetailsInstance { get; } = new();

    public ShellView() => Content(ui =>
    {
        var root = ui.Panel();
        root.WithChildren(
            ui.Panel(DetailsInstance.Named("DetailsPart")).Named("Body"),
            ui.Panel().Named("Tray"));
        return root;
    });

    public void MoveDetails()
    {
        var details = this.DetailsPart;
        this.Body.Items.Remove(details);
        this.Tray.Add(details);
    }
}

internal sealed class HostView : View
{
    public HostView() => Content(ui => ui.Panel().Named("Host"));
}

internal sealed class ClashView : View
{
    public ClashView() => Content(ui =>
    {
        var root = ui.Panel(ui.Text("First").Named("Duplicated"));
        root.WithChildren(ui.Panel().Named("Slot"));
        return root;
    });
}
