using TrueMoon.Diagnostics;

namespace TrueMoon;

/// <summary>
/// Default app lifetime implementation
/// </summary>
public class DefaultAppLifetime : IAppLifetime, IAppLifetimeHandler
{
    private readonly IEventsSource<IAppLifetime> _eventsSource;

    public DefaultAppLifetime(IEventsSource<IAppLifetime> eventsSource, 
        AppCancellationTokenSourceHandle? appCancellationTokenSourceHandle = default)
    {
        _eventsSource = eventsSource;
        _cts = appCancellationTokenSourceHandle?.CancellationTokenSource ?? new CancellationTokenSource();
    }
    
    private readonly List<Action> _stoppingActions = [];
    private readonly List<Action> _stoppedActions = [];
    private readonly TaskCompletionSource _tcs = new ();
    private readonly CancellationTokenSource _cts;
    private bool _isCancelRequested;

    private readonly Lock _sync = new ();

    public CancellationToken AppCancellationToken => _cts.Token;
    
    /// <inheritdoc />
    public void Cancel()
    {
        lock (_sync)
        {
            if (_isCancelRequested)
            {
                return;
            }

            _isCancelRequested = true;
        
            _eventsSource.Trace();
            _tcs.TrySetResult();
            try
            {
                _cts.Cancel();
            }
            catch (Exception)
            {
                //
            }
        }
    }

    /// <inheritdoc />
    public void OnStopped(Action action)
    {
        _stoppedActions.Add(action);
    }

    /// <inheritdoc />
    public void OnStopping(Action action)
    {
        _stoppingActions.Add(action);
    }

    /// <inheritdoc />
    public Task WaitAsync(CancellationToken cancellationToken = default) 
        => _tcs.Task.WaitAsync(cancellationToken);

    /// <inheritdoc />
    public void Stopping()
    {
        lock (_sync)
        {
            _isCancelRequested = true;
        }

        _eventsSource.Trace(nameof(Stopping));
        foreach (var action in _stoppingActions)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                _eventsSource.Exception(e);
            }
        }
    }

    /// <inheritdoc />
    public void Stopped()
    {
        foreach (var action in _stoppedActions)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                _eventsSource.Exception(e);
            }
        }
        _eventsSource.Trace(nameof(Stopped));
    }
}