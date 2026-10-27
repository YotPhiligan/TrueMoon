using TrueMoon.Aether.Editor;
using System.Text.Json;
using System.ComponentModel;
using System.Runtime.InteropServices;
using SkiaSharp;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.ManualTests.AetherEditorHost;

// Directed input and framebuffer readback affect only this application's UI session.
internal static partial class EditorHeaderSmoke
{
    internal static async Task VerifyAsync(string directory, HostedUiWindow window, UiSession ui, EditorView view, CancellationToken token)
    {
        var header = view.Header ?? throw new InvalidOperationException("Compact header was not composed.");
        var documents = view.Documents ?? throw new InvalidOperationException("Document tools were not composed.");
        var captures = new List<string>();
        float caption = 0, run = 0;
        Rect brand = default, disclosure = default;
        await UntilAsync(ui, () => !ui.NeedsUpdate, token);
        await ui.InvokeAsync(() =>
        {
            Require(!view.Toolbar.IsVisible && documents.Commands.Values.All(b => !b.IsVisible), "Legacy command rows are still visible.");
            var frame = (WindowFrame)ui.Root;
            Require(frame.TitleBarOverContent && frame.DefaultTitleBar!.OverlayContent && ReferenceEquals(frame.Content, view) &&
                view.Canvas.Bounds.Y < frame.TitleBar.Bounds.Y + frame.TitleBar.Bounds.Height &&
                view.Canvas.Bounds.Y + view.Canvas.Bounds.Height > view.StatusBar!.Bounds.Y && !documents.IsVisible,
                "Viewport did not extend beneath chrome or idle document tools reserved space.");
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
            var title = ((WindowFrame)ui.Root).DefaultTitleBar!;
            Require(title.OverlayReveal == 1, $"Header hover did not reveal its frosted background: reveal={title.OverlayReveal}, active={title.State.Active}.");
        }).WaitAsync(token);
        await Capture("header-expanded.png");
        await ui.InvokeAsync(() => Click(ui, header.Menu.Headings["view"])).WaitAsync(token);
        await UntilAsync(ui, () => header.Menu.IsMenuOpen && view.Overlays.Popup!.Children.OfType<MenuItem>().Any(item => item.ActionId == "view.theme") && !ui.NeedsRender, token,
            () => HoldGroup(ui, header.Menu, view.Overlays, "view"));
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
        await ui.InvokeAsync(() =>
        {
            Click(ui, header.Menu.MoreButton);
            Require(header.Menu.MoreButton.IsFocused && view.Overlays.Popup == null,
                "Second disclosure click must close File and retain a focus target.");
        }).WaitAsync(token);
        await UntilAsync(ui, () => header.Menu.RevealProgress == 0 && !ui.NeedsUpdate, token);
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
        await NativeCaptionMoveAsync(window, ui, header.BrandIcon, token);
        await NativeCaptionLeaveAsync(window, token);
        await UntilAsync(ui, () => ((WindowFrame)ui.Root).DefaultTitleBar!.OverlayReveal == 0 && !ui.NeedsUpdate, token);
        File.WriteAllText(Path.Combine(directory, "header.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Hover = true, CaptionShift = true, FixedSimulationButtons = true, Keyboard = true,
            ThemeRetention = true, Tooltip = true, FocusLost = true, BrandMark = true, DisclosureOpensFirst = true,
            DisclosureToggle = true, FullAreaViewport = true, HoverBackdrop = true, NativeCaptionHover = true, IdleDocumentToolsHidden = true,
            BrandBounds = brand, DisclosureBounds = disclosure, Captures = captures
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
    // Only the owned HWND is messaged on its owner thread; the user's cursor, focus and OS settings are untouched.
    internal static Task NativeCaptionMoveAsync(HostedUiWindow window, UiSession ui, Element target, CancellationToken token)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.PostWindow(host =>
        {
            try
            {
                var handle = host.NativeWindow.Native!.Win32!.Value.Hwnd;
                var area = target.Bounds;
                var point = new NativePoint { X = (int)MathF.Round((area.X + area.Width / 2) * ui.Viewport.Scale), Y = (int)MathF.Round((area.Y + area.Height / 2) * ui.Viewport.Scale) };
                if (ClientToScreen(handle, ref point) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
                var packed = (nint)((uint)(ushort)point.X | (uint)(ushort)point.Y << 16);
                Require(SendMessageW(handle, 0x84, 0, packed) == 2, "Native header point lost Caption metadata.");
                UiInput? received = null;
                void Observe(UiInput input) { if (input.Kind == InputKind.PointerMove) received = input; }
                host.Input += Observe;
                try { SendMessageW(handle, 0xA0, 2, packed); host.DispatchPendingWindowInput(); }
                finally { host.Input -= Observe; }
                Require(received is { } move && Math.Abs(move.X - (area.X + area.Width / 2)) <= 1 && Math.Abs(move.Y - (area.Y + area.Height / 2)) <= 1,
                    "Native caption hover did not reach the UI in logical coordinates.");
                var title = ((WindowFrame)ui.Root).DefaultTitleBar!;
                Require(title.NeedsFrame || title.OverlayReveal == 1, "Native hover did not schedule the title backdrop transition.");
                // TrackMouseEvent follows the real cursor, which this directed proof never moves.
                // Complete this owner-driven transition before the OS may post its real-pointer leave.
                title.AdvanceFrame(TimeSpan.FromMilliseconds(160), ui);
                Require(title.OverlayReveal == 1, "Native caption hover did not reveal the title backdrop.");
                done.TrySetResult();
            }
            catch (Exception error) { done.TrySetException(error); }
        });
        return done.Task.WaitAsync(token);
    }
    private static Task NativeCaptionLeaveAsync(HostedUiWindow window, CancellationToken token)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.PostWindow(host =>
        {
            try { SendMessageW(host.NativeWindow.Native!.Win32!.Value.Hwnd, 0x2A2, 0, 0); host.DispatchPendingWindowInput(); done.TrySetResult(); }
            catch (Exception error) { done.TrySetException(error); }
        });
        return done.Task.WaitAsync(token);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { internal int X, Y; }
    [LibraryImport("user32.dll", SetLastError = true)] private static partial int ClientToScreen(nint window, ref NativePoint point);
    [LibraryImport("user32.dll")] private static partial nint SendMessageW(nint window, uint message, nuint wparam, nint lparam);
    internal static async Task ClickActionAsync(UiSession ui, EditorView view, string group, string action, CancellationToken token, bool confirmPath = true)
    {
        var menu = view.Header!.Menu;
        await ui.InvokeAsync(() => { menu.CloseMenu(); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F10)); }).WaitAsync(token);
        await UntilAsync(ui, () => menu.RevealProgress == 1 && !ui.NeedsRender, token,
            () => { if (!menu.IsExpanded) ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F10)); });
        await ui.InvokeAsync(() => Click(ui, menu.Headings[group])).WaitAsync(token);
        await UntilAsync(ui, () => menu.IsMenuOpen && view.Overlays.Popup!.Children.OfType<MenuItem>().Any(item => item.ActionId == action) && !ui.NeedsRender, token,
            () => HoldGroup(ui, menu, view.Overlays, group));
        await ui.InvokeAsync(() =>
        {
            var item = view.Overlays.Popup!.Children.OfType<MenuItem>().Single(row => row.ActionId == action);
            Require(item.IsEnabled && item.Bounds.Width > 0 && item.Bounds.Height > 0, "Menu action is unavailable: " + action);
            Click(ui, item);
        }).WaitAsync(token);
        if (confirmPath && (action is "document.Open" or "document.Save" or "document.SaveAs"))
            await ConfirmDocumentPathAsync(ui, view, token);
    }
    internal static async Task ConfirmDocumentPathAsync(UiSession ui, EditorView view, CancellationToken token)
    {
        var requested = false;
        await ui.InvokeAsync(() => requested = view.Documents?.DataContext is TrueMoon.Aether.Editor.Documents.EditorDocumentViewModel { IsPathPromptOpen: true }).WaitAsync(token);
        if (!requested) return;
        await UntilAsync(ui, () => view.Documents is { IsVisible: true } tools && tools.Actions.IsVisible && tools.ConfirmPath.IsEnabled && !ui.NeedsUpdate, token);
        await ui.InvokeAsync(() => Click(ui, view.Documents!.ConfirmPath)).WaitAsync(token);
    }
    // Native focus changes can legitimately dismiss an owned popup between directed gestures.
    // Retry only opening this group; never repeat an application command.
    private static void HoldGroup(UiSession ui, MenuBar menu, OverlayHost overlays, string group)
    {
        if (overlays.Popup != null) return;
        if (!menu.IsExpanded) ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F10));
        if (menu.RevealProgress == 1) Click(ui, menu.Headings[group]);
    }
    internal static async Task UntilAsync(UiSession ui, Func<bool> condition, CancellationToken token, Action? holdInput = null)
    {
        while (true)
        {
            var ready = false; await ui.InvokeAsync(() => { holdInput?.Invoke(); ready = condition(); }).WaitAsync(token);
            if (ready) return; await Task.Delay(10, token);
        }
    }
    internal static void Move(UiSession ui, Element element) => ui.HandleInput(new(InputKind.PointerMove,
        element.Bounds.X + element.Bounds.Width / 2, element.Bounds.Y + element.Bounds.Height / 2));
    internal static void Click(UiSession ui, Element element)
    {
        var bounds = element.Bounds; var x = bounds.X + bounds.Width / 2; var y = bounds.Y + bounds.Height / 2;
        ui.HandleInput(new(InputKind.PointerDown, x, y)); ui.HandleInput(new(InputKind.PointerUp, x, y));
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
