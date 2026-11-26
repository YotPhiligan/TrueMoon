using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Runtime.Semantics;
using TrueMoon.Argentis;
using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Styling;

namespace TrueMoon.ManualTests.AetherEditorHost;

internal static class EditorVisualStatesSmoke
{
    private const string Apply = "editor.inspector.apply";
    private const string Cancel = "editor.inspector.cancel";
    private const string Create = "editor.hierarchy.create";
    private const string Invalid = "Введите конечное число с точкой";
    private static readonly string LongDraft = new string('0', 80) + "12.5";
    private static readonly JsonSerializerOptions ReportJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    internal static async Task RunNativeAsync(string directory, HostedUiWindow window, UiSession ui, EditorView view,
        Func<int> undoCount, CancellationToken token)
    {
        var inspector = view.Inspector ?? throw new InvalidOperationException("Inspector is missing.");
        await NextFrame(current =>
        {
            if (Target(current, Create).Status != UiSemanticTargetStatus.Ready) return false;
            Click(current, Create);
            return true;
        });
        var initialUndo = 0;
        await NextFrame(_ =>
        {
            if (inspector.TransformEditors.Count != 1) return false;
            initialUndo = undoCount();
            return true;
        });
        foreach (var (theme, name) in new[] { (Theme.Dark, "dark"), (Theme.Light, "light") })
        {
            var output = Path.Combine(directory, name);
            await RunAsync(ui, inspector, theme, name, "opengl", output, requireWrap: false, NativeStep);
        }
        await ui.InvokeAsync(() => Require(undoCount() == initialUndo, "Blocked Apply committed to the scene.")).WaitAsync(token);
        window.Close();
        await window.Completion.WaitAsync(token);

        async Task NativeStep(Action act, Action<SKImage> inspect)
        {
            // Posted callbacks run inside Update, where semantic capture is not allowed.
            await NextFrame(_ => { act(); return true; });
            await NextFrame(current =>
            {
                using var image = current.ReadbackOpenGLImage();
                inspect(image);
                return true;
            });
        }

        async Task NextFrame(Func<UiSession, bool> step)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(UiSession current)
            {
                if (current.NeedsUpdate) return;
                try
                {
                    if (!step(current)) return;
                    window.FramePresented -= Presented;
                    done.TrySetResult();
                }
                catch (Exception error) { done.TrySetException(error); }
            }
            window.FramePresented += Presented;
            try { window.RequestFrame(); await done.Task.WaitAsync(token); }
            finally { window.FramePresented -= Presented; }
        }
    }

    internal static Task RunRasterAsync(UiSession ui, EditorInspectorView inspector, Theme theme, string name, string directory) =>
        RunAsync(ui, inspector, theme, name, "raster", directory, requireWrap: true, (act, inspect) =>
        {
            act();
            ui.Update();
            using var image = ui.SnapshotRasterImage();
            inspect(image);
            return Task.CompletedTask;
        });

    private static async Task RunAsync(UiSession ui, EditorInspectorView inspector, Theme theme, string themeName, string backend,
        string directory, bool requireWrap, Func<Action, Action<SKImage>, Task> frame)
    {
        Directory.CreateDirectory(directory);
        var checkpoints = new List<Checkpoint>();
        var component = default(TrueMoon.Aether.Scenes.ComponentId);
        var x = "";
        var y = "";
        TextBox X() => (TextBox)inspector.Properties[(component, "x")].Input;
        TextBox Y() => (TextBox)inspector.Properties[(component, "y")].Input;
        SKBitmap? shortText = null;
        string? error = null;
        try
        {
            await Step("ready", () => ui.SetTheme(theme), _ =>
            {
                component = inspector.TransformEditors.Keys.Single();
                x = $"editor.inspector.transform.{component.Value:N}.x";
                y = $"editor.inspector.transform.{component.Value:N}.y";
                RejectApply(ui);
                Require(!inspector.Error.IsVisible, "A clean inspector exposed an error.");
            });
            await Step("focused-x", () => { Click(ui, x); Type(ui, x, "7"); }, pixels =>
            {
                Require(Focused(ui) == x && X().Value == "7" && X().HorizontalOffset == 0, "The short draft or its focus was lost.");
                Require(Target(ui, Apply).Status == UiSemanticTargetStatus.Ready, "A valid draft did not enable Apply.");
                Require(FocusInk(pixels, X()) > 0 && FocusInk(pixels, Y()) == 0, "X focus was not drawn or leaked to Y.");
                if (requireWrap) shortText = pixels.Copy();
            });
            if (requireWrap)
                await Step("clipped-x", () => Type(ui, x, LongDraft), pixels =>
                {
                    Require(X().Value == LongDraft && X().HorizontalOffset > 0 && !X().HasError, "A long valid draft did not scroll.");
                    var before = shortText ?? throw new InvalidOperationException("Missing baseline pixels.");
                    Require(Different(before, pixels, X().Bounds.Deflate(new(4)), ui.Viewport.Scale), "The long draft did not change X pixels.");
                    Require(!Different(before, pixels, Y().Bounds.Deflate(new(2)), ui.Viewport.Scale), "Long X text drew over Y.");
                    var bounds = X().Bounds;
                    Require(!Different(before, pixels, new(bounds.X + 2, bounds.Y + 5, X().Padding.Left - 2, bounds.Height - 10),
                        ui.Viewport.Scale), "Long X text escaped into the left padding.");
                    before.Dispose(); shortText = null;
                });
            await Step("tab-to-y", () => Route(ui, x, new(InputKind.KeyDown, Key: UiKey.Tab)), pixels =>
            {
                Require(Focused(ui) == y, "Tab did not move focus from X to Y.");
                Require(FocusInk(pixels, X()) == 0 && FocusInk(pixels, Y()) > 0, "The focus outline did not move to Y.");
            });
            await Step("validation-error", () => Type(ui, y, "-"), pixels =>
            {
                var node = Target(ui, y).Node!;
                Require(node.IsFocused && node.HasError && Y().Value == "-", "The invalid Y draft lost focus or validation state.");
                RejectApply(ui);
                var diagnostic = inspector.Error;
                Require(diagnostic.IsVisible && diagnostic.Text.Contains(Invalid, StringComparison.Ordinal) && !diagnostic.Text.Contains('\n'),
                    "The inspector did not present the expected single-paragraph error.");
                Require(FocusInk(pixels, Y()) > 0, "Validation hid the focused Y outline.");
                var ink = Ink(pixels, diagnostic.Bounds.Deflate(diagnostic.Padding), ui.Viewport.Scale, theme.Error);
                Require(ink.Count > 0, "The error message has no visible ink.");
                if (requireWrap)
                {
                    var singleLine = ui.Rendering.TextLayout.Measure("Mg", diagnostic.FontSize, diagnostic.FontFamily,
                        diagnostic.FontWeight, diagnostic.FontItalic).Height;
                    Require(ink.Height > MathF.Ceiling(singleLine * ui.Viewport.Scale),
                        $"The diagnostic did not soft-wrap within its clip: ink={ink.Height}, line={singleLine * ui.Viewport.Scale}.");
                }
            });
            await Step("canceled", () => Click(ui, Cancel), _ =>
            {
                Require(!Y().HasError && !inspector.Error.IsVisible && !inspector.DataContext!.HasDraft, "Cancel did not clear the invalid draft.");
                RejectApply(ui);
            });
        }
        catch (Exception failure) { error = failure.Message; throw; }
        finally
        {
            shortText?.Dispose();
            File.WriteAllText(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(new
            {
                Passed = error == null, Error = error, Backend = backend, Theme = themeName, Checkpoints = checkpoints
            }, ReportJson));
        }

        int FocusInk(SKBitmap pixels, TextBox editor)
        {
            var bounds = editor.Bounds;
            // Sample the upper edge, away from corners, validation badge and text/caret.
            return Ink(pixels, new(bounds.X + 6, bounds.Y + 1, bounds.Width / 2 - 6, 3), ui.Viewport.Scale, theme.Focus).Count;
        }

        Task Step(string id, Action act, Action<SKBitmap> check) => frame(act, image =>
        {
            var path = Path.Combine(directory, id + ".png");
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            using (var output = File.Open(path, FileMode.CreateNew)) png.SaveTo(output);
            using var pixels = SKBitmap.FromImage(image);
            check(pixels);
            using var input = File.OpenRead(path);
            var snapshot = ui.CaptureSemantics(includeAccessibleNames: true);
            checkpoints.Add(new(id, Path.GetFileName(path), Convert.ToHexString(SHA256.HashData(input)), snapshot.Viewport,
                snapshot.Nodes.SingleOrDefault(node => node.IsFocused)?.Id,
                snapshot.Nodes.Where(node => node.Id == x || node.Id == y || node.Id is Apply or Cancel).ToArray(),
                inspector.Error.IsVisible ? inspector.Error.Text : null));
        });
    }

    private static UiSemanticTargetResult Target(UiSession ui, string id) => ui.FindSemanticTarget(ui.CaptureSemantics(), id);
    private static string? Focused(UiSession ui) => ui.CaptureSemantics().Nodes.SingleOrDefault(node => node.IsFocused)?.Id;

    private static void Click(UiSession ui, string id)
    {
        var target = Target(ui, id);
        Require(target.Status == UiSemanticTargetStatus.Ready, $"{id}: {target.Status}.");
        var b = target.Node!.VisibleBounds;
        Route(ui, id, Pointer(InputKind.PointerDown, b));
        // TextBox focuses on the down event; only command buttons consume the release.
        Route(ui, id, Pointer(InputKind.PointerUp, b), requireHandled: target.Node.Role != UiSemanticRole.TextInput);
    }

    private static void Type(UiSession ui, string id, string value)
    {
        Route(ui, id, new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        Route(ui, id, new(InputKind.Text, Text: value));
    }

    private static void Route(UiSession ui, string id, UiInput input, bool requireHandled = true)
    {
        var result = ui.HandleSemanticInput(ui.CaptureSemantics(), id, input);
        Require(result.Status == UiSemanticTargetStatus.Routed && (!requireHandled || result.Input.Handled),
            $"{id} {input.Kind}/{input.Key}: {result.Status}, handled={result.Input.Handled}, hit={result.HitTargetId}.");
    }

    private static void RejectApply(UiSession ui)
    {
        var snapshot = ui.CaptureSemantics();
        var target = ui.FindSemanticTarget(snapshot, Apply);
        Require(target.Status == UiSemanticTargetStatus.Disabled && target.Node is { IsVisible: true, IsEnabled: false },
            $"Apply should be visible but disabled, not {target.Status}.");
        var result = ui.HandleSemanticInput(snapshot, Apply, Pointer(InputKind.PointerDown, target.Node!.VisibleBounds));
        Require(result.Status == UiSemanticTargetStatus.Disabled && !result.Input.Handled, "Disabled Apply accepted input.");
    }

    private static UiInput Pointer(InputKind kind, Rect b) => new(kind, b.X + b.Width / 2, b.Y + b.Height / 2);

    private static SKRectI PixelArea(SKBitmap pixels, Rect area, float scale) => new(
        Math.Clamp((int)MathF.Ceiling(area.X * scale), 0, pixels.Width),
        Math.Clamp((int)MathF.Ceiling(area.Y * scale), 0, pixels.Height),
        Math.Clamp((int)MathF.Floor((area.X + area.Width) * scale), 0, pixels.Width),
        Math.Clamp((int)MathF.Floor((area.Y + area.Height) * scale), 0, pixels.Height));

    private static bool Different(SKBitmap before, SKBitmap after, Rect area, float scale)
    {
        var rect = PixelArea(after, area, scale);
        Require(rect.Width > 0 && rect.Height > 0, "Pixel comparison region is empty.");
        for (var y = rect.Top; y < rect.Bottom; y++)
            for (var x = rect.Left; x < rect.Right; x++)
                if (before.GetPixel(x, y) != after.GetPixel(x, y)) return true;
        return false;
    }

    private static (int Count, int Height) Ink(SKBitmap pixels, Rect area, float scale, Color color)
    {
        var rect = PixelArea(pixels, area, scale);
        var first = rect.Bottom; var last = rect.Top - 1; var count = 0;
        for (var y = rect.Top; y < rect.Bottom; y++)
            for (var x = rect.Left; x < rect.Right; x++)
            {
                var pixel = pixels.GetPixel(x, y);
                if (Math.Abs(pixel.Red - color.R) > 24 || Math.Abs(pixel.Green - color.G) > 24 || Math.Abs(pixel.Blue - color.B) > 24) continue;
                first = Math.Min(first, y); last = y; count++;
            }
        return (count, count == 0 ? 0 : last - first + 1);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed record Checkpoint(string Id, string Png, string Sha256, UiViewport Viewport, string? Focused,
        UiSemanticNode[] Targets, string? Diagnostic);
}
