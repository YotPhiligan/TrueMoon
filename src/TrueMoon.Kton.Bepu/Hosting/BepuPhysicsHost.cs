namespace TrueMoon.Kton.Bepu.Hosting;

/// <summary>App-owned admission and failed-creation cleanup. Published worlds remain exclusively caller-owned.</summary>
/// <remarks>Borrow this service. Normally App stops/disposes it. Retain it for an explicit cleanup retry if App disposal fails;
/// App and DI providers may cache their first disposal failure. No successful world, pool or worker is created at startup.
/// Consumers join their own work and dispose worlds before App shutdown. Worlds have no borrowed App resources and can
/// still be disposed after the host closes. This host never races their Step or Dispose.</remarks>
public sealed class BepuPhysicsHost : IStartable, IStoppable, IDisposable
{
    private readonly Lock _sync = new();
    private readonly BepuPhysicsWorldFactory _factory;
    private readonly PhysicsWorldOptions _defaults;
    private readonly FaultInjection _faults;
    private bool _running, _closed, _released, _busy;
    internal FaultInjection Faults => _faults;

    internal BepuPhysicsHost(PhysicsWorldOptions defaults, FaultInjection? faults = null)
    {
        _defaults = defaults;
        _faults = faults ?? new FaultInjection();
        _factory = new BepuPhysicsWorldFactory(_faults);
    }

    /// <summary>Ownership diagnostics, including caller-owned worlds that the host will not dispose.</summary>
    public BepuResourceSnapshot Resources { get { lock (_sync) return _factory.Resources; } }
    /// <summary>Whether host-owned cleanup succeeded. This does not assert that caller-owned worlds have been disposed.</summary>
    public bool IsReleased { get { lock (_sync) return _released; } }

    internal IPhysicsWorld Create(PhysicsWorldOptions? arguments, bool useDefaults = false)
    {
        lock (_sync)
        {
            Enter();
            try
            {
                ObjectDisposedException.ThrowIf(_closed, this);
                if (!_running) throw new InvalidOperationException("Kton requires successful App startup before world creation.");
                return _factory.Create(useDefaults ? _defaults : arguments!);
            }
            finally { _busy = false; }
        }
    }

    /// <summary>Opens admission without creating a physics world. Repeated running starts are harmless; closed hosts cannot restart.</summary>
    /// <param name="cancellationToken">Checked before changing startup state.</param><returns>Synchronous completion.</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            Enter();
            try
            {
                ObjectDisposedException.ThrowIf(_closed, this);
                cancellationToken.ThrowIfCancellationRequested();
                if (_running) return Task.CompletedTask;
                try { _faults.Visit(Checkpoint.HostStarting); _running = true; }
                catch (Exception startup)
                {
                    _closed = true;
                    try { Release(); }
                    catch (Exception cleanup) { throw new AggregateException(startup, cleanup); }
                    throw;
                }
                return Task.CompletedTask;
            }
            finally { _busy = false; }
        }
    }

    /// <summary>Closes admission and cleans unpublished failed creations. Published worlds are not drained or disposed.</summary>
    /// <param name="cancellationToken">Does not interrupt synchronous owned cleanup; callers drain their world work separately.</param>
    /// <returns>Synchronous completion, or a retained cleanup failure that StopAsync/Dispose can retry.</returns>
    public Task StopAsync(CancellationToken cancellationToken = default) { Dispose(); return Task.CompletedTask; }

    /// <summary>Closes admission permanently. Failure retains the same cleanup owner for retry; success is idempotent.</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            Enter();
            try { _closed = true; _running = false; Release(); }
            finally { _busy = false; }
        }
    }

    private void Release()
    {
        if (_released) return;
        _factory.Dispose();
        _released = true;
    }
    private void Enter()
    {
        if (_busy) throw BepuPhysicsWorldFactory.Error(PhysicsErrorCode.Busy, "App.Host");
        _busy = true;
    }
}
