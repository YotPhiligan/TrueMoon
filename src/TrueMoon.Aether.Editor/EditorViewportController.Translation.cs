using System.Numerics;
using TrueMoon.Aether.Editor.Translation;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

public sealed partial class EditorViewportController
{
    private TranslationTarget? _translationTarget;
    private TranslationDrag? _translationDrag;
    private TranslationPosition _translationPosition;
    private Vector3 _translationDelta;
    private PreviewViewState? _translationObservedView;
    private ulong _translationObservedLayout, _translationObservedSelection;
    private bool _translationObservedEnabled;

    /// <summary>Whether this controller owns a translation gesture; the document is unchanged until release.</summary>
    public bool IsTranslating => _gesture == Gesture.Translate;
    /// <summary>Current candidate in local authoring coordinates, or null outside a translation.</summary>
    public TranslationPosition? TranslationCandidate => IsTranslating ? _translationPosition : null;

    internal void TranslationPresentationChanged()
    {
        if (_disposed || !IsActive) return;
        var receipt = Ready(); var enabled = _model.CanTranslateViewport;
        if (_translationObservedView == receipt?.State && _translationObservedLayout == (receipt?.LayoutRevision ?? 0) &&
            _translationObservedSelection == _workspace.SelectionRevision && _translationObservedEnabled == enabled) return;
        _translationObservedView = receipt?.State; _translationObservedLayout = receipt?.LayoutRevision ?? 0;
        _translationObservedSelection = _workspace.SelectionRevision; _translationObservedEnabled = enabled;
        _view.Canvas.Invalidate();
    }

    private bool CaptureTranslation(PreviewPresentedView receipt, out TranslationTarget? target, out TranslationGeometry? geometry)
    {
        target = null; geometry = null;
        return _model.CanTranslateViewport && _workspace.Document is { } document &&
            Guid.TryParse(_workspace.SelectedId, out var entity) &&
            TranslationTarget.TryCapture(document, receipt.Picks, new(entity), out target) &&
            TranslationGeometry.TryCreate(receipt.Projection, receipt.ImageBounds, target!.World.Translation, out geometry);
    }

    private bool BeginTranslation(PreviewPresentedView receipt, UiInput input, IInputContext context)
    {
        if (!CaptureTranslation(receipt, out var target, out var geometry)) return false;
        var handle = geometry!.Hit(input.X, input.Y);
        if (handle == TranslationHandle.None || !TranslationDrag.TryCreate(geometry, handle, input.X, input.Y, out var drag)) return false;
        var selection = _workspace.SelectionRevision;
        context.Focus(_view.Canvas);
        if (Ready() is not { } current || current.State != receipt.State || current.LayoutRevision != receipt.LayoutRevision ||
            selection != _workspace.SelectionRevision || !_model.CanTranslateViewport) return true;
        _translationTarget = target; _translationDrag = drag; _translationPosition = target!.Original; _translationDelta = default;
        _gesture = Gesture.Translate; _start = receipt; _viewport = _ui!.Viewport; _selection = selection; _capture = context;
        try { context.Capture(_view.Canvas); _view.Canvas.Invalidate(); }
        catch { End(false); throw; }
        return true;
    }

    private bool TranslationCurrent(PreviewPresentedView receipt) => _translationTarget != null &&
        _workspace.SelectionRevision == _selection && _workspace.SelectedId == _translationTarget.Entity.Value.ToString("D") &&
        ReferenceEquals(_workspace.Document?.Document, _translationTarget.Document) && _model.CanTranslateViewport && _preview.IsCurrent(receipt);

