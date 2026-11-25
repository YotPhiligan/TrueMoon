using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using System.Text.Json;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace AlloyPackageConsumer;

internal static class PresenterScreen
{
    private sealed class SignalButton : Button;

    private sealed class Stripe : IElementPresenter<IconButtonPaintState>
    {
        public void Draw(IconButtonPaintState state, IDrawingContext context) =>
            context.Fill(state.Bounds with { Width = state.Bounds.Width / 3 }, new Color(255, 0, 255));
    }

    private sealed class Signal : IElementPresenter<Rect>
    {
        public void Draw(Rect bounds, IDrawingContext context) =>
            context.Fill(bounds.Deflate(new Thickness(6)), new Color(0, 210, 90));
    }


    private static void Configure(UiPresenters presenters, bool registerSignal)
    {
        presenters.RegisterIconButton(new Factory<IElementPresenter<IconButtonPaintState>>(static () => new Stripe()));
        presenters.Require<SignalButton>();
        if (registerSignal)
            presenters.Register<SignalButton, Rect>(static button => button.Bounds,
                new Factory<IElementPresenter<Rect>>(static () => new Signal()));
    }
    private static IconButton Stop() => new(UiSymbol.Stop, "Stop")
    {
        SemanticId = "stop", RevealOnInteraction = true, BorderWidth = 0, Foreground = new Color(20, 80, 160)
    };

    internal static void Verify(SkiaFontCatalog fonts, string reportPath)
    {
        VerifyMissingRegistration(fonts);
        var icon = Stop();
        var signal = new SignalButton { Width = 32, Height = 32, SemanticId = "signal", AccessibleName = "Signal" };
        var row = new HStack { Spacing = 0 };
        row.Add(icon);
        row.Add(signal);
        var stock = Stop();
        using var unmodified = UiSession.Create(stock, new SkiaRasterRenderBackend(fonts), new RasterUiTarget(), new UiViewport(32, 32));
        object[] choices;
        using (var custom = UiSession.Create(row, new SkiaRasterRenderBackend(fonts), new RasterUiTarget(), new UiViewport(64, 32),
            null, new UiDispatchOptions(), configurePresenters: static p => Configure(p, registerSignal: true)))
        {
            custom.Update();
            unmodified.Update();
            using var image = custom.SnapshotRasterImage();
            using var pixels = SKBitmap.FromImage(image);
            ExpectPixel(pixels, icon, 6, 6, new SKColor(255, 0, 255));
            ExpectPixel(pixels, icon, 16, 16, SKColors.Transparent);
            ExpectPixel(pixels, signal, 16, 16, new SKColor(0, 210, 90));
            VerifyStock(unmodified, stock);
            var iconClicks = 0;
            var signalClicks = 0;
            icon.Click += () => iconClicks++;
            signal.Click += () => signalClicks++;
            Click(custom, icon);
            Require(iconClicks == 1 && signalClicks == 0, "Icon click did not activate only the icon once.");
            Click(custom, signal);
            Require(iconClicks == 1 && signalClicks == 1, "Signal click did not activate only the signal once.");
            choices =
            [
                Choice("custom", icon, typeof(Stripe), "exact registration"),
                Choice("custom", signal, typeof(Signal), "exact registration"),
                Choice("independent", stock, typeof(IconButtonPresenter), "stock")
            ];
        }

        unmodified.SetTheme(Theme.Light);
        Require(unmodified.Update(), "Surviving session did not redraw.");
        VerifyStock(unmodified, stock);
        var stockClicks = 0;
        stock.Click += () => stockClicks++;
        Click(unmodified, stock);
        Require(stockClicks == 1, "Closing the custom session broke the stock button.");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { Choices = choices, StockSurvivedCustomSession = true },
            new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void VerifyMissingRegistration(SkiaFontCatalog fonts)
    {
        using var ui = UiSession.Create(new SignalButton(), new SkiaRasterRenderBackend(fonts), new RasterUiTarget(), new UiViewport(32, 32),
            null, new UiDispatchOptions(), configurePresenters: static p => Configure(p, registerSignal: false));
        try { ui.Update(); }
        catch (InvalidOperationException error) when (error.Message.Contains(typeof(SignalButton).FullName!, StringComparison.Ordinal)) { return; }
        throw new InvalidOperationException("The required signal presenter was missing but the draw succeeded.");
    }

    private static object Choice(string session, Button button, Type presenter, string reason) =>
        new { Session = session, button.SemanticId, Control = button.GetType().FullName, Presenter = presenter.FullName, Reason = reason };

    private static void VerifyStock(UiSession session, IconButton stock)
    {
        using var image = session.SnapshotRasterImage();
        using var pixels = SKBitmap.FromImage(image);
        ExpectPixel(pixels, stock, 6, 6, SKColors.Transparent);
        ExpectPixel(pixels, stock, 16, 16, new SKColor(20, 80, 160));
    }

    private static void ExpectPixel(SKBitmap pixels, Button button, int dx, int dy, SKColor expected)
    {
        var actual = pixels.GetPixel((int)button.Bounds.X + dx, (int)button.Bounds.Y + dy);
        Require(expected.Alpha == 0 ? actual.Alpha == 0 : actual == expected,
            $"{button.SemanticId} pixel ({dx},{dy}): expected {expected}, got {actual}.");
    }

    private static void Click(UiSession session, Button target)
    {
        var bounds = target.Bounds;
        var x = bounds.X + bounds.Width / 2;
        var y = bounds.Y + bounds.Height / 2;
        var down = session.HandleInput(new(InputKind.PointerDown, x, y));
        Require(down.PointerCaptured && target.IsFocused && target.IsPressed, $"{target.SemanticId} did not receive the press.");
        session.HandleInput(new(InputKind.PointerUp, x, y));
        Require(!target.IsPressed && target.IsFocused, $"{target.SemanticId} did not finish the gesture.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
