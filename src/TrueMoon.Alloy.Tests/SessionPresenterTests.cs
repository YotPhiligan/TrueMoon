using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using SkiaSharp;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Runtime.Semantics;
using TrueMoon.Argentis;
using Xunit;
using Xunit.Abstractions;

namespace TrueMoon.Alloy.Tests;

public sealed class SessionPresenterTests(ITestOutputHelper output)
{
    [Fact]
    public void Registration_IsLazyAndExact_AndReusesOnePresenterUntilSessionDisposal()
    {
        var root = new HStack { Spacing = 0 };
        root.Add(new DerivedIconButton());
        var created = new List<MarkerPresenter>();
        var factory = new Factory<IElementPresenter<IconButtonPaintState>>(() =>
        {
            var presenter = new MarkerPresenter();
            created.Add(presenter);
            return presenter;
        });
        using var ui = Create(root, factory, width: 96);
        Assert.Empty(created);
        Assert.True(ui.Update());
        Assert.Empty(created); // An IconButton subclass is not an exact match.

        var first = Icon();
        var second = Icon();
        root.Add(first);
        root.Add(second);
        Assert.True(ui.Update());
        var instance = Assert.Single(created);
        Assert.Equal(new[] { first.Bounds, second.Bounds }, instance.DrawnBounds);

        instance.DrawnBounds.Clear();
        ui.SetTheme(Theme.Light);
        Assert.True(ui.Update());
        Assert.Same(instance, Assert.Single(created));
        Assert.Equal(new[] { first.Bounds, second.Bounds }, instance.DrawnBounds);
        Assert.Equal(0, instance.Disposals);
        ui.Dispose();
        ui.Dispose();
        Assert.Equal(1, instance.Disposals);
    }

    [Fact]
    public void Registration_ReplacesOnlyTheSelectedSessionsGlyph()
    {
        const string id = "transport.stop";
        var customIcon = Icon();
        var stockIcon = Icon();
        customIcon.SemanticId = stockIcon.SemanticId = id;
        var marker = new MarkerPresenter();
        using var custom = Create(customIcon, new Factory<IElementPresenter<IconButtonPaintState>>(() => marker));
        using var stock = UiSession.Create(stockIcon, new SkiaRasterRenderBackend(), new RasterUiTarget(), new(32, 32),
            null, new UiDispatchOptions(), configurePresenters: p => p.Register<Button, Button>(static b => b,
                new Factory<IElementPresenter<Button>>(static () => throw new InvalidOperationException("A Button override matched IconButton."))));
        Assert.True(custom.Update());
        Assert.True(stock.Update());
        using var stockImage = stock.SnapshotRasterImage();
        using var customImage = custom.SnapshotRasterImage();
        using var stockPixels = SKBitmap.FromImage(stockImage);
        using var customPixels = SKBitmap.FromImage(customImage);
        Assert.Equal((byte)0, stockPixels.GetPixel(6, 6).Alpha);
        Assert.Equal(new SKColor(20, 80, 160), stockPixels.GetPixel(16, 16));
        Assert.Equal(new SKColor(255, 0, 255), customPixels.GetPixel(6, 6));
        Assert.Equal((byte)0, customPixels.GetPixel(16, 16).Alpha);
        Assert.Equal(new Rect(0, 0, 32, 32), Assert.Single(marker.DrawnBounds));
        ReportChoice(custom, marker.GetType(), "exact IconButton registration (marker raster)");
        ReportChoice(stock, typeof(IconButtonPresenter), "stock (no exact registration; stock glyph raster)");

        void ReportChoice(UiSession session, Type presenter, string reason)
        {
            var snapshot = session.CaptureSemantics();
            var target = session.FindSemanticTarget(snapshot, id);
            Assert.Equal(UiSemanticTargetStatus.Ready, target.Status);
            var node = Assert.IsType<UiSemanticNode>(target.Node);
            Assert.Equal(id, node.Id);
            Assert.Equal(new Rect(0, 0, 32, 32), node.Bounds);
            Assert.Equal(node.Bounds, node.VisibleBounds);
            var hit = session.HandleSemanticInput(snapshot, id, new(InputKind.PointerMove, 31.5f, 31.5f));
            Assert.Equal(UiSemanticTargetStatus.Routed, hit.Status);
            Assert.Equal(id, hit.HitTargetId);
            Assert.Equal(UiSemanticTargetStatus.OutsideTarget,
                session.HandleSemanticInput(snapshot, id, new(InputKind.PointerMove, 33, 31.5f)).Status);
            output.WriteLine($"session={session.Id} id={node.Id} control={session.Root.GetType().FullName} presenter={presenter.FullName} reason={reason}");
        }
    }

