using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace TrueMoon.Diagnostics;

public interface IEventsSource
{
    string Name { get; }

    /// <summary>Tests structured-event filtering before constructing properties. Legacy sources remain compatible.</summary>
    /// <param name="level">Event severity.</param>
    /// <param name="eventName">Stable event name.</param>
    /// <returns>Whether the source can accept this event.</returns>
    bool IsEnabled(DiagnosticLevel level, string? eventName = null) => true;

    Activity StartActivity(string? details = default, string? category = default,
        [CallerMemberName] string? caller = default);
    void StopActivity(Activity activity);

    void Write(Func<object>? func, string? category = default, [CallerMemberName] string? caller = default);
}

public interface IEventsSource<T> : IEventsSource {}