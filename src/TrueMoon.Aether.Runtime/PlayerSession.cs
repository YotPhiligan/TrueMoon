using System.Security.Cryptography;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Input;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Gpu;
using TrueMoon.Aether.Runtime.Gpu.OpenGl;
using TrueMoon.Aether.Runtime.Gpu.Surfaces;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Runtime.Transport;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Runtime;

/// <summary>App-owned standalone game. Owns the loader/protocol/local surface, borrows factories and mesh manager, and creates no process/IPC/editor.</summary>
public sealed class PlayerSession : IStartable,IStoppable,IAsyncDisposable,IGpuSurfaceFrameSource
{
    private readonly PlayerStart _start;
    private readonly IFactory<GameBuildLoader,GameBuildReference> _games;
    private readonly IFactory<RuntimeProtocol,(SessionIdentity Identity,IGameSession Game)> _protocols;
    private readonly IFactory<GpuLocalSurfaceSession,GpuLocalSurfaceRequest> _surfaces;
    private readonly RuntimeMeshRenderHost _meshes;
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly Lock _cleanupGate=new();
    private readonly CancellationTokenSource _end=new();
    private GameBuildLoader? _game;
    private RuntimeProtocol? _protocol;
    private GpuLocalSurfaceSession? _surface;
    private ViewportPresentation? _presentation;
    private GameHudSnapshot? _hud;
    private ulong _sequence;
    private bool _resumePrimitive;
    private int _closing;
    private Task? _cleanup;
    private PlayerStatus _status=new(PlayerState.Created,0,false);
    internal PlayerSession(PlayerStart start,IFactory<GameBuildLoader,GameBuildReference> games,
        IFactory<RuntimeProtocol,(SessionIdentity,IGameSession)> protocols,IFactory<GpuLocalSurfaceSession,GpuLocalSurfaceRequest> surfaces,RuntimeMeshRenderHost meshes)
    {
        _start=start;_games=games;_protocols=protocols;_surfaces=surfaces;_meshes=meshes;
        Identity=new(Guid.NewGuid(),1,start.Artifact.HostContractId,start.Artifact.BuildId,Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    }
    /// <summary>Local identity; never an environment bootstrap or a fake owned-process lease.</summary>
    public SessionIdentity Identity{get;}
    /// <summary>Last detached observation.</summary>
    public PlayerStatus Status=>Volatile.Read(ref _status);
    /// <summary>Current local presentation, hidden after closing admission.</summary>
    public ViewportPresentation? Presentation=>Volatile.Read(ref _closing)==0?Volatile.Read(ref _presentation):null;
    /// <summary>Latest detached HUD for the active game. Closing immediately hides the snapshot.</summary>
    public GameHudSnapshot? Hud=>Volatile.Read(ref _closing)==0?Volatile.Read(ref _hud):null;
    /// <summary>Whether the selected game supplies the primitive or mesh rendering capability.</summary>
    public bool SupportsRendering=>_game?.Session is IRenderGameSession or Rendering.Snapshots.IRenderSnapshotGameSession;
    /// <summary>Last local native owner; keep the UI context alive until it reports released.</summary>
    public GpuLocalSurfaceSession? Surface=>_surface;
    /// <summary>Loads/activates the captured deployment and scene once, leaving GPU/presentation readiness explicit.</summary>
    /// <param name="cancellationToken">Cooperative activation cancellation.</param><returns>CPU activation completion.</returns>
    public async Task StartAsync(CancellationToken cancellationToken=default)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,_end.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        Exception? failure=null;var activation=false;
        try
        {
            Guard();if(Status.State!=PlayerState.Created)throw new InvalidOperationException("Player session starts once.");
            activation=true;
            Publish(new(PlayerState.Loading,0,false));
            _game=_games.Create(new(_start.Artifact.DirectoryPath,_start.Artifact.BuildId));
            _protocol=_protocols.Create((Identity,_game.Session));
            await SendAsync(CommandKind.Hello,null,linked.Token).ConfigureAwait(false);
            if(_start.World is {} world)await _protocol.LoadLocalWorldAsync(world,linked.Token).ConfigureAwait(false);
            else await SendAsync(CommandKind.LoadScene,request=>
            {request.SceneId=_start.Snapshot!.Id;request.Revision=_start.Snapshot.Revision;request.Entities=_start.Snapshot.CopyEntities();},linked.Token).ConfigureAwait(false);
            await RefreshAsync(linked.Token).ConfigureAwait(false);
            RefreshHud();
        }
        catch(Exception error)when(activation){failure=error;Volatile.Write(ref _hud,null);Publish(Status with{State=PlayerState.Faulted,Error=error});}
        finally{_gate.Release();}
        if(failure!=null)
        {
            try{await DisposeAsync().ConfigureAwait(false);}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
    /// <summary>Creates the sole owned surface before native work, using a borrowed live GL context.</summary>
    /// <param name="adapter">Matching GL/Vulkan device.</param><param name="context">Borrowed owner context.</param><param name="validation">Core/synchronization validation.</param>
    /// <param name="token">Cancellation.</param><returns>Unconfigured surface owner.</returns>
    public async Task<GpuLocalSurfaceSession> AttachSurfaceAsync(GpuAdapterId adapter,GpuOpenGlContext context,bool validation=false,CancellationToken token=default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            Ready();if(!SupportsRendering)throw new NotSupportedException("Selected game supplies no render capability.");
            if(_surface!=null)throw new InvalidOperationException("Player owns one local surface.");
            return _surface=_surfaces.Create(new(Identity,this,adapter,context,validation));
        }
        finally{_gate.Release();}
    }
    /// <summary>Closes the old ticket and drains its native generation before publishing the new extent.</summary>
    /// <param name="width">Physical width;0 with height0 suspends.</param><param name="height">Physical height.</param><param name="token">Cancellation.</param><returns>The new presentation.</returns>
    public async Task<ViewportPresentation> ConfigureSurfaceAsync(int width,int height,CancellationToken token=default)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(token,_end.Token);await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            Ready();Interlocked.Exchange(ref _presentation,null)?.Close();
            if(_game!.Session is not Rendering.Snapshots.IRenderSnapshotGameSession && Status.State==PlayerState.Playing)
            {
                _resumePrimitive=true;
                Apply(await SendAsync(CommandKind.SetMode,request=>request.Playing=false,linked.Token).ConfigureAwait(false));
            }
            var surface=_surface??throw new InvalidOperationException("Attach the local surface first.");
            await surface.ConfigureAsync(width,height,linked.Token).ConfigureAwait(false);
            var presentation=new ViewportPresentation(new(Identity,surface.Generation,width,height,ViewportTransportKind.Gpu));
            Volatile.Write(ref _presentation,presentation);await RefreshAsync(linked.Token).ConfigureAwait(false);return presentation;
        }
        finally{_gate.Release();}
    }
    /// <summary>Admits only the exact actually drawn/presented ticket of the active local surface.</summary>
    /// <param name="frame">Retained with the independent presented image.</param><param name="token">Cancellation.</param><returns>False for superseded tickets/sources.</returns>
    public async Task<bool> PresentedAsync(ViewportFrame frame,CancellationToken token=default)
    {
        if(Presentation is not {} active||!ReferenceEquals(frame.Presentation,active)||!active.Presented(frame))return false;
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if(Volatile.Read(ref _closing)!=0||!ReferenceEquals(Presentation,active))return false;
            if(frame.Metadata.RenderView is {} stamp)
            {
                var reply=await SendAsync(CommandKind.PresentRenderView,request=>request.RenderViewStamp=stamp.Encode(),token).ConfigureAwait(false);
                Apply(reply);return reply.RenderViewPresentationAccepted;
            }
            if(_resumePrimitive)
            {
                Apply(await SendAsync(CommandKind.SetMode,request=>request.Playing=true,token).ConfigureAwait(false));
                _resumePrimitive=false;
            }
            return true;
        }
        finally{_gate.Release();}
    }
    /// <summary>Sets local gameplay mode; mesh Playing still requires a current actual-presentation fence.</summary>
    /// <param name="playing">Requested mode.</param><param name="token">Cancellation.</param><returns>Applied observation.</returns>
    public async Task<PlayerStatus> SetPlayingAsync(bool playing,CancellationToken token=default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            Ready();
            if(playing&&_surface!=null&&Presentation?.FirstPresented.IsCompletedSuccessfully!=true)
                throw new InvalidOperationException("Player surface has not been presented.");
            Apply(await SendAsync(CommandKind.SetMode,request=>request.Playing=playing,token).ConfigureAwait(false));
            _resumePrimitive=false;return Status;
        }
        finally{_gate.Release();}
    }
    /// <summary>Runs one paused tick through the same game/input protocol.</summary><param name="token">Cancellation.</param><returns>Applied tick.</returns>
    public Task<PlayerStatus> StepAsync(CancellationToken token=default)=>CommandAsync(CommandKind.Step,null,token);
    /// <summary>Stages a detached input packet; it does not perform container resolution.</summary><param name="input">Bounded input.</param><param name="token">Cancellation.</param><returns>Capability outcome.</returns>
    public async Task<GameInputOutcome> InputAsync(GameInputState input,CancellationToken token=default)
    {
        input.Validate();await _gate.WaitAsync(token).ConfigureAwait(false);
        try{Ready();var reply=await SendAsync(CommandKind.SetInput,request=>{request.InputCaptured=input.Captured;request.InputButtons=(int)input.Buttons;request.InputYaw=input.Yaw;request.InputPitch=input.Pitch;request.InputInteract=input.InteractPressed;},token).ConfigureAwait(false);Apply(reply);return (GameInputOutcome)reply.InputOutcome;}
        finally{_gate.Release();}
    }
    /// <summary>Attempts one caller-driven fixed tick and returns its current mode/tick. No timers are hidden inside the session.</summary>
    /// <param name="token">Cancellation.</param><returns>Completed game observation.</returns>
    public async Task<PlayerStatus> FixedTickAsync(CancellationToken token=default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            Ready();
            var previousTick=Status.Tick;
            _protocol!.FixedTick();
            await RefreshAsync(token).ConfigureAwait(false);
            if(Status.Tick!=previousTick)RefreshHud();
            return Status;
        }
        catch(Exception error){Volatile.Write(ref _hud,null);Publish(Status with{State=PlayerState.Faulted,Error=error});throw;}
        finally{_gate.Release();}
    }
    /// <summary>Reads the optional game observation at exactly the requested completed tick.</summary>
    /// <param name="tick">Tick already completed by this Player session.</param>
    /// <param name="token">Cancels waiting to read the session.</param>
    /// <returns>A detached snapshot, or null when the game supplies no observation.</returns>
    public async Task<GameScenarioSnapshot?> ReadScenarioObservationAsync(ulong tick, CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            Ready();
            return await _protocol!.ReadScenarioObservationAsync(tick, token).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task<PlayerStatus> CommandAsync(CommandKind kind,Action<ControlRequest>? configure,CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            Ready();var previousTick=Status.Tick;
            var reply=await SendAsync(kind,configure,token).ConfigureAwait(false);Apply(reply);
            if(Status.Tick!=previousTick)RefreshHud();
            return Status;
        }
        finally{_gate.Release();}
    }
    private async Task<ControlReply> SendAsync(CommandKind kind,Action<ControlRequest>? configure,CancellationToken token)
    {var next=checked(_sequence+1);var request=ControlRequest.Create(Identity,next,kind);configure?.Invoke(request);var reply=await _protocol!.SendAsync(request,token).ConfigureAwait(false);_sequence=next;return reply;}
    private async Task<PlayerStatus> RefreshAsync(CancellationToken token){Apply(await SendAsync(CommandKind.GetStatus,null,token).ConfigureAwait(false));return Status;}
    private void RefreshHud()=>Volatile.Write(ref _hud,(_game?.Session as IGameHudSource)?.ReadHud());
    private void Apply(ControlReply reply)=>Publish(new(reply.Phase=="Playing"?PlayerState.Playing:PlayerState.Paused,checked((ulong)reply.Tick),reply.RenderViewReady,Status.Error));
    private void Publish(PlayerStatus status)=>Volatile.Write(ref _status,status);
    private void Guard()=>ObjectDisposedException.ThrowIf(Volatile.Read(ref _closing)!=0,this);
    private void Ready(){Guard();if(_protocol==null||Status.State is not (PlayerState.Paused or PlayerState.Playing))throw new InvalidOperationException("Player scene is not active.");}
    Task<GpuFrameData> IGpuSurfaceFrameSource.CaptureAsync(GpuSurfaceRenderRequest request,CancellationToken token)=>_protocol!.CaptureSurfaceAsync(request,_meshes,token);
    async Task IGpuSurfaceFrameSource.DetachAsync(){_protocol?.DetachRenderSurface();await _meshes.DetachAsync().ConfigureAwait(false);}
    void IGpuSurfaceFrameSource.Produced(GpuFrameData data)=>_protocol!.SurfaceProduced(data);
    /// <summary>Stops gameplay and drains local render work before game/World/context unload. Cancellation only cancels the wait.</summary>
    /// <param name="cancellationToken">Wait cancellation.</param><returns>Complete safe cleanup.</returns>
    public Task StopAsync(CancellationToken cancellationToken=default)=>DisposeAsync().AsTask().WaitAsync(cancellationToken);
    /// <summary>Closes admission immediately; failed cleanup retains the game and supports an explicit retry.</summary>
    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _closing,1);try{_end.Cancel();}catch(ObjectDisposedException){}
        Interlocked.Exchange(ref _presentation,null)?.Close();
        lock(_cleanupGate)
        {
            if(_cleanup is {IsCompletedSuccessfully:true} or {IsCompleted:false})return new(_cleanup);
            return new(_cleanup=CleanupAsync());
        }
    }
    private async Task CleanupAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            Publish(Status with{State=PlayerState.Stopping});
            Volatile.Write(ref _hud,null);
            if(_surface!=null)await _surface.DisposeAsync().ConfigureAwait(false);
            await _meshes.DisposeAsync().ConfigureAwait(false);
            _protocol?.Dispose();_protocol=null;_game?.Dispose();_game=null;
            _end.Dispose();Publish(Status with{State=PlayerState.Stopped,RenderViewReady=false});
        }
        catch(Exception error){Publish(Status with{State=PlayerState.CleanupFailed,Error=Status.Error==null?error:new AggregateException(Status.Error,error)});throw;}
        finally{_gate.Release();}
    }
}
