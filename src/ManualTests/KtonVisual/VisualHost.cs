using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Gpu;
using TrueMoon.Aether.Runtime.Gpu.OpenGl;
using TrueMoon.Aether.Runtime.Gpu.Surfaces;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Kton.Bepu.Hosting;

namespace TrueMoon.ManualTests.KtonVisual;

internal sealed record VisualHostArguments(VisualSession Session,FrameSource Source,string Directory,bool Verify);

internal sealed class VisualHost
{
    private readonly HostedUiWindow _window;
    private readonly VisualSession _session;
    private readonly FrameSource _source;
    private readonly GpuResourceHost _gpu;
    private readonly IFactory<GpuLocalSurfaceSession,GpuLocalSurfaceRequest> _surfaces;
    private readonly VisualData _data;
    private readonly Controls _controls;
    private readonly TaskCompletionSource _shown=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private UiSession? _ui;
    private VisualView? _view;
    private GpuOpenGlContext? _context;
    private Action? _current;
    private int _owner,_slot;
    private long _version;
    private TaskCompletionSource? _presented;
    private ViewportFrame? _ticket;
    private ViewportPresentation? _presentation;
    private GpuLocalSurfaceSession? _surface;
    private readonly List<object> _captures=[],_retired=[];
    private readonly string _directory;
    private readonly bool _verify;

    internal VisualHost(HostedUiWindow window,VisualSession session,FrameSource source,GpuResourceHost gpu,
        IFactory<GpuLocalSurfaceSession,GpuLocalSurfaceRequest> surfaces,VisualData data,Controls controls,string directory,bool verify)
    {_window=window;_session=session;_source=source;_gpu=gpu;_surfaces=surfaces;_data=data;_controls=controls;_directory=directory;_verify=verify;}

