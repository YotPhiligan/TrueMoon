using TrueMoon.Argentis.Input;
using System.Numerics;
using TrueMoon.Aether.Editor.Tooling.Translation;
using TrueMoon.Aether.Editor.UI.Translation;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Preview;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Tooling;

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

    public void TranslationPresentationChanged()
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
        _workspace.ViewportGestureOwner = ViewId;
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

    public TranslationOverlay? CaptureTranslationOverlay(ViewportFrame frame)
    {
        PreviewPresentedView? receipt; TranslationTarget? target; TranslationGeometry? geometry;
        if (IsTranslating)
        {
            receipt = _start; target = _translationTarget; geometry = _translationDrag?.Geometry;
            if (receipt == null || target == null || geometry == null || !GestureCurrent()) return null;
        }
        else
        {
            receipt = Ready();
            if (receipt == null || !CaptureTranslation(receipt, out target, out geometry)) return null;
        }
        if (!ReferenceEquals(frame.Presentation, receipt.Frame.Presentation) || frame.Metadata.PreviewView != receipt.State.Stamp)
            return null;
        return new(geometry!, receipt.ClipBounds, target!.Ghosts, _translationDrag?.Handle ?? TranslationHandle.None, _translationDelta);
    }
}
