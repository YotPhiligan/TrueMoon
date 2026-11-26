using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Painting;

namespace TrueMoon.Aether.Player;

/// <summary>One independently retained image and exact ticket. The parent tree owns this control.</summary>
public sealed class PlayerCanvas(PlayerInput input):Element
{
    private readonly DrawReceipts _receipts = new();
    private Rect _imageBounds;
    private SkiaImageSource? _image;
    private ViewportFrame? _frame;
    private GameHudSnapshot? _hud;
    private Func<bool>? _current;
    /// <summary>Ticket actually drawn, acknowledged only after the host's subsequent presentation event.</summary>
    public ViewportFrame? Drawn => _receipts.Frame is {} frame && ReferenceEquals(frame, _frame) &&
        _current?.Invoke() == true && VisibleImage() == _receipts.VisibleImage ? frame : null;
    /// <inheritdoc />
    public override bool Focusable=>true;
    internal void Upload(SkiaImageSource image,ViewportFrame frame,GameHudSnapshot? hud,Func<bool> current)
    {
        VerifyAccess(); ObjectDisposedException.ThrowIf(IsDisposed, this);
        _receipts.Reset();
        var old = _image; _image = image; _frame = frame; _hud = hud; _current = current;
        PrepareImageBounds(); old?.Dispose(); Invalidate();
    }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available,ITextLayoutService text)=>available;
    /// <inheritdoc />
    protected override void ArrangeCore(Rect contentBounds) => PrepareImageBounds();

    private void PrepareImageBounds()
    {
        if (_frame == null) return;
        var surface = _frame.Presentation.Surface;
        var scale = Math.Min(Bounds.Width / surface.Width, Bounds.Height / surface.Height);
        var width = surface.Width * scale; var height = surface.Height * scale;
        var rect = new Rect(Bounds.X + (Bounds.Width - width) / 2, Bounds.Y + (Bounds.Height - height) / 2, width, height);
        if (_imageBounds == rect) return;
        _imageBounds = rect; _receipts.Reset();
    }

    private Rect VisibleImage()
    {
        var clip = _imageBounds;
        for (Element? element = this; element != null; element = element.Parent)
        {
            if (element.IsDisposed || !element.IsVisible || element.Opacity <= 0) return default;
            if (element.Parent is not {} parent) break;
            var bounds = parent.ChildClipBounds;
            var left = Math.Max(clip.X, bounds.X); var top = Math.Max(clip.Y, bounds.Y);
            clip = new(left, top, Math.Max(0, Math.Min(clip.X + clip.Width, bounds.X + bounds.Width) - left),
                Math.Max(0, Math.Min(clip.Y + clip.Height, bounds.Y + bounds.Height) - top));
        }
        return clip;
    }

    // A superseded or failed paint cannot publish a ticket for the next host callback.
    private sealed class DrawReceipts
    {
        private ulong _generation;
        public ViewportFrame? Frame { get; private set; }
        public Rect VisibleImage { get; private set; }
        public ulong Reset() { Frame = null; return _generation = checked(_generation + 1); }
        public void Complete(ulong generation, ViewportFrame frame, Rect visibleImage)
        {
            if (generation != _generation) return;
            Frame = frame; VisibleImage = visibleImage;
        }
    }

    internal readonly record struct DrawState(SkiaImageSource? Image, ViewportFrame? Frame, Rect Bounds, Rect VisibleImage,
        GameHudSnapshot? Hud, Color Card, Color Foreground, float Radius, float FontSize, string Family, int Weight,
        bool Italic, ulong Generation);

    internal DrawState BeginDraw()
    {
        var generation = _receipts.Reset();
        var image = _image; var frame = _frame;
        return new(image, _current?.Invoke() == true ? frame : null, _imageBounds, VisibleImage(), _hud,
            Theme.Control, Foreground, CornerRadius, FontSize, FontFamily, FontWeight, FontItalic, generation);
    }

    internal void CompleteDraw(in DrawState state, ViewportFrame frame) =>
        _receipts.Complete(state.Generation, frame, state.VisibleImage);

    /// <inheritdoc />
    public override bool HandleInput(UiInput value,IInputContext context)
    {
        switch(value.Kind)
        {
            case InputKind.PointerDown when value.Button==0:context.Focus(this);context.Capture(this);input.Capture(value.X,value.Y);return true;
            case InputKind.PointerMove when input.IsCaptured:input.Move(value.X,value.Y);return true;
            case InputKind.KeyDown:return input.Key(value.Key,true);
            case InputKind.KeyUp:return input.Key(value.Key,false);
            default:return false;
        }
    }
    /// <inheritdoc />
    public override void OnInputCancelled()=>input.Release();
    /// <inheritdoc />
    public override void Dispose(){VerifyAccess();_receipts.Reset();_image?.Dispose();_image=null;_frame=null;_hud=null;_current=null;base.Dispose();}
}