    internal static async Task<int> Run(string directory,bool verify,bool microsoft)
    {
        await using var app=Program.Build(microsoft,context=>{Program.Configure(context);Program.ConfigureWindow(context);});
        var window=app.Services.Resolve<HostedUiWindow>()!;
        using var session=app.Services.Resolve<IFactory<VisualSession>>()!.Create()!;
        var source=app.Services.Resolve<IFactory<FrameSource,VisualSession>>()!.Create(session)!;
        // Application composition creates this coordinator; it borrows App singletons and owns
        // only the factory-created game/surface products, drained before the window disappears.
        var host=app.Services.Resolve<IFactory<VisualHost,VisualHostArguments>>()!.Create(new(session,source,directory,verify))!;
        var ended=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var end=new CancellationTokenSource(verify?TimeSpan.FromMinutes(3):Timeout.InfiniteTimeSpan);
        async Task<bool> Closing(UiCloseReason _){end.Cancel();await ended.Task;return true;}
        window.FramePresented+=host.OnPresented;window.Closing+=Closing;
        Exception? error=null;
        try
        {
            await app.StartAsync(end.Token);await host._shown.Task.WaitAsync(end.Token);
            await host.Initialize(end.Token);session.Reset();await host.StartSurface(end.Token);
            if(verify)await host.Verify(end.Token);else await host.Play(end.Token);
        }
        catch(OperationCanceledException)when(!verify&&end.IsCancellationRequested){}
        catch(Exception failure){error=failure;}
        finally
        {
            try{await host.StopSurface();session.Stop();}
            catch(Exception cleanup){error=error==null?cleanup:new AggregateException(error,cleanup);}
            ended.TrySetResult();
            window.FramePresented-=host.OnPresented;window.Closing-=Closing;
            try{await app.StopAsync();}catch(Exception cleanup){error=error==null?cleanup:new AggregateException(error,cleanup);}
            var physics=app.Services.Resolve<BepuPhysicsHost>()!;
            var uiSessions=app.Services.Resolve<HostedUiSessionFactory>()!.ActiveSessionCount;
            var clean=host._gpu.ResourceCount==0&&host._gpu.OpenHandleCount==0&&physics.Resources.ActiveResourceOwners==0&&uiSessions==0;
            var report=new{Completed=error==null&&clean,Stage="PHYS8.4",Mode=verify?"Native scripted":"Interactive",Provider=microsoft?"Microsoft":"Cobalt",Assemblies=Program.Assemblies(),
                PhysicsTick=session.Tick,Captures=host._captures,RetiredSurfaces=host._retired,NativeWindow=true,
                VulkanToOpenGl=true,PhysicalKeyboardValidated=false,ColliderGeometry="stepped capsule proxy, exact body centres and rotations",
                GpuOwners=host._gpu.ResourceCount,Handles=host._gpu.OpenHandleCount,UiSessions=uiSessions,Physics=physics.Resources,Error=error?.ToString()};
            await File.WriteAllTextAsync(Path.Combine(directory,"verification.json"),JsonSerializer.Serialize(report,Program.Json));
        }
        if(error!=null)throw error;
        return 0;
    }
    private void OnPresented(UiSession ui)
    {
        _ui=ui;_view=(VisualView)ui.Root;_shown.TrySetResult();
        if(_view.Canvas.Drawn==_version&&_ticket!=null&&_presentation?.Presented(_ticket)==true)_presented?.TrySetResult();
    }
    private Task Owner(Action work)
    {
        if(Environment.CurrentManagedThreadId==_owner){_current!();work();return Task.CompletedTask;}
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _window.PostWindow(_=>{try{_current?.Invoke();work();done.TrySetResult();}catch(Exception error){done.TrySetException(error);}});
        return done.Task;
    }
    private async Task Initialize(CancellationToken token)
    {
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _window.PostWindow(host=>
        {
            try
            {
                _owner=Environment.CurrentManagedThreadId;var gl=host.NativeWindow.GLContext!;_current=gl.MakeCurrent;_current();
                nint Resolve(string name)=>gl.TryGetProcAddress(name,out var address)?address:0;
                _context=new(Resolve,_current,Owner);done.TrySetResult();
            }
            catch(Exception error){done.TrySetException(error);}
        });
        await done.Task.WaitAsync(token);
    }
    private async Task StartSurface(CancellationToken token)
    {
        GpuAdapterId adapter=null!;await Owner(()=>adapter=_context!.ReadAdapter());
        var identity=new SessionIdentity(Guid.NewGuid(),_session.Generation,new('A',64),new('B',64),Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        _surface=_surfaces.Create(new(identity,_source,adapter,_context!,_verify));
        await _surface!.ConfigureAsync(960,540,token);
        _presentation=new(new(identity,_surface.Generation,960,540,ViewportTransportKind.Gpu));_slot=0;
    }
    private async Task StopSurface()
    {
        _presentation?.Close();_presentation=null;_ticket=null;_controls.ResetMotion();
        if(_surface!=null)
        {
            await _surface.DisposeAsync();var diagnostics=_surface.Diagnostics;
            Verification.Check(_surface.IsReleased&&diagnostics.LiveSlots==0&&diagnostics.OpenHandles==0,"Surface retained ownership.");
            if(_verify)Verification.Check(diagnostics.Errors==0&&diagnostics.Warnings==0&&diagnostics.ValidationEnabled,"Vulkan validation failed.");
            _retired.Add(diagnostics);_surface=null;
        }
        if(_view!=null)await Owner(()=>{_data.Status="Stopped — Home starts a fresh session";_view.Refresh();_view.Canvas.Clear();});
    }
    private async Task<string> Capture(string? name,float alpha,CancellationToken token)
    {
        _source.Alpha=alpha;_presented=new(TaskCreationOptions.RunContinuationsAsynchronously);_ticket=null;
        var status=$"{(_session.Playing?"Playing":"Paused")} | tick {_session.Tick} | support {_session.CharacterState.IsSupported} | contacts {_session.LastContacts}";
        await _surface!.ProduceAsync(_slot,_presentation!,image=>
        {
            _data.Status=status;_view!.Refresh();
            var retained=((SkiaOpenGLSurface)_ui!.Rendering).CopyTexture(image.Texture.Name,image.Texture.Width,image.Texture.Height);
            _version++;_ticket=image.Frame;_view.Canvas.Upload(retained,_version);_window.RequestFrame();
        },token);
        _slot=(_slot+1)%3;await _presented.Task.WaitAsync(token);
        if(name==null)return "";
        var hash="";
        await Owner(()=>
        {
            using var full=((SkiaOpenGLSurface)_ui!.Rendering).ReadbackImage();
            var bounds=_view!.Canvas.DrawBounds;var scale=_ui.Viewport.Scale;
            var x=(int)MathF.Round(bounds.X*scale);var y=(int)MathF.Round(bounds.Y*scale);
            using var image=full.Subset(new SKRectI(x,y,Math.Min(full.Width,x+(int)(bounds.Width*scale)),Math.Min(full.Height,y+(int)(bounds.Height*scale))));
            if(image==null)throw new InvalidDataException("Viewport readback is empty.");
            using var png=image.Encode(SKEncodedImageFormat.Png,100);using var file=File.Create(Path.Combine(_directory,name+".png"));png.SaveTo(file);
            using var pixels=image.PeekPixels();hash=Convert.ToHexString(SHA256.HashData(pixels.GetPixelSpan()));
            using var bitmap=SKBitmap.FromImage(image);var colorful=0;
            foreach(var pixel in bitmap.Pixels)if(Math.Max(pixel.Red,Math.Max(pixel.Green,pixel.Blue))-Math.Min(pixel.Red,Math.Min(pixel.Green,pixel.Blue))>30)colorful++;
            Verification.Check(colorful>1000,"GPU image contains no colored scene geometry.");
        });
        _captures.Add(new{Name=name,Tick=_session.Tick,Alpha=alpha,Hash=hash,Presented=true,WorldId=_session.World.Id,SessionGeneration=_session.Generation,SurfaceGeneration=_surface!.Generation,Character=_session.Position(Level.Character),Platform=_session.Position(Level.Platform)});
        return hash;
    }
    private async Task Verify(CancellationToken token)
    {
        await Capture("initial",1,token);
        for(var i=0;i<90;i++)_session.Advance(Vector2.Zero);
        await Capture("floor",1,token);
        _session.SetPlaying(true);
        for(var i=0;i<205;i++)_session.Advance(new(2,0));
        await Capture("slope",1,token);
        _session.Teleport(new(-6,1.05f,5));for(var i=0;i<30;i++)_session.Advance(Vector2.Zero);
        _session.Advance(Vector2.Zero,true);for(var i=0;i<10;i++)_session.Advance(Vector2.Zero);
        Verification.Check(_session.Position(Level.Character).Y is >1.2f and <1.7f,"Ceiling capture did not limit ascent.");
        await Capture("ceiling",1,token);
        _session.Teleport(_session.Position(Level.Platform)+new Vector3(0,1.25f,0));
        for(var i=0;i<90;i++)_session.Advance(Vector2.Zero);
        var tick=_session.Tick;var before=Verification.WorldHash(_session);
        var previous=await Capture("interpolation-0",0,token);
        var midpoint=await Capture("interpolation-half",.5f,token);
        var current=await Capture("interpolation-1",1,token);
        Verification.Check(_session.Tick==tick&&before==Verification.WorldHash(_session),"GPU rendering mutated the physical World.");
        Verification.Check(previous!=current&&midpoint!=previous&&midpoint!=current,"Fractional presentation did not move the platform.");
        await RoutedCommand(UiKey.Enter,token);
        var paused=await Capture("paused-a",0,token);await Task.Delay(100,token);
        Verification.Check(paused==await Capture("paused-b",.8f,token)&&_session.Tick==tick,"Pause changed pixels or tick.");
        await RoutedCommand(UiKey.F,token);await Capture("single-step",1,token);
        Verification.Check(_session.Tick==tick+1,"Step advanced multiple ticks.");
        _session.SetPlaying(true);_session.Advance(Vector2.Zero,true);for(var i=0;i<10;i++)_session.Advance(Vector2.Zero);
        Verification.Check(!_session.CharacterState.IsSupported,"Jump capture is not airborne.");
        await Capture("jump",1,token);
        var oldTicket=_ticket!;var oldPresentation=_presentation!;var oldWorld=_session.World;
        await RoutedCommand(UiKey.End,token);Verification.Check(_gpu.ResourceCount==0&&_session.Stopped,"Stop retained GPU/game resources.");
        Verification.Check(oldWorld.IsDisposed&&!oldPresentation.Presented(oldTicket),"Stop accepted a stale presentation or retained its World.");
        await RoutedCommand(UiKey.Home,token);Verification.Check(_session.World.Id!=oldWorld.Id,"Reset reused a retired World identity.");await Capture("reset",1,token);
    }
    private async Task RoutedCommand(UiKey key,CancellationToken token)
    {
        await Owner(()=>
        {
            var bounds=_view!.Canvas.Bounds;
            _ui!.HandleInput(new UiInput(InputKind.PointerDown,bounds.X+10,bounds.Y+10));
            _ui.HandleInput(new UiInput(InputKind.PointerUp,bounds.X+10,bounds.Y+10));
            _ui.HandleInput(new UiInput(InputKind.KeyDown,Key:key));_ui.HandleInput(new UiInput(InputKind.KeyUp,Key:key));
        });
        var input=_controls.Take();Verification.Check(input.Commands.Length==1,"Routed command did not reach the viewport.");
        await Command(input.Commands[0],input.Move,input.Jump,token);
    }
    private async Task Command(VisualCommand command,Vector2 move,bool jump,CancellationToken token)
    {
        switch(command)
        {
            case VisualCommand.Toggle:_session.SetPlaying(!_session.Playing);_controls.ResetMotion();break;
            case VisualCommand.Step:if(!_session.Stopped&&!_session.Playing)_session.Advance(move,jump);break;
            case VisualCommand.Stop:await StopSurface();_session.Stop();break;
            case VisualCommand.Reset:await StopSurface();_session.Reset();await StartSurface(token);break;
        }
    }
    private async Task Play(CancellationToken token)
    {
        var clock=Stopwatch.StartNew();var previous=clock.Elapsed.TotalSeconds;var accumulator=0d;var pendingJump=false;var epoch=0L;
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(1d/60));
        while(await timer.WaitForNextTickAsync(token))
        {
            var input=_controls.Take();var now=clock.Elapsed.TotalSeconds;
            var elapsed=Math.Min(.1,now-previous);previous=now;
            if(input.Epoch!=epoch){pendingJump=false;epoch=input.Epoch;}
            pendingJump|=input.Jump;
            foreach(var command in input.Commands)
            {
                await Command(command,input.Move,pendingJump,token);pendingJump=false;accumulator=0;elapsed=0;previous=clock.Elapsed.TotalSeconds;
            }
            if(_session.Stopped)continue;
            accumulator=_session.Playing?accumulator+elapsed:0;
            while(accumulator>=1d/60){_session.Advance(input.Move,pendingJump);pendingJump=false;accumulator-=1d/60;}
            await Capture(null,_session.Playing?(float)(accumulator*60):1,token);
        }
    }
}
