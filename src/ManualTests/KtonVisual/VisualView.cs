using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace TrueMoon.ManualTests.KtonVisual;

internal sealed class VisualData
{
    internal string Status { get; set; }="Paused — press Enter to play";
}

internal sealed class VisualCanvas(Controls controls):Element
{
    private SkiaImageSource? _image;
    private long _version;
    private Size _size;
    internal long Drawn {get;private set;}
    public override bool Focusable=>true;
    internal void Upload(SkiaImageSource image,long version)
    {
        VerifyAccess();_size=image.Size;var old=_image;_image=image;_version=version;Drawn=0;old?.Dispose();Invalidate();
    }
    internal Rect DrawBounds
    {
        get{var scale=Math.Min(Bounds.Width/_size.Width,Bounds.Height/_size.Height);return new(Bounds.X,Bounds.Y,_size.Width*scale,_size.Height*scale);}
    }
    internal void Clear(){VerifyAccess();_image?.Dispose();_image=null;Drawn=0;Invalidate();}
    protected override Size MeasureCore(Size available,ITextLayoutService text)=>new(Math.Min(960,available.Width),540);
    protected override void DrawCore(IDrawingContext context){if(_image!=null){context.Image(_image,DrawBounds);Drawn=_version;}}
    public override bool HandleInput(UiInput input,IInputContext context)
    {
        if(input.Kind==InputKind.PointerDown){context.Focus(this);return true;}
        return input.Kind switch{InputKind.KeyDown=>controls.Key(input.Key,true),InputKind.KeyUp=>controls.Key(input.Key,false),_=>false};
    }
    public override void OnInputCancelled()=>controls.Release();
    public override void Dispose(){VerifyAccess();_image?.Dispose();_image=null;base.Dispose();}
}

internal sealed class VisualView:View<VisualData>
{
    internal VisualCanvas Canvas {get;}
    internal Text Status {get;}=Text.Plain();
    internal VStack Stack {get;}
    private readonly VisualData _data;
    public VisualView(VisualData data,Controls controls,IViewStyle<VisualView> style)
    {
        _data=data;Canvas=new(controls);
        Stack=VStack.Create().WithChildren(Text.Plain("Kton physics playground — slope, platform, contacts"),
            Text.Plain("Click image | WASD move | Space jump | Enter play/pause | F step | Home reset | End stop"),Status,Canvas);
        Content(data,_=>Stack);this.WithViewStyle(style);Refresh();
    }
    internal void Refresh()=>Status.Value=_data.Status;
}
internal sealed class VisualStyle:IViewStyle<VisualView>
{
    public void Configure(IViewStyleBuilder<VisualView> style)
    {style.Root().Background(theme=>theme.Surface);style.Part(view=>view.Stack).Padding(12).Spacing(6);}
}
