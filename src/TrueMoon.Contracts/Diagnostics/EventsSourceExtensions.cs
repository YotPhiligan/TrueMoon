using System.Runtime.CompilerServices;

namespace TrueMoon.Diagnostics;

public static class EventsSourceExtensions
{
    public static EventsSourceActivityHandler UseActivity(this IEventsSource eventsSource, string? details = default, string? category = default, [CallerMemberName] string? caller = default)
    {
        var activity = eventsSource.StartActivity(details, category, caller);

        return new EventsSourceActivityHandler(eventsSource, activity);
    }
    
    public static void Trace(this IEventsSource eventsSource, string? category = default, [CallerMemberName] string? caller = default)
    {
        eventsSource.Write(null, category, caller);
    }
    
    public static void Exception(this IEventsSource eventsSource, Exception e, string? category = default, [CallerMemberName] string? caller = default)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            category = "Exception";
        }

        eventsSource.Write(()=>e, category, caller);
    }
}