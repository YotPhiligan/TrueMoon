using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace AetherGuiConsumer;

/// <summary>Scoped native OpenGL acceptance. Routed input and optional Windows input have separate evidence.</summary>
internal static class NumericWorkflow
{
    internal static async Task<object> Run(string directory, HostedUiWindow window, UiSession ui, EditorView view,
        IApp app, bool windowsInput, bool windowsArrows, CancellationToken token)
    {
        var projects = app.Services.Resolve<EditorProjects>()!;
        var documents = app.Services.Resolve<EditorDocumentCommands>()!;
        var workspace = app.Services.Resolve<EditorWorkspace>()!;
        var document = projects.Active!.Documents!;
        var panel = view.Inspector!;
        var model = panel.DataContext!;
        var proofs = new List<object>();
        var captures = new List<string>();
        var owner = 0;
        if (windowsInput) window.PostWindow(native => native.Input += input =>
        {
            if (input.Kind != InputKind.PointerMove)
                File.AppendAllText(Path.Combine(directory, "windows-input.jsonl"), JsonSerializer.Serialize(input) + Environment.NewLine);
        });
        await Until(() => documents.CanExecute(DocumentCommand.Open));
        await OnUi(() => Program.Check(documents.Execute(DocumentCommand.Open, "Original.aetherscene"), "Open rejected."));
        await documents.Pending.WaitAsync(token);
        Program.Check(documents.Read().Error == null, "Open failed: " + documents.Read().Error);
        await Until(() => !documents.Read().Busy && workspace.EntityIds.Contains(Program.CubeId.ToString("D")));
        await OnUi(() => { owner = Environment.CurrentManagedThreadId; workspace.Select(Program.CubeId.ToString("D")); });
        await Until(() => panel.TransformEditors.Count == 1 && !ui.NeedsUpdate);
        var transform = panel.TransformEditors.Values.Single();
        var position = transform.Position.First;
        var scale = transform.Scale.First;
        var form = panel.Form;
        var original = document.Read().Content!;
        await OnUi(() => { Program.Check(position.Editor.Value == "-1.5", "Unexpected position baseline."); Scales(2, 3, 5); });

        await OnUi(() => { ui.Focus(position.Editor); Key(UiKey.Up); });
        await Until(() => position.Editor.Value == "-1.4");
        await OnUi(() => { Key(UiKey.Up, shift: true); Key(UiKey.Down, control: true); });
        await Until(() => position.Editor.Value == "-0.41");
        await OnUi(() => { Uncommitted(); Key(UiKey.Escape); });
        await Until(() => !model.HasDraft && position.Editor.Value == "-1.5");
        proofs.Add(new { Input = "UiSession.HandleInput", Case = "ArrowShiftCtrlAndDraftEscape", Passed = true });

        await OnUi(() => { ui.Focus(position.Editor); Key(UiKey.A, control: true); ui.HandleInput(new(InputKind.Text, Text: "NaN")); Key(UiKey.Up); });
        await Until(() => position.Editor.Value == "NaN" && !model.CanApply);
        await Capture("invalid.png");
        await OnUi(() => { Uncommitted(); Key(UiKey.Escape); });
        await Until(() => !model.HasDraft);
        proofs.Add(new { Input = "UiSession.HandleInput", Case = "InvalidArrowRejected", Passed = true });

        await OnUi(() =>
        {
            Field("sx").Value = "02.0"; Field("sy").Value = "03.0"; Field("sz").Value = "05.0";
            model.ScaleLinked = true;
        });
        await Until(() => scale.Editor.Value == "02.0" && transform.LinkScale.IsChecked && !ui.NeedsUpdate);
        var start = await Start(scale);
        await OnUi(() =>
        {
            Program.Check(ui.HandleInput(new(InputKind.PointerMove, start.X + 8, start.Y)).PointerCaptured, "Drag capture lost.");
        });
        await Until(() => scale.Editor.Value == "2.02");
        await OnUi(() => { Scales(2.02, 3.03, 5.05); Uncommitted(); });
        await Capture("linked-drag.png");
        await OnUi(() =>
        {
            var result = ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape));
            Program.Check(result.Handled && !result.PointerCaptured && !scale.IsDragging, "Esc did not release capture.");
        });
        await Until(() => scale.Editor.Value == "02.0");
        await OnUi(() =>
        {
            Program.Check(Field("sy").Value == "03.0" && Field("sz").Value == "05.0" && model.HasDraft,
                "Gesture Escape globally cancelled or changed exact previous drafts.");
            Uncommitted(); Key(UiKey.Escape);
        });
        await Until(() => !model.HasDraft && scale.Editor.Value == "2");
        proofs.Add(new { Input = "UiSession.HandleInput", Case = "CapturedLinkedDragExactEscape", Passed = true });

        start = await Start(scale);
        await OnUi(() => ui.HandleInput(new(InputKind.PointerMove, start.X + 40, start.Y)));
        await Until(() => scale.Editor.Value == "2.1");
        await OnUi(() =>
        {
            var released = ui.HandleInput(new(InputKind.PointerUp, start.X + 40, start.Y));
            Program.Check(!released.PointerCaptured && !scale.IsDragging, "Release retained capture.");
            Scales(2.1, 3.15, 5.25); Uncommitted(); Key(UiKey.Enter, control: true);
        });
        await Until(() => !model.HasDraft && document.Read().Content!.UndoCount == 1);
        await Capture("applied.png");
        await OnUi(() =>
        {
            var entity = document.Read().Content!.Resolved.Entities[new(Program.CubeId)];
            var values = entity.Components.Values.Single(component => component.Properties.ContainsKey("sx")).Properties;
            Program.Check(values["sx"].Value.GetNumber() == 2.1 && values["sy"].Value.GetNumber() == 3.15 && values["sz"].Value.GetNumber() == 5.25,
                "Explicit Apply did not commit the proportional values.");
            Click(panel.Undo);
        });
        await Until(() => !document.Read().IsDirty && document.Read().Content!.UndoCount == 0 && !ui.NeedsUpdate);
        proofs.Add(new { Input = "UiSession.HandleInput", Case = "ReleaseDraftExplicitApplySingleUndo", Passed = true });
        // Undo leaves an equivalent immutable document; retain this exact reference for the Windows input phase.
        original = document.Read().Content!;
        await OnUi(() => model.ScaleLinked = false);
        await Until(() => !transform.LinkScale.IsChecked && !ui.NeedsUpdate);
        if (windowsInput)
        {
            if (windowsArrows)
            {
            await Stage("01-position-up", () => position.Editor.Value == "-1.4", Uncommitted);
            await Stage("02-position-shift-up", () => position.Editor.Value == "-0.4", Uncommitted);
            await Stage("03-position-ctrl-down", () => position.Editor.Value == "-0.41", Uncommitted);
            await Stage("04-escape-draft", () => !model.HasDraft && position.Editor.Value == "-1.5", Uncommitted);
            }
            await Stage("05-link-scale", () => model.ScaleLinked && transform.LinkScale.IsChecked, Uncommitted);
            if (!windowsArrows)
            {
                await Stage("06a-drag-to-cancel", () => !scale.IsDragging && Number("sx") > 2, Uncommitted);
                await Stage("06b-escape-draft", () => !model.HasDraft && scale.Editor.Value == "2", Uncommitted);
            }
            double changedScale = 0;
            await Stage("06-scale-drag-release", () => !scale.IsDragging && Number("sx") > 2, () =>
            {
                changedScale = Number("sx"); Scales(changedScale, changedScale * 1.5, changedScale * 2.5); Uncommitted();
            });
            await Stage("07-ctrl-enter", () => !model.HasDraft && document.Read().IsDirty, () =>
                Program.Check(document.Read().Content!.UndoCount == 1 && Number("sx") == changedScale, "Windows Apply history/value mismatch."));
            await Stage("08-undo-button", () => !document.Read().IsDirty && document.Read().Content!.UndoCount == 0, () => Scales(2, 3, 5));
        }
        await OnUi(() => Program.Check(ReferenceEquals(form, panel.Form) && ReferenceEquals(transform, panel.TransformEditors.Values.Single()) &&
            ReferenceEquals(position, transform.Position.First), "Numeric input rebuilt retained controls."));
        await Capture("restored.png");
        await OnUi(() => Program.Check(!model.HasDraft && !document.Read().IsDirty, "Acceptance left dirty state."));
        Program.Check(await window.RequestCloseAsync().WaitAsync(token), "Clean close vetoed.");
        await window.Completion.WaitAsync(token);
        return new { Profile = "NumericNative", OwnerThread = owner, WindowsInput = windowsInput, WindowsArrows = windowsArrows, Proofs = proofs, Captures = captures,
            Frames = window.PresentedFrames, StableControls = true, NoRuntimeRequested = true };

        InspectorProperty Field(string key) => panel.Properties.Values.Single(property => property.Property.Key == key).Property;
        double Number(string key) => double.Parse(Field(key).Value, CultureInfo.InvariantCulture);
        void Scales(double x, double y, double z)
        {
            Program.Check(Math.Abs(Number("sx") - x) < 1e-9 && Math.Abs(Number("sy") - y) < 1e-9 && Math.Abs(Number("sz") - z) < 1e-9,
                $"Scale mismatch: {Field("sx").Value}/{Field("sy").Value}/{Field("sz").Value}.");
        }
        void Uncommitted() => Program.Check(ReferenceEquals(original, document.Read().Content) && !document.Read().IsDirty && original.UndoCount == 0,
            "Numeric draft automatically committed.");
        void Key(UiKey key, bool shift = false, bool control = false) => ui.HandleInput(new(InputKind.KeyDown, Key: key, Shift: shift, Control: control));
        void Click(Element element)
        {
            var bounds = element.Bounds;
            Program.Check(bounds.Width > 0 && bounds.Height > 0 && element.IsEffectivelyEnabled, "Click has no enabled layout.");
            ui.HandleInput(new(InputKind.PointerDown, bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
            ui.HandleInput(new(InputKind.PointerUp, bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
        }
        Task OnUi(Action action) => ui.InvokeAsync(() =>
        {
            if (owner != 0) Program.Check(Environment.CurrentManagedThreadId == owner, "Callback escaped UI owner.");
            action();
        }, token).WaitAsync(token);
        async Task Until(Func<bool> condition, int seconds = 30)
        {
            var elapsed = Stopwatch.StartNew();
            while (true)
            {
                if (window.Completion.IsCompleted) throw new InvalidOperationException("Window closed before numeric acceptance completed.");
                var ready = false; await OnUi(() => ready = condition()); if (ready) return;
                if (elapsed.Elapsed.TotalSeconds > seconds) throw new TimeoutException("Numeric acceptance condition timed out.");
                await Task.Delay(20, token);
            }
        }
        async Task<(float X, float Y)> Start(NumberBox box)
        {
            await OnUi(() => panel.Scroll.BringIntoView(box.Bounds)); await Until(() => !ui.NeedsUpdate);
            var point = (X: 0f, Y: 0f);
            await OnUi(() =>
            {
                var bounds = box.DragBounds; point = (bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                Program.Check(ui.HandleInput(new(InputKind.PointerDown, point.X, point.Y)).PointerCaptured && box.IsDragging, "Drag not admitted.");
            });
            return point;
        }
        async Task Stage(string name, Func<bool> condition, Action check)
        {
            var path = Path.Combine(directory, "stage.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { Stage = name, Waiting = true }), token);
            Console.WriteLine("Windows input: " + name);
            await Until(condition, 600); await OnUi(check);
            await Capture("windows-" + name + ".png");
            proofs.Add(new { Input = "Windows", Case = name, Passed = true });
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { Stage = name, Waiting = false }), token);
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
                    using var image = current.ReadbackOpenGLImage(); using var png = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var file = File.Create(Path.Combine(directory, name)); png.SaveTo(file); done.TrySetResult();
                }
                catch (Exception error) { done.TrySetException(error); }
            }
            window.FramePresented += Presented; window.RequestFrame();
            try { await done.Task.WaitAsync(TimeSpan.FromSeconds(20), token); captures.Add(name); }
            finally { window.FramePresented -= Presented; }
        }
    }
}
