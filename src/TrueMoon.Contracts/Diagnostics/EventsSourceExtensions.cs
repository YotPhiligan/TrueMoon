using System.Runtime.CompilerServices;

namespace TrueMoon.Diagnostics;

public static class EventsSourceExtensions
{
    /// <summary>Emits structured producer data; properties are evaluated only when collection is enabled.</summary>
    /// <param name="eventsSource">Borrowed diagnostic source.</param>
    /// <param name="level">Severity.</param>
    /// <param name="eventId">Stable module-owned event number.</param>
    /// <param name="eventName">Stable event name.</param>
    /// <param name="messageTemplate">Unformatted message template.</param>
    /// <param name="properties">Optional lazy named values, copied synchronously by the pipeline.</param>
    /// <param name="error">Optional original exception.</param>
    /// <param name="caller">Calling member.</param>
    public static void Write(this IEventsSource eventsSource, DiagnosticLevel level, int eventId,
        string eventName, string messageTemplate, Func<IReadOnlyDictionary<string, string?>>? properties = null,
        Exception? error = null, [CallerMemberName] string? caller = null)
    {
        ArgumentNullException.ThrowIfNull(eventsSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(messageTemplate);
        if (!Enum.IsDefined(level)) throw new ArgumentOutOfRangeException(nameof(level));
        if (eventId < 0) throw new ArgumentOutOfRangeException(nameof(eventId));
        if (!eventsSource.IsEnabled(level, eventName)) return;
        eventsSource.Write(() => new DiagnosticMessage(level, eventId, eventName, messageTemplate,
            properties?.Invoke(), error, caller), eventName, caller);
    }

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