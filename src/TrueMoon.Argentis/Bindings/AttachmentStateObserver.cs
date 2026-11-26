using System.ComponentModel;

namespace TrueMoon.Argentis.Bindings;

internal sealed class AttachmentStateObserver : IDisposable
{
    private readonly Element _element;
    private readonly INotifyPropertyChanged[] _sources;
    private readonly Action _synchronize;
    private readonly UiSynchronization _timing;
    private Connection? _connection;
    private bool _disposed;

    internal AttachmentStateObserver(Element element, INotifyPropertyChanged[] sources, Action synchronize, UiSynchronization timing)
    {
        _element = element;
        _sources = sources;
        _synchronize = synchronize;
        _timing = timing;
        element.AttachmentChanged += Reconnect;
        try { Reconnect(); }
        catch (Exception error)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }

    private void Reconnect()
    {
        var previous = _connection;
        _connection = null;
        previous?.Dispose();
        if (_disposed || !_element.IsAttached) return;

        var connection = new Connection(this);
        _connection = connection;
        try { connection.Connect(); }
        catch (Exception error)
        {
            if (ReferenceEquals(_connection, connection)) _connection = null;
            try { connection.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _element.AttachmentChanged -= Reconnect;
        var connection = _connection;
        _connection = null;
        connection?.Dispose();
    }

    private sealed class Connection : IDisposable
    {
        private const int Idle = 0, Pending = 1, Ended = 2;
        private readonly AttachmentStateObserver _observer;
        private readonly Action<Action> _dispatch;
        private readonly Action _flush;
        private readonly PropertyChangedEventHandler _changed;
        private int _state;
        private int _subscribed;

        internal Connection(AttachmentStateObserver observer)
        {
            _observer = observer;
            _dispatch = observer._element.CaptureAttachmentDispatcher();
            _flush = Flush;
            _changed = Changed;
        }

        internal void Connect()
        {
            var sources = _observer._sources;
            while (_subscribed < sources.Length)
                sources[_subscribed++].PropertyChanged += _changed;
            if (_observer._timing == UiSynchronization.AfterTreeChange) RequestSynchronization();
            else _observer._synchronize();
        }

        private void Changed(object? sender, PropertyChangedEventArgs args) => RequestSynchronization();

        private void RequestSynchronization()
        {
            if (!_observer._element.IsAttached || Interlocked.CompareExchange(ref _state, Pending, Idle) != Idle) return;
            try { _dispatch(_flush); }
            catch (ObjectDisposedException) when (Volatile.Read(ref _state) == Ended || !_observer._element.IsAttached)
            {
                // A publisher can already have copied the handler when detach removes it.
                Interlocked.CompareExchange(ref _state, Idle, Pending);
            }
            catch
            {
                Interlocked.CompareExchange(ref _state, Idle, Pending);
                throw;
            }
        }

        private void Flush()
        {
            // Clear before reading so notifications during synchronization can request the next read.
            if (Interlocked.CompareExchange(ref _state, Idle, Pending) == Pending)
                _observer._synchronize();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _state, Ended) == Ended) return;
            List<Exception>? errors = null;
            for (var i = 0; i < _subscribed; i++)
            {
                try { _observer._sources[i].PropertyChanged -= _changed; }
                catch (Exception error) { (errors ??= []).Add(error); }
            }
            TreeChange.ThrowErrors(errors);
        }
    }
}
