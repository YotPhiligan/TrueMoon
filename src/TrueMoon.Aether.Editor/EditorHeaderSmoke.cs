using System.Text.Json;
using SkiaSharp;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

// Directed input and framebuffer readback affect only this application's UI session.
internal static class EditorHeaderSmoke
{
    internal static async Task VerifyAsync(string directory, HostedUiWindow window, UiSession ui, EditorView view, CancellationToken token)
    {
        var header = view.Header ?? throw new InvalidOperationException("Compact header was not composed.");
        var captures = new List<string>();
        float caption = 0, run = 0;
        Rect brand = default, disclosure = default;
        await UntilAsync(ui, () => !ui.NeedsUpdate, token);
        await ui.InvokeAsync(() =>
        {
            Require(!view.Toolbar.IsVisible && view.Documents!.Commands.Values.All(b => !b.IsVisible), "Legacy command rows are still visible.");
            Require(header.Menu.RevealProgress == 0, "Menu must initially be collapsed.");
            brand = header.BrandIcon.Bounds; disclosure = header.Menu.MoreButton.Bounds;
            Require(brand.Width >= 20 && brand.X + brand.Width <= disclosure.X, "TrueMoon mark must precede the menu.");
            Require(!header.BrandIcon.Focusable && header.Menu.MoreButton.BorderWidth == 0, "Brand/disclosure semantics changed.");
            caption = header.Caption.Bounds.X; run = header.RunButton.Bounds.X;
            Move(ui, header.Menu.MoreButton);
        }).WaitAsync(token);
        await UntilAsync(ui, () => header.Menu.RevealProgress == 1 && !ui.NeedsUpdate, token, () => Move(ui, header.Menu.MoreButton));
        await ui.InvokeAsync(() =>
        {
            Require(header.Caption.Bounds.X > caption + 100, "Disclosure did not move the caption.");
            Require(header.RunButton.Bounds.X == run, "Disclosure moved fixed simulation controls.");
            Require(header.BrandIcon.Bounds == brand && header.Menu.MoreButton.Bounds == disclosure, "Disclosure moved the brand or its hit target.");
            Require(!header.Menu.IsMenuOpen, "Hover must reveal headings without opening a dropdown.");
        }).WaitAsync(token);
        await Capture("header-expanded.png");
        await ui.InvokeAsync(() => Click(ui, header.Menu.Headings["view"])).WaitAsync(token);
        await UntilAsync(ui, () => view.Overlays.Popup != null && !ui.NeedsUpdate, token);
        await Capture("header-menu.png");
        var oldSurface = header.Theme.Surface;
        await ClickActionAsync(ui, view, "view", "view.theme", token);
        await UntilAsync(ui, () => header.Theme.Surface != oldSurface, token);
        Require(ReferenceEquals(view.Header, header), "Theme rebuilt header.");
        await ClickActionAsync(ui, view, "view", "view.theme", token);
        await UntilAsync(ui, () => header.Theme.Surface == oldSurface, token);
        await ui.InvokeAsync(() => { header.Menu.CloseMenu(); Move(ui, view.Canvas); }).WaitAsync(token);
        await UntilAsync(ui, () => header.Menu.RevealProgress == 0 && !ui.NeedsUpdate, token);
        await ui.InvokeAsync(() => Click(ui, header.Menu.MoreButton)).WaitAsync(token);
        await UntilAsync(ui, () => header.Menu.IsMenuOpen && !ui.NeedsUpdate, token);
        await ui.InvokeAsync(() =>
        {
            Require(view.Overlays.Popup!.Children.OfType<MenuItem>().Any(row => row.ActionId == "document.New"), "Disclosure did not open File.");
            Require(header.BrandIcon.Bounds == brand && header.Menu.MoreButton.Bounds == disclosure, "Click moved the brand or disclosure.");
        }).WaitAsync(token);
        await Capture("header-file-menu.png");
        await ui.InvokeAsync(() => Click(ui, header.Menu.MoreButton)).WaitAsync(token);
        await UntilAsync(ui, () => header.Menu.RevealProgress == 0 && !ui.NeedsUpdate, token);
        await ui.InvokeAsync(() => Require(header.Menu.MoreButton.IsFocused && view.Overlays.Popup == null,
            "Second disclosure click must close File and retain a focus target.")).WaitAsync(token);
        await ui.InvokeAsync(() =>
        {
            ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F10));
            ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Right));
            ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Down));
        }).WaitAsync(token);
        await UntilAsync(ui, () => header.Menu.IsMenuOpen && !ui.NeedsUpdate, token);
        await ui.InvokeAsync(() =>
        {
            ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape));
            Require(header.Menu.MoreButton.IsFocused && !header.Menu.IsExpanded && view.Overlays.Popup == null, "Esc did not dismiss and restore focus.");
            Move(ui, header.RunButton);
        }).WaitAsync(token);
        await UntilAsync(ui, () => ReferenceEquals(view.Overlays.PopupOwner, header) && !ui.NeedsUpdate, token, () => Move(ui, header.RunButton));
        await Capture("header-tooltip.png");
        await ui.InvokeAsync(() => { Move(ui, view.Canvas); ui.HandleInput(new(InputKind.FocusLost)); }).WaitAsync(token);
        await UntilAsync(ui, () => header.Menu.RevealProgress == 0 && view.Overlays.Popup == null && !ui.NeedsUpdate, token);
        await Capture("header-collapsed.png");
        File.WriteAllText(Path.Combine(directory, "header.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Hover = true, CaptionShift = true, FixedSimulationButtons = true, Keyboard = true,
            ThemeRetention = true, Tooltip = true, FocusLost = true, BrandMark = true, DisclosureOpensFirst = true,
            DisclosureToggle = true, BrandBounds = brand, DisclosureBounds = disclosure, Captures = captures
        }, new JsonSerializerOptions { WriteIndented = true }));

        async Task Capture(string name)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(UiSession current)
            {
                if (current.NeedsUpdate) return;
                window.FramePresented -= Presented;
                try
                {
                    using var image = current.ReadbackOpenGLImage(); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var output = File.Create(Path.Combine(directory, name)); data.SaveTo(output); captures.Add(name); completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
            }
            window.FramePresented += Presented; window.RequestFrame();
            try { await completion.Task.WaitAsync(token); } finally { window.FramePresented -= Presented; }
        }
    }
    internal static async Task ClickActionAsync(UiSession ui, EditorView view, string group, string action, CancellationToken token)
    {
        var menu = view.Header!.Menu;
        await ui.InvokeAsync(() => { menu.CloseMenu(); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F10)); }).WaitAsync(token);
        await UntilAsync(ui, () => menu.RevealProgress == 1 && !ui.NeedsRender, token,
            () => { if (!menu.IsExpanded) ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F10)); });
        await ui.InvokeAsync(() => Click(ui, menu.Headings[group])).WaitAsync(token);
        await UntilAsync(ui, () => view.Overlays.Popup != null && !ui.NeedsRender, token);
        await ui.InvokeAsync(() =>
        {
            var item = view.Overlays.Popup!.Children.OfType<MenuItem>().Single(row => row.ActionId == action);
            Require(item.IsEnabled && item.Bounds.Width > 0 && item.Bounds.Height > 0, "Menu action is unavailable: " + action);
            Click(ui, item);
        }).WaitAsync(token);
    }
    private static async Task UntilAsync(UiSession ui, Func<bool> condition, CancellationToken token, Action? holdInput = null)
    {
        while (true)
        {
            var ready = false; await ui.InvokeAsync(() => { holdInput?.Invoke(); ready = condition(); }).WaitAsync(token);
            if (ready) return; await Task.Delay(10, token);
        }
    }
    private static void Move(UiSession ui, Element element) => ui.HandleInput(new(InputKind.PointerMove,
        element.Bounds.X + element.Bounds.Width / 2, element.Bounds.Y + element.Bounds.Height / 2));
    internal static void Click(UiSession ui, Element element)
    {
        var bounds = element.Bounds; var x = bounds.X + bounds.Width / 2; var y = bounds.Y + bounds.Height / 2;
        ui.HandleInput(new(InputKind.PointerDown, x, y)); ui.HandleInput(new(InputKind.PointerUp, x, y));
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
