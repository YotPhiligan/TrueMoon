using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Player;

/// <summary>Plain optional HUD data; no renderer/control/native references.</summary>
public sealed record PlayerViewModel(string Title);

/// <summary>One independently retained image and exact ticket. The parent tree owns this control.</summary>
public sealed class PlayerCanvas(PlayerInput input):Element
{
    private SkiaImageSource? _image;
    private ViewportFrame? _frame;
    private Func<bool>? _current;
    /// <summary>Ticket actually drawn, acknowledged only after the host's subsequent presentation event.</summary>
    public ViewportFrame? Drawn{get;private set;}
    /// <inheritdoc />
    public override bool Focusable=>true;
    internal void Upload(SkiaImageSource image,ViewportFrame frame,Func<bool> current)
    {VerifyAccess();ObjectDisposedException.ThrowIf(IsDisposed,this);var old=_image;_image=image;_frame=frame;_current=current;Drawn=null;old?.Dispose();Invalidate();}
    /// <inheritdoc />
    protected override Size MeasureCore(Size available,ITextLayoutService text)=>available;
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context)
    {
        Drawn=null;if(_image==null||_frame==null||!_current!())return;
        var surface=_frame.Presentation.Surface;var scale=Math.Min(Bounds.Width/surface.Width,Bounds.Height/surface.Height);
        var bounds=new Rect(Bounds.X+(Bounds.Width-surface.Width*scale)/2,Bounds.Y+(Bounds.Height-surface.Height*scale)/2,surface.Width*scale,surface.Height*scale);
        context.Image(_image,bounds);Drawn=_frame;
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
    public override void Dispose(){VerifyAccess();_image?.Dispose();_image=null;_frame=null;_current=null;Drawn=null;base.Dispose();}
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
    {_input=input;Canvas=new(input);Content(data,_=>Canvas);this.WithViewStyle(style);}
    /// <inheritdoc />
    public bool PreviewInput(UiInput input,IInputContext context)
    {
        if(input.Kind==InputKind.FocusLost||input.Kind==InputKind.KeyDown&&input.Key==UiKey.Escape)
        {_input.Release();context.ReleaseCapture(Canvas);return input.Kind!=InputKind.FocusLost;}
        return false;
    }
}

/// <summary>Default minimal viewport style, separately replaceable from view composition.</summary>
public sealed class PlayerViewStyle:IViewStyle<PlayerView>
{
    /// <inheritdoc />
    public void Configure(IViewStyleBuilder<PlayerView> styles)
    {styles.Root().Padding(0).Background(theme=>theme.Surface);styles.Part(view=>view.Canvas).Padding(0);}
}
