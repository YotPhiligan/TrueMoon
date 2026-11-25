using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Windows;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Services;
using Xunit;

namespace TrueMoon.Alloy.Hosting.Tests;

public sealed class HostedIconButtonPresenterTests
{
    [Fact]
    public async Task UseAlloy_RegistersFactoryForFreshStockPresenters()
    {
        await using var app = App.Build(context => context.UseAlloy(o => o.UseSkiaRaster().UseExternalHost()));
        var factory = Required<IFactory<IElementPresenter<IconButtonPaintState>>>(app);
        var first = Assert.IsType<IconButtonPresenter>(factory.Create());
        Assert.NotSame(first, Assert.IsType<IconButtonPresenter>(factory.Create()));
    }

    [Fact]
    public async Task UseAlloy_BorrowsRegisteredFactoryAndOwnsOneLazyPresenterPerSession()
    {
        var created = new List<Presenter>();
        var custom = new Factory<IElementPresenter<IconButtonPaintState>>(() =>
        {
            var presenter = new Presenter();
            created.Add(presenter);
            return presenter;
        });
        await using var app = App.Build(context =>
        {
            context.Services(services => services.Singleton<IFactory<IElementPresenter<IconButtonPaintState>>>(_ => custom));
            context.UseAlloy(o => o.UseSkiaRaster().UseExternalHost());
        });
        Assert.Same(custom, Required<IFactory<IElementPresenter<IconButtonPaintState>>>(app));
        var sessions = Required<IUiSessionFactory>(app);
        using var first = Create(sessions);
        using var second = Create(sessions);
        Assert.Empty(created);

        Assert.True(first.Update());
        var a = Assert.Single(created);
        Assert.Equal(1, a.Draws);
        first.SetTheme(Theme.Light);
        Assert.True(first.Update());
        Assert.Same(a, Assert.Single(created));
        Assert.Equal(2, a.Draws);

        Assert.True(second.Update());
        Assert.Equal(2, created.Count);
        var b = created[1];
        Assert.NotSame(a, b);
        Assert.Equal(1, b.Draws);
        first.Dispose();
        Assert.Equal(1, a.Disposals);
        Assert.Equal(0, b.Disposals);
        second.SetTheme(Theme.Light);
        Assert.True(second.Update());
        Assert.Equal(2, b.Draws);

        Required<HostedUiSessionFactory>(app).Dispose();
        Assert.Equal(1, b.Disposals);
        second.Dispose();
        Assert.Equal(1, a.Disposals);
        Assert.Equal(1, b.Disposals);
    }

    [Fact]
    public async Task ConfiguredPresenter_DrawsNestedContentAndOverlaysInTreeOrder()
    {
        using var outer = new Panel();
        outer.Add(new Panel());
        var calls = new List<string>();
        var factory = new Factory<IElementPresenter<string>>(() => new LayerRecorder(calls));
        await using var app = App.Build(context => context.UseAlloy(o => o.UseSkiaRaster().UseExternalHost()
            .ConfigurePresenters(p => p.Register<Panel, string>(
                panel => ReferenceEquals(panel, outer) ? "outer" : "inner", factory))));
        using var session = Required<IUiSessionFactory>(app).Create(outer, new RasterUiTarget(), new UiViewport(32, 32));
        Assert.True(session.Update());
        Assert.Equal(new[] { "before outer", "before inner", "after inner", "after outer" }, calls);
    }

    [Fact]
    public async Task TypedPresenterFactory_ResolvesOnceWhenHostedFactoryIsCreated()
    {
        var resolutions = 0;
        var creations = 0;
        var calls = new List<string>();
        await using var app = App.Build(context =>
        {
            context.Services(services => services.Transient<IFactory<IElementPresenter<string>>>(_ =>
            {
                resolutions++;
                return new Factory<IElementPresenter<string>>(() => { creations++; return new LayerRecorder(calls); });
            }));
            context.UseAlloy(o => o.UseSkiaRaster().UseExternalHost()
                .UsePresenterFactory<Button, string>(button => button.Value));
        });
        var sessions = Required<IUiSessionFactory>(app);
        Assert.Equal(1, resolutions);
        var button = new Button("first");
        using var session = sessions.Create(button, new RasterUiTarget(), new UiViewport(32, 32));
        Assert.Equal(0, creations);
        Assert.True(session.Update());
        button.Value = "second";
        Assert.True(session.Update());
        Assert.Equal(new[] { "before first", "after first", "before second", "after second" }, calls);
        Assert.Equal(1, resolutions);
        Assert.Equal(1, creations);
    }

    private sealed class LayerRecorder(List<string> calls) : IAfterChildrenPresenter<string>
    {
        public void Draw(string state, IDrawingContext context) => calls.Add("before " + state);
        public void DrawAfterChildren(string state, IDrawingContext context) => calls.Add("after " + state);
    }

