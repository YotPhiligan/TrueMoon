using System.Runtime.ExceptionServices;
using TrueMoon.Diagnostics;
using TrueMoon.Mithril.Units;

namespace TrueMoon.Mithril;

/// <summary>Serializes startup and cleanup of the units owned by one application.</summary>
public class UnitsController : IUnitsController, IStartable, IStoppable, IDisposable, IAsyncDisposable
{
    private readonly IEventsSource<UnitsController> _eventsSource;
    private readonly IAppLifetime _appLifetime;
    private readonly IReadOnlyList<IUnitConfiguration> _unitConfigurations;
    private readonly IFactory<IUnitHandle, IUnitConfiguration> _handles;
    private readonly Dictionary<int, IUnitHandle> _unitHandles = [];
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly object _disposalGate = new();
    private Task? _disposeTask;
    private readonly AsyncLocal<bool> _disposing = new();
    private volatile bool _stopping;
    private bool _started;
    private int _disposed;
    private int _lifetimeCancelled;

    /// <summary>Creates a controller with an independent snapshot of its unit configurations.</summary>
    /// <param name="eventsSource">Diagnostics.</param>
    /// <param name="appLifetime">Lifetime optionally cancelled by natural unit exit.</param>
    /// <param name="configurations">App-local units; omitted means no units.</param>
    /// <param name="handles">Borrowed handle factory; each result is owned by this controller.</param>
    public UnitsController(IEventsSource<UnitsController> eventsSource, IAppLifetime appLifetime,
        IReadOnlyList<IUnitConfiguration>? configurations = null, IFactory<IUnitHandle, IUnitConfiguration>? handles = null)
    {
        ArgumentNullException.ThrowIfNull(eventsSource);
        ArgumentNullException.ThrowIfNull(appLifetime);
        _eventsSource = eventsSource;
        _appLifetime = appLifetime;
        _unitConfigurations = UnitConfigurationSnapshot.Create(configurations ?? []);
        _handles = handles ?? new UnitHandleFactory(eventsSource);
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            CheckStarting();
            if (_started) return;
            try
            {
                foreach (var unit in _unitConfigurations.Where(u => u.StartupPolicy == UnitStartupPolicy.Immediate))
                    await SpawnCoreAsync(unit, cancellationToken).ConfigureAwait(false);
                _started = true;
            }
            catch (Exception error)
            {
                _stopping = true;
                var errors = new List<Exception> { error };
                await CleanupAsync(CancellationToken.None, false, errors).ConfigureAwait(false);
                ThrowErrors(errors);
            }
        }
        finally { _lifecycle.Release(); }
    }

    /// <inheritdoc />
    public Task SpawnUnitAsync(string unitName, IReadOnlyList<object>? parameters = default, CancellationToken cancellationToken = default)
        => SpawnAsync(u => u.Name == unitName, parameters, cancellationToken);

    /// <inheritdoc />
    public Task SpawnUnitAsync(int index, IReadOnlyList<object>? parameters = default, CancellationToken cancellationToken = default)
        => SpawnAsync(u => u.Index == index, parameters, cancellationToken);

    private async Task SpawnAsync(Func<IUnitConfiguration, bool> match, IReadOnlyList<object>? parameters, CancellationToken token)
    {
        if (parameters is { Count: > 0 }) throw new NotSupportedException("Unit launch parameters are not implemented.");
        await _lifecycle.WaitAsync(token).ConfigureAwait(false);
        try
        {
            CheckStarting();
            var unit = _unitConfigurations.FirstOrDefault(match) ?? throw new ArgumentException("Unknown unit name or index.");
            await SpawnCoreAsync(unit, token).ConfigureAwait(false);
        }
        finally { _lifecycle.Release(); }
    }

    private void CheckStarting()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_stopping) throw new InvalidOperationException("A stopped unit controller cannot be restarted.");
    }

    private async Task SpawnCoreAsync(IUnitConfiguration config, CancellationToken token)
    {
        CheckStarting();
        token.ThrowIfCancellationRequested();
        if (_unitHandles.ContainsKey(config.Index)) throw new InvalidOperationException($"Unit '{config.Name}' is already started.");
        var handle = CreateHandle(config);
        _unitHandles.Add(config.Index, handle);
        handle.OnExit(OnUnitExit);
        try
        {
            if (await handle.StartAsync(token).ConfigureAwait(false) is not true)
                throw new InvalidOperationException($"Unit '{config.Name}' did not start.");
            token.ThrowIfCancellationRequested();
            CheckStarting();
        }
        catch (Exception error)
        {
            var errors = new List<Exception> { error };
            await CleanupHandleAsync(handle, CancellationToken.None, true, errors).ConfigureAwait(false);
            ThrowErrors(errors);
        }
    }

    /// <summary>Creates the runtime handle for a configured hosting mode.</summary>
    /// <param name="configuration">The controller's validated configuration snapshot.</param>
    /// <returns>A new owned unit handle.</returns>
    protected virtual IUnitHandle CreateHandle(IUnitConfiguration configuration) => _handles.Create(configuration);

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _stopping = true;
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            var errors = new List<Exception>();
            await CleanupAsync(cancellationToken, false, errors).ConfigureAwait(false);
            ThrowErrors(errors);
        }
        finally { _lifecycle.Release(); }
    }

    private async Task CleanupAsync(CancellationToken token, bool disposing, List<Exception> errors)
    {
        foreach (var handle in _unitHandles.Values.Reverse().ToArray())
            await CleanupHandleAsync(handle, token, disposing, errors).ConfigureAwait(false);
    }

    private async Task CleanupHandleAsync(IUnitHandle handle, CancellationToken token, bool disposing, List<Exception> errors)
    {
        handle.OnExit(static _ => { });
        var stopped = false;
        try
        {
            stopped = await handle.StopAsync(token).ConfigureAwait(false) is true;
            if (!stopped) errors.Add(new InvalidOperationException($"Unit '{handle.GetConfiguration().Name}' did not stop."));
        }
        catch (Exception error) { errors.Add(error); }
        if (stopped || disposing)
        {
            try
            {
                if (handle is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                else if (handle is IDisposable disposable) disposable.Dispose();
                // A failed stop remains retryable unless disposal itself is the cleanup boundary.
                if (stopped || handle is IDisposable or IAsyncDisposable) _unitHandles.Remove(handle.GetConfiguration().Index);
            }
            catch (Exception error) { errors.Add(error); }
        }
    }

    private void OnUnitExit(IUnitHandle handle)
    {
        if (_stopping || Volatile.Read(ref _disposed) != 0 || handle.GetConfiguration().IsControlAppLifetime is not true) return;
        if (Interlocked.CompareExchange(ref _lifetimeCancelled, 1, 0) != 0) return;
        try { _appLifetime.Cancel(); }
        catch (Exception error) { _eventsSource.Exception(error); }
    }

    /// <inheritdoc />
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposing.Value) return ValueTask.CompletedTask;
        lock (_disposalGate)
        {
            Volatile.Write(ref _disposed, 1);
            _stopping = true;
            return new ValueTask(_disposeTask ??= DisposeCoreAsync());
        }
    }

    private async Task DisposeCoreAsync()
    {
        _disposing.Value = true;
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            var errors = new List<Exception>();
            await CleanupAsync(CancellationToken.None, true, errors).ConfigureAwait(false);
            ThrowErrors(errors);
        }
        finally { _lifecycle.Release(); _disposing.Value = false; }
    }

    private static void ThrowErrors(List<Exception> errors)
    {
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }
}
