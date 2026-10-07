using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace TrueMoon.Argentis;

internal sealed class PropertyBinding<TSource, TValue> : IDisposable where TSource : class, INotifyPropertyChanged
{
    private readonly Element _element;
    private readonly UiProperty<TValue> _property;
    private readonly TSource _source;
    private readonly Func<TSource, TValue> _read;
    private readonly Action<TSource, TValue>? _write;
    private readonly string _sourceProperty;
    private Connection? _connection;
    private bool _disposed;

    internal PropertyBinding(Element element, UiProperty<TValue> property, TSource source, Expression<Func<TSource, TValue>> selector, bool twoWay = false)
    {
        Expression body = selector.Body;
        while (!twoWay && body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked, Method: null } conversion)
            body = conversion.Operand;
        if (body is not MemberExpression { Member: PropertyInfo, Expression: var receiver } member || receiver != selector.Parameters[0])
            throw new ArgumentException("Select a direct instance property of the source, for example x => x.Name.", nameof(selector));
        _element = element;
        _property = property;
        _source = source;
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
        _element.AttachmentChanged += Reconnect;
        try { Reconnect(); }
        catch { Dispose(); throw; }
    }

    private void Reconnect()
    {
        _connection?.Dispose();
        _connection = null;
        if (_disposed || !_element.IsAttached) return;
        var connection = new Connection(this);
        _connection = connection;
        try
        {
            _element.OwnAttachment(connection);
            connection.Connect();
        }
        catch
        {
            connection.Dispose();
            _connection = null;
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _element.AttachmentChanged -= Reconnect;
        _connection?.Dispose();
        _connection = null;
    }

    private sealed class Connection(PropertyBinding<TSource, TValue> binding) : IDisposable
    {
        private int _active = 1;
        private bool _subscribed;
        private bool _busy;
        private bool _writingSource;
        private readonly int _ownerThread = Environment.CurrentManagedThreadId;
        private readonly Action<Action> _dispatch = binding._element.CaptureAttachmentDispatcher();
        private bool Active => Volatile.Read(ref _active) != 0 && binding._element.IsAttached;

        internal void Connect()
        {
            _subscribed = true;
            binding._source.PropertyChanged += Changed;
            if (binding._write != null) binding._element.PropertyChanged += UiChanged;
            Synchronize();
        }

        private void Synchronize()
        {
            // Both the captured dispatcher generation and the connection lifetime guard stale work.
            if (!Active || _busy) return;
            _busy = true;
            try { ApplyCanonicalValue(); }
            finally { _busy = false; }
        }

        private void ApplyCanonicalValue()
        {
            if (!Active) return;
            var value = binding._read(binding._source);
            if (Active) binding._element.Set(binding._property, value);
        }

        private void UiChanged(Element element, object property)
        {
            if (!ReferenceEquals(property, binding._property) || !Active || _busy) return;
            element.VerifyAccess();
            _busy = true;
            List<Exception>? errors = null;
            try
            {
                try
                {
                    var value = element.Get(binding._property);
                    var previous = binding._read(binding._source);
                    if (Active && !EqualityComparer<TValue>.Default.Equals(previous, value))
                    {
                        _writingSource = true;
                        try { binding._write!(binding._source, value); }
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
            try { _dispatch(Synchronize); }
            catch (ObjectDisposedException) when (Volatile.Read(ref _active) == 0 || !binding._element.IsAttached || binding._element.IsDisposed)
            {
                // A notification already in flight can outlive its source subscription/session.
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _active, 0) == 0) return;
            if (binding._write != null) binding._element.PropertyChanged -= UiChanged;
            if (_subscribed) binding._source.PropertyChanged -= Changed;
        }
    }
}