    [Fact]
    public async Task SharedOwnerLoop_HoverAndCloseLeaveSiblingIdleAndAbleToReceiveInput()
    {
        var loop = new UiOwnerLoop();
        var firstReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var siblingReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var siblingDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeout = TimeSpan.FromSeconds(10);
        RasterWindow? first = null, sibling = null;
        UiDiagnosticsSnapshot baseline = default;
        bool hovered = false, siblingSawHover = false;
        try
        {
            loop.Schedule(() =>
            {
                sibling = new RasterWindow(loop);
                Assert.True(sibling.Tick());
                baseline = sibling.Diagnostics.Snapshot;
                Assert.Equal(1, sibling.Painter.Draws);
            }, () =>
            {
                // Service the real on-demand pump both after hover and after close.
                var presented = sibling!.Tick();
                if (!hovered) return false;
                Assert.False(presented);
                var current = sibling.Diagnostics.Snapshot;
                Assert.Equal(baseline.Measure, current.Measure);
                Assert.Equal(baseline.Arrange, current.Arrange);
                Assert.Equal(baseline.Render, current.Render);
                Assert.Equal(1, sibling.Painter.Draws);
                Assert.Equal(0, sibling.Painter.Disposals);
                siblingSawHover = true;
                if (!firstDone.Task.IsCompletedSuccessfully) return false;
                Assert.Equal(1, first!.Painter.Disposals);
                sibling.Input(InputKind.PointerDown);
                sibling.Input(InputKind.PointerUp);
                Assert.Equal(1, sibling.Clicks);
                Assert.True(sibling.Tick());
                Assert.Equal(2, sibling.Painter.Draws);
                return true;
            }, () => sibling?.Dispose(), () => false, siblingReady, siblingDone);
            await siblingReady.Task.WaitAsync(timeout);

            loop.Schedule(() =>
            {
                first = new RasterWindow(loop);
                Assert.True(first.Tick());
                Assert.NotSame(sibling!.Painter, first.Painter);
            }, () =>
            {
                if (hovered) return siblingSawHover;
                first!.Input(InputKind.PointerMove);
                Assert.True(first.Icon.IsHovered);
                Assert.True(first.Tick());
                Assert.Equal(2, first.Painter.Draws);
                hovered = true;
                return false;
            }, () => first?.Dispose(), () => false, firstReady, firstDone);
            await firstReady.Task.WaitAsync(timeout);
            // Propagate a participant assertion without waiting for its sibling's next step.
            await (await Task.WhenAny(firstDone.Task, siblingDone.Task).WaitAsync(timeout));
            await Task.WhenAll(firstDone.Task, siblingDone.Task).WaitAsync(timeout);
            Assert.Equal(1, sibling!.Painter.Disposals);
        }
        finally { await loop.StopAsync().WaitAsync(timeout); }
    }

    private sealed class RasterWindow : IDisposable
    {
        public IconButton Icon { get; } = new(UiSymbol.Stop, "Stop") { RevealOnInteraction = true };
        public Presenter Painter { get; } = new();
        public UiDiagnostics Diagnostics { get; }
        public int Clicks { get; private set; }
        private readonly UiSession _session;
        private readonly UiFramePump _pump;

        public RasterWindow(UiOwnerLoop loop)
        {
            Assert.True(loop.IsOwnerThread);
            var root = new Window();
            root.SetContent(Icon);
            Icon.Click += () => Clicks++;
            _session = UiSession.Create(root, new SkiaRasterRenderBackend(), new RasterUiTarget(), new(64, 64),
                null, new UiDispatchOptions(), configurePresenters: p => p.RegisterIconButton(
                    new Factory<IElementPresenter<IconButtonPaintState>>(() => Painter)));
            Diagnostics = _session.EnableDiagnostics();
            _pump = new UiFramePump(UiFrameMode.OnDemand, 60, loop.Wake);
            _pump.Attach(_session);
        }

        public bool Tick() => _pump.Tick(false, static () => true, immediate: true);
        public void Input(InputKind kind)
        {
            var r = Icon.Bounds;
            _session.HandleInput(new(kind, r.X + r.Width / 2, r.Y + r.Height / 2));
        }
        public void Dispose() { _pump.Dispose(); _session.Dispose(); }
    }

    private static T Required<T>(IApp app) => Assert.IsAssignableFrom<T>(app.Services.GetService(typeof(T)));

    private static UiSession Create(IUiSessionFactory sessions) =>
        sessions.Create(new IconButton(UiSymbol.Stop, "Stop"), new RasterUiTarget(), new UiViewport(32, 32));

    private sealed class Presenter : IElementPresenter<IconButtonPaintState>, IDisposable
    {
        public int Draws { get; private set; }
        public int Disposals { get; private set; }
        public void Draw(IconButtonPaintState state, IDrawingContext context) => Draws++;
        public void Dispose() => Disposals++;
    }
}
