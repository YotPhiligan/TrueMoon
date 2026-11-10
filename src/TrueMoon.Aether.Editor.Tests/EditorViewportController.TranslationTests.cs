using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Translation;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorViewportTranslationTests
{
    [Fact]
    public void InvalidMove_PreservesLastValidOutlineUntilAnExplicitValidRelease()
    {
        using var t = new ViewportInputFixture(); t.Start(); var p = Begin(t); var original = t.Content;
        t.Input(InputKind.PointerMove, p.X + 30, p.Y); var candidate = t.Controller.TranslationCandidate;
        t.Input(InputKind.PointerMove, float.NaN, p.Y);
        Assert.True(t.Controller.IsTranslating); Assert.Equal(candidate, t.Controller.TranslationCandidate);
        Assert.Same(original.Document, t.Content.Document); Assert.Equal(0, t.Content.UndoCount);
        t.Input(InputKind.PointerUp, p.X + 30, p.Y);
        Assert.False(t.Controller.IsTranslating); Assert.Equal(1, t.Content.UndoCount);
        Assert.Equal(candidate!.Value.X, t.Content.Resolved.Entities[DocumentData.Parent].Components[DocumentData.Transform].Properties["x"].Value.GetNumber());
    }

    [Fact]
    public void ThrowingInvalidation_StillClearsGestureAndCaptureBeforeCleanupCallbacks()
    {
        using var t = new ViewportInputFixture(); t.Start(); var p = Begin(t); var original = t.Content;
        void Failure(Invalidation _) => throw new IOException("invalidation");
        t.View.Canvas.Invalidated += Failure;
        try { Assert.Throws<AggregateException>(() => t.Input(InputKind.PointerMove, p.X + 30, p.Y)); }
        finally { t.View.Canvas.Invalidated -= Failure; }
        Assert.False(t.Controller.IsTranslating); Assert.Null(t.Controller.TranslationCandidate);
        Assert.False(t.Input(InputKind.PointerMove, p.X + 30, p.Y).PointerCaptured);
        Assert.Same(original.Document, t.Content.Document); Assert.Equal(0, t.Content.UndoCount);
    }

    [Fact]
    public void ThrowingReleaseCallback_DoesNotCommitOrRetainGesture()
    {
        using var t = new ViewportInputFixture(); t.Start(); var p = Handle(t); var original = t.Content;
        var context = new CallbackContext(t.Ui, () => throw new IOException("release"));
        t.View.Canvas.HandleInput(new(InputKind.PointerDown, p.X, p.Y), context);
        t.View.Canvas.HandleInput(new(InputKind.PointerMove, p.X + 30, p.Y), context);
        Assert.Throws<IOException>(() => t.View.Canvas.HandleInput(new(InputKind.PointerUp, p.X + 30, p.Y), context));
        Assert.Equal(1, context.Releases); Assert.False(t.Controller.IsTranslating);
        Assert.Same(original.Document, t.Content.Document); Assert.Equal(0, t.Content.UndoCount);
        Assert.False(t.Input(InputKind.PointerMove, p.X + 30, p.Y).PointerCaptured);
    }

    [Fact]
    public void MissingActualReceipt_ObservingPresentationSettlesWithoutContinuousIdleInvalidation()
    {
        using var t = new ViewportInputFixture();
        t.Controller.TranslationPresentationChanged(); t.Ui.Update();
        t.Controller.TranslationPresentationChanged(); Assert.False(t.Ui.Update());
        Assert.False(t.Controller.IsTranslating); Assert.Equal(0, t.Content.UndoCount);
    }

    [Fact]
    public void Drag_UsesOutlineWithoutMutatingDocumentThenReleaseCommitsOnePositionEditWithUndoRedo()
    {
        using var t = new ViewportInputFixture(); t.Start(); var original = t.Content;
        var surface = t.Frames.Presentation; var camera = t.State.Camera; var nodes = t.View.Canvas;
        var p = Begin(t); t.Input(InputKind.PointerMove, p.X + 30, p.Y);
        Assert.True(t.Controller.IsTranslating); Assert.Same(original.Document, t.Content.Document);
        Assert.Equal(0, t.Content.UndoCount); Assert.False(t.Owner.Document.Read().IsDirty);
        var position = t.Controller.TranslationCandidate!.Value;
        Assert.True(position.X > -1.2); Assert.Equal(1, position.Y); Assert.Equal(0, position.Z);
        var drawing = new Drawing(); t.View.Canvas.Draw(drawing);
        Assert.Contains(drawing.Outlines, item => item.Color == Theme.Dark.Modified);
        Assert.Contains(drawing.Labels, text => text.Contains("Перемещение"));
        Assert.Same(surface, t.Frames.Presentation); Assert.Same(camera, t.State.Camera); Assert.Single(t.Runtime.Leases);
        Assert.False(t.Input(InputKind.PointerUp, p.X + 30, p.Y).PointerCaptured);
        Assert.False(t.Controller.IsGesturing); Assert.Null(t.Controller.TranslationCandidate);
        Assert.Equal(original.Document.Revision + 1, t.Content.Document.Revision); Assert.Equal(1, t.Content.UndoCount);
        var transform = t.Content.Resolved.Entities[DocumentData.Parent].Components[DocumentData.Transform];
        Assert.Equal(position.X, transform.Properties["x"].Value.GetNumber());
        Assert.Equal(1, transform.Properties["y"].Value.GetNumber()); Assert.Equal(0, transform.Properties["yaw"].Value.GetNumber());
        var overrides = t.Content.Document.Entities.Single(e => e.Id == DocumentData.Parent).Changes.PropertyOverrides;
        Assert.Equal(new[] { "x", "y" }, overrides.Select(e => e.PropertyId));
        t.Inspector.Undo(); Assert.Equal(-1.2, t.Content.Resolved.Entities[DocumentData.Parent].Components[DocumentData.Transform].Properties["x"].Value.GetNumber());
        Assert.Equal(1, t.Content.RedoCount); t.Inspector.Redo();
        Assert.Equal(position.X, t.Content.Resolved.Entities[DocumentData.Parent].Components[DocumentData.Transform].Properties["x"].Value.GetNumber());
        Assert.Same(nodes, t.View.Canvas); Assert.Equal(DocumentData.Parent.Value.ToString("D"), t.Workspace.SelectedId);
    }

    [Fact]
    public void ExactNoOpAndReturnToStart_PreserveDocumentAndExistingRedo()
    {
        using var t = new ViewportInputFixture();
        t.Owner.Document.Apply(DocumentData.Edit(t.Content.Document.Revision, 3));
        t.Owner.Document.Undo(DocumentData.Operation(t.Content.Document.Revision)); t.SyncSource(); t.Inspector.Refresh(t.Workspace.SelectedId);
        t.Start(); var original = t.Content; Assert.Equal(1, original.RedoCount);
        var p = Begin(t); t.Input(InputKind.PointerUp, p.X, p.Y);
        Assert.Same(original.Document, t.Content.Document); Assert.Equal(1, t.Content.RedoCount);
        p = Begin(t); t.Input(InputKind.PointerMove, p.X + 30, p.Y); t.Input(InputKind.PointerMove, p.X, p.Y);
        t.Input(InputKind.PointerUp, p.X, p.Y);
        Assert.Same(original.Document, t.Content.Document); Assert.Equal(original.UndoCount, t.Content.UndoCount);
        Assert.Equal(1, t.Content.RedoCount); Assert.False(t.Controller.IsTranslating);
    }

    [Theory]
    [InlineData("escape")] [InlineData("window-focus")] [InlineData("element-focus")] [InlineData("foreign-capture")]
    [InlineData("popup")] [InlineData("hide")] [InlineData("disable")] [InlineData("resize")]
    [InlineData("dpi")] [InlineData("selection-same")] [InlineData("selection-other")] [InlineData("selection-clear")]
    [InlineData("opening")] [InlineData("camera")] [InlineData("draft")] [InlineData("stop")] [InlineData("dispose")] [InlineData("detach")]
    public void Cancellation_DropsCandidateWithoutChangingHistoryOrRestoringAnOldCamera(string reason)
    {
        using var t = new ViewportInputFixture(); t.Start(); var original = t.Content; var p = Begin(t);
        t.Input(InputKind.PointerMove, p.X + 30, p.Y); var camera = t.State.Camera;
        Element? detached = null;
        switch (reason)
        {
            case "escape": t.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); break;
            case "window-focus": t.Ui.HandleInput(new(InputKind.FocusLost)); break;
            case "element-focus": t.Ui.Focus(t.View.Entities.Values.First()); break;
            case "foreign-capture": var other = t.View.Entities.Values.First(); t.Ui.Capture(other); t.Ui.ReleaseCapture(other); break;
            case "popup": t.View.Overlays.Open(t.View.Canvas, new Button("popup"), new(400, 200, 10, 10), t.Ui); break;
            case "hide": t.View.IsVisible = false; break;
            case "disable": t.View.IsEnabled = false; break;
            case "resize": t.Ui.Resize(new(900, 700)); break;
            case "dpi": t.Ui.Resize(t.Ui.Viewport with { Scale = 1.5f }); break;
            case "selection-same": t.Workspace.Select(t.Workspace.SelectedId!); break;
            case "selection-other": t.Model.Select(DocumentData.Child.Value.ToString("D")); break;
            case "selection-clear": t.Model.ClearSelection(); break;
            case "opening": t.Workspace.SetDocument(t.Content, Guid.NewGuid()); break;
            case "camera": Assert.True(EditorCameraNavigation.TryDolly(camera, 1, out var moved)); Assert.True(t.State.Queue(t.Controller.ViewId, t.Preview.DesiredView!.Context, moved)); camera = moved; break;
            case "draft": t.Inspector.Fields.Single(f => f.Key == "x").Value = "-"; break;
            case "stop": DocumentsUi.DocumentUiFixture.Wait(t.Coordinator.StopAsync()); break;
            case "dispose": t.Controller.Dispose(); break;
            case "detach": detached = t.View.ViewportFrame.Child; t.View.ViewportFrame.SetContent(null); break;
        }
        t.Ui.Update();
        Assert.False(t.Controller.IsTranslating); Assert.Null(t.Controller.TranslationCandidate);
        Assert.Same(original.Document, t.Content.Document); Assert.Equal(original.UndoCount, t.Content.UndoCount);
        if (reason != "opening") Assert.Same(camera, t.State.Camera);
        if (reason == "draft") Assert.Equal("-", t.Inspector.Fields.Single(f => f.Key == "x").Value);
        Assert.False(t.Input(InputKind.PointerUp, p.X + 30, p.Y).PointerCaptured);
        Assert.Same(original.Document, t.Content.Document);
        detached?.Dispose();
    }

    [Theory]
    [InlineData("")] [InlineData("-")] [InlineData("invalid")] [InlineData("2.5")]
    public void ExistingInspectorDraft_BlocksTranslationAndPreservesExactInput(string draft)
    {
        using var t = new ViewportInputFixture(); t.Start(); var p = Handle(t); var original = t.Content;
        var field = t.Inspector.Fields.Single(f => f.Key == "x"); field.Value = draft;
        t.Input(InputKind.PointerDown, p.X, p.Y);
        Assert.False(t.Controller.IsTranslating); t.Input(InputKind.PointerMove, p.X + 30, p.Y); t.Input(InputKind.PointerUp, p.X + 30, p.Y);
        Assert.Equal(draft, field.Value); Assert.True(t.Inspector.HasDraft);
        Assert.Same(original.Document, t.Content.Document); Assert.Equal(0, t.Content.UndoCount);
    }

    [Fact]
    public void NewFrameOfSameView_DoesNotInvalidateFrozenDragButSourceChangeDoes()
    {
        using var t = new ViewportInputFixture(); t.Start(); var p = Begin(t); var receipt = t.Preview.PresentedView!;
        t.Present(); Assert.NotEqual(receipt.Frame.Metadata.FrameId, t.Preview.PresentedView!.Frame.Metadata.FrameId);
        t.Input(InputKind.PointerMove, p.X + 30, p.Y); Assert.True(t.Controller.IsTranslating);
        Assert.True(t.Owner.Document.Apply(DocumentData.Edit(t.Content.Document.Revision, 3)).Succeeded); t.SyncSource(); var changed = t.Content;
        Assert.False(t.Controller.IsTranslating); t.Input(InputKind.PointerUp, p.X + 30, p.Y);
        Assert.Same(changed.Document, t.Content.Document); Assert.Equal(1, t.Content.UndoCount);
    }

    [Fact]
    public void InvalidRelease_DiscardsLastValidCandidateAndSecondaryButtonDoesNotComplete()
    {
        using var t = new ViewportInputFixture(); t.Start(); var original = t.Content; var p = Begin(t);
        t.Input(InputKind.PointerMove, p.X + 30, p.Y);
        Assert.True(t.Input(InputKind.PointerUp, p.X + 30, p.Y, 1).PointerCaptured); Assert.True(t.Controller.IsTranslating);
        Assert.False(t.Input(InputKind.PointerUp, float.NaN, p.Y).PointerCaptured);
        Assert.False(t.Controller.IsTranslating); Assert.Same(original.Document, t.Content.Document); Assert.Equal(0, t.Content.UndoCount);
    }

    [Fact]
    public void ReleaseCallbackWithNewSelectionIntent_PreventsLateCommitAfterCaptureIsReleased()
    {
        using var t = new ViewportInputFixture(); t.Start(); var p = Handle(t); var original = t.Content;
        var context = new CallbackContext(t.Ui, () => t.Workspace.Select(t.Workspace.SelectedId!));
        Assert.True(t.View.Canvas.HandleInput(new(InputKind.PointerDown, p.X, p.Y), context));
        Assert.True(t.Controller.IsTranslating);
        t.View.Canvas.HandleInput(new(InputKind.PointerMove, p.X + 30, p.Y), context);
        t.View.Canvas.HandleInput(new(InputKind.PointerUp, p.X + 30, p.Y), context);
        Assert.Equal(1, context.Releases); Assert.False(t.Controller.IsTranslating);
        Assert.Same(original.Document, t.Content.Document); Assert.Equal(0, t.Content.UndoCount);
        Assert.False(t.Input(InputKind.PointerMove, p.X + 30, p.Y).PointerCaptured);
    }

    private static (float X, float Y) Begin(ViewportInputFixture t)
    {
        var p = Handle(t); var result = t.Input(InputKind.PointerDown, p.X, p.Y);
        Assert.True(result.PointerCaptured, $"No capture: point={p}, canvas={t.View.Canvas.Bounds}, selected={t.Workspace.SelectedId}, ready={t.View.Canvas.CanAcknowledgeDraw}, edit={t.Inspector.CanEdit}, draft={t.Inspector.HasDraft}");
        Assert.True(t.Controller.IsTranslating, $"Not translation: point={p}, selected={t.Workspace.SelectedId}, canTranslate={t.Model.CanTranslateViewport}, gesture={t.Controller.IsGesturing}"); return p;
    }
    private static (float X, float Y) Handle(ViewportInputFixture t)
    {
        // Production EditorSession refreshes the inspector before admitting viewport editing.
        t.Inspector.Refresh(t.Workspace.SelectedId);
        var receipt = t.Preview.PresentedView!; var primitive = receipt.Picks.Primitives.Single(p => p.Entity == DocumentData.Parent);
        Assert.True(TranslationGeometry.TryCreate(receipt.Projection, receipt.ImageBounds, primitive.World.Translation, out var geometry));
        var handle = geometry!.Handles.Single(h => h.Kind == TranslationHandle.X);
        return ((float)((handle.Points[0].X + handle.Points[1].X) / 2), (float)((handle.Points[0].Y + handle.Points[1].Y) / 2));
    }
    private sealed class CallbackContext(UiSession ui, Action callback) : IInputContext
    {
        public int Releases;
        public void Focus(Element element) => ui.Focus(element);
        public void Capture(Element element) => ui.Capture(element);
        public void ReleaseCapture(Element element) { ui.ReleaseCapture(element); Releases++; callback(); }
        public string? GetClipboardText() => null;
        public void SetClipboardText(string text) { }
    }
    private sealed class Drawing : IDrawingContext
    {
        public readonly List<(Rect Bounds, Color Color)> Outlines = [];
        public readonly List<string> Labels = [];
        public void Save() { } public void Restore() { } public void Clip(Rect rectangle) { } public void Translate(float x, float y) { }
        public void Fill(Rect rectangle, Color color, float radius = 0) { }
        public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) => Outlines.Add((rectangle, color));
        public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) => Labels.Add(text);
        public void Image(IImageSource source, Rect destination) { }
    }
}
