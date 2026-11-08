using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Rendering;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

public sealed class EditorViewport : Element, IUiFrameParticipant
{
    private EditorViewportController? _controller;
    private UiViewport _observedViewport;
    private ulong _viewportEpoch, _drawnViewportEpoch;
    internal void AttachController(EditorViewportController controller)
    { if(_controller!=null)throw new InvalidOperationException("Viewport already has a controller.");_controller=controller; }
    internal void DetachController(EditorViewportController controller) {if(ReferenceEquals(_controller,controller))_controller=null;}
    internal void ObserveViewport(UiViewport viewport)
    { if(viewport==_observedViewport)return;_observedViewport=viewport;InvalidatePresentedGeometry(); }
    internal void InvalidatePresentedGeometry()
    { _viewportEpoch=checked(_viewportEpoch+1);Invalidate(); }
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
    public ViewportFrame? DrawnFrame { get; private set; }
    public Rect ImageBounds { get; private set; }
    /// <summary>Gets the effective ancestor clip retained with the last draw.</summary>
    public Rect DrawnClipBounds { get; private set; }
    /// <summary>Gets a monotonic draw-geometry revision, independent of FrameId.</summary>
    public ulong LayoutRevision { get; private set; }
    /// <summary>Whether the retained draw still describes visible current geometry and its exact uploaded ticket.</summary>
    public bool CanAcknowledgeDraw
    {
        get
        {
            return HasCurrentDrawGeometry() && ReferenceEquals(DrawnFrame, _frame);
        }
    }
    /// <summary>Checks frozen presented geometry without requiring the latest received/uploaded FrameId.</summary>
    /// <param name="image">Receipt image bounds.</param><param name="clip">Receipt effective clip.</param><param name="revision">Receipt layout revision.</param>
    /// <returns>False after layout, visibility, clip or surface invalidation; a newer incoming frame alone is allowed.</returns>
    public bool MatchesPresentedLayout(ViewportImageBounds image, ViewportImageBounds clip, ulong revision)
        => HasCurrentDrawGeometry() && revision == LayoutRevision &&
            image == new ViewportImageBounds(_drawnImage.X,_drawnImage.Y,_drawnImage.Width,_drawnImage.Height) &&
            clip == new ViewportImageBounds(DrawnClipBounds.X,DrawnClipBounds.Y,DrawnClipBounds.Width,DrawnClipBounds.Height);
    private bool HasCurrentDrawGeometry()
    {
        if (IsDisposed || DrawnFrame == null || _current?.Invoke() != true || Bounds != _drawnBounds || LayoutRevision == 0 ||
            _drawnViewportEpoch!=_viewportEpoch || EffectiveClip() != DrawnClipBounds) return false;
        for (Element? element = this; element != null; element = element.Parent)
            if (!element.IsVisible || element.Opacity <= 0) return false;
        return Bounds.Width > 0 && Bounds.Height > 0;
    }
    public void Upload(SkiaImageSource image, ViewportFrame frame, Func<bool> current)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(image); ArgumentNullException.ThrowIfNull(frame); ArgumentNullException.ThrowIfNull(current);
        var previous = _image; _image = image; _frame = frame; _current = current; previous?.Dispose(); Invalidate();
    }
    protected override Size MeasureCore(Size available, ITextLayoutService text) => new(600, 420);
    protected override void DrawCore(IDrawingContext context)
    {
        context.Fill(Bounds, new(10, 15, 26)); DrawnFrame = null; ImageBounds = Bounds;
        if (_image != null && _frame != null && _current!())
        {
            var surface = _frame.Presentation.Surface; var scale = Math.Min(Bounds.Width / surface.Width, Bounds.Height / surface.Height);
            ImageBounds = new(Bounds.X + (Bounds.Width - surface.Width * scale) / 2, Bounds.Y + (Bounds.Height - surface.Height * scale) / 2, surface.Width * scale, surface.Height * scale);
            var clip = EffectiveClip();
            if (LayoutRevision == 0 || _drawnImage != ImageBounds || DrawnClipBounds != clip || _drawnViewportEpoch!=_viewportEpoch)
            { LayoutRevision = checked(LayoutRevision + 1); _drawnImage = ImageBounds; DrawnClipBounds = clip; }
            _drawnViewportEpoch=_viewportEpoch;
            _drawnBounds = Bounds; context.Image(_image, ImageBounds); DrawnFrame = _frame; return;
        }
        var line = new Color(28, 37, 52);
        for (var x = Bounds.X; x < Bounds.X + Bounds.Width; x += 32) context.Fill(new(x, Bounds.Y, 1, Bounds.Height), line);
        for (var y = Bounds.Y; y < Bounds.Y + Bounds.Height; y += 32) context.Fill(new(Bounds.X, y, Bounds.Width, 1), line);
        context.Text(EmptyText, Bounds.X + 24, Bounds.Y + Math.Max(16, Bounds.Height / 2 - 12), new(142, 154, 170), FontSize, FontFamily, FontWeight, FontItalic);
    }
    private Rect _drawnImage, _drawnBounds;
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
    { public void Dispose() { owner._image?.Dispose(); owner._image = null; owner._frame = null; owner._current = null; owner.DrawnFrame = null; } }
}
