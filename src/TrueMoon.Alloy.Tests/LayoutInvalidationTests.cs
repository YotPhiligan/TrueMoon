using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using Xunit;

namespace TrueMoon.Alloy.Tests;

/// <summary>Measure and arrange validity: unchanged subtrees are skipped, a dirty leaf walks only its ancestors.</summary>
public sealed class LayoutInvalidationTests
{
    [Fact]
    public void UnchangedAndRootOnlyInvalidation_SkipChildWalks()
    {
        var (ui, root, first, second) = Session();
        using (ui)
        {
            ui.Update();
            Assert.True(first.Measures > 0 && first.Arranges > 0 && second.Measures > 0 && second.Arranges > 0);
            first.Reset(); second.Reset();
            ui.Update();
            Assert.Equal(0, first.Measures); Assert.Equal(0, first.Arranges);
            Assert.Equal(0, second.Measures); Assert.Equal(0, second.Arranges);
            root.Invalidate(Invalidation.Layout);
            ui.Update();
            Assert.Equal(0, first.Measures); Assert.Equal(0, first.Arranges);
            Assert.Equal(0, second.Measures); Assert.Equal(0, second.Arranges);
        }
    }

    [Fact]
    public void LeafValueEdit_WalksAncestorPathAndLeavesSiblingAlone()
    {
        var (ui, _, first, second) = Session();
        using (ui)
        {
            ui.Update(); first.Reset(); second.Reset();
            var untouched = second.Children[0].Bounds;
            ((Text)first.Children[0]).Value = "changed text";
            ui.Update();
            Assert.Equal(1, first.Measures); Assert.Equal(1, first.Arranges);
            Assert.Equal(0, second.Measures); Assert.Equal(0, second.Arranges);
            Assert.Equal(untouched, second.Children[0].Bounds);
        }
    }

    [Fact]
    public void ViewportResize_Re_measuresEverySubtree()
    {
        var (ui, _, first, second) = Session();
        using (ui)
        {
            ui.Update(); first.Reset(); second.Reset();
            ui.Resize(new UiViewport(120, 90));
            ui.Update();
            Assert.Equal(1, first.Measures); Assert.Equal(1, second.Measures);
            Assert.Equal(1, first.Arranges); Assert.Equal(1, second.Arranges);
        }
    }

    [Fact]
    public void ScrollOffsetChange_ArrangesContentAgain()
    {
        var content = new CountingStack { Height = 400 }.WithChildren(new SizedElement());
        var scroll = new ScrollViewer();
        scroll.SetContent(content);
        using var ui = new UiSession(scroll, new HeadlessSurface(), new UiViewport(240, 120));
        ui.Update();
        var bounds = content.Bounds;
        content.Reset();
        scroll.Offset = 40;
        ui.Update();
        Assert.Equal(0, content.Measures);
        Assert.Equal(1, content.Arranges);
        Assert.NotEqual(bounds, content.Bounds);
    }

    private static (UiSession ui, VStack root, CountingStack first, CountingStack second) Session()
    {
        var first = new CountingStack { Height = 20 }.WithChildren(new Text { Value = "one" });
        var second = new CountingStack { Height = 20 }.WithChildren(new Text { Value = "two" });
        var root = new VStack().WithChildren(first, second);
        return (new UiSession(root, new HeadlessSurface(), new UiViewport(240, 140)), root, first, second);
    }

    private sealed class CountingStack : VStack
    {
        public int Measures { get; private set; }
        public int Arranges { get; private set; }
        public void Reset() { Measures = 0; Arranges = 0; }
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            Measures++;
            return base.MeasureCore(available, text);
        }
        protected override void ArrangeCore(Rect contentBounds)
        {
            Arranges++;
            base.ArrangeCore(contentBounds);
        }
    }

    private sealed class HeadlessSurface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport, UiPresenters? presenters = null) { }
        public void Dispose() { }
    }
}
