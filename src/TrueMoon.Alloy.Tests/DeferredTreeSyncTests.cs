using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Layout;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Runtime.Semantics;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class DeferredTreeSyncTests
{
    [Flags] private enum Work { None = 0, Structure = 1, Present = 2 }
    private static UiSession Create(Element root) => new(root, new HeadlessSurface(), new UiViewport(240, 140));

    [Fact]
    public void RepeatedRequestsBeforeUpdate_RunOnePassWithTheCombinedRequest()
    {
        var owner = new VStack();
        using var root = new VStack(); root.Add(owner);
        using var ui = Create(root); ui.Update();
        var passes = new List<Work>();
        var queue = new DeferredTreeSync<Work>(owner, Work.None, static (a, b) => a | b, pass => passes.Add(pass));

        queue.Request(Work.Structure);
        Assert.True(queue.IsPending);
        queue.Request(Work.Present);
        queue.Request(Work.Structure);
        Assert.True(queue.IsPending);
        ui.Update();

        Assert.False(queue.IsPending);
        Assert.Equal([Work.Structure | Work.Present], passes);
    }

    [Fact]
    public void DetachBeforeUpdate_DropsQueuedPassWithoutCreatingNodes()
    {
        var owner = new VStack();
        using var root = new VStack(); root.Add(owner);
        using var ui = Create(root); ui.Update();
        var passes = 0;
        var queue = new DeferredTreeSync<Work>(owner, Work.None, static (a, b) => a | b,
            _ => { passes++; owner.Add(new Text("node")); });
        queue.BlocksInput(owner);

        queue.BlockInput();
        queue.Request(Work.Structure);
        Assert.True(queue.IsPending);
        Assert.True(owner.IsInputBlocked);

        root.Items.Remove(owner);
        Assert.False(owner.IsAttached);
        Assert.False(queue.IsPending);
        Assert.False(owner.IsInputBlocked);
        ui.Update();
        Assert.Equal(0, passes);
        Assert.Empty(owner.Items);
        owner.Dispose();
    }

    [Fact]
    public void Reattach_DoesNotReplayTheSupersededPass()
    {
        var owner = new VStack();
        using var root = new VStack(); root.Add(owner);
        using var ui = Create(root); ui.Update();
        var passes = new List<Work>();
        var queue = new DeferredTreeSync<Work>(owner, Work.None, static (a, b) => a | b, pass => passes.Add(pass));

        queue.Request(Work.Structure);
        root.Items.Remove(owner);
        root.Add(owner);
        ui.Update();
        Assert.Empty(passes);

        queue.Request(Work.Present);
        ui.Update();
        Assert.Equal([Work.Present], passes);
    }

    [Fact]
    public void BlockingWindow_ExcludesSubtreeFromRoutedInputUntilThePassRuns()
    {
        var clicks = 0;
        const string id = "test.deferred-action";
        var action = new Button("Action").SemanticId(id).OnClick(() => clicks++);
        var container = new Panel().Fill().WithChildren(action);
        var owner = new VStack(); owner.Add(container);
        using var ui = Create(owner); ui.Update();
        var bounds = action.Bounds;
        var x = bounds.X + bounds.Width / 2;
        var y = bounds.Y + bounds.Height / 2;

        var passes = 0;
        var queue = new DeferredTreeSync<Work>(owner, Work.None, static (a, b) => a | b, _ => passes++);
        queue.BlocksInput(container);
        queue.BlockInput();
        queue.Request(Work.Structure);

        Assert.True(container.IsInputBlocked);
        Assert.True(action.IsEffectivelyEnabled);
        var snapshot = ui.CaptureSemantics();
        Assert.Equal(UiSemanticTargetStatus.Disabled, ui.FindSemanticTarget(snapshot, id).Status);
        Assert.False(ui.HandleInput(new(InputKind.PointerDown, x, y)).PointerCaptured);
        Assert.False(ui.HandleInput(new(InputKind.PointerUp, x, y)).Handled);
        Assert.Equal(0, clicks);

        ui.Update();
        Assert.False(container.IsInputBlocked);
        Assert.False(queue.IsPending);
        Assert.Equal(1, passes);
        Assert.Equal(UiSemanticTargetStatus.Ready, ui.FindSemanticTarget(ui.CaptureSemantics(), id).Status);
        Assert.True(ui.HandleInput(new(InputKind.PointerDown, x, y)).PointerCaptured);
        ui.HandleInput(new(InputKind.PointerUp, x, y));
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void DetachedOwner_SynchronizesInlineWithoutOpeningAWindow()
    {
        var owner = new VStack();
        var passes = new List<Work>();
        var queue = new DeferredTreeSync<Work>(owner, Work.None, static (a, b) => a | b, pass => passes.Add(pass));

        queue.Request(Work.Structure);
        Assert.False(queue.IsPending);
        queue.Request(Work.Present);
        Assert.False(queue.IsPending);
        Assert.Equal([Work.Structure, Work.Present], passes);
        owner.Dispose();
    }

    [Fact]
    public void FailingSynchronize_ReleasesBlockingAndPropagatesFromUpdate()
    {
        var owner = new VStack();
        using var root = new VStack(); root.Add(owner);
        using var ui = Create(root); ui.Update();
        var queue = new DeferredTreeSync<Work>(owner, Work.None, static (a, b) => a | b,
            _ => throw new InvalidOperationException("synchronization failed"));
        queue.BlocksInput(owner);
        queue.BlockInput();
        queue.Request(Work.Structure);
        Assert.True(owner.IsInputBlocked);

        var error = Assert.Throws<InvalidOperationException>(() => ui.Update());
        Assert.Equal("synchronization failed", error.Message);
        Assert.False(owner.IsInputBlocked);
        Assert.False(queue.IsPending);

        // A rejected pass leaves the queue reusable: the next request opens the window again.
        var passes = 0;
        var reopened = new DeferredTreeSync<Work>(owner, Work.None, static (a, b) => a | b, _ => passes++);
        reopened.Request(Work.Structure);
        Assert.True(reopened.IsPending);
        ui.Update();
        Assert.Equal(1, passes);
    }

    [Fact]
    public void EmptyRequest_KeepsTheWindowClosed()
    {
        var owner = new VStack();
        using var root = new VStack(); root.Add(owner);
        using var ui = Create(root); ui.Update();
        var passes = 0;
        var queue = new DeferredTreeSync<Work>(owner, Work.None, static (a, b) => a | b, _ => passes++);

        queue.Request(Work.None);
        Assert.False(queue.IsPending);
        ui.Update();
        Assert.Equal(0, passes);
    }

    [Fact]
    public void RequestDuringSynchronization_OpensASecondWindowWithoutLosingTheRequest()
    {
        var owner = new VStack();
        using var root = new VStack(); root.Add(owner);
        using var ui = Create(root); ui.Update();
        var passes = new List<Work>();
        DeferredTreeSync<Work> queue = null!;
        queue = new DeferredTreeSync<Work>(owner, Work.None, static (a, b) => a | b, pass =>
        {
            passes.Add(pass);
            if (passes.Count == 1) { queue.Request(Work.Present); queue.BlockInput(); }
        });
        queue.BlocksInput(owner);
        queue.BlockInput();
        queue.Request(Work.Structure);

        ui.Update();
        Assert.Equal([Work.Structure, Work.Present], passes);
        Assert.False(queue.IsPending);
        Assert.False(owner.IsInputBlocked);
    }

    [Fact]
    public void RejectedDispatch_ReleasesBlockingAndLetsTheNextRequestOpenAWindow()
    {
        var clicks = 0;
        const string id = "test.deferred-retry";
        var action = new Button("Action").SemanticId(id).OnClick(() => clicks++);
        var container = new Panel().Fill().WithChildren(action);
        var owner = new VStack(); owner.Add(container);
        using var root = new VStack(); root.Add(owner);
        using var ui = new UiSession(root, new HeadlessSurface(), new UiViewport(240, 140), null,
            new UiDispatchOptions { Capacity = 2 });
        ui.Update();
        var bounds = action.Bounds;
        var x = bounds.X + bounds.Width / 2;
        var y = bounds.Y + bounds.Height / 2;

        var passes = 0;
        var queue = new DeferredTreeSync<Work>(owner, Work.None, static (a, b) => a | b, _ => passes++);
        queue.BlocksInput(container);
        root.Dispatch(() => { });
        root.Dispatch(() => { });
        queue.BlockInput();

        var error = Assert.Throws<InvalidOperationException>(() => queue.Request(Work.Structure));
        Assert.Contains("capacity", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, passes);
        Assert.False(queue.IsPending);
        // No queued pass exists to release the block, so the rejection itself must.
        Assert.False(container.IsInputBlocked);
        Assert.Equal(UiSemanticTargetStatus.Ready, ui.FindSemanticTarget(ui.CaptureSemantics(), id).Status);
        Assert.True(ui.HandleInput(new(InputKind.PointerDown, x, y)).PointerCaptured);
        ui.HandleInput(new(InputKind.PointerUp, x, y));
        Assert.Equal(1, clicks);

        ui.Update();
        queue.Request(Work.Structure);
        Assert.True(queue.IsPending);
        ui.Update();
        Assert.Equal(1, passes);
        Assert.False(queue.IsPending);
        Assert.False(container.IsInputBlocked);
    }

    private sealed class HeadlessSurface : IUiRenderSurface
    {
        public int Renders { get; private set; }
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport, UiPresenters? presenters = null)
        {
            TrueMoon.Alloy.Runtime.UiTreeRenderer.Draw(root, new RecordingDrawingContext(), presenters); Renders++;
        }
        public void Dispose() { }
    }
}
