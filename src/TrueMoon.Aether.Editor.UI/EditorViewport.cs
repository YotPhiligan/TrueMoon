using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Aether.Editor.UI.Translation;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.UI;

public sealed class EditorViewport : Element, IUiFrameParticipant
{
    private readonly DrawReceipts _receipts = new();
    private IEditorViewportController? _controller;
    private UiViewport _observedViewport;
    private ulong _viewportEpoch;
    internal void AttachController(IEditorViewportController controller)
    { if(_controller!=null)throw new InvalidOperationException("Viewport already has a controller.");_controller=controller; }
    internal void DetachController(IEditorViewportController controller) {if(ReferenceEquals(_controller,controller))_controller=null;}
    internal void ObserveViewport(UiViewport viewport)
    { if(viewport==_observedViewport)return;_observedViewport=viewport;InvalidatePresentedGeometry(); }
    internal void InvalidatePresentedGeometry()
    { _viewportEpoch=checked(_viewportEpoch+1); _receipts.Supersede(); Invalidate(); }
    /// <inheritdoc />
    public override bool Focusable=>true;
    /// <inheritdoc />
    public override bool HandleInput(UiInput input,IInputContext context)=>_controller?.HandleInput(input,context)==true;
    /// <inheritdoc />
    public override void OnInputCancelled()=>_controller?.CancelInput();
    /// <inheritdoc />
    public bool NeedsFrame=>_controller?.NeedsFrame==true;
    /// <inheritdoc />
    public void AdvanceFrame(TimeSpan elapsed,IInputContext context)
    {if(context is UiSession ui)_controller?.Observe(ui);}
    private SkiaImageSource? _image;
    private ViewportFrame? _frame;
    private Func<bool>? _current;
    private string _emptyText = "Соберите проект и запустите сцену";
    /// <summary>Gets/sets the empty viewport explanation without changing frame ownership.</summary>
    public string EmptyText
    {
        get => _emptyText;
        set { VerifyAccess(); ArgumentNullException.ThrowIfNull(value); if (_emptyText != value) { _emptyText = value; Invalidate(); } }
    }
    public EditorViewport() => Own(new ImageLifetime(this));
    /// <summary>Ticket recorded by the last successful viewport paint, not a host presentation.</summary>
    public ViewportFrame? DrawnFrame => _receipts.Frame;
    /// <summary>Current letterboxed destination, prepared on layout and upload even before paint.</summary>
    public Rect ImageBounds { get; private set; }
    /// <summary>Gets the effective ancestor clip retained with the last draw.</summary>
    public Rect DrawnClipBounds => _receipts.Geometry.Clip;
    /// <summary>Gets a monotonic draw-geometry revision, independent of FrameId.</summary>
    public ulong LayoutRevision => _receipts.LayoutRevision;
    /// <summary>Whether the retained draw still describes visible current geometry and its exact uploaded ticket.</summary>
    public bool CanAcknowledgeDraw => CurrentDraw is not null;
    internal DrawReceipt? CurrentDraw => _receipts.Frame is {} frame && ReferenceEquals(frame, _frame) &&
        _receipts.IsCurrent && HasCurrentDrawGeometry() ? new(frame, _receipts.Geometry.Image, DrawnClipBounds, LayoutRevision) : null;
    internal readonly record struct DrawReceipt(ViewportFrame Frame, Rect Image, Rect Clip, ulong Revision);
    /// <summary>Checks frozen presented geometry without requiring the latest received/uploaded FrameId.</summary>
    /// <param name="image">Receipt image bounds.</param><param name="clip">Receipt effective clip.</param><param name="revision">Receipt layout revision.</param>
    /// <returns>False after layout, visibility, clip or surface invalidation; a newer incoming frame alone is allowed.</returns>
    public bool MatchesPresentedLayout(ViewportImageBounds image, ViewportImageBounds clip, ulong revision)
        => HasCurrentDrawGeometry() && revision == LayoutRevision &&
            image == new ViewportImageBounds(ImageBounds.X,ImageBounds.Y,ImageBounds.Width,ImageBounds.Height) &&
            clip == new ViewportImageBounds(DrawnClipBounds.X,DrawnClipBounds.Y,DrawnClipBounds.Width,DrawnClipBounds.Height);
    private bool HasCurrentDrawGeometry()
    {
        if (IsDisposed || DrawnFrame == null || _current?.Invoke() != true || _receipts.Geometry != Geometry()) return false;
        for (Element? element = this; element != null; element = element.Parent)
            if (!element.IsVisible || element.Opacity <= 0) return false;
        return true;
    }
    public void Upload(SkiaImageSource image, ViewportFrame frame, Func<bool> current)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(image); ArgumentNullException.ThrowIfNull(frame); ArgumentNullException.ThrowIfNull(current);
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        var previous = _image; _receipts.Supersede(); _image = image; _frame = frame; _current = current;
        PrepareImageBounds(); previous?.Dispose(); Invalidate();
    }
    protected override Size MeasureCore(Size available, ITextLayoutService text) => new(600, 420);
    protected override void ArrangeCore(Rect contentBounds) => PrepareImageBounds();

    private void PrepareImageBounds()
    {
        var bounds = Bounds;
        if (_frame is {} frame)
        {
            var surface = frame.Presentation.Surface;
            var scale = Math.Min(bounds.Width / surface.Width, bounds.Height / surface.Height);
            var width = surface.Width * scale; var height = surface.Height * scale;
            bounds = new(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
        }
        if (ImageBounds == bounds) return;
        ImageBounds = bounds;
        _viewportEpoch = checked(_viewportEpoch + 1);
        _receipts.Supersede();
    }

    private DrawGeometry Geometry() => new(Bounds, ImageBounds, EffectiveClip(), _viewportEpoch);
    internal readonly record struct DrawGeometry(Rect Bounds, Rect Image, Rect Clip, ulong Epoch)
    {
        public bool HasVisibleImage => Math.Max(Image.X, Clip.X) < Math.Min(Image.X + Image.Width, Clip.X + Clip.Width) &&
            Math.Max(Image.Y, Clip.Y) < Math.Min(Image.Y + Image.Height, Clip.Y + Clip.Height);
    }

    // UI-owner mailbox. Neither the ticket nor a layout revision is published for an incomplete or superseded draw.
    private sealed class DrawReceipts
    {
        private ulong _generation, _completed;
        public ViewportFrame? Frame { get; private set; }
        public DrawGeometry Geometry { get; private set; }
        public ulong LayoutRevision { get; private set; }
        public bool IsCurrent => _completed == _generation;
        public ulong Supersede() => _generation = checked(_generation + 1);
        public void Clear(ulong generation) { if (generation == _generation) Frame = null; }
        public void Complete(ulong generation, ViewportFrame frame, DrawGeometry geometry)
        {
            if (generation != _generation) return;
            if (LayoutRevision == 0 || Geometry != geometry) LayoutRevision = checked(LayoutRevision + 1);
            Geometry = geometry; Frame = frame; _completed = generation;
        }
    }

    internal readonly record struct DrawState(DrawGeometry Geometry, SkiaImageSource? Image, ViewportFrame? Frame,
        TranslationOverlay? Overlay, Theme Theme, string EmptyText, float FontSize, string FontFamily, int FontWeight,
        bool FontItalic, ulong Generation);

    internal DrawState BeginDraw()
    {
        var generation = _receipts.Supersede();
        try
        {
            var geometry = Geometry();
            var frame = _frame;
            var image = _image;
            if (_current?.Invoke() != true) frame = null;
            var overlay = frame != null && geometry.HasVisibleImage ? _controller?.CaptureTranslationOverlay(frame) : null;
            return new(geometry, image, frame, overlay, Theme, EmptyText, FontSize, FontFamily, FontWeight, FontItalic, generation);
        }
        finally { _receipts.Clear(generation); }
    }

    internal void CompleteDraw(in DrawState state, ViewportFrame frame) =>
        _receipts.Complete(state.Generation, frame, state.Geometry);

    private Rect EffectiveClip()
    {
        var clip = Bounds;
        for (var parent = Parent; parent != null; parent = parent.Parent)
        {
            var bounds = parent.ChildClipBounds;
            var left = Math.Max(clip.X, bounds.X); var top = Math.Max(clip.Y, bounds.Y);
            clip = new(left, top, Math.Max(0, Math.Min(clip.X + clip.Width, bounds.X + bounds.Width) - left),
                Math.Max(0, Math.Min(clip.Y + clip.Height, bounds.Y + bounds.Height) - top));
        }
        return clip;
    }
    private sealed class ImageLifetime(EditorViewport owner) : IDisposable
    { public void Dispose() { owner._receipts.Clear(owner._receipts.Supersede()); owner._image?.Dispose(); owner._image = null; owner._frame = null; owner._current = null; } }
}
