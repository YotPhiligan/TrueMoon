using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Rendering.Snapshots;

namespace TrueMoon.Aether.Rendering.Resources;

/// <summary>App-owned resource root, owning its CPU catalog and device products with early caller cleanup and fallback retention.</summary>
public sealed class RenderResourceHost : IAsyncDisposable, IFactory<RenderDeviceOwner, RenderDeviceRequest>,
    IFactory<RenderPreparation, RenderPreparationRequest>, IFactory<RenderWorldBinding, RenderWorldBindingRequest>,
    IFactory<RenderSnapshotView, RenderSnapshotViewRequest>
{
    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _cleanupGate = new(1, 1);
    private readonly IFactory<IRenderResourceBackend, RenderDeviceRequest> _backends;
    private readonly RenderResourceOptions _options;
    private readonly List<RenderDeviceOwner> _devices = [];
    private readonly HashSet<RenderWorldBinding> _bindings = [];
    private readonly HashSet<RenderSnapshotView> _views = [];
    private bool _closed;
    /// <summary>Takes ownership of a CPU catalog and borrows a backend factory. No native work starts here.</summary>
    /// <param name="content">Exclusively owned CPU catalog.</param><param name="backends">Borrowed factory.</param><param name="options">Copied device/preparation limits.</param>
    public RenderResourceHost(RenderContentCatalog content, IFactory<IRenderResourceBackend, RenderDeviceRequest> backends, RenderResourceOptions? options = null)
    { ArgumentNullException.ThrowIfNull(content); ArgumentNullException.ThrowIfNull(backends); Content = content; _backends = backends; _options = (options ?? new()).Snapshot(); }
    /// <summary>Gets the borrowed App-owned CPU catalog.</summary>
    public RenderContentCatalog Content { get; }
    /// <summary>Gets the live/failed device product count.</summary>
    public int DeviceCount { get { lock (_gate) return _devices.Count(static device => !device.IsReleased); } }
    /// <summary>Whether devices and catalog released completely.</summary>
    public bool IsReleased { get; private set; }
    /// <summary>Creates one explicit lazy device owner. Release the old device before another generation.</summary>
    /// <param name="arguments">Session/world/content identity.</param><returns>Caller-disposable product retained by this host until cleanup succeeds.</returns>
    public RenderDeviceOwner Create(RenderDeviceRequest arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        lock (_gate)
        {
            Guard(); _devices.RemoveAll(static device => device.IsReleased);
            ResourceGuard.Require(_devices.Count == 0, RenderResourceError.LimitExceeded, "V1 supports one resource device/view per host.");
            var device = new RenderDeviceOwner(Content, _backends, arguments, _options); _devices.Add(device); return device;
        }
    }
    /// <summary>Creates a bounded preparation through the matching owned device.</summary><param name="arguments">Typed operation arguments.</param><returns>Caller-disposable operation.</returns>
    public RenderPreparation Create(RenderPreparationRequest arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(arguments.Device);
        lock (_gate) { Guard(); RequireDevice(arguments.Device); return arguments.Device.Create(arguments.Content, arguments.CancellationToken); }
    }
    /// <summary>Creates a resource binding for a borrowed serialized World, without preparing native buffers yet.</summary>
    /// <param name="arguments">World/device/content arguments.</param><returns>Caller-owned binding tracked for fallback cleanup.</returns>
    public RenderWorldBinding Create(RenderWorldBindingRequest arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(arguments.Device);
        lock (_gate)
        {
            Guard(); RequireDevice(arguments.Device);
            ResourceGuard.Require(_bindings.Count == 0, RenderResourceError.LimitExceeded, "V1 supports one required World/view binding per host.");
            var binding = new RenderWorldBinding(this, arguments); _bindings.Add(binding); return binding;
        }
    }
    private void RequireDevice(RenderDeviceOwner device) => ResourceGuard.Require(_devices.Contains(device) && !device.IsReleased, RenderResourceError.InvalidIdentity, "Foreign/released resource device.");
    /// <summary>Creates the sole caller-owned extraction/mailbox/submission view, tracked before any work starts.</summary>
    /// <param name="arguments">Borrowed matching binding and captured source metadata.</param><returns>View released before binding/device/World.</returns>
    public RenderSnapshotView Create(RenderSnapshotViewRequest arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(arguments.Binding);
        lock (_gate)
        {
            Guard();
            ResourceGuard.Require(_bindings.Contains(arguments.Binding) && !arguments.Binding.IsReleased, RenderResourceError.InvalidIdentity, "Foreign/released view binding.");
            ResourceGuard.Require(_views.Count == 0, RenderResourceError.LimitExceeded, "V1 supports one snapshot view per host.");
            var view = new RenderSnapshotView(this, arguments); _views.Add(view); return view;
        }
    }
    private void Guard() => ResourceGuard.Require(!_closed, RenderResourceError.Closed, "Resource host admission is closed.");
    internal void BindingReleased(RenderWorldBinding binding) { lock (_gate) _bindings.Remove(binding); }
    internal void ViewReleased(RenderSnapshotView view) { lock (_gate) _views.Remove(view); }
    /// <summary>Closes admission and awaits actual cleanup; cancelling this wait never frees live resources.</summary><param name="cancellationToken">Wait cancellation.</param><returns>Confirmed cleanup.</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default) => await DisposeAsync().AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
    /// <summary>Disposes bindings, then devices, then catalog. A failed stage retains ownership for retry.</summary>
    public async ValueTask DisposeAsync()
    {
        lock (_gate) _closed = true;
        Content.CloseAdmission();
        RenderDeviceOwner[] closingDevices;
        lock (_gate) closingDevices = _devices.ToArray();
        foreach (var device in closingDevices) device.BeginStop();
        await _cleanupGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsReleased) return; var errors = new List<Exception>(); RenderWorldBinding[] bindings; RenderDeviceOwner[] devices; RenderSnapshotView[] views;
            lock (_gate) { bindings = _bindings.ToArray(); devices = _devices.ToArray(); views = _views.ToArray(); }
            foreach (var view in views) { try { await view.DisposeAsync().ConfigureAwait(false); } catch (Exception error) { errors.Add(error); } }
            // Failed view cleanup still owns accepted native use: do not force-dispose its dependencies.
            if (errors.Count != 0) throw new AggregateException("App snapshot views retained ownership.", errors);
            foreach (var binding in bindings) { try { await binding.DisposeAsync().ConfigureAwait(false); } catch (Exception error) { errors.Add(error); } }
            foreach (var device in devices) { try { await device.DisposeAsync().ConfigureAwait(false); } catch (Exception error) { errors.Add(error); } }
            if (errors.Count != 0) throw new AggregateException("App resource cleanup retained owners.", errors);
            Content.Dispose(); IsReleased = true;
        }
        finally { _cleanupGate.Release(); }
    }
}
