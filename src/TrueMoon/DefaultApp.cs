using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Services;

namespace TrueMoon;

/// <summary>Owns the application's service provider and serialized service lifecycle.</summary>
public class DefaultApp : IApp
{
    private enum AppState { Created, Starting, Running, Stopping, Stopped }
    private readonly IEventsSource<DefaultApp> _eventsSource;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly List<IStartable> _started = [];
    private readonly AsyncLocal<bool> _disposing = new();
    private readonly TaskCompletionSource _disposeCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConsoleCancelEventHandler _cancelHandler;
    private AppState _state;
    private int _disposeRequested;
    private bool _consoleSubscribed;

    /// <summary>Creates an application owning the supplied service provider.</summary>
    /// <param name="eventsSource">Application diagnostic events.</param>
    /// <param name="parameters">Application configuration.</param>
    /// <param name="services">The root resolver or provider owned by the application.</param>
    public DefaultApp(IEventsSource<DefaultApp> eventsSource, IConfiguration parameters, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(eventsSource);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(services);
        _eventsSource = eventsSource;
        Configuration = parameters;
        Services = services.GetService(typeof(IServiceResolver)) as IServiceProvider ?? services;
        _cancelHandler = (_, args) =>
        {
            args.Cancel = true;
            try { Services.Resolve<IAppLifetime>()?.Cancel(); }
            catch (Exception error) { _eventsSource.Exception(error); }
        };
    }

    /// <inheritdoc />
    public string Name => Configuration.GetName() ?? "";
    /// <inheritdoc />
    public IServiceProvider Services { get; }
    /// <inheritdoc />
    public IConfiguration Configuration { get; }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeRequested) != 0, this);
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeRequested) != 0, this);
            if (_state == AppState.Running) return;
            if (_state != AppState.Created)
                throw new InvalidOperationException("A stopped application cannot be restarted; build a new application.");
            _state = AppState.Starting;
            SubscribeConsole();
            _eventsSource.Trace("Starting");
            try
            {
                foreach (var service in Services.ResolveAll<IStartable>().Distinct<IStartable>(ReferenceEqualityComparer.Instance))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await service.StartAsync(cancellationToken).ConfigureAwait(false);
                    _started.Add(service);
                }
                _state = AppState.Running;
                _eventsSource.Trace("Started");
            }
            catch (Exception error)
            {
                var errors = new List<Exception> { error };
                UnsubscribeConsole();
                using var rollback = new CancellationTokenSource(AppRunner.ShutdownTimeout);
                await StopServicesAsync(false, rollback.Token, errors).ConfigureAwait(false);
                _state = AppState.Stopped;
                AppRunner.ThrowErrors(errors);
            }
        }
        finally { _lifecycle.Release(); }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeRequested) != 0, this);
        await _lifecycle.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeRequested) != 0, this);
            if (_state == AppState.Stopped) return;
            _state = AppState.Stopping;
            UnsubscribeConsole();
            var errors = new List<Exception>();
            await StopServicesAsync(true, cancellationToken, errors).ConfigureAwait(false);
            _state = AppState.Stopped;
            AppRunner.ThrowErrors(errors);
        }
        finally { _lifecycle.Release(); }
    }

    private async Task StopServicesAsync(bool includeRegistered, CancellationToken token, List<Exception> errors)
    {
        _eventsSource.Trace("Stopping");
        var services = new List<IStoppable>();
        var seen = new HashSet<IStoppable>(ReferenceEqualityComparer.Instance);
        for (var i = _started.Count - 1; i >= 0; i--)
            if (_started[i] is IStoppable service && seen.Add(service)) services.Add(service);
        if (includeRegistered)
        {
            try
            {
                var registered = Services.ResolveAll<IStoppable>().ToArray();
                for (var i = registered.Length - 1; i >= 0; i--)
                    if (seen.Add(registered[i])) services.Add(registered[i]);
            }
            catch (Exception error) { errors.Add(error); }
        }
        _started.Clear();
        foreach (var service in services)
        {
            try { await service.StopAsync(token).ConfigureAwait(false); }
            catch (Exception error) { errors.Add(error); _eventsSource.Exception(error); }
        }
        _eventsSource.Trace("Stopped");
    }

    private void SubscribeConsole()
    {
        if (_consoleSubscribed) return;
        Console.CancelKeyPress += _cancelHandler;
        _consoleSubscribed = true;
    }

    private void UnsubscribeConsole()
    {
        if (!_consoleSubscribed) return;
        Console.CancelKeyPress -= _cancelHandler;
        _consoleSubscribed = false;
    }

    /// <inheritdoc />
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        // Containers can contain the app itself: avoid waiting on our own disposal.
        if (_disposing.Value) return ValueTask.CompletedTask;
        if (Interlocked.CompareExchange(ref _disposeRequested, 1, 0) != 0)
            return new ValueTask(_disposeCompleted.Task);
        return new ValueTask(DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        _disposing.Value = true;
        var errors = new List<Exception>();
        try
        {
            await _lifecycle.WaitAsync().ConfigureAwait(false);
            try
            {
                UnsubscribeConsole();
                if (_state == AppState.Running)
                {
                    using var shutdown = new CancellationTokenSource(AppRunner.ShutdownTimeout);
                    await StopServicesAsync(true, shutdown.Token, errors).ConfigureAwait(false);
                }
                _state = AppState.Stopped;
                try
                {
                    switch (Services)
                    {
                        case IAsyncDisposable asyncDisposable:
                            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                            break;
                        case IDisposable disposable:
                            disposable.Dispose();
                            break;
                    }
                }
                catch (Exception error) { errors.Add(error); }
            }
            finally { _lifecycle.Release(); }
            AppRunner.ThrowErrors(errors);
            _disposeCompleted.TrySetResult();
        }
        catch (Exception error)
        {
            _disposeCompleted.TrySetException(error);
            // Observe the shared completion even when nobody repeats Dispose.
            _ = _disposeCompleted.Task.Exception;
            throw;
        }
        finally { _disposing.Value = false; GC.SuppressFinalize(this); }
    }
}