    [Fact]
    public void IconHoverRendersOnly_FontChangeCompletesLayoutBeforeDraw_ThemeReusesPresenter()
    {
        var icon = new IconButton(UiSymbol.Stop, "Stop") { RevealOnInteraction = true };
        var text = new Text("Stop") { FontSize = 12 };
        var row = new HStack();
        row.Add(icon);
        row.Add(text);
        UiDiagnostics? diagnostics = null;
        UiDiagnosticsSnapshot atDraw = default;
        Color foreground = default;
        var creations = 0;
        var marker = new MarkerPresenter(state =>
        {
            atDraw = diagnostics!.Snapshot;
            foreground = state.Foreground;
        });
        using var ui = Create(row, new Factory<IElementPresenter<IconButtonPaintState>>(() => { creations++; return marker; }), width: 128);
        diagnostics = ui.EnableDiagnostics();
        Assert.Equal(0, creations);
        Assert.True(ui.Update());
        var initial = diagnostics.Snapshot;
        Assert.Equal(1, initial.Measure.Completed);
        Assert.Equal(1, initial.Arrange.Completed);
        Assert.Equal(1, initial.Render.Completed);

        var r = icon.Bounds;
        ui.HandleInput(new(InputKind.PointerMove, r.X + r.Width / 2, r.Y + r.Height / 2));
        Assert.True(icon.IsHovered);
        Assert.True(ui.Update());
        var hover = diagnostics.Snapshot;
        Assert.Equal(initial.Measure, hover.Measure);
        Assert.Equal(initial.Arrange, hover.Arrange);
        Assert.Equal(initial.Render.Completed + 1, hover.Render.Completed);

        var narrow = text.Bounds.Width;
        text.FontSize = 24;
        Assert.True(ui.Update());
        // Sample from inside the glyph presenter, not just the finished frame.
        Assert.Equal(hover.Measure.Completed + 1, atDraw.Measure.Completed);
        Assert.Equal(hover.Arrange.Completed + 1, atDraw.Arrange.Completed);
        Assert.Equal(hover.Render.Attempts + 1, atDraw.Render.Attempts);
        Assert.Equal(hover.Render.Completed, atDraw.Render.Completed);
        Assert.True(text.Bounds.Width > narrow);
        var resized = diagnostics.Snapshot;
        Assert.Equal(hover.Render.Completed + 1, resized.Render.Completed);

        var darkForeground = foreground;
        ui.SetTheme(Theme.Light);
        Assert.True(ui.Update());
        Assert.NotEqual(darkForeground, foreground);
        Assert.Equal(icon.Foreground, foreground);
        Assert.Equal(resized.Render.Completed + 1, diagnostics.Snapshot.Render.Completed);
        Assert.Equal(4, marker.DrawnBounds.Count);
        Assert.Equal(1, creations);
        Assert.Equal(0, marker.Disposals);
        ui.Dispose();
        Assert.Equal(1, marker.Disposals);
    }

