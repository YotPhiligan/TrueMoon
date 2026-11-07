namespace TrueMoon.Aether.Ecs;

/// <summary>Caller-owned sequential system graph. Borrows an exclusive World lease, never its disposal ownership.</summary>
/// <remarks>Tick/Stop/Dispose require serialized owner access; only RequestStop may be called concurrently.</remarks>
public sealed class EcsScheduler : IDisposable
{
    private Entry[] _entries;
    private readonly World _world;
    private int _state = (int)EcsSchedulerState.Ready;
    private int _stopRequested;
    private bool _ticking;
    internal EntityCommandBuffer Commands { get; }
    private EcsScheduler(World world, EcsSystemRegistration[] ordered, EcsSchedulerOptions options)
    {
        _world = world;
        Commands = new(world, options.MaximumCommandsPerPhase);
        _entries = new Entry[ordered.Length];
        for (var i = 0; i < ordered.Length; i++) _entries[i] = new(ordered[i], new(this, world, ordered[i].Plan));
    }
    /// <summary>Gets the lifecycle state.</summary>
    public EcsSchedulerState State { get => (EcsSchedulerState)Volatile.Read(ref _state); private set => Volatile.Write(ref _state, (int)value); }
    /// <summary>Gets fully completed ticks, excluding interrupted/faulted ticks.</summary>
    public ulong TickNumber { get; private set; }
    /// <summary>Gets the first execution/cleanup failure, if any.</summary>
    public Exception? Failure { get; private set; }

