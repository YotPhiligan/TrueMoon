using System.Text.Json;
using SkiaSharp;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

// Directed acceptance only drives this owned UI tree; no global/native input injection.
internal static class EditorStatusBarSmoke
{
    internal static async Task VerifyAsync(string directory, HostedUiWindow window, UiSession ui, EditorView view, CancellationToken token)
    {
        var footer = view.StatusBar ?? throw new InvalidOperationException("Status bar was not composed.");
        var captures = new List<string>();
        await Until(() => !ui.NeedsUpdate);
        await ui.InvokeAsync(() =>
        {
            Require(footer.Bounds.Height == 28 && footer.SummaryButton.FontSize == 12, "Compact status dimensions changed.");
            Require(footer.DetailsButton.Bounds.Width == 24 && footer.DetailsButton.Bounds.Height == 24, "Details target changed.");
            Require(!view.Transform!.PreviewActions.IsVisible && !view.Transform.PreviewStatus.IsVisible, "Legacy inspector Preview remains visible.");
            Require(!footer.SummaryButton.Value.Contains("Tick") && !footer.SummaryButton.Value.Contains("PID"), "Diagnostics leaked into the primary status.");
        }).WaitAsync(token);
        await Capture("status-bar.png");
        await ui.InvokeAsync(() => EditorHeaderSmoke.Click(ui, footer.DetailsButton)).WaitAsync(token);
        await Until(() => view.Overlays.Popup is EditorStatusPopupView && !ui.NeedsUpdate);
        EditorStatusPopupView popup = null!;
        await ui.InvokeAsync(() =>
        {
            popup = (EditorStatusPopupView)view.Overlays.Popup!;
            Require(ReferenceEquals(view.Overlays.PopupOwner, footer.DetailsButton), "Details owner changed.");
            Require(popup.Bounds.Y + popup.Bounds.Height <= footer.Bounds.Y, "Bottom popup did not open above the footer.");
            Require(ReferenceEquals(popup.DataContext, footer.DataContext) && popup.Backdrop != null, "Popup lost borrowed data or material.");
        }).WaitAsync(token);
        await Capture("status-details.png");
        var theme = footer.Theme;
        await ui.InvokeAsync(() => ui.SetTheme(Theme.Light with { FontFamily = theme.FontFamily })).WaitAsync(token);
        await Until(() => popup.Theme.Surface == Theme.Light.Surface && !ui.NeedsUpdate);
        Require(ReferenceEquals(view.Overlays.Popup, popup) && ReferenceEquals(view.StatusBar, footer), "Theme rebuilt footer or popup.");
        await Capture("status-light.png");
        await ui.InvokeAsync(() =>
        {
            ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape));
            Require(view.Overlays.Popup == null && footer.DetailsButton.IsFocused, "Esc did not return details focus.");
            ui.SetTheme(theme);
            EditorHeaderSmoke.Click(ui, footer.PreviewButton);
        }).WaitAsync(token);
        await Until(() => view.Overlays.Popup is EditorStatusPopupView && !ui.NeedsUpdate);
        await Capture("status-preview.png");
        await ui.InvokeAsync(() =>
        {
            Require(((EditorStatusPopupView)view.Overlays.Popup!).Title.Value == "Edit Preview", "Preview trigger opened the wrong panel.");
            ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Tab));
            Require(view.Overlays.Popup == null, "Tab retained a stale popup.");
            EditorHeaderSmoke.Click(ui, footer.DetailsButton);
        }).WaitAsync(token);
        await Until(() => view.Overlays.Popup != null && !ui.NeedsUpdate);
        await ui.InvokeAsync(() =>
        {
            var area = view.Canvas.Bounds;
            ui.HandleInput(new(InputKind.PointerDown, area.X + area.Width / 2, area.Y + area.Height / 2));
            ui.HandleInput(new(InputKind.PointerUp, area.X + area.Width / 2, area.Y + area.Height / 2));
            Require(view.Overlays.Popup == null, "Outside click did not dismiss status details.");
            EditorHeaderSmoke.Click(ui, footer.DetailsButton);
            ui.HandleInput(new(InputKind.FocusLost));
            Require(view.Overlays.Popup == null, "FocusLost retained status details.");
        }).WaitAsync(token);
        await Until(() => !ui.NeedsUpdate);
        File.WriteAllText(Path.Combine(directory, "status.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Compact = true, Details = true, Preview = true, ThemeRetention = true,
            Dismissal = true, InspectorPreviewHidden = true, Captures = captures
        }, new JsonSerializerOptions { WriteIndented = true }));

        async Task Until(Func<bool> predicate)
        {
            while (true)
            {
                var ready = false; await ui.InvokeAsync(() => ready = predicate()).WaitAsync(token);
                if (ready) return; await Task.Delay(10, token);
            }
        }
        async Task Capture(string name)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(UiSession current)
            {
                if (current.NeedsUpdate) return;
                window.FramePresented -= Presented;
                try
                {
                    using var image = current.ReadbackOpenGLImage(); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var output = File.Create(Path.Combine(directory, name)); data.SaveTo(output); captures.Add(name); done.TrySetResult();
                }
                catch (Exception error) { done.TrySetException(error); }
            }
            window.FramePresented += Presented; window.RequestFrame();
            try { await done.Task.WaitAsync(token); } finally { window.FramePresented -= Presented; }
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
