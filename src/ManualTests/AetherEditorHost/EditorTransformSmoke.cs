using TrueMoon.Aether.Editor;
using System.Numerics;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.ManualTests.AetherEditorHost;

// Directed acceptance uses only the current app tree and a disposable external project; never global input/settings.
internal static class EditorTransformSmoke
{
    internal static async Task RunAsync(string directory, HostedUiWindow window, UiSession ui, EditorView view,
        EditorWorkspace workspace, EditorDocumentCommands commands, SceneDocumentSession document, ISceneDocumentStore store,
        EditorPreview preview, AetherHost engine, NativeRuntimeFactory runtime)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3)); var token = deadline.Token;
        var panel = view.Inspector ?? throw new InvalidOperationException("Missing schema-driven inspector.");
        var model = (EditorInspectorViewModel)panel.DataContext!;
        var documents = view.Documents!; var documentModel = (EditorDocumentViewModel)documents.DataContext!;
        var captures = new List<string>(); var revisions = new List<long>(); var owners = new HashSet<int>();
        var source = Path.Combine(commands.Read().ScenesDirectory!, "Transform.aetherscene");
        var sourceBytes = await File.ReadAllBytesAsync(source, token);
        var owner = 0;
        await ui.InvokeAsync(() => { owner = Environment.CurrentManagedThreadId; model.PropertyChanged += Changed; }).WaitAsync(token);
        try
        {
            await Doc(DocumentCommand.Open, "Transform.aetherscene");
            await Until(() => panel.Properties.Values.Any(p => p.Property.Key == "x" && p.Input.IsEnabled));
            var fields = panel.Properties.Values.Where(p => p.Property is TransformField)
                .ToDictionary(p => p.Property.Key, p => (TextBox)p.Input);
            var original = document.Read().Content!; var selected = workspace.SelectedId;
            var input = fields["x"]; var root = panel.Form;
            await Set(input, "NaN");
            await Until(() => !panel.Apply.IsEnabled && model.Fields[0].Error.Length != 0);
            Require(document.Read().Content!.Document.Revision == original.Document.Revision && !document.Read().IsDirty, "Invalid draft committed.");
            await ui.InvokeAsync(() => ui.SetTheme(Theme.Light with { FontFamily = "Noto Sans" })).WaitAsync(token);
            await Until(() => panel.Theme.Surface == Theme.Light.Surface);
            Require(ReferenceEquals(input, panel.Properties.Values.Single(p => p.Property.Key == "x").Input) && ReferenceEquals(root, panel.Form) && input.Value == "NaN", "Theme rebuilt fields or discarded draft.");
            await Click(panel.Cancel); await Until(() => input.Value == "-1.5");
            await Set(input, "1.5");
            await ui.InvokeAsync(() => ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true))).WaitAsync(token);
            await Until(() => document.Read().IsDirty && panel.Undo.IsEnabled);
            Require(document.Read().Content!.UndoCount == 1 && X() == 1.5, "Gesture did not use one authoring history transaction.");
            await PreviewAction(start: true); await Preview(); await Capture("transform-edited.png", 1.5f);
            await Click(panel.Undo); await Until(() => !document.Read().IsDirty); await Preview(); await Capture("transform-undo.png", -1.5f);
            await Click(panel.Redo); await Until(() => document.Read().IsDirty); await Preview(); await Capture("transform-redo.png", 1.5f);
            Require(ReferenceEquals(input, panel.Properties.Values.Single(p => p.Property.Key == "x").Input) && ReferenceEquals(root, panel.Form) && workspace.SelectedId == selected, "Committed refresh rebuilt the editable tree or selection.");
            await Doc(DocumentCommand.SaveAs, "Edited.aetherscene");
            var currentSource = await File.ReadAllBytesAsync(source, token);
            Require(!document.Read().IsDirty && sourceBytes.SequenceEqual(currentSource), "Save changed source fixture or lost checkpoint.");
            await Doc(DocumentCommand.Close); Require(runtime.Leases.All(x => x.Reaped && x.ResourcesReleased), "Document close retained a preview child.");
            await Doc(DocumentCommand.Open, "Edited.aetherscene");
            await Until(() => panel.Properties.Values.Any(p => p.Property.Key == "x" && ((TextBox)p.Input).Value == "1.5"));
            Require(X() == 1.5 && document.Read().Content!.UndoCount == 0 && !document.Read().IsDirty, "Reopen did not persist Transform or retained ephemeral history.");
            var saved = await store.ReadAsync(document.Read().Path!, token);
            Require(saved.Document.Id == original.Document.Id, "Save/reopen changed scene identity.");
            await PreviewAction(start: true); await Preview();
            await PreviewAction(start: false); await preview.Pending.WaitAsync(token);
            Require(engine.Preview.Document == null && runtime.Leases.All(x => x.Reaped && x.ResourcesReleased), "Stop did not drain preview ownership.");
            await ui.InvokeAsync(() => ClickInput(((WindowFrame)ui.Root).DefaultTitleBar!.CloseButton)).WaitAsync(token);
            await window.Completion.WaitAsync(token);
            Require(owners.SetEquals([owner]) && engine.Builds.ProcessId == null && runtime.Leases.Count == 4, "Notifications escaped UI owner or preview used game build.");
            File.WriteAllText(Path.Combine(directory, "transform.json"), JsonSerializer.Serialize(new
            {
                Passed = true, Captures = captures, PresentedRevisions = revisions, Children = runtime.Leases.Count,
                CompilerStarted = engine.Builds.ProcessId != null, OwnerThread = owner, NotificationThreads = owners.ToArray(),
                SavedFile = saved.Document.Id.Value, SourceUnchanged = true, StableFields = true
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { model.PropertyChanged -= Changed; }
        void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs change) => owners.Add(Environment.CurrentManagedThreadId);
        double X() => document.Read().Content!.Resolved.Entities[new(Guid.Parse(workspace.SelectedId!))].Components.Values
            .Single(c => c.TypeId == BuiltInComponents.TransformId).Properties["x"].Value.GetNumber();
        async Task PreviewAction(bool start)
        {
            await ui.InvokeAsync(() => ClickInput(view.StatusBar!.PreviewButton)).WaitAsync(token);
            await Until(() => view.Overlays.Popup is EditorStatusPopupView && !ui.NeedsUpdate);
            var popup = (EditorStatusPopupView)view.Overlays.Popup!;
            await Click(start ? popup.StartPreview : popup.StopPreview);
            await ui.InvokeAsync(() => ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape))).WaitAsync(token);
        }
        async Task Doc(DocumentCommand command, string path = "")
        {
            File.AppendAllText(Path.Combine(directory, "progress.log"), "Document " + command + " " + path + Environment.NewLine);
            await Until(() => documents.Commands[command].IsEnabled);
            await ui.InvokeAsync(() => documentModel.PathDraft = path).WaitAsync(token);
            await EditorHeaderSmoke.ClickActionAsync(ui, view, "file", "document." + command, token);
            await commands.Pending.WaitAsync(token);
            Require(commands.Read().Error == null, "Document command failed: " + commands.Read().Error);
            await Until(() => !commands.Read().Busy && ReferenceEquals(documentModel.Document?.Content?.Document, document.Read().Content?.Document));
        }
        async Task Preview()
        {
            File.AppendAllText(Path.Combine(directory, "progress.log"), "Await preview " + document.Read().Content!.Document.Revision + Environment.NewLine);
            await Until(() => preview.Read() is { Busy: false, Presented: { } stamp } && stamp.Revision == document.Read().Content!.Document.Revision);
            Require(preview.Read().Error == null && engine.Preview.LastPresented?.Document == preview.Read().Presented, "Preview did not acknowledge actual current revision.");
            revisions.Add(preview.Read().Presented!.Revision);
        }
        async Task Set(TextBox field, string value)
        {
            await ui.InvokeAsync(() => { ClickInput(field); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true)); ui.HandleInput(new(InputKind.Text, Text: value)); }).WaitAsync(token);
            await Until(() => field.Value == value);
        }
        async Task Click(Button button) { await Until(() => button.IsEffectivelyEnabled); await ui.InvokeAsync(() => ClickInput(button)).WaitAsync(token); }
        void ClickInput(Element element)
        {
            var bounds = element.Bounds;
            Require(bounds.Width > 0 && bounds.Height > 0, "Acceptance control has no layout.");
            ui.HandleInput(new(InputKind.PointerDown, bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
            ui.HandleInput(new(InputKind.PointerUp, bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
        }
        async Task Until(Func<bool> condition)
        {
            while (true)
            {
                var ready = false; await ui.InvokeAsync(() => ready = condition()).WaitAsync(token);
                if (ready) return; await Task.Delay(10, token);
                if (preview.Read().Error is { } error) throw new InvalidOperationException("Preview failed.", error);
            }
        }
        async Task Capture(string name, float x)
        {
            File.AppendAllText(Path.Combine(directory, "progress.log"), "Capture " + name + Environment.NewLine);
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(UiSession current)
            {
                if (current.NeedsUpdate || view.Canvas.DrawnFrame is not { } frame || engine.Preview.Describe(frame)?.Document != preview.Read().Presented) return;
                window.FramePresented -= Presented;
                try
                {
                    Require(view.StatusBar?.DataContext is EditorStatusBarViewModel
                        { Kind: EditorStatusKind.Ready, Summary: "Редактирование сцены" },
                        "Independent Edit Preview was presented as game simulation.");
                    using var image = current.ReadbackOpenGLImage(); using var bitmap = SKBitmap.FromImage(image);
                    var bounds = view.Canvas.ImageBounds;
                    var projection = new PrimitiveScene(PreviewGameSession.Camera, 0, Vector3.One).CreateViewProjection(640, 360);
                    var clip = Vector4.Transform(new Vector4(x, 1, -1, 1), projection);
                    var pixel = bitmap.GetPixel((int)((bounds.X + bounds.Width * (clip.X / clip.W + 1) / 2) * current.Viewport.Scale),
                        (int)((bounds.Y + bounds.Height * (clip.Y / clip.W + 1) / 2) * current.Viewport.Scale));
                    Require(Math.Abs(pixel.Red - 204) <= 2 && Math.Abs(pixel.Green - 51) <= 2 && Math.Abs(pixel.Blue - 38) <= 2, "Transform preview pixel differs: " + pixel);
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100); using var file = File.Create(Path.Combine(directory, name)); data.SaveTo(file);
                    captures.Add(name); completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
            }
            window.FramePresented += Presented; window.RequestFrame();
            try { await completion.Task.WaitAsync(token); } finally { window.FramePresented -= Presented; }
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
