using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using TrueMoon.Aether.Abstractions.Sessions;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Player;

/// <summary>Plain optional HUD data; no renderer/control/native references.</summary>
public sealed record PlayerViewModel(string Title);

/// <summary>One independently retained image and exact ticket. The parent tree owns this control.</summary>
public sealed class PlayerCanvas(PlayerInput input):Element
{
    private static readonly ElementPresentation Paint = ElementPresentation.Create<PlayerCanvas, PaintState>(
        static canvas => canvas.Capture(), new Factory<IElementPresenter<PaintState>>(static () => new Painter()));
    private readonly DrawReceipts _receipts = new();
    private Rect _imageBounds;
    /// <inheritdoc />
    public override ElementPresentation Presentation => Paint;
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

    private readonly record struct PaintState(SkiaImageSource? Image, ViewportFrame? Frame, Rect Bounds, Rect VisibleImage,
        GameHudSnapshot? Hud, Color Card, Color Foreground, float Radius, float FontSize, string Family, int Weight,
        bool Italic, DrawReceipts Receipts, ulong Generation);

    private PaintState Capture()
    {
        var generation = _receipts.Reset();
        var image = _image; var frame = _frame;
        return new(image, _current?.Invoke() == true ? frame : null, _imageBounds, VisibleImage(), _hud,
            Theme.Control, Foreground, CornerRadius, FontSize, FontFamily, FontWeight, FontItalic, _receipts, generation);
    }

    private sealed class Painter : IElementPresenter<PaintState>
    {
        public void Draw(PaintState state, IDrawingContext context)
        {
            if (state.Frame is not {} frame || state.Image is not {} image ||
                state.VisibleImage.Width <= 0 || state.VisibleImage.Height <= 0) return;
            context.Image(image, state.Bounds);
            if (state.Hud is {} hud) DrawHud(context, state, hud);
            state.Receipts.Complete(state.Generation, frame, state.VisibleImage);
        }

        // The HUD shares the viewport image's letterboxed destination and never becomes an input target.
        private static void DrawHud(IDrawingContext context, PaintState state, GameHudSnapshot hud)
        {
            const float margin = 8;
            var viewport = state.Bounds;
            var line = state.FontSize + 6;
            var width = Math.Min(400, viewport.Width - 2 * margin);
            var height = viewport.Height - 2 * margin;
            if (width < 2 * margin || height < line + 2 * margin) return;

            var prompt = !string.IsNullOrWhiteSpace(hud.Prompt);
            var status = !string.IsNullOrWhiteSpace(hud.Status);
            var availableRows = Math.Min((int)((height - 2 * margin) / line), GameHudSnapshot.JournalCapacity + 2);
            var rows = Math.Max(0, availableRows - (prompt ? 1 : 0) - (status ? 1 : 0));
            var journalRows = Math.Min(rows, hud.Journal.Length);
            var lineCount = Math.Min(availableRows, (prompt ? 1 : 0) + (status ? 1 : 0) + journalRows);
            if (lineCount == 0) return;

            var card = new Rect(viewport.X + margin, viewport.Y + margin, width, lineCount * line + 2 * margin);
            context.Fill(card, state.Card, state.Radius);
            context.Save();
            try
            {
                context.Clip(card.Deflate(new Thickness(margin)));
                var y = card.Y + margin;
                void Show(string text)
                {
                    context.Text(text, card.X + margin, y, state.Foreground, state.FontSize, state.Family, state.Weight, state.Italic);
                    y += line;
                }
                if (prompt) Show(hud.Prompt!);
                if (status && (!prompt || availableRows >= 2)) Show(hud.Status!);
                for (var index = hud.Journal.Length - journalRows; index < hud.Journal.Length; index++) Show(hud.Journal[index]);
            }
            finally { context.Restore(); }
        }
    }
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

/// <summary>Minimal typed Player view; canvas composition is independent from Editor/Tooling.</summary>
public sealed class PlayerView:View<PlayerViewModel>,IUiInputObserver
{
    private readonly PlayerInput _input;
    /// <summary>Owned viewport control.</summary>
    public PlayerCanvas Canvas{get;}
    /// <summary>Creates UI on the owner thread, borrowing data/input/style services.</summary>
    /// <param name="data">Plain data.</param><param name="input">Borrowed input state.</param><param name="style">View style.</param>
    public PlayerView(PlayerViewModel data,PlayerInput input,IViewStyle<PlayerView> style)
    {
        _input = input;
        DataContext = data;
        Canvas = new(input);
        SetContent(Canvas);
        this.WithViewStyle(style);
    }
    /// <inheritdoc />
    public bool PreviewInput(UiInput input,IInputContext context)
    {
        if(input.Kind==InputKind.FocusLost||input.Kind==InputKind.KeyDown&&input.Key==UiKey.Escape)
        {_input.Release();context.ReleaseCapture(Canvas);return input.Kind!=InputKind.FocusLost;}
        return false;
    }
}

/// <summary>Default viewport and HUD text style, separately replaceable from view composition.</summary>
public sealed class PlayerViewStyle:IViewStyle<PlayerView>
{
    /// <inheritdoc />
    public void Configure(IViewStyleBuilder<PlayerView> styles)
    {styles.Root().Padding(0).Background(theme=>theme.Surface);styles.Part(view=>view.Canvas).Padding(0).Set(UiProperties.CornerRadius,6f);}
}
