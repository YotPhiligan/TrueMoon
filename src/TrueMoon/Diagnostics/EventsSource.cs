using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace TrueMoon.Diagnostics;

public class EventsSource : IEventsSource, IDisposable
{
    private readonly DiagnosticSource _source;
    private int _disposed;
    public string Name { get; }

    /// <inheritdoc />
    public bool IsEnabled(DiagnosticLevel level, string? eventName = null)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return Enum.IsDefined(level) && _source.IsEnabled(eventName ?? Name, level, null);
    }
    
    public EventsSource(string name) : this(name, null) { }

    internal EventsSource(string name, DiagnosticSubscription? subscription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        
        _source = subscription?.CreateSource(Name) ?? new DiagnosticListener(Name);
    }
    
    public Activity StartActivity(string? details = default, string? category = default, [CallerMemberName] string? caller = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var eventName = CreateEventName(category, caller);
        var activity = new Activity(eventName);
        activity.AddTag("caller", caller);
        _source.StartActivity(activity, DiagnosticEvent.Create(details, category:category, caller:caller));

        return activity;
    }

    private string CreateEventName(string? category, string? caller)
    {
        var cat = category is null ? default : $".{category}";
        var cal = caller is null ? default : $".{caller}";
        var m = $"{Name}{cal}{cat}";
        return m;
    }

    public void StopActivity(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var caller = $"{activity.GetTagItem("caller")}";
        _source.StopActivity(activity,DiagnosticEvent.Trace(caller:caller));
    }

    public void Write(Func<object>? func, string? category = default, [CallerMemberName] string? caller = default)
    {
        DiagnosticEvent GetEvent()
        {
            var payload = func!();
            return payload is Exception e 
                ? DiagnosticEvent.Exception(e, category:category, caller: caller) 
                : DiagnosticEvent.Create(payload, category:category, caller: caller);
        }

        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var eventName = CreateEventName(category, caller);
        
        if (!_source.IsEnabled(eventName)) return;
        
        var message = func is null 
            ? DiagnosticEvent.Trace(category:category,caller: caller)
            : GetEvent();
        _source.Write(eventName, message);
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && _source is IDisposable disposable) disposable.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class EventsSource<T> : EventsSource, IEventsSource<T>
{
    public EventsSource() : base(typeof(T).FullName ?? typeof(T).Name)
    {
    }

    /// <summary>Creates a typed source using the App's diagnostic routing.</summary>
    /// <param name="subscription">The borrowed App diagnostic subscription, if configured.</param>
    public EventsSource(DiagnosticSubscription? subscription)
        : base(typeof(T).FullName ?? typeof(T).Name, subscription)
    {
    }
}
