using TrueMoon.Diagnostics;

namespace TrueMoon;

/// <summary>
/// Default app lifetime implementation
/// </summary>
public class DefaultAppLifetime : IAppLifetime, IAppLifetimeHandler
{
    private readonly IEventsSource<IAppLifetime> _eventsSource;

    public DefaultAppLifetime(IEventsSource<IAppLifetime> eventsSource)
    {
        _eventsSource = eventsSource;
    }
    
    private readonly List<Action> _stoppingActions = new ();
    private readonly List<Action> _stoppedActions = new ();
    private readonly TaskCompletionSource _tcs = new ();
    private CancellationTokenSource _cts;
    private bool _isCancelRequested;

    private readonly object _sync = new ();

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
    public async Task WaitAsync(CancellationToken cancellationToken = default) 
        => await _tcs.Task.WaitAsync(cancellationToken);

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

    public void SetCancellationTokenSource(CancellationTokenSource source)
    {
        _cts = source;
    }
}