    /// <summary>Validates/freezes the graph, then obtains owned systems through supplied borrowed factories.</summary>
    /// <param name="world">Borrowed World; dispose scheduler before World.</param><param name="systems">Bounded registrations.</param>
    /// <param name="options">Optional limits, copied before use.</param><returns>A caller-owned scheduler.</returns>
    /// <exception cref="EcsSchedulerCreationException">Activation and cleanup failed; CleanupOwner retains resources for retry.</exception>
    public static EcsScheduler Create(World world, IEnumerable<EcsSystemRegistration> systems, EcsSchedulerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world); ArgumentNullException.ThrowIfNull(systems);
        var limits = (options ?? new()).Snapshot();
        var registrations = new List<EcsSystemRegistration>();
        foreach (var registration in systems)
        {
            if (registration == null) throw new ArgumentException("A system registration is null.", nameof(systems));
            if (registrations.Count == limits.MaximumSystems) throw new ArgumentException("System count limit exceeded.", nameof(systems));
            registrations.Add(registration);
        }
        var order = SystemGraph.Order(registrations.Select(r => r.Plan).ToArray());
        var ordered = order.Select(i => registrations[i]).ToArray();
        var scheduler = new EcsScheduler(world, ordered, limits);
        world.ClaimScheduler(scheduler);
        try
        {
            var instances = new HashSet<IEcsSystem>(ReferenceEqualityComparer.Instance);
            foreach (var entry in scheduler._entries)
            {
                var system = entry.Registration.Factory.Create(world);
                if (system == null) throw new InvalidOperationException("A system factory returned null.");
                if (!instances.Add(system)) throw new InvalidOperationException("System factory results must have exclusive ownership, without aliases.");
                entry.System = system;
            }
            world.CompleteActivation(scheduler);
            return scheduler;
        }
        catch (Exception creation)
        {
            world.CompleteActivation(scheduler);
            scheduler.State = EcsSchedulerState.Faulted; scheduler.Failure = creation;
            try { scheduler.Dispose(); }
            catch (Exception cleanup) { throw new EcsSchedulerCreationException(creation, cleanup, scheduler); }
            throw;
        }
    }

    /// <summary>Runs one positive caller-supplied fixed step. No timer, catch-up or container resolution occurs.</summary>
    /// <param name="deltaTime">Positive fixed step.</param><param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>True after a complete tick; false after a cooperative stop request.</returns>
    public bool Tick(TimeSpan deltaTime, CancellationToken cancellationToken = default)
    {
        if (State != EcsSchedulerState.Ready || _ticking) throw new InvalidOperationException("The scheduler is not ready for a tick.");
        if (deltaTime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(deltaTime));
        cancellationToken.ThrowIfCancellationRequested();
        var targetTick = checked(TickNumber + 1);
        _world.CheckPlayback();
        if (Volatile.Read(ref _stopRequested) != 0) { CompleteStop(); return false; }
        _ticking = true; State = EcsSchedulerState.Running;
        try
        {
            for (var i = 0; i < _entries.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Volatile.Read(ref _stopRequested) != 0) { CompleteStop(); return false; }
                var entry = _entries[i];
                _world.BeginSystem(this, entry.Registration.Plan);
                try
                {
                    entry.Context.Activate(targetTick, deltaTime, cancellationToken);
                    entry.System!.Execute(entry.Context);
                }
                finally { entry.Context.Deactivate(); _world.EndSystem(); }
                cancellationToken.ThrowIfCancellationRequested();
                if (Volatile.Read(ref _stopRequested) != 0) { CompleteStop(); return false; }
                if (i + 1 == _entries.Length || _entries[i + 1].Registration.Plan.Phase != entry.Registration.Plan.Phase)
                    Commands.Playback(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (Volatile.Read(ref _stopRequested) != 0) { CompleteStop(); return false; }
            TickNumber = targetTick; State = EcsSchedulerState.Ready;
            return true;
        }
        catch (OperationCanceledException error) when (cancellationToken.IsCancellationRequested)
        {
            Failure ??= error;
            var cleanup = StopAfterFailure(EcsSchedulerState.Stopped);
            if (cleanup.Count != 0) { _world.MarkFaulted(error); State = EcsSchedulerState.Faulted; throw new AggregateException([error, .. cleanup]); }
            throw;
        }
        catch (Exception error)
        {
            Failure ??= error; _world.MarkFaulted(error);
            var cleanup = StopAfterFailure(EcsSchedulerState.Faulted);
            if (cleanup.Count != 0) throw new AggregateException([error, .. cleanup]);
            throw;
        }
        finally { _ticking = false; }
    }

    /// <summary>Requests cooperative terminal stop. Thread-safe; it does not abort arbitrary user code.</summary>
    public void RequestStop()
    { ObjectDisposedException.ThrowIf(State == EcsSchedulerState.Disposed, this); Interlocked.Exchange(ref _stopRequested, 1); }

    /// <summary>Stops systems in reverse order; during Execute only requests stop until the callback exits.</summary>
    public void Stop()
    {
        if (State == EcsSchedulerState.Disposed) return;
        Interlocked.Exchange(ref _stopRequested, 1);
        if (!_ticking) CompleteStop();
    }

    /// <summary>Stops and disposes owned systems in reverse order. Failed disposals remain owned for explicit retry.</summary>
    /// <remarks>Borrowed factories/dependencies/World are not disposed. Release the World only after this cleanup succeeds.</remarks>
    public void Dispose()
    {
        if (State == EcsSchedulerState.Disposed) return;
        if (_ticking) throw new InvalidOperationException("Request stop, then dispose after the active callback returns.");
        var errors = new List<Exception>();
        try { Stop(); } catch (Exception error) { errors.Add(error); }
        for (var i = _entries.Length - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            if (entry.System == null || entry.Disposed) continue;
            try { entry.System.Dispose(); entry.Disposed = true; entry.System = null; }
            catch (Exception error) { Failure ??= error; errors.Add(error); }
        }
        Commands.Dispose();
        if (_entries.All(e => e.System == null || e.Disposed))
        { _entries = []; _world.ReleaseScheduler(this); State = EcsSchedulerState.Disposed; }
        else State = EcsSchedulerState.Faulted;
        if (errors.Count != 0) throw new AggregateException("System graph cleanup failed.", errors);
    }

    private void CompleteStop()
    {
        var errors = StopAfterFailure(State == EcsSchedulerState.Faulted ? EcsSchedulerState.Faulted : EcsSchedulerState.Stopped);
        if (errors.Count == 0) return;
        var error = new AggregateException("System stop failed.", errors); Failure ??= error;
        _world.MarkFaulted(error); State = EcsSchedulerState.Faulted; throw error;
    }
    private List<Exception> StopAfterFailure(EcsSchedulerState state)
    {
        State = state;
        if (!Commands.IsDisposed) Commands.Reset();
        var errors = new List<Exception>();
        for (var i = _entries.Length - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            if (entry.System == null || entry.Stopped || entry.Disposed) continue;
            entry.Stopped = true;
            try { entry.System.Stop(); } catch (Exception error) { errors.Add(error); }
        }
        return errors;
    }
    private sealed class Entry(EcsSystemRegistration registration, EcsSystemContext context)
    {
        internal EcsSystemRegistration Registration { get; } = registration;
        internal EcsSystemContext Context { get; } = context;
        internal IEcsSystem? System;
        internal bool Stopped, Disposed;
    }
}
