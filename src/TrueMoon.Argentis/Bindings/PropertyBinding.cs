using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace TrueMoon.Argentis.Bindings;

internal sealed class PropertyBinding<TSource, TValue> : IDisposable where TSource : class, INotifyPropertyChanged
{
    private readonly Element _element;
    private readonly UiProperty<TValue> _property;
    private readonly TSource? _source;
    private readonly IDataContext<TSource>? _context;
    private readonly Func<TSource, TValue> _read;
    private readonly Action<TSource, TValue>? _write;
    private readonly string _sourceProperty;
    private Connection? _connection;
    private bool _observingContext;
    private bool _disposed;

    internal PropertyBinding(Element element, UiProperty<TValue> property, TSource? source,
        Expression<Func<TSource, TValue>> selector, bool twoWay = false, IDataContext<TSource>? context = null)
    {
        Expression body = selector.Body;
        while (!twoWay && body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked, Method: null } conversion)
            body = conversion.Operand;
        if (body is not MemberExpression { Member: PropertyInfo, Expression: var receiver } member || receiver != selector.Parameters[0])
            throw new ArgumentException("Select a direct instance property of the source, for example x => x.Name.", nameof(selector));
        _element = element;
        _property = property;
        _source = source;
        _context = context;
        _read = selector.Compile(preferInterpretation: true);
        _sourceProperty = member.Member.Name;
        if (twoWay)
        {
            var sourceProperty = (PropertyInfo)member.Member;
            if (sourceProperty.PropertyType != typeof(TValue) || sourceProperty.GetMethod?.IsPublic != true
                || sourceProperty.SetMethod is not { IsPublic: true } setter
                || setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)))
                throw new ArgumentException("Select a public readable/writable property of exactly the UI property's type, without an init-only setter.", nameof(selector));
            var value = Expression.Parameter(typeof(TValue), "value");
            _write = Expression.Lambda<Action<TSource, TValue>>(Expression.Assign(member, value), selector.Parameters[0], value).Compile(preferInterpretation: true);
        }
        _element.AttachmentChanged += AttachmentChanged;
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
        if (!_element.IsAttached)
        {
            Disconnect();
            return;
        }
        if (_context != null && !_observingContext)
        {
            _observingContext = true;
            _context.DataContextChanged += Reconnect;
        }
        Reconnect();
    }

    private void Reconnect()
    {
        if (_disposed || !_element.IsAttached) return;
        _element.VerifyAccess();
        var source = _context != null ? _context.DataContext : _source;
        var previous = _connection;
        if (previous != null && ReferenceEquals(previous.Source, source)) return;
        _connection = null;
        previous?.Dispose();
        if (source == null) return;

        var connection = new Connection(this, source);
        _connection = connection;
        try { connection.Connect(); }
        catch (Exception original)
        {
            if (ReferenceEquals(_connection, connection)) _connection = null;
            try { connection.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
        connection.Synchronize();
    }

    private void Disconnect()
    {
        var previous = _connection;
        _connection = null;
        try { previous?.Dispose(); }
        finally
        {
            if (_observingContext)
            {
                _observingContext = false;
                _context!.DataContextChanged -= Reconnect;
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

    private sealed class Connection(PropertyBinding<TSource, TValue> binding, TSource source) : IDisposable
    {
        private int _active = 1;
        private int _pending;
        private bool _subscribed;
        private bool _busy;
        private bool _writingSource;
        private readonly int _ownerThread = Environment.CurrentManagedThreadId;
        private readonly Action<Action> _dispatch = binding._element.CaptureAttachmentDispatcher();
        internal TSource Source { get; } = source;
        private bool Active => Volatile.Read(ref _active) != 0 && binding._element.IsAttached;
        // Consult the context on the owner thread, including during the change notification itself.
        private bool Current => Active && (binding._context == null || ReferenceEquals(Source, binding._context.DataContext));

        internal void Connect()
        {
            _subscribed = true;
            Source.PropertyChanged += Changed;
            if (binding._write != null) binding._element.PropertyChanged += UiChanged;
        }

        internal void Synchronize()
        {
            // Both the captured dispatcher generation and the connection lifetime guard stale work.
            if (!Current || _busy) return;
            _busy = true;
            try { ApplyCanonicalValue(); }
            finally { _busy = false; }
        }

        private void ApplyCanonicalValue()
        {
            if (!Current) return;
            var value = binding._read(Source);
            if (Current) binding._element.Set(binding._property, value);
        }

        private void UiChanged(Element element, object property)
        {
            if (!ReferenceEquals(property, binding._property) || !Current || _busy) return;
            element.VerifyAccess();
            _busy = true;
            List<Exception>? errors = null;
            try
            {
                try
                {
                    var value = element.Get(binding._property);
                    var previous = binding._read(Source);
                    if (Current && !EqualityComparer<TValue>.Default.Equals(previous, value))
                    {
                        _writingSource = true;
                        try { binding._write!(Source, value); }
                        finally { _writingSource = false; }
                    }
                }
                catch (Exception error) { (errors ??= []).Add(error); }
                try { ApplyCanonicalValue(); }
                catch (Exception error) { (errors ??= []).Add(error); }
            }
            finally { _busy = false; }
            TreeChange.ThrowErrors(errors);
        }

        private void Changed(object? sender, PropertyChangedEventArgs args)
        {
            if (!Active || (!string.IsNullOrEmpty(args.PropertyName) && args.PropertyName != binding._sourceProperty)) return;
            if (Environment.CurrentManagedThreadId == _ownerThread && _writingSource) return;
            // Read the latest source value once for a burst, without merging ordinary UI commands.
            if (Interlocked.Exchange(ref _pending, 1) != 0) return;
            try { _dispatch(() => { Interlocked.Exchange(ref _pending, 0); Synchronize(); }); }
            catch (ObjectDisposedException) when (Volatile.Read(ref _active) == 0 || !binding._element.IsAttached || binding._element.IsDisposed)
            {
                Interlocked.Exchange(ref _pending, 0);
                // A notification already in flight can outlive its source subscription/session.
            }
            catch { Interlocked.Exchange(ref _pending, 0); throw; }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _active, 0) == 0) return;
            if (binding._write != null) binding._element.PropertyChanged -= UiChanged;
            if (_subscribed) Source.PropertyChanged -= Changed;
        }
    }
}
