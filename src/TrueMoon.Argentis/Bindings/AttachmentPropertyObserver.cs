using System.ComponentModel;

namespace TrueMoon.Argentis.Bindings;

internal sealed class AttachmentPropertyObserver : IDisposable
{
    private readonly Element _element;
    private readonly INotifyPropertyChanged _source;
    private readonly PropertyChangedEventHandler _handler;
    private readonly Action? _synchronize;
    private readonly UiSynchronization _timing;
    private readonly Func<bool>? _isCurrent;
    private Connection? _connection;
    private bool _disposed;

    internal AttachmentPropertyObserver(Element element, INotifyPropertyChanged source,
        PropertyChangedEventHandler handler, Action? synchronize, UiSynchronization timing = UiSynchronization.Immediate,
        Func<bool>? isCurrent = null)
    {
        _element = element;
        _source = source;
        _handler = handler;
        _synchronize = synchronize;
        _timing = timing;
        _isCurrent = isCurrent;
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
        try
        {
            connection.Connect();
        }
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

    private sealed class Connection(AttachmentPropertyObserver observer) : IDisposable
    {
        private readonly Action<Action> _dispatch = observer._element.CaptureAttachmentDispatcher();
        private int _active = 1;
        private bool _subscribed;

        private bool Active => Volatile.Read(ref _active) != 0 && observer._element.IsAttached;
        private bool IsCurrent => Active && (observer._isCurrent?.Invoke() ?? true);

        internal void Connect()
        {
            _subscribed = true;
            observer._source.PropertyChanged += Changed;
            if (observer._synchronize is { } synchronize)
            {
                if (observer._timing == UiSynchronization.AfterTreeChange) _dispatch(Synchronize);
                else synchronize();
            }
        }

        private void Synchronize()
        {
            if (IsCurrent) observer._synchronize?.Invoke();
        }

        private void Changed(object? sender, PropertyChangedEventArgs args)
        {
            if (!Active) return;
            try
            {
                _dispatch(() =>
                {
                    if (IsCurrent) observer._handler(sender, args);
                });
            }
            catch (ObjectDisposedException) when (!Active || observer._element.IsDisposed)
            {
                // A publisher can already have copied the handler when detach removes it.
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _active, 0) != 0 && _subscribed)
                observer._source.PropertyChanged -= Changed;
        }
    }
}
