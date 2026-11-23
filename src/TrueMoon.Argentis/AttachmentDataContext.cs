namespace TrueMoon.Argentis;

// Owns one connection to a borrowed data context for each attached element.
internal sealed class AttachmentDataContext<TData> : IDisposable where TData : class
{
    private readonly Element _element;
    private readonly IDataContext<TData> _context;
    private readonly Func<TData, Func<bool>, IDisposable?> _connect;
    private readonly Action<bool>? _synchronize;
    private IDisposable? _connection;
    private TData? _source;
    private long _version;
    private bool _initialized, _observing, _disposed;

    internal AttachmentDataContext(Element element, IDataContext<TData> context,
        Func<TData, Func<bool>, IDisposable?> connect, Action<bool>? synchronize)
    {
        _element = element;
        _context = context;
        _connect = connect;
        _synchronize = synchronize;
        element.AttachmentChanged += AttachmentChanged;
        try { AttachmentChanged(); }
        catch (Exception original)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
    }

    private void AttachmentChanged()
    {
        if (_disposed) return;
        if (!_element.IsAttached) { Disconnect(); return; }
        if (!_observing)
        {
            _observing = true;
            _context.DataContextChanged += Reconnect;
        }
        Reconnect();
    }

    private void Reconnect()
    {
        if (_disposed || !_element.IsAttached) return;
        _element.VerifyAccess();
        var source = _context.DataContext;
        if (_initialized && ReferenceEquals(_source, source)) return;

        var version = ClearConnection();
        if (version != _version || !_element.IsAttached) return;
        _source = source;
        _initialized = true;
        bool Current() => version == _version && _element.IsAttached && ReferenceEquals(source, _context.DataContext);

        try
        {
            var connection = source == null ? null : _connect(source, Current);
            if (!Current()) { connection?.Dispose(); return; }
            _connection = connection;
            _synchronize?.Invoke(connection != null);
        }
        catch (Exception original)
        {
            if (version == _version)
                try { ClearConnection(); }
                catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
    }

    private long ClearConnection()
    {
        var version = ++_version;
        var connection = _connection;
        _connection = null;
        _source = null;
        _initialized = false;
        connection?.Dispose();
        return version;
    }

    private void Disconnect()
    {
        try { ClearConnection(); }
        finally
        {
            if (_observing)
            {
                _observing = false;
                _context.DataContextChanged -= Reconnect;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _element.AttachmentChanged -= AttachmentChanged;
        Disconnect();
    }
}
