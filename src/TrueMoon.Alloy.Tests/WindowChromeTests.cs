using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class WindowChromeTests
{
    private static readonly FixedTextLayout TextLayout = new();
    private static void Arrange(Element root, float width = 400, float height = 200)
    { root.Measure(new Size(width, height), TextLayout); root.Arrange(new Rect(0, 0, width, height)); }

    [Fact]
    public void Map_CaptionIsInheritedByText_ControlsRemainClientIncludingDisabled()
    {
        using var root = new HStack { WindowRegion = WindowRegionRole.Caption, Height = 40 };
        var text = new Text("title") { Width = 80 };
        var button = new Button("close") { Width = 80, IsEnabled = false, WindowRegion = WindowRegionRole.Caption };
        var editor = new TextBox { Width = 160 };
        root.Add(text); root.Add(button); root.Add(editor); Arrange(root);
        var map = WindowRegionMap.Create(root);
        Assert.Equal(WindowRegionRole.Caption, map.HitTest(20, 20));
        Assert.Equal(WindowRegionRole.Client, map.HitTest(100, 20));
        Assert.Equal(WindowRegionRole.Client, map.HitTest(180, 20));
        Assert.Equal(WindowRegionRole.Caption, map.HitTest(390, 20));
        Assert.Equal(WindowRegionRole.Client, map.HitTest(400, 20));
        Assert.Equal(WindowRegionRole.Client, map.HitTest(20, 40));
    }
    [Fact]
    public void Map_ClientSubtreeCannotBeMadeCaptionByDescendant()
    {
        using var root = new Panel { WindowRegion = WindowRegionRole.Client };
        root.Add(new Text("nested") { WindowRegion = WindowRegionRole.Caption }); Arrange(root);
        Assert.Equal(WindowRegionRole.Client, WindowRegionMap.Create(root).HitTest(20, 20));
    }
    [Fact]
    public void Map_TopmostVisibleRegionWins_AndSnapshotSurvivesRemovalAndDisposal()
    {
        using var root = new Panel { WindowRegion = WindowRegionRole.Caption };
        var overlay = new Text("overlay") { WindowRegion = WindowRegionRole.Client, Width = 80 };
        root.Add(overlay); Arrange(root);
        var before = WindowRegionMap.Create(root);
        overlay.IsVisible = false; Arrange(root);
        var hidden = WindowRegionMap.Create(root);
        root.Items.Remove(overlay); overlay.Dispose();
        Assert.Equal(WindowRegionRole.Client, before.HitTest(20, 20));
        Assert.Equal(WindowRegionRole.Caption, hidden.HitTest(20, 20));
        Assert.Equal(WindowRegionRole.Caption, WindowRegionMap.Create(root).HitTest(20, 20));
    }
    [Fact]
    public void Map_ClipsScrolledDescendants_AndDoesNotExposeOffscreenCaption()
    {
        using var root = new Panel();
        var scroll = new ScrollViewer { Height = 50, Padding = new Thickness(10), WindowRegion = WindowRegionRole.Caption };
        scroll.SetContent(new SizedElement(200, 300) { WindowRegion = WindowRegionRole.Client });
        root.Add(scroll); Arrange(root); scroll.Offset = 30; Arrange(root);
        var map = WindowRegionMap.Create(root);
        Assert.Equal(WindowRegionRole.Caption, map.HitTest(5, 20));
        Assert.Equal(WindowRegionRole.Client, map.HitTest(20, 20));
        Assert.Equal(WindowRegionRole.Client, map.HitTest(20, 60));
    }
    [Fact]
    public void Map_EmptyAndInvalidPointsAreClient_NullAndDisposedRootsRejected()
    {
        Assert.Equal(WindowRegionRole.Client, WindowRegionMap.Empty.HitTest(float.NaN, 0));
        using var root = new Panel { WindowRegion = WindowRegionRole.Caption }; Arrange(root);
        var map = WindowRegionMap.Create(root);
        Assert.Equal(WindowRegionRole.Client, map.HitTest(-1, 20));
        Assert.Equal(WindowRegionRole.Client, map.HitTest(float.PositiveInfinity, 20));
        Assert.Throws<ArgumentNullException>(() => WindowRegionMap.Create(null!));
        root.Dispose(); Assert.Throws<ObjectDisposedException>(() => WindowRegionMap.Create(root));
    }
    [Fact]
    public void Frame_ReservesTitleHeightAndGivesContentRemainingViewport()
    {
        var title = new SizedElement { Height = 40, WindowRegion = WindowRegionRole.Caption };
        var content = new SizedElement();
        using var frame = new WindowFrame(title, content); Arrange(frame);
        Assert.Equal(new Rect(0, 0, 400, 40), title.Bounds);
        Assert.Equal(new Rect(0, 40, 400, 160), content.Bounds);
        Assert.Equal(new Size(400, 160), content.Available);
        Assert.Equal(WindowRegionRole.Caption, WindowRegionMap.Create(frame).HitTest(20, 20));
        Assert.Equal(WindowRegionRole.Client, WindowRegionMap.Create(frame).HitTest(20, 80));
        Arrange(frame, 400, 20); Assert.Equal(0, content.Bounds.Height);
        frame.Dispose(); Assert.True(title.IsDisposed); Assert.True(content.IsDisposed);
    }
    [Fact]
    public void Frame_RejectsSameOrOwnedChildWithoutChangingOwnership()
    {
        using var child = new SizedElement();
        Assert.Throws<ArgumentException>(() => new WindowFrame(child, child));
        Assert.Null(child.Parent); Assert.False(child.IsDisposed);
        using var owner = new Panel(); owner.Add(child);
        using var other = new SizedElement();
        Assert.Throws<ArgumentException>(() => new WindowFrame(child, other));
        Assert.Same(owner, child.Parent); Assert.Null(other.Parent);
    }
    [Theory]
    [InlineData(-1)] [InlineData(4)]
    public void Role_RejectsUnknownValues(int value)
    {
        using var element = new SizedElement();
        Assert.Throws<ArgumentOutOfRangeException>(() => element.WindowRegion = (WindowRegionRole)value);
        Assert.Equal(WindowRegionRole.Inherit, element.WindowRegion);
    }
    [Fact]
    public void Options_DefaultsAreOptIn_AndSizesIncludeExactEndpoints()
    {
        var options = new WindowChromeOptions { MinimumSize = new Size(200, 100), MaximumSize = new Size(600, 500) };
        options.Validate(); Assert.False(options.NativeDrag); Assert.True(options.Resizable);
        options.ValidateSize(new Size(200, 100)); options.ValidateSize(new Size(600, 500));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.ValidateSize(new Size(float.BitDecrement(200), 100)));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.ValidateSize(new Size(600, float.BitIncrement(500))));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.ValidateSize(new Size(float.NaN, 200)));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.ValidateSize(new Size(200, float.PositiveInfinity)));
        new WindowChromeOptions().ValidateSize(new Size(10000, 10000));
    }
    [Theory]
    [InlineData(-1)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)] [InlineData(64.001f)]
    public void Options_InvalidBorderRejected(float value)
        => Assert.Throws<ArgumentOutOfRangeException>(() => (new WindowChromeOptions { ResizeBorder = value }).Validate());
    [Fact]
    public void Options_InvalidAndInvertedSizesRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => (new WindowChromeOptions { MinimumSize = new Size(0, 100) }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (new WindowChromeOptions { MinimumSize = new Size(100, float.PositiveInfinity) }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (new WindowChromeOptions { MaximumSize = new Size(159, 100) }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (new WindowChromeOptions { MaximumSize = new Size(160, float.NaN) }).Validate());
        (new WindowChromeOptions { ResizeBorder = 0 }).Validate();
        (new WindowChromeOptions { ResizeBorder = 64 }).Validate();
    }
    [Fact]
    public void Map_MaximizeButtonIsExplicit_AndDisabledOrClientAncestorExcludesIt()
    {
        using var root = new Panel { WindowRegion = WindowRegionRole.Caption };
        var button = new Button("maximize") { Width = 80, Height = 40, WindowRegion = WindowRegionRole.Maximize };
        root.Add(button); Arrange(root);
        var enabled = WindowRegionMap.Create(root);
        Assert.Equal(WindowRegionRole.Maximize, enabled.HitTest(20, 20));
        button.IsEnabled = false;
        var disabled = WindowRegionMap.Create(root);
        Assert.Equal(WindowRegionRole.Client, disabled.HitTest(20, 20));
        button.IsEnabled = true; root.WindowRegion = WindowRegionRole.Client;
        Assert.Equal(WindowRegionRole.Client, WindowRegionMap.Create(root).HitTest(20, 20));
        Assert.Equal(WindowRegionRole.Maximize, enabled.HitTest(20, 20)); // snapshot is independent
    }
    [Fact]
    public void Map_MaximizeRoleRespectsTopmostOcclusionAndDisabledAncestor()
    {
        using var root = new Panel();
        root.Add(new Button("maximize") { Width = 80, Height = 40, WindowRegion = WindowRegionRole.Maximize });
        var overlay = new Text("overlay") { Width = 80, Height = 40, WindowRegion = WindowRegionRole.Client };
        root.Add(overlay); Arrange(root);
        Assert.Equal(WindowRegionRole.Client, WindowRegionMap.Create(root).HitTest(20, 20));
        overlay.IsVisible = false; Arrange(root);
        Assert.Equal(WindowRegionRole.Maximize, WindowRegionMap.Create(root).HitTest(20, 20));
        root.IsEnabled = false;
        Assert.Equal(WindowRegionRole.Client, WindowRegionMap.Create(root).HitTest(20, 20));
    }
    [Fact]
    public void Options_SnapRequiresResizable_AndSystemMenuCanBeDisabledIndependently()
    {
        Assert.False(new WindowChromeOptions().NativeSnapLayouts);
        Assert.True(new WindowChromeOptions().SystemMenu);
        var fixedWindow = new WindowChromeOptions { Resizable = false, NativeDrag = true, SystemMenu = false };
        fixedWindow.Validate();
        var error = Assert.Throws<ArgumentException>(() => (fixedWindow with { NativeSnapLayouts = true }).Validate());
        Assert.Equal("NativeSnapLayouts", error.ParamName);
        (fixedWindow with { Resizable = true, NativeSnapLayouts = true }).Validate();
    }
    [Theory]
    [InlineData(330)] [InlineData(640)]
    public void DemoTitleBar_NarrowSnapSizeKeepsWindowButtonsVisibleAndRegionsCorrect(float width)
    {
        using var title = new AlloyTest.CustomTitleBar(new WindowCommands());
        Arrange(title, width, 44);
        Assert.True(title.Children[1].Bounds.Width >= 160);
        var minimize = title.Children[2].Bounds;
        var maximize = title.Children[3].Bounds;
        var close = title.Children[4].Bounds;
        Assert.Equal(width - 8, close.X + close.Width);
        Assert.Equal(40, maximize.Width);
        Assert.Equal(minimize.X + minimize.Width, maximize.X);
        Assert.Equal(maximize.X + maximize.Width, close.X);
        Assert.True(minimize.X >= title.Children[0].Bounds.X + title.Children[0].Bounds.Width);
        var regions = WindowRegionMap.Create(title);
        Assert.Equal(WindowRegionRole.Caption, regions.HitTest(20, 20));
        Assert.Equal(WindowRegionRole.Maximize, regions.HitTest(maximize.X + 20, 20));
        Assert.Equal(WindowRegionRole.Client, regions.HitTest(close.X + 20, 20));
        Assert.Equal(WindowRegionRole.Client, regions.HitTest(title.Children[1].Bounds.X + 10, 20));
    }
    [Fact]
    public void DemoTitleBar_FirstUpdateAndResizeLeaveSessionReadyForPresentation()
    {
        using var session = new UiSession(new WindowFrame(new AlloyTest.CustomTitleBar(new WindowCommands()), new Panel()),
            new LayoutSurface(), new UiViewport(640, 400));
        Assert.True(session.Update());
        Assert.False(session.NeedsUpdate);
        Assert.False(session.Update());
        session.Resize(new UiViewport(330, 300));
        Assert.True(session.Update());
        Assert.False(session.NeedsUpdate);
        Assert.False(session.Update());
    }
    private sealed class LayoutSurface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout => WindowChromeTests.TextLayout;
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) => root.Draw(new RecordingDrawingContext());
        public void Dispose() { }
    }
    private sealed class WindowCommands : IWindowCommands
    {
        public UiViewport Viewport => new(640, 400);
        public UiWindowState State => UiWindowState.Normal;
        public event Action? RenderRequested { add { } remove { } }
        public event Action<UiInput>? Input { add { } remove { } }
        public void Run() { }
        public void Close() { }
        public void Minimize() { }
        public void Maximize() { }
        public void Restore() { }
        public void Dispose() { }
    }
}
