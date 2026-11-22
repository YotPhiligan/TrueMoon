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
    private SkiaImageSource? _image;
    private ViewportFrame? _frame;
    private GameHudSnapshot? _hud;
    private Func<bool>? _current;
    /// <summary>Ticket actually drawn, acknowledged only after the host's subsequent presentation event.</summary>
    public ViewportFrame? Drawn{get;private set;}
    /// <inheritdoc />
    public override bool Focusable=>true;
    internal void Upload(SkiaImageSource image,ViewportFrame frame,GameHudSnapshot? hud,Func<bool> current)
    {VerifyAccess();ObjectDisposedException.ThrowIf(IsDisposed,this);var old=_image;_image=image;_frame=frame;_hud=hud;_current=current;Drawn=null;old?.Dispose();Invalidate();}
    /// <inheritdoc />
    protected override Size MeasureCore(Size available,ITextLayoutService text)=>available;
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context)
    {
        Drawn=null;if(_image==null||_frame==null||!_current!())return;
        var surface=_frame.Presentation.Surface;var scale=Math.Min(Bounds.Width/surface.Width,Bounds.Height/surface.Height);
        var bounds=new Rect(Bounds.X+(Bounds.Width-surface.Width*scale)/2,Bounds.Y+(Bounds.Height-surface.Height*scale)/2,surface.Width*scale,surface.Height*scale);
        context.Image(_image,bounds);
        if(_hud is {} hud)DrawHud(context,bounds,hud);
        Drawn=_frame;
    }

    // The HUD paints on the existing viewport. The canvas remains the only input target.
    private void DrawHud(IDrawingContext context,Rect viewport,GameHudSnapshot hud)
    {
        const float margin=8;
        var line=FontSize+6;
        var width=Math.Min(400,viewport.Width-2*margin);
        var height=viewport.Height-2*margin;
        if(width<2*margin||height<line+2*margin)return;

        var prompt=!string.IsNullOrWhiteSpace(hud.Prompt);
        var status=!string.IsNullOrWhiteSpace(hud.Status);
        var availableRows=Math.Min((int)((height-2*margin)/line),GameHudSnapshot.JournalCapacity+2);
        var rows=Math.Max(0,availableRows-(prompt?1:0)-(status?1:0));
        var journalRows=Math.Min(rows,hud.Journal.Length);
        var lineCount=Math.Min(availableRows,(prompt?1:0)+(status?1:0)+journalRows);
        if(lineCount==0)return;

        var card=new Rect(viewport.X+margin,viewport.Y+margin,width,lineCount*line+2*margin);
        context.Fill(card,Theme.Control,CornerRadius);
        context.Save();
        try
        {
            context.Clip(card.Deflate(new Thickness(margin)));
            var y=card.Y+margin;
            void Show(string text)
            {
                context.Text(text,card.X+margin,y,Foreground,FontSize,FontFamily,FontWeight,FontItalic);
                y+=line;
            }
            if(prompt)Show(hud.Prompt!);
            if(status&&(!prompt||availableRows>=2))Show(hud.Status!);
            for(var index=hud.Journal.Length-journalRows;index<hud.Journal.Length;index++)
                Show(hud.Journal[index]);
        }
        finally{context.Restore();}
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
    public override void Dispose(){VerifyAccess();_image?.Dispose();_image=null;_frame=null;_hud=null;_current=null;Drawn=null;base.Dispose();}
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

/// <summary>Default viewport and HUD text style, separately replaceable from view composition.</summary>
public sealed class PlayerViewStyle:IViewStyle<PlayerView>
{
    /// <inheritdoc />
    public void Configure(IViewStyleBuilder<PlayerView> styles)
    {styles.Root().Padding(0).Background(theme=>theme.Surface);styles.Part(view=>view.Canvas).Padding(0).Set(UiProperties.CornerRadius,6f);}
}
