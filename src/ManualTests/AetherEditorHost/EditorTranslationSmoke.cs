using System.Numerics;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether;
using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Translation;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Scenes;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.ManualTests.AetherEditorHost;

internal static class EditorTranslationSmoke
{
    internal static async Task RunAsync(string directory, HostedUiWindow window, UiSession ui, EditorView view, EditorWorkspace workspace,
        EditorDocumentCommands commands, SceneDocumentSession document, AetherHost engine, NativeRuntimeFactory runtime,
        EditorSession editor, EditorPreview coordinator)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2)); var token = deadline.Token;
        await ui.InvokeAsync(() => Require(commands.Execute(DocumentCommand.Open, "Transform.aetherscene"), "Open rejected."));
        await commands.Pending.WaitAsync(token); if (commands.Read().Error is { } openingError) throw openingError;
        await ui.InvokeAsync(() => { editor.Present(ui, view); Require(coordinator.Show(document.Read().Content!.Resolved), "Preview rejected."); });
        await coordinator.Pending.WaitAsync(token); if (coordinator.Read().Error is { } showingError) throw showingError;
        var source = document.Read().Content!; var entity = source.Document.Entities[0].Id;
        var inspector = view.Inspector!.DataContext!; TranslationPosition candidate = default; TranslationTarget? target = null;
        (float X, float Y) pointer = default; var captures = new List<object>();
        await Capture("translation-initial.png", source.Document.Revision);
        await ui.InvokeAsync(() =>
        {
            view.DataContext!.Select(entity.Value.ToString("D")); editor.Present(ui, view);
            pointer = Handle(); var before = inspector.Fields.Single(f => f.Key == "x"); before.Value = "invalid";
            ui.HandleInput(new(InputKind.PointerDown, pointer.X, pointer.Y)); Require(!view.ViewportController!.IsTranslating, "Draft admitted translation.");
            ui.HandleInput(new(InputKind.PointerMove, pointer.X + 30, pointer.Y)); ui.HandleInput(new(InputKind.PointerUp, pointer.X + 30, pointer.Y));
            Require(before.Value == "invalid" && inspector.HasDraft, "Blocked gesture lost the draft."); inspector.Cancel();
            pointer = Handle(); Require(ui.HandleInput(new(InputKind.PointerDown, pointer.X, pointer.Y)).PointerCaptured && view.ViewportController.IsTranslating, "Translation capture rejected.");
            ui.HandleInput(new(InputKind.PointerMove, pointer.X + 35, pointer.Y)); candidate = view.ViewportController.TranslationCandidate!.Value;
            var receipt = editor.ReadPresentedView(view)!;
            Require(TranslationTarget.TryCapture(source, receipt.Picks, entity, out target), "Target capture failed.");
            Require(ReferenceEquals(source.Document, document.Read().Content!.Document) && source.UndoCount == document.Read().Content!.UndoCount, "Move mutated canonical source.");
        });
        await Capture("translation-outline.png", source.Document.Revision, true);
        await ui.InvokeAsync(() =>
        {
            Require(!ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)).PointerCaptured, "Esc retained capture.");
            Require(!view.ViewportController!.IsTranslating && ReferenceEquals(source.Document, document.Read().Content!.Document), "Cancel committed.");
            pointer = Handle(); Require(ui.HandleInput(new(InputKind.PointerDown, pointer.X, pointer.Y)).PointerCaptured, "Second capture rejected.");
            ui.HandleInput(new(InputKind.PointerMove, pointer.X + 35, pointer.Y)); candidate = view.ViewportController.TranslationCandidate!.Value;
            Require(!ui.HandleInput(new(InputKind.PointerUp, pointer.X + 35, pointer.Y)).PointerCaptured, "Release retained capture.");
            Require(document.Read().Content!.UndoCount == source.UndoCount + 1, "Release did not create exactly one Undo.");
            Require(document.Read().Content!.Resolved.Entities[entity].Components[target!.Component].Properties["x"].Value.GetNumber() == candidate.X, "Commit position differs.");
        });
        await Capture("translation-committed.png", source.Document.Revision + 1, false, true);
        await ui.InvokeAsync(() => inspector.Undo()); await Capture("translation-undo.png", source.Document.Revision + 2);
        await ui.InvokeAsync(() =>
        {
            var current = document.Read().Content!; var x = current.Resolved.Entities[entity].Components[target!.Component].Properties["x"].Value.GetNumber();
            Require(x == target.Original.X && current.RedoCount == 1, "Shared Undo failed."); inspector.Redo();
        });
        await Capture("translation-redo.png", source.Document.Revision + 3, false, true);
        await ui.InvokeAsync(() => Require(commands.Execute(DocumentCommand.Save), "Fixture Save rejected.")); await commands.Pending.WaitAsync(token);
        if (commands.Read().Error is { } saveError) throw saveError;
        await coordinator.StopAsync().WaitAsync(token); Require(runtime.Leases.All(l => l.ResourcesReleased), "Preview child remained.");
        File.WriteAllText(Path.Combine(directory, "translation.json"), JsonSerializer.Serialize(new
        { Passed = true, WorldAxes = true, OutlineBeforeCommit = true, CanonicalUnchangedOnMoveAndCancel = true, OneCommit = true, SharedUndoRedo = true,
            DirectedUiInput = true, PhysicalInput = false, OriginalRevision = source.Document.Revision, Candidate = candidate,
            Children = runtime.Leases.Count, Reaped = runtime.Leases.Count(l => l.ResourcesReleased), Captures = captures }, new JsonSerializerOptions { WriteIndented = true }));
        await EditorHeaderSmoke.ClickActionAsync(ui, view, "file", "file.exit", token); await window.Completion.WaitAsync(token);

        (float X, float Y) Handle()
        {
            var receipt = editor.ReadPresentedView(view) ?? throw new InvalidOperationException("No actual view.");
            var primitive = receipt.Picks.Primitives.Single(p => p.Entity == entity);
            Require(TranslationGeometry.TryCreate(receipt.Projection, receipt.ImageBounds, primitive.World.Translation, out var geometry), "Geometry unavailable.");
            var axis = geometry!.Handles.Single(h => h.Kind == TranslationHandle.X);
            return ((float)((axis.Points[0].X + axis.Points[1].X) / 2), (float)((axis.Points[0].Y + axis.Points[1].Y) / 2));
        }
        async Task Capture(string name, long revision, bool outline = false, bool committed = false)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(UiSession current)
            {
                var receipt = editor.ReadPresentedView(view);
                if ((!outline && current.NeedsUpdate) || receipt?.State.Context.SceneRevision != revision || !engine.Preview.IsCurrent(receipt)) return;
                window.FramePresented -= Presented;
                try
                {
                    using var image = current.ReadbackOpenGLImage(); using var bitmap = SKBitmap.FromImage(image);
                    var witnesses = 0;
                    if (outline)
                    {
                        var worldDelta = new Vector3((float)(candidate.X - target!.Original.X), (float)(candidate.Y - target.Original.Y), (float)(candidate.Z - target.Original.Z));
                        double left = double.PositiveInfinity, top = double.PositiveInfinity, right = double.NegativeInfinity;
                        for (var i = 0; i < 8; i++)
                        {
                            var corner = Vector3.Transform(new((i & 1) == 0 ? -.5f : .5f, (i & 2) == 0 ? -.5f : .5f, (i & 4) == 0 ? -.5f : .5f), target.World) + worldDelta;
                            Require(TranslationGeometry.Project(corner, receipt.Projection, receipt.ImageBounds, out var point), "Ghost projection failed.");
                            left = Math.Min(left, point.X); right = Math.Max(right, point.X); top = Math.Min(top, point.Y);
                        }
                        var scale = current.Viewport.Scale; var expected = view.Canvas.Theme.Modified;
                        for (var y = Math.Max(0, (int)(top * scale) - 4); y <= Math.Min(bitmap.Height - 1, (int)(top * scale) + 4); y++)
                            for (var x = Math.Max(0, (int)(left * scale)); x <= Math.Min(bitmap.Width - 1, (int)(right * scale)); x++)
                            {
                                var pixel = bitmap.GetPixel(x, y);
                                if (Math.Abs(pixel.Red - expected.R) <= 2 && Math.Abs(pixel.Green - expected.G) <= 2 && Math.Abs(pixel.Blue - expected.B) <= 2) witnesses++;
                            }
                        Require(witnesses > 10 && view.ViewportController!.IsTranslating, "Actual outline pixels missing.");
                    }
                    if (committed)
                    {
                        var cube = receipt.Picks.Primitives.Single(p => p.Entity == entity);
                        Require(Math.Abs(cube.World.M41 - (float)candidate.X) < .00001f, "Actual new pick payload has old translation.");
                        Require(TranslationGeometry.Project(cube.World.Translation, receipt.Projection, receipt.ImageBounds, out var center), "Cube center unavailable.");
                        var pixel = bitmap.GetPixel((int)(center.X * current.Viewport.Scale), (int)(center.Y * current.Viewport.Scale));
                        var expected = PrimitiveSceneAdapter.Project(document.Read().Content!.Resolved).CopyEntities().Single(e => e.Id == entity.Value.ToString("D")).Primitive!.ToPrimitive().Color;
                        Require(Math.Abs(pixel.Red - expected.X * 255) <= 2 && Math.Abs(pixel.Green - expected.Y * 255) <= 2 && Math.Abs(pixel.Blue - expected.Z * 255) <= 2, "Actual committed solid pixels differ.");
                    }
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100); using var stream = File.Create(Path.Combine(directory, name)); data.SaveTo(stream);
                    captures.Add(new { Name = name, Revision = revision, OutlineWitnesses = witnesses, Frame = receipt.Frame.Metadata.FrameId }); done.TrySetResult();
                }
                catch (Exception error) { done.TrySetException(error); }
            }
            window.FramePresented += Presented; window.RequestFrame();
            try { await done.Task.WaitAsync(token); } finally { window.FramePresented -= Presented; }
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