    private bool HandleTranslation(UiInput input)
    {
        try { return HandleTranslationCore(input); }
        catch (Exception original)
        {
            try { End(false); }
            catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
    }
    private bool HandleTranslationCore(UiInput input)
    {
        if (input.Kind == InputKind.KeyDown && input.Key == UiKey.Escape) { End(false); return true; }
        if (input.Kind == InputKind.PointerMove) { MoveTranslation(input.X, input.Y); return true; }
        if (input.Kind == InputKind.PointerUp && input.Button == 0)
        {
            var valid = MoveTranslation(input.X, input.Y);
            var target = _translationTarget!; var position = _translationPosition; var receipt = _start!;
            var selection = _selection; var viewport = _viewport;
            End(false); // Release before callbacks, then repeat every semantic admission fence.
            if (valid && _ui?.Viewport == viewport && _workspace.SelectionRevision == selection &&
                ReferenceEquals(_workspace.Document?.Document, target.Document) && _model.CanTranslateViewport &&
                Ready() is { } current && current.State == receipt.State && current.LayoutRevision == receipt.LayoutRevision)
                _model.CommitViewportTranslation(target, position);
            return true;
        }
        return input.Kind == InputKind.Wheel;
    }

    private bool MoveTranslation(float x, float y)
    {
        if (_translationDrag == null || _translationTarget == null || !_translationDrag.TryMove(x, y, out var delta) ||
            !_translationTarget.TryPosition(delta, out var position)) return false;
        if (position != _translationPosition)
        { _translationPosition = position; _translationDelta = delta; _view.Canvas.Invalidate(); }
        return true;
    }

    private void ClearTranslation()
    {
        if (_translationTarget == null && _translationDrag == null) return;
        _translationTarget = null; _translationDrag = null; _translationDelta = default;
    }

    internal void DrawTranslationOverlay(IDrawingContext drawing)
    {
        PreviewPresentedView? receipt; TranslationTarget? target; TranslationGeometry? geometry;
        if (IsTranslating)
        {
            receipt = _start; target = _translationTarget; geometry = _translationDrag?.Geometry;
            if (receipt == null || target == null || geometry == null || !GestureCurrent()) return;
        }
        else
        {
            receipt = Ready();
            if (receipt == null || !CaptureTranslation(receipt, out target, out geometry)) return;
        }
        var clip = receipt.ClipBounds; var image = receipt.ImageBounds; var theme = _view.Canvas.Theme;
        drawing.Save();
        try
        {
            drawing.Clip(new((float)clip.Left, (float)clip.Top, (float)clip.Width, (float)clip.Height));
            DrawBounds(drawing, target!.Ghosts, receipt, default, theme.Focus);
            if (IsTranslating) DrawBounds(drawing, target.Ghosts, receipt, _translationDelta, theme.Modified);
            foreach (var handle in geometry!.Handles)
            {
                var color = handle.Kind switch
                {
                    TranslationHandle.X or TranslationHandle.YZ => theme.AxisX,
                    TranslationHandle.Y or TranslationHandle.XZ => theme.AxisY,
                    _ => theme.AxisZ
                };
                if (_translationDrag?.Handle == handle.Kind) color = theme.Focus;
                var points = handle.Points;
                if (points.Length == 2)
                {
                    DrawLine(drawing, points[0], points[1], color);
                    drawing.Fill(new((float)points[1].X - 3, (float)points[1].Y - 3, 6, 6), color);
                    drawing.Text(handle.Kind.ToString(), (float)points[1].X + 5, (float)points[1].Y - 6, color, 12, _view.Canvas.FontFamily, _view.Canvas.FontWeight, _view.Canvas.FontItalic);
                }
                else for (var i = 0; i < points.Length; i++) DrawLine(drawing, points[i], points[(i + 1) % points.Length], color with { A = 180 });
            }
            if (IsTranslating) drawing.Text("Перемещение · отпустите для применения · Esc — отмена",
                (float)Math.Max(image.Left + 8, image.Left + image.Width / 2 - 160),
                (float)Math.Min(image.Top + image.Height, clip.Top + clip.Height) - 24,
                theme.Modified, 12, _view.Canvas.FontFamily, _view.Canvas.FontWeight, _view.Canvas.FontItalic);
        }
        finally { drawing.Restore(); }
    }

    private static void DrawLine(IDrawingContext drawing, TranslationPoint a, TranslationPoint b, Color color)
    {
        var steps = (int)Math.Clamp(Math.Ceiling(Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y))), 1, 192);
        for (var i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            drawing.Fill(new((float)(a.X + (b.X - a.X) * t) - 1, (float)(a.Y + (b.Y - a.Y) * t) - 1, 2, 2), color);
        }
    }

    private static void DrawBounds(IDrawingContext drawing, IEnumerable<PreviewPickPrimitive> primitives, PreviewPresentedView receipt, Vector3 delta, Color color)
    {
        foreach (var primitive in primitives)
        {
            double left = double.PositiveInfinity, top = double.PositiveInfinity, right = double.NegativeInfinity, bottom = double.NegativeInfinity;
            var valid = true;
            for (var i = 0; i < 8; i++)
            {
                var corner = Vector3.Transform(new((i & 1) == 0 ? -.5f : .5f, (i & 2) == 0 ? -.5f : .5f, (i & 4) == 0 ? -.5f : .5f), primitive.World) + delta;
                if (!TranslationGeometry.Project(corner, receipt.Projection, receipt.ImageBounds, out var point)) { valid = false; break; }
                left = Math.Min(left, point.X); right = Math.Max(right, point.X); top = Math.Min(top, point.Y); bottom = Math.Max(bottom, point.Y);
            }
            var bounds = new Rect((float)left, (float)top, (float)(right - left), (float)(bottom - top));
            if (valid && float.IsFinite(bounds.X) && float.IsFinite(bounds.Y) && float.IsFinite(bounds.Width) && float.IsFinite(bounds.Height)) drawing.Stroke(bounds, color, 2);
        }
    }
}