    [Fact]
    public void CustomPresenter_PreservesPointerActivationAndFocus()
    {
        var icon = Icon();
        var clicks = 0;
        icon.Click += () => clicks++;
        using var ui = Create(icon, new Factory<IElementPresenter<IconButtonPaintState>>(() => new MarkerPresenter()));
        Assert.True(ui.Update());
        Assert.True(ui.HandleInput(new(InputKind.PointerDown, 24, 16)).PointerCaptured);
        Assert.True(icon.IsPressed);
        Assert.True(icon.IsFocused);
        Assert.Equal(0, clicks);
        ui.HandleInput(new(InputKind.PointerUp, 24, 16));
        Assert.False(icon.IsPressed);
        Assert.True(icon.IsFocused);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void StandaloneDraw_ReusesOwnedPresenter_AndCanDrawStockWithoutRegistry()
    {
        using var icon = Icon();
        icon.Measure(new Size(32, 32), new FixedTextLayout());
        icon.Arrange(new Rect(0, 0, 32, 32));
        var context = new RecordingDrawingContext();
        var created = new List<MarkerPresenter>();
        using var registry = UiPresenters.Create(p => p.RegisterIconButton(
            new Factory<IElementPresenter<IconButtonPaintState>>(() =>
            {
                var value = new MarkerPresenter(); created.Add(value); return value;
            })));
        Assert.Empty(created);
        UiTreeRenderer.Draw(icon, context, registry);
        UiTreeRenderer.Draw(icon, context, registry);
        var presenter = Assert.Single(created);
        Assert.Equal(2, presenter.DrawnBounds.Count);
        registry.Dispose();
        Assert.Equal(1, presenter.Disposals);
        UiTreeRenderer.Draw(icon, context);
        Assert.Equal(context.Saves, context.Restores);
        Assert.Equal(2, presenter.DrawnBounds.Count);
    }

    [Fact]
    public void DerivedStockGlyph_RemainsVisibleWhenExactBasePresenterIsReplaced()
    {
        var icon = new InheritedStop { RevealOnInteraction = true, BorderWidth = 0, Foreground = new Color(20, 80, 160) };
        using var ui = Create(icon, new Factory<IElementPresenter<IconButtonPaintState>>(() =>
            throw new InvalidOperationException("Base override must not be created for a derived type.")));
        Assert.True(ui.Update());
        using var image = ui.SnapshotRasterImage();
        using var pixels = SKBitmap.FromImage(image);
        Assert.Equal(new SKColor(20, 80, 160), pixels.GetPixel(16, 16));
    }

    [Fact]
    public void StandaloneOwner_ReleasesEarlierPresenterWhenAnotherFactoryFails()
    {
        using var root = new HStack();
        root.Add(Icon()); root.Add(new InheritedStop());
        root.Measure(new Size(64, 32), new FixedTextLayout());
        root.Arrange(new Rect(0, 0, 64, 32));
        var first = new MarkerPresenter();
        var failure = new InvalidOperationException("second factory");
        using (var registry = UiPresenters.Create(p =>
        {
            p.RegisterIconButton(new Factory<IElementPresenter<IconButtonPaintState>>(() => first));
            p.Register<InheritedStop, IconButtonPaintState>(_ => default,
                new Factory<IElementPresenter<IconButtonPaintState>>(() => throw failure));
        }))
        {
            var context = new RecordingDrawingContext();
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => UiTreeRenderer.Draw(root, context, registry)));
            Assert.Single(first.DrawnBounds);
            Assert.Equal(context.Saves, context.Restores);
        }
        Assert.Equal(1, first.Disposals);
    }

    [Fact]
    public void RequiredPresenter_ReportsMissingControlWithoutTreatingStructuralParentAsDrawable()
    {
        using var root = new Panel();
        root.Add(new UnregisteredButton());
        root.Measure(new Size(32, 32), new FixedTextLayout());
        root.Arrange(new Rect(0, 0, 32, 32));
        var context = new RecordingDrawingContext();
        using var registry = UiPresenters.Create(p => p.Require<UnregisteredButton>());
        var error = Assert.Throws<InvalidOperationException>(() => UiTreeRenderer.Draw(root, context, registry));
        Assert.Contains(typeof(UnregisteredButton).FullName!, error.Message);
        Assert.Equal(context.Saves, context.Restores);
    }

    private sealed class UnregisteredButton : Button;

    private sealed class InheritedStop() : IconButton(UiSymbol.Stop, "Stop") { }

    private static IconButton Icon() => new(UiSymbol.Stop, "Stop")
    {
        RevealOnInteraction = true, BorderWidth = 0, Foreground = new Color(20, 80, 160)
    };

    private static UiSession Create(Element root, IFactory<IElementPresenter<IconButtonPaintState>> factory, int width = 32) =>
        UiSession.Create(root, new SkiaRasterRenderBackend(), new RasterUiTarget(), new UiViewport(width, 32),
            null, new UiDispatchOptions(), configurePresenters: p => p.RegisterIconButton(factory));

    private sealed class DerivedIconButton() : IconButton(UiSymbol.Play, "Play") { }

    private sealed class MarkerPresenter(Action<IconButtonPaintState>? onDraw = null) : IElementPresenter<IconButtonPaintState>, IDisposable
    {
        public List<Rect> DrawnBounds { get; } = [];
        public int Disposals { get; private set; }

        public void Draw(IconButtonPaintState state, IDrawingContext context)
        {
            onDraw?.Invoke(state);
            DrawnBounds.Add(state.Bounds);
            context.Fill(state.Bounds with { Width = state.Bounds.Width / 3 }, new Color(255, 0, 255));
        }

        public void Dispose() => Disposals++;
    }
}
