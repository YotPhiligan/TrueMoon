using System.Diagnostics;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Gpu.OpenGl;
using TrueMoon.Aether.Runtime.Gpu.Vulkan;
using TrueMoon.Aether.Runtime.Sessions;

namespace TrueMoon.Aether.Runtime.Gpu.Surfaces;

/// <summary>Explicit local surface creation arguments. Source/context remain borrowed until safe cleanup.</summary>
public sealed record GpuLocalSurfaceRequest(SessionIdentity Identity, IGpuSurfaceFrameSource Source,
    GpuAdapterId Adapter, GpuOpenGlContext Context, bool Validation = false);

/// <summary>Caller-owned, App-retained local Vulkan/GL surface. Uses no child process, bootstrap, pipe or remote lease.</summary>
public sealed class GpuLocalSurfaceSession : IGpuResource
{
    private readonly GpuResourceHost _host;
    private readonly GpuLocalSurfaceRequest _request;
    private readonly SemaphoreSlim _gate = new(1,1);
    private readonly CancellationTokenSource _end = new();
    private VulkanSurface? _surface;
    private GpuSurfaceImports? _imports;
    private GpuSlotLedger? _ledger;
    private ulong _generation;
    private int _closed, _created, _released, _slotsCreated, _slotsReleased, _devicesCreated, _devicesReleased, _errors, _warnings;
    private readonly List<string> _messages=[];
    private readonly Queue<VulkanResourceDiagnostics> _meshes=[];
    internal GpuLocalSurfaceSession(GpuResourceHost host, GpuLocalSurfaceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request); request.Identity.Validate(); ArgumentNullException.ThrowIfNull(request.Source);
        ArgumentNullException.ThrowIfNull(request.Adapter); ArgumentNullException.ThrowIfNull(request.Context); _host=host; _request=request;
    }
    /// <summary>Current acknowledged surface generation, including suspended generations.</summary>
    public ulong Generation => _generation;
    /// <summary>True only after local imports, mesh resources and exporter/device have been released.</summary>
    public bool IsReleased { get; private set; }
    /// <summary>Copied ownership/validation observations including retired surfaces.</summary>
    public GpuSurfaceProducerDiagnostics Diagnostics
    {
        get
        {
            var live=_surface; var diagnostics=live?.Diagnostics;
            return new(_request.Validation,_errors+(diagnostics?.Errors??0),_warnings+(diagnostics?.Warnings??0),
                _messages.Concat(diagnostics?.Messages??[]).Take(64).ToArray(),_created,_released,_slotsCreated,_slotsReleased,live?.LiveSlots??0,_host.OpenHandleCount,
                _devicesCreated+(live?.DeviceCreations??0),_devicesReleased+(live?.DeviceReleases??0))
                { MeshResources=Array.AsReadOnly(_meshes.Concat(live?.ResourceDiagnostics??[]).TakeLast(16).ToArray()) };
        }
    }
    /// <summary>Drains the previous generation before configuring a fresh extent; 0x0 suspends without a GPU allocation.</summary>
    /// <param name="width">Physical target width.</param><param name="height">Physical target height.</param><param name="token">Cancellation.</param><returns>Configured generation.</returns>
    public async Task ConfigureAsync(int width,int height,CancellationToken token=default)
    {
        GpuSurfaceOffer.ValidateExtent(width,height); using var linked=CancellationTokenSource.CreateLinkedTokenSource(token,_end.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            Guard(); await DestroyAsync().ConfigureAwait(false); _generation=checked(_generation+1);
            if(width==0)return;
            _surface=new(_request.Identity,_generation,_request.Adapter,width,height,_request.Validation); _created++;
            try{await _surface.InitializeAsync(linked.Token).ConfigureAwait(false); _host.ObserveDevice(_surface.DeviceObservation);}
            finally{_slotsCreated+=_surface.SlotCount;}
            _imports=new(_surface.Offer,_request.Context);
            // Real current-process handle, borrowed only while duplicating exact exporter tokens.
            using(var process=Process.GetCurrentProcess())
                await _request.Context.Invoke(()=>_imports.Initialize(process.SafeHandle)).ConfigureAwait(false);
            _ledger=new(_request.Identity,_generation); _ledger.Imported();
        }
        catch{Fence();throw;}
        finally{_gate.Release();}
    }
    /// <summary>Produces one complete snapshot, copies on the GL owner, observes GL completion and retires the slot.</summary>
    /// <param name="slot">Idle slot0..2.</param><param name="presentation">Current local presentation.</param><param name="consume">Copy/submit synchronously; never retain the borrowed texture.</param>
    /// <param name="token">Cancellation.</param><returns>The issued ticket; production/copy is not actual presentation.</returns>
    public async Task<ViewportFrame?> ProduceAsync(int slot,ViewportPresentation presentation,Action<GpuViewportImage> consume,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(presentation); ArgumentNullException.ThrowIfNull(consume);
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(token,_end.Token); await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            Guard(); var ledger=_ledger??throw new InvalidOperationException("Local surface is suspended/not configured.");
            if(presentation.Surface.Identity!=_request.Identity||presentation.Surface.Generation!=_generation||presentation.Surface.Transport!=ViewportTransportKind.Gpu)
                throw new InvalidDataException("Presentation belongs to another local surface.");
            var stamp=ledger.Begin(slot); var offer=_surface!.Offer.Slots[slot];
            var data=await _request.Source.CaptureAsync(new(stamp,offer.Width,offer.Height,_surface.RenderBackends),linked.Token).ConfigureAwait(false);
            GpuFrameCompletion ready;
            try
            {
                data.Validate(stamp,offer.Width,offer.Height);
                ready=data.Snapshot is {} recording ? await _surface.ProduceSnapshotAsync(stamp,recording,linked.Token).ConfigureAwait(false)
                    : await _surface.ProduceAsync(stamp,linked.Token,data.Pixels,data.Scene).ConfigureAwait(false);
                _request.Source.Produced(data);
            }
            catch(Exception original)
            {
                if(data.Snapshot is {IsReleased:false} recording)
                    try{await recording.DisposeAsync().ConfigureAwait(false);}catch(Exception cleanup){throw new AggregateException(original,cleanup);}
                throw;
            }
            ledger.Produced(ready); ledger.BeginRead(ready); ViewportFrame? frame=null;
            await _request.Context.Invoke(()=>_imports!.Use(ready,texture=>
            {
                if(presentation.TryIssue(data.Metadata) is {} ticket){frame=ticket;consume(new(texture,ticket));}
            },Guard)).ConfigureAwait(false);
            GpuFrameCompletion? completed=null; var clock=Stopwatch.StartNew();
            while(completed==null)
            {
                linked.Token.ThrowIfCancellationRequested(); await _request.Context.Invoke(()=>completed=_imports!.Poll(slot)).ConfigureAwait(false);
                if(clock.Elapsed>TimeSpan.FromSeconds(5))throw new TimeoutException("Local GL use remains unconfirmed.");
                if(completed==null)await Task.Delay(2,linked.Token).ConfigureAwait(false);
            }
            ledger.ReadCompleted(completed); ledger.Return(completed);
            ledger.Retired(await _surface.RetireAsync(completed,linked.Token).ConfigureAwait(false)); return frame;
        }
        catch{Fence();throw;}
        finally{_gate.Release();}
    }
    private void Guard()=>ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed)!=0,this);
    private void Fence(){if(Interlocked.Exchange(ref _closed,1)==0){_ledger?.Fault();_end.Cancel();}}
    private async Task DestroyAsync()
    {
        await _request.Source.DetachAsync().ConfigureAwait(false);
        if(_imports!=null){await _imports.DisposeAsync().ConfigureAwait(false);_imports=null;}
        if(_surface==null)return;
        await _surface.DisposeAsync().ConfigureAwait(false); var diagnostic=_surface.Diagnostics;
        _errors+=diagnostic.Errors;_warnings+=diagnostic.Warnings;_messages.AddRange(diagnostic.Messages.Take(Math.Max(0,64-_messages.Count)));
        foreach(var mesh in _surface.ResourceDiagnostics){if(_meshes.Count==16)_meshes.Dequeue();_meshes.Enqueue(mesh);}
        _released++;_slotsReleased+=_surface.SlotCount;_devicesCreated+=_surface.DeviceCreations;_devicesReleased+=_surface.DeviceReleases;
        _surface=null;_ledger=null;
    }
    /// <summary>Closes admission and releases local use before exporter/context; errors retain ownership for retry.</summary>
    public async ValueTask DisposeAsync()
    {
        Fence();await _gate.WaitAsync().ConfigureAwait(false);
        try{if(IsReleased)return;await DestroyAsync().ConfigureAwait(false);IsReleased=true;_host.LastSurfaceProducer=Diagnostics;_end.Dispose();}
        finally{_gate.Release();}
    }
}
