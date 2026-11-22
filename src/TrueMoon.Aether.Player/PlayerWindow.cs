
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Gpu;
using TrueMoon.Aether.Runtime.Gpu.OpenGl;
using TrueMoon.Aether.Runtime.Gpu.Surfaces;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;

namespace TrueMoon.Aether.Player;

/// <summary>App-owned Player window coordination. Borrows window/session/input, owns bounded loop/acknowledgement, retains no game World.</summary>
public sealed class PlayerWindow:IStartable,IStoppable,IAsyncDisposable
{
    private readonly HostedUiWindow _window;
    private readonly PlayerSession _session;
    private readonly PlayerInput _input;
    private readonly PlayerOptions _options;
    private readonly CancellationTokenSource _end=new();
    private readonly TaskCompletionSource _first=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _stopGate=new();
    private Task _loop=Task.CompletedTask,_ack=Task.CompletedTask;
    private Task? _stop;
    private UiSession? _ui;
    private PlayerView? _view;
    private SilkWindowHost? _native;
    private GpuOpenGlContext? _context;
    private Action? _current;
    private int _owner,_closing;
    internal PlayerWindow(HostedUiWindow window,PlayerSession session,PlayerInput input,PlayerOptions options)
    {_window=window;_session=session;_input=input;_options=options;window.FramePresented+=OnPresented;window.Closing+=OnClosing;}
    /// <summary>Completes only after actual current presentation and admission to Playing.</summary>
    public Task Ready=>_ready.Task;
    /// <summary>Owned fixed/render loop; a fault is terminal until App cleanup.</summary>
    public Task Completion=>_loop;
    /// <summary>Confirmed GPU copies, retained images are bounded to one.</summary>
    public long CopiedFrames{get;private set;}
    /// <summary>Admitted actual presentation attempts.</summary>
    public long PresentedFrames{get;private set;}
    /// <summary>Starts after the registered window and local game; no parent/bootstrap or SDK lookup occurs.</summary><param name="cancellationToken">Startup cancellation.</param><returns>First playable frame.</returns>
    public async Task StartAsync(CancellationToken cancellationToken=default)
    {
        try
        {
            if(!_session.SupportsRendering)throw new NotSupportedException("Player window requires a game render capability.");
            var initialized=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);GpuAdapterId adapter=null!;
            _window.PostWindow(host=>
            {
                try
                {
                    _native=host;_owner=Environment.CurrentManagedThreadId;var gl=host.NativeWindow.GLContext!;_current=gl.MakeCurrent;_current();
                    nint Resolve(string name)=>gl.TryGetProcAddress(name,out var pointer)?pointer:0;
                    _context=new(Resolve,_current,Invoke);adapter=_context.ReadAdapter();initialized.TrySetResult();
                }
                catch(Exception error){initialized.TrySetException(error);}
            });
            await initialized.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            var surface=await _session.AttachSurfaceAsync(adapter,_context!,_options.Validation,cancellationToken).ConfigureAwait(false);
            _loop=RunAsync(surface,_end.Token);
            await _first.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            await _session.SetPlayingAsync(true,cancellationToken).ConfigureAwait(false);_ready.TrySetResult();
        }
        catch(Exception error){_ready.TrySetException(error);_end.Cancel();throw;}
    }
    /// <summary>Fits a physical extent within the existing GPU profile, preserving aspect. Tiny/zero/extreme extents suspend.</summary>
    /// <param name="width">Framebuffer width.</param><param name="height">Framebuffer height.</param><returns>Bounded render extent.</returns>
    public static (int Width,int Height) FitExtent(int width,int height)
    {
        if(width<0||height<0)throw new ArgumentOutOfRangeException(nameof(width));
        if(width<16||height<16)return(0,0);
        var scale=Math.Min(1d,Math.Min(1920d/width,1080d/height));var result=((int)(width*scale),(int)(height*scale));
        return result.Item1<16||result.Item2<16?(0,0):result;
    }
    private async Task RunAsync(GpuLocalSurfaceSession surface,CancellationToken token)
    {
        try
        {
            using var timer=new PeriodicTimer(TimeSpan.FromSeconds(1d/60));var slot=0;var iteration=0; (int Width,int Height)? old=null;
            do
            {
                token.ThrowIfCancellationRequested();if(_ack.IsFaulted)await _ack.ConfigureAwait(false);
                (int Width,int Height) extent=default;
                await Invoke(()=>{var viewport=_native!.Viewport;extent=FitExtent(viewport.Width,viewport.Height);}).ConfigureAwait(false);
                if(old!=extent)
                {
                    _input.Release();await _session.ConfigureSurfaceAsync(extent.Width,extent.Height,token).ConfigureAwait(false);old=extent;slot=0;_window.RequestFrame();
                }
                await _session.InputAsync(_input.Take(),token).ConfigureAwait(false);await _session.FixedTickAsync(token).ConfigureAwait(false);
                if(extent.Width!=0&&iteration++%2==0)
                {
                    var presentation=_session.Presentation!;
                    await surface.ProduceAsync(slot,presentation,Consume,token).ConfigureAwait(false);slot=(slot+1)%3;
                }
            }while(await timer.WaitForNextTickAsync(token).ConfigureAwait(false));
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){}
        catch(Exception error){_first.TrySetException(error);throw;}
    }
    private void Consume(GpuViewportImage source)
    {
        if(_ui==null||_view==null||!ReferenceEquals(_session.Presentation,source.Frame.Presentation)||Volatile.Read(ref _closing)!=0)return;
        var image=((SkiaOpenGLSurface)_ui.Rendering).CopyTexture(source.Texture.Name,source.Texture.Width,source.Texture.Height);
        try{_view.Canvas.Upload(image,source.Frame,_session.Hud,()=>ReferenceEquals(_session.Presentation,source.Frame.Presentation));CopiedFrames++;}
        catch{image.Dispose();throw;}
        _window.RequestFrame();
    }
    private void OnPresented(UiSession ui)
    {
        if(Volatile.Read(ref _closing)!=0)return;
        _ui=ui;_view=(PlayerView)ui.Root;
        if(_ack.IsCompleted&&_view.Canvas.Drawn is {} frame)_ack=AcknowledgeAsync(frame);
    }
    private async Task AcknowledgeAsync(ViewportFrame frame)
    {if(await _session.PresentedAsync(frame,_end.Token).ConfigureAwait(false)){PresentedFrames++;_first.TrySetResult();}}
    private Task Invoke(Action action)
    {
        if(Environment.CurrentManagedThreadId==_owner){_current!();action();return Task.CompletedTask;}
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _window.PostWindow(_=>{try{_current!();action();done.TrySetResult();}catch(Exception error){done.TrySetException(error);}});return done.Task;
    }
    private async Task<bool> OnClosing(UiCloseReason reason){await StopAsync().ConfigureAwait(false);return true;}
    /// <summary>Drains local game/GPU work while the borrowed UI context is still live. Never waits for window Completion from its close handler.</summary>
    /// <param name="cancellationToken">Cancels only the caller wait.</param><returns>Drained work.</returns>
    public Task StopAsync(CancellationToken cancellationToken=default)
    {
        lock(_stopGate)
        {
            if(_stop is not ({IsCompletedSuccessfully:true} or {IsCompleted:false}))_stop=StopCoreAsync();
            return _stop.WaitAsync(cancellationToken);
        }
    }
    private async Task StopCoreAsync()
    {
        Interlocked.Exchange(ref _closing,1);_end.Cancel();_input.Release();_first.TrySetCanceled();
        await _session.StopAsync().ConfigureAwait(false);
        // Execution faults remain observable on Completion. They must not prevent
        // releasing subscriptions after the native/session drain has succeeded.
        try{await _loop.ConfigureAwait(false);}catch(Exception)when(_loop.IsCompleted){}
        try{await _ack.ConfigureAwait(false);}catch(Exception)when(_ack.IsCompleted){}
        _window.FramePresented-=OnPresented;_window.Closing-=OnClosing;_ui=null;_view=null;
        _end.Dispose();
    }
    /// <summary>App fallback for the same owned loop and subscriptions.</summary>
    public ValueTask DisposeAsync()=>new(StopAsync());
}
