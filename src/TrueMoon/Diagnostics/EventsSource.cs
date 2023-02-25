using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace TrueMoon.Diagnostics;

public class EventsSource : IEventsSource
{
    private readonly DiagnosticSource _source;
    public string Name { get; }
    
    public EventsSource(string name)
    {
        Name = name;
        
        _source = new DiagnosticListener(Name);
    }
    
    public Activity StartActivity(string? details = default, string? category = default, [CallerMemberName] string? caller = default)
    {
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
        var caller = $"{activity.GetTagItem("caller")}";
        _source.StopActivity(activity,DiagnosticEvent.Trace(caller:caller));
    }

    public void Write(Func<object>? func, string? category = default, [CallerMemberName] string? caller = default)
    {
        DiagnosticEvent GetEvent()
        {
            var payload = func();
            return payload is Exception e 
                ? DiagnosticEvent.Exception(e, category:category, caller: caller) 
                : DiagnosticEvent.Create(payload, category:category, caller: caller);
        }

        var eventName = CreateEventName(category, caller);
        
        if (!_source.IsEnabled(eventName)) return;
        
        var message = func is null 
            ? DiagnosticEvent.Trace(category:category,caller: caller)
            : GetEvent();
        _source.Write(eventName, message);
    }
}

public class EventsSource<T> : EventsSource, IEventsSource<T>
{
    public EventsSource() : base(typeof(T).FullName ?? typeof(T).Name)
    {
    }
}