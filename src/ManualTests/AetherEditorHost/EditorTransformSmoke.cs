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
            var compact = panel.TransformEditors.Values.Single();
            var resetX = X();
            await Click(compact.Degrees); await Until(() => model.AnglesInDegrees && compact.Rotation.First.Step == 1);
            Require(ReferenceEquals(original, document.Read().Content) && !model.HasDraft && !document.Read().IsDirty &&
                compact.Rotation.Label.Value == "Поворот, °", "Unit-only switch created a draft/history or lost its display units.");
            await Click(compact.Degrees); await Until(() => !model.AnglesInDegrees && compact.Rotation.First.Step == .01);
            await Set(input, "NaN");
            await Until(() => !panel.Apply.IsEnabled && model.Fields[0].Error.Length != 0);
            Require(document.Read().Content!.Document.Revision == original.Document.Revision && !document.Read().IsDirty, "Invalid draft committed.");
            await ui.InvokeAsync(() => ui.SetTheme(Theme.Light with { FontFamily = panel.Theme.FontFamily, FontSize = panel.Theme.FontSize, FontWeight = panel.Theme.FontWeight, FontItalic = panel.Theme.FontItalic })).WaitAsync(token);
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

            // Group reset is a draft action. Invalid rotation and linked scale can be recovered without committing.
            var beforeGroups = document.Read().Content!;
            await Set(fields["pitch"], "-"); await Click(compact.ResetRotation);
            await Until(() => model.Fields[3].IsReset && !model.Fields[3].HasError);
            Require(ReferenceEquals(beforeGroups, document.Read().Content), "Rotation reset committed before Apply.");
            await Click(panel.Cancel);
            await Click(compact.LinkScale); await Set(fields["sy"], "-"); await Set(fields["sx"], "8");
            await Until(() => model.HasDiagnostics && !panel.Apply.IsEnabled);
            await Click(compact.ResetScale); await Until(() => !model.HasDiagnostics && panel.Apply.IsEnabled);
            Require(model.ScaleLinked && model.Fields[6].IsReset && model.Fields[7].IsReset &&
                ReferenceEquals(beforeGroups, document.Read().Content), "Linked Scale reset propagated values or committed early.");
            await Capture("transform-group-scale-staged.png", 1.5f);
            await Click(panel.Cancel);
            await ui.InvokeAsync(() =>
            {
                panel.Scroll.BringIntoView(compact.ResetPosition.Bounds); ui.Focus(compact.ResetPosition);
                ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Space)); ui.HandleInput(new(InputKind.KeyUp, Key: UiKey.Space));
            }).WaitAsync(token);
            await Until(() => model.Fields[0].IsReset && !compact.ResetPosition.IsEnabled);
            Require(ReferenceEquals(beforeGroups, document.Read().Content), "Position reset committed before Apply.");
            await Click(panel.Apply);
            await Until(() => !model.HasDraft && document.Read().Content!.UndoCount == beforeGroups.UndoCount + 1);
            Require(X() == resetX && !model.Fields[0].CanReset, "Group reset did not restore inherited Position.");
            await Preview(); await Capture("transform-group-reset.png", (float)resetX);
            await Click(panel.Undo); await Until(() => X() == 1.5); await Preview(); await Capture("transform-group-undo.png", 1.5f);
            await Click(panel.Redo); await Until(() => X() == resetX); await Preview(); await Capture("transform-group-redo.png", (float)resetX);
            Require(ReferenceEquals(compact, panel.TransformEditors.Values.Single()) && ReferenceEquals(input, compact.Position.First.Editor) &&
                model.ScaleLinked && workspace.SelectedId == selected, "Group reset/history rebuilt controls or lost presentation state.");

            // Rotation display changes never enter authoring; a degree edit commits canonical radians once.
            await Click(compact.Degrees); await Until(() => model.AnglesInDegrees && compact.Rotation.First.Step == 1);
            var beforeAngle = document.Read().Content!;
            await Set(fields["pitch"], "-"); await Until(() => !compact.Degrees.IsEnabled && model.Fields[3].HasError);
            Require(model.AnglesInDegrees && fields["pitch"].Value == "-", "Invalid angle changed units or lost its text.");
            await Capture("transform-angle-invalid.png", (float)resetX);
            await Click(panel.Cancel); await Until(() => compact.Degrees.IsEnabled && model.AnglesInDegrees);
            await Set(fields["yaw"], "9.000e1");
            await Click(compact.Degrees); await Until(() => !model.AnglesInDegrees);
            Require(model.Fields[4].Value == fields["yaw"].Value && Rotation() == 0, "Display switch committed or did not expose radians.");
            await Click(compact.Degrees); await Until(() => model.AnglesInDegrees && fields["yaw"].Value == "9.000e1");
            Require(ReferenceEquals(beforeAngle, document.Read().Content) && ReferenceEquals(fields["yaw"], compact.Rotation.Second.Editor),
                "Unit round trip committed or replaced a retained angle editor.");
            await Click(panel.Apply); await Until(() => !model.HasDraft && document.Read().Content!.UndoCount == beforeAngle.UndoCount + 1);
            Require(Rotation() == Math.PI / 2, "Degree edit was not committed as canonical radians.");
            await Preview(); await Capture("transform-angle-edited.png", (float)resetX, rotated: true);
            await Click(panel.Undo); await Until(() => Rotation() == 0); await Preview(); await Capture("transform-angle-undo.png", (float)resetX);
            await Click(panel.Redo); await Until(() => Rotation() == Math.PI / 2); await Preview(); await Capture("transform-angle-redo.png", (float)resetX, rotated: true);
            await Doc(DocumentCommand.SaveAs, "Edited.aetherscene");
            var currentSource = await File.ReadAllBytesAsync(source, token);
            Require(!document.Read().IsDirty && sourceBytes.SequenceEqual(currentSource), "Save changed source fixture or lost checkpoint.");
            await Doc(DocumentCommand.Close); Require(runtime.Leases.All(x => x.Reaped && x.ResourcesReleased), "Document close retained a preview child.");
            await Doc(DocumentCommand.Open, "Edited.aetherscene");
            await Until(() => panel.Properties.Values.Any(p => p.Property.Key == "x" && ((TextBox)p.Input).Value == resetX.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Require(X() == resetX && !model.Fields[0].CanReset && document.Read().Content!.UndoCount == 0 && !document.Read().IsDirty,
                "Reopen did not persist inherited group reset or retained ephemeral history.");
            var reopened = panel.TransformEditors.Values.Single();
            Require(Rotation() == Math.PI / 2 && model.AnglesInDegrees && reopened.Degrees.IsChecked && reopened.Rotation.Second.Editor.Value == "90",
                "Save/reopen lost canonical radians or the transient unit preference.");
            var saved = await store.ReadAsync(document.Read().Path!, token);
            Require(saved.Document.Id == original.Document.Id, "Save/reopen changed scene identity.");
            await PreviewAction(start: true); await Preview();
            await PreviewAction(start: false); await preview.Pending.WaitAsync(token);
            Require(engine.Preview.Document == null && runtime.Leases.All(x => x.Reaped && x.ResourcesReleased), "Stop did not drain preview ownership.");
            await ui.InvokeAsync(() => ClickInput(((WindowFrame)ui.Root).DefaultTitleBar!.CloseButton)).WaitAsync(token);
            await window.Completion.WaitAsync(token);
            Require(owners.SetEquals([owner]) && engine.Builds.ProcessId == null && runtime.Leases.Count == 10, "Notifications escaped UI owner or preview used game build.");
            File.WriteAllText(Path.Combine(directory, "transform.json"), JsonSerializer.Serialize(new
            {
                Passed = true, Captures = captures, PresentedRevisions = revisions, Children = runtime.Leases.Count,
                CompilerStarted = engine.Builds.ProcessId != null, OwnerThread = owner, NotificationThreads = owners.ToArray(),
                SavedFile = saved.Document.Id.Value, SourceUnchanged = true, StableFields = true,
                TransformGroupReset = true, LinkedScaleReset = true, GroupResetKeyboard = true, GroupResetSaveReopen = true,
                AngleUnits = true, UnitOnlyNoHistory = true, InvalidAngleAdmission = true, ExactUnitRoundTrip = true, CanonicalRadiansSaveReopen = true
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { model.PropertyChanged -= Changed; }
        void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs change) => owners.Add(Environment.CurrentManagedThreadId);
        double X() => document.Read().Content!.Resolved.Entities[new(Guid.Parse(workspace.SelectedId!))].Components.Values
            .Single(c => c.TypeId == BuiltInComponents.TransformId).Properties["x"].Value.GetNumber();
        double Rotation() => document.Read().Content!.Resolved.Entities[new(Guid.Parse(workspace.SelectedId!))].Components.Values
            .Single(c => c.TypeId == BuiltInComponents.TransformId).Properties["yaw"].Value.GetNumber();
        async Task PreviewAction(bool start)
        {
            // As with the header menu, native focus loss may dismiss a directed popup.
            // Hold only its disclosure; the application Start/Stop action below executes once.
            await Until(() => view.Overlays.Popup is EditorStatusPopupView && !ui.NeedsUpdate,
                () => { if (view.Overlays.Popup == null) ClickInput(view.StatusBar!.PreviewButton); });
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
        async Task Until(Func<bool> condition, Action? holdInput = null,
            [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string expression = "")
        {
            File.AppendAllText(Path.Combine(directory, "progress.log"), "Await " + expression + Environment.NewLine);
            var lastState = "No UI sample";
            try
            {
                while (true)
                {
                    var ready = false; await ui.InvokeAsync(() =>
                    {
                        holdInput?.Invoke(); ready = condition();
                        lastState = $"Popup={view.Overlays.Popup?.GetType().Name ?? "none"}; NeedsUpdate={ui.NeedsUpdate}; Selection={workspace.SelectedId}; Degrees={model.AnglesInDegrees}; Draft={model.HasDraft}";
                    }).WaitAsync(token);
                    if (ready) return; await Task.Delay(10, token);
                    if (preview.Read().Error is { } error) throw new InvalidOperationException("Preview failed.", error);
                }
            }
            catch (OperationCanceledException)
            {
                File.AppendAllText(Path.Combine(directory, "progress.log"), "Timed out: " + expression + " · " + lastState + Environment.NewLine);
                throw;
            }
        }
        async Task Capture(string name, float x, bool rotated = false)
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
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100); using var file = File.Create(Path.Combine(directory, name)); data.SaveTo(file);
                    // cube.vert assigns .65 to the left face exposed by +90° yaw; the original front face uses 1.
                    var light = rotated ? .65f : 1f;
                    Require(Math.Abs(pixel.Red - 204 * light) <= 2 && Math.Abs(pixel.Green - 51 * light) <= 2 && Math.Abs(pixel.Blue - 38 * light) <= 2,
                        "Transform preview pixel differs: " + pixel);
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